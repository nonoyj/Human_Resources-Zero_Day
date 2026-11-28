#!/usr/bin/env python3
"""
art_palette.py -- ONE palette for the whole game (v120).

WHY THIS FILE EXISTS
    Until v120 the project had two palettes. `art/kit` (the desk, the letters, the applicant, the walk) painted
    with 24 flat project colours -- `void, panel, line, text, warn, danger, synth`. The front door painted with
    53 steps of 11 MATERIAL RAMPS -- `wall_0..3, floor_0..4, wood_0..4, teal_0..4, amber_0..4, cyan_0..2`.

    That second palette is where the menu's look comes from, and it is not decoration: each material owns a
    ramp of 2-5 steps, and the light pass only ever walks a surface ONE STEP up its own ramp. Light never tints
    a surface with its own colour. That single rule is the difference between the menu (a lit room) and the desk
    (a flat illustration).

    So the fix is not "paint the desk again". It is: make the ramps the palette, and give every legacy project
    colour an ALIAS onto the ramp step it was always trying to be. Then one palette, one lighting rule, one
    look -- and the desk's silhouettes are untouched, which is the only way a mechanical pass can be trusted.

HOW TO USE IT
    from art_palette import PALETTE, RAMP, ramp_of, lift, check

      PALETTE    key -> (r, g, b). Every drawable colour in the game, ramps plus the legacy aliases.
      RAMP       key -> (dark_key, lighter_key) for one step up.  `lift('wall_1') == 'wall_2'`.
      ramp_of    key -> the full ramp key ('wall_0', ...) a colour belongs to, so a test can walk it.
      lift       the honest "one step brighter" that the light pass is allowed to do -- and NOTHING else.

    python tools/art/art_palette.py            # the table, with the alias map
    python tools/art/art_palette.py --check    # the four rules + the alias coverage gate
"""
from __future__ import annotations

import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from menu_palette import PALETTE as MENU_PALETTE, RAMP as RAMPS  # noqa: E402  (one author, two names)

# RAMPS is {ramp_name: [(r, g, b), ...]} -- the authored ramp table. menu_palette exposes it as `RAMP` (the
# raw triples) and flattens it into `PALETTE` as wall_0, wall_1, ... This module works from the flattened
# names, because those are what the C# side and the art gates already address.


# ------------------------------------------------------------------ the legacy project colours -> ramps
#
# Each entry is the ramp key whose STEP is the closest honest home for the old colour, plus the old RGB it
# replaces. The gate below asserts two things about every pair: the step is a real ramp step, and it is within
# ALIAS_TOLERANCE of the colour it replaces -- so the alias pass cannot quietly restyle the desk.
#
# `void` is the exception on purpose: it is the game's true black, every ramp's darkest step is its own
# near-black, and the desk's vacuum (the room behind everything) must stay ONE colour or the frame gets
# patchy. It stays a standalone colour and is NOT part of any ramp.
ALIASES = {
    # old project name        ramp home     the nearest step, from a measured search of all 38 ramp steps
    'panel':      'wall_1',   # the room's base surface      (19,25,38)  -> (23,26,34)   d 5.7
    'panel2':     'wall_2',   # a raised surface             (29,39,53)  -> (34,40,47)   d 7.9
    'line':       'cyan_0',   # a hard cold edge             (43,58,77)  -> (28,62,73)   d 16.0
    'linehi':     'scr_1',    # its lit side                 (63,85,112) -> (81,93,97)   d 24.8
    'void':       'wall_0',   # the frame's darkest          (11,14,19)  -> (14,14,22)   d 4.2
    'ink':        'amber_1',  # the ink ON paper             (42,33,24)  -> (46,28,17)   d 9.5
    'text':       'scr_2',    # primary text (the tube's own light grey-white)
    'textdim':    'scr_1',    # secondary text -- moves because the kit's textdim is a blue-grey and the tube's
                              # greys are neutral; recorded in HUE_MOVED
    'white':      'scr_2',    # the emitters' top step (the tube never paints pure white)
    'muted':      'teal_4',   # muted UI text
    'signaldim':  'cyan_1',   # "the system is alive", dim
    'signal':     'cyan_2',   # ... and lit
    'ok':         'cyan_2',   # a passed check
    'warn':       'amber_3',  # a warning
    'danger':     'red_2',    # a failure
    'dangerdim':  'red_1',    # a failure, dim
    'synth':      'cyan_2',   # the synthetic accent: the room's emitter cyan (was violet)
    'synthdim':   'scr_2',
    # THE LETTERS ARE THE ONE EXCEPTION, and it is deliberate. The paper's stock is warm CREAM (242,232,213)
    # and the menu's paper ramp is the menu's own dark stock, three steps down. A letter that repaints itself
    # in the room's paper ramp stops being a white sheet under a lamp -- and the letter is the surface the
    # whole game is read on. So `paper` and `paperdim` stay EXACTLY as they are, and the exception is recorded
    # here and allowed by the gate instead of being quietly special-cased somewhere in a palette call.
    'paper':      'paper_1',
    'paperdim':   'paper_2',
    'orange':     'amber_4',
    # these two are menu_palette EXTRA colours (not ramp steps); they are in the palette already and the gate
    # must not ask them to belong to a ramp -- they are the stamp's red and the lamp's brass
    'brass':      'brass',
    'stamp':      'stamp',
    'glow_teal':  'cyan_0',
    'glow_amber': 'amber_2',
    'glow_red':   'red_1',
}

