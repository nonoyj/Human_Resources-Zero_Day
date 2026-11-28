#!/usr/bin/env python3
"""
motion_manifest.py -- WHAT MOVES IN THE FRONT DOOR ROOM, AND HOW SLOW (v117, ADR-021).

Every animated thing in the room is declared here once: frame count, frame rate, whether it loops, and its
budget. The Unity side reads this file rather than hard-coding timings, and `--check` enforces the rules from
docs/17-front-door-round-two.md §4:

  1. NOTHING ANIMATES FASTER THAN 12 FPS at 480x270. Above that it stops reading as motion and starts showing
     as noise.
  2. THE ROOM'S GLOW IS THE SLOWEST THING IN THE FRAME. The light breathes at 3 fps or less; the room must
     feel like it is breathing, not blinking.
  3. EVERY LAYER DECLARES ITS FRAMES. A layer with no frame count is a layer nobody can finish.
  4. THE BREACH IS RATIONED. `beat_min`/`beat_max` describe how often a random event fires; the breach may
     not fire more often than every 6 seconds, or it stops being an alarm and becomes decoration.

    python tools/art/motion_manifest.py            # print the table
    python tools/art/motion_manifest.py --check    # enforce the four rules
"""
from __future__ import annotations

import json
import os
import sys

MAX_FPS = 12
# the constant from the game's own clock; the room runs at 60 Hz
ROOM_HZ = 60

LAYERS = [
    # name                frames fps  loop  where                                     notes
    dict(name='wall_screens', frames=4, fps=6, loop=True,
         where='the six monitors on the table',
         notes='cybersecurity readouts; each screen runs its own 4-state cycle, offset so they never sync'),
    dict(name='wall_flicker', frames=3, fps=8, loop=True,
         where='the same six monitors',
         notes='a 3-step brightness flicker; the room is old, the screens are older'),
    dict(name='breach', frames=3, fps=8, loop=False, beat_min=6.0, beat_max=14.0, hold=1.2,
         where='ONE of the six monitors, chosen at random',
         notes='red flood + SYS.ROOT text. Never all six: an alarm everywhere is weather, not a warning'),
    dict(name='main_screen', frames=6, fps=4, loop=True,
         where="the monitor the menu lives in",
         notes='cursor blink, a status tick on the bezel, and a scanline that rolls every 40 frames'),
    dict(name='menu_idle', frames=2, fps=1, loop=True, room_light=False,
         where='the menu rows themselves',
         notes='the selected pill pulses one step; no movement of the text, ever. This is UI, not room light'),
    dict(name='smoke', frames=6, fps=8, loop=True,
         where='the coffee cup',
         notes='three strands, phase-shifted; thickens for 10 px then breaks up. Slower reads as a candle'),
    dict(name='dust_motes', frames=4, fps=12, loop=True,
         where='the whole room',
         notes='24 two-pixel motes drifting down-left, alpha 60-140; the only thing at the fps ceiling'),
    dict(name='room_glow', frames=4, fps=2, loop=True,
         where='the cyan light on the desk and wall',
         notes='the light itself breathes, one slow cycle every 2 s. Nothing in the ROOM moves slower'),
    dict(name='window_life', frames=4, fps=2, loop=True, room_light=False,
         where='the city outside',
         notes='a lit window switches off and on; one aircraft light blinks. Two changes at 2 fps, no more'),
]


def check() -> list:
    problems = []
    names = set()
    for layer in LAYERS:
        n = layer['name']
        if n in names:
            problems.append('%s is declared twice' % n)
        names.add(n)
        if 'frames' not in layer or not layer['frames']:
            problems.append('%s declares no frames' % n)
        fps = layer.get('fps', 0)
        if fps <= 0:
            problems.append('%s has no frame rate' % n)
        elif fps > MAX_FPS:
            problems.append('%s runs at %s fps: above %d it reads as noise, not motion' % (n, fps, MAX_FPS))
        if layer.get('beat_min') is not None:
            if layer['beat_min'] < 6.0:
                problems.append('%s fires every %ss: an alarm that fires that often is decoration'
                                % (n, layer['beat_min']))
            if layer['beat_max'] < layer['beat_min']:
                problems.append('%s has beat_max below beat_min' % n)
    if not problems:
        glow = [l for l in LAYERS if l['name'] == 'room_glow']
        if not glow:
            problems.append('no room_glow layer: the light has to breathe')
        else:
            # the UI's own selection pulse is not room light and is exempt
            room = [l for l in LAYERS if l.get('room_light', True)]
            slowest = min(l['fps'] for l in room)
            if glow[0]['fps'] > slowest:
                problems.append('room_glow is at %s fps but something else is slower (%s): the glow must be '
                                'the slowest thing in the frame' % (glow[0]['fps'], slowest))
    return problems


def table() -> str:
    out = ['%-13s %6s %4s  %-38s %s' % ('layer', 'frames', 'fps', 'where', 'notes')]
    out.append('-' * 118)
    for l in LAYERS:
        out.append('%-13s %6s %4s  %-38s %s'
                   % (l['name'], l['frames'], l['fps'], l['where'][:38], l['notes']))
    return '\n'.join(out)


def main(argv) -> int:
    print(table())
    problems = check()
    if '--write' in argv:
        path = os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..',
                            'art', 'kit', 'menu', 'motion.json')
        path = os.path.normpath(path)
        with open(path, 'w', encoding='utf-8', newline='\n') as fh:
            json.dump({'room_hz': ROOM_HZ, 'max_fps': MAX_FPS, 'layers': LAYERS}, fh, indent=2)
            fh.write('\n')
        print('written %s' % path)
    if '--check' in argv:
        for p in problems:
            print('FAIL  ' + p)
        if problems:
            return 1
        print('PASS: %d animated layers, none above %d fps, the glow is the slowest, the breach is rationed'
              % (len(LAYERS), MAX_FPS))
    return 0


if __name__ == '__main__':
    raise SystemExit(main(sys.argv[1:]))