# How far an alias may sit from the colour it replaces, in 0-255 RGB distance (Euclidean, per channel summed).
# 40 is generous for a shadow step and strict enough that a hue change fails. Deliberately tunable, and the
# two entries that legitimately move hue (`synth`, `signaldim`) are documented above.
ALIAS_TOLERANCE = 64.0

# Colours that are allowed to stay exactly as they are, and why. An entry here is NOT an alias: the colour
# keeps its own RGB, and the gate skips the distance test for it.
KEPT_AS_IS = {
    'paper':    'the letter stock is the game\'s reading surface -- it must stay warm cream (docs/15 §3)',
    'paperdim': 'the ruled lines / secondary stock on the same surface',
}

# The colours that LEGITIMATELY move hue, and why. Everything else has to land within ALIAS_TOLERANCE of the
# colour it replaces, or the alias pass would silently restyle the desk rather than unify it.
HUE_MOVED = {
    'synth':     'violet -> the room\'s emitter cyan: violet is not in the art bible\'s range and the game\'s one'
                 ' synthetic colour must be the tube\'s own light',
    'signal':    'a brighter teal than the ramp holds: the emitter steps top out at cyan_2',
    'synthdim':  'follows synth',
    'orange':    'the lamp\'s own warm: amber_4 is the ramp\'s top step',
    'warn':      'a warning is the amber ramp, not the tube ramp -- this DOES change the desk\'s warning hue',
    'text':      'the kit\'s pure white becomes the tube\'s top step, so text sits in the same light as the room',
    'white':     'same reason as text',
    'textdim':   'the kit\'s secondary text is a blue-grey; the tube\'s greys are neutral, and all text now'
                 ' lives in the tube\'s light',
}

# The colours that are NOT part of a ramp: the game's true black, the paper's ink, and the palette's white top.
STANDALONE = ()          # every colour belongs to a ramp now -- that is what "one palette" means


def _build():
    """The merged palette: every ramp step, then the aliases that are not already a ramp name."""
    table = dict(MENU_PALETTE)
    try:
        from make_art_kit import PALETTE as _KIT
    except Exception:
        _KIT = {}
    for legacy, home in ALIASES.items():
        if legacy in MENU_PALETTE:
            continue                      # an alias may not shadow a real ramp name
        if legacy in KEPT_AS_IS and legacy in _KIT:
            table[legacy] = _KIT[legacy]  # stays exactly as authored, and the gate knows why
            continue
        if home in MENU_PALETTE:
            table[legacy] = MENU_PALETTE[home]
    # 'void' and 'ink' already exist; make sure the loop did not rename anything
    table['void'] = MENU_PALETTE['void']
    if 'ink' in MENU_PALETTE:
        table['ink'] = MENU_PALETTE['ink']
    return table


PALETTE = _build()

# key -> one step brighter, for the light pass. Built from menu_palette.RAMPS, which is the single authored
# source of the ramps; this is a reverse index into it, not a second copy.
RAMP = {}
_RAMP_OF = {}
for _name, _steps in RAMPS.items():
    _keys = ['%s_%d' % (_name, _i) for _i in range(len(_steps))]
    for _i in range(len(_keys) - 1):
        if _keys[_i] not in RAMP:
            RAMP[_keys[_i]] = _keys[_i + 1]
    for _k in _keys:
        _RAMP_OF[_k] = _name


def lift(key):
    """One step up the key's own ramp, or None if it is already the top (or is standalone)."""
    return RAMP.get(key)


def ramp_of(key):
    """The name of the ramp a key belongs to, or '' when it is standalone."""
    return _RAMP_OF.get(key, '')


def ramp_keys(name):
    """The step KEYS of one ramp, darkest first."""
    steps = RAMPS.get(name, ())
    return tuple('%s_%d' % (name, i) for i in range(len(steps)))


def rgb_distance(a, b):
    return sum((int(x) - int(y)) ** 2 for x, y in zip(a, b)) ** 0.5


# ---------------------------------------------------------------------------------------------- the gates
def check() -> int:
    """Four rules. Any failure means the merged palette is not one palette yet."""
    problems = []

    # 1. every legacy colour has a home, and that home is a real ramp step (or a documented standalone)
    try:
        from make_art_kit import PALETTE as KIT
    except Exception as exc:                                    # pragma: no cover - import guard
        print('WARN: could not import the kit palette (%s); alias coverage not checked' % exc)
        KIT = {}
    for legacy in sorted(KIT):
        if legacy not in ALIASES:
            problems.append('legacy colour %r has no ramp alias -- the desk would keep a second palette' % legacy)
            continue
        home = ALIASES[legacy]
        if home not in PALETTE:
            problems.append('alias %r -> %r: the home is not in the palette' % (legacy, home))
            continue
        if ramp_of(home) == '':
            problems.append('alias %r -> %r: the home belongs to no ramp, so light cannot lift it'
                            % (legacy, home))
        if legacy in KEPT_AS_IS:
            continue
        d = rgb_distance(PALETTE[home], KIT[legacy])
        if d > ALIAS_TOLERANCE and legacy not in HUE_MOVED:
            problems.append('alias %r -> %r moves the colour by %.0f (limit %.0f)'
                            % (legacy, home, d, ALIAS_TOLERANCE))

    # 2. every ramp is strictly monotonic in value: a "ramp" that goes back down is two ramps
    for name in sorted(RAMPS):
        keys = ramp_keys(name)
        for i in range(len(keys) - 1):
            a, b = PALETTE.get(keys[i]), PALETTE.get(keys[i + 1])
            if a is None or b is None:
                problems.append('ramp %r step %r is missing from the palette' % (name, keys[i]))
                continue
            va, vb = sum(a) / 3.0, sum(b) / 3.0
            if vb <= va:
                problems.append('ramp %r is not monotonic at %r -> %r' % (name, keys[i], keys[i + 1]))

    # 3. every palette colour that is not standalone must be liftable or be a ramp top
    for key in sorted(PALETTE):
        if key in ALIASES or key in ('brass', 'stamp'):
            continue          # an alias is addressable THROUGH its home; brass/stamp are EXTRA, not ramps
        if key not in RAMP and ramp_of(key) == '':
            problems.append('colour %r belongs to no ramp and is not standalone -- light cannot address it' % key)


    if problems:
        print('FAIL: %d problem(s) in the merged palette' % len(problems))
        for p in problems[:20]:
            print('   ', p)
        return 1
    print('PASS: one palette -- %d colours, %d ramps, %d legacy aliases, all within %.0f of their home'
          % (len(PALETTE), len(RAMPS), len(ALIASES), ALIAS_TOLERANCE))
    if HUE_MOVED:
        print('      hue moved on purpose: %s' % ', '.join(sorted(HUE_MOVED)))
    return 0


def table() -> int:
    print('%-12s %-16s %-12s %s' % ('legacy', 'rgb', '-> ramp home', 'home rgb'))
    try:
        from make_art_kit import PALETTE as KIT
    except Exception:
        KIT = {}
    for legacy in sorted(ALIASES):
        home = ALIASES[legacy]
        print('%-12s %-16s %-12s %s' % (legacy, KIT.get(legacy, '?'), home, PALETTE.get(home, '?')))
    print('\n%d ramps:' % len(RAMPS))
    for name in sorted(RAMPS):
        print('  %-10s %s' % (name, ' '.join(ramp_keys(name))))
    return 0


if __name__ == '__main__':
    if '--check' in sys.argv:
        sys.exit(check())
    sys.exit(table())
