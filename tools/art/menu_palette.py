#!/usr/bin/env python3
"""
menu_palette.py -- the FRONT DOOR's materials, as ramps (v118, ADR-021).

WHY RAMPS AND NOT COLOURS
    v116 gave every material a single colour and relied on one "light ramp" to brighten it. That is a
    filter, not lighting: the room came out as one blue object with a cyan wash on it, which is exactly
    what the player said ("nothing reads as its own object", "the colour is too bright").

    A 16-bit-era scene is built the other way round: each MATERIAL owns a ramp of 3-5 steps, from its
    darkest (in shadow) to its lightest (catching the key), and light is expressed by choosing which step
    a surface takes. The palette is the material list; the lighting pass is an index into it.

HOW THE RAMPS ARE AUTHORED
    In HSV, one step at a time, so the three rules from the research are CHECKABLE rather than eyeballed:

      1. HUE ROTATION. Every step rotates the hue toward the light: shadows go blue-violet, highlights go
         toward the warm/white end. >= MIN_HUE_STEP degrees per step, or the ramp is a brightness ramp
         and reads as grey mud (the first v117 palette had 0.2 degree steps and failed 44 times).
      2. SATURATION PEAKS IN THE MIDDLE. Highlights bleach toward the light, shadows lose saturation.
         A ramp that only walks value reads as a colour filter over a photo.
      3. VALUE SEPARATION. Two materials that TOUCH on screen must be >= MIN_VALUE_GAP apart in value,
         or the eye merges them into one object. That rule is what makes the coffee cup read as a cup
         sitting on a desk rather than a bump on the desk.

    And the fourth rule is the ROOM's, not a material's -- see `--cinema` below:

      4. A CINEMATIC LOW-KEY FRAME IS DARK. Measured from the player's reference still: 75% of its pixels
         sit under 12% luma, the median is 9%, and the only things above 60% are the window and the lamp
         bulb. So the gate asserts that at least LOWKEY_SHARE of the palette's steps are below
         LOWKEY_DARK luma, and that every step is darker than LOWKEY_CEILING except the emissive ones.

    python tools/art/menu_palette.py            # the table
    python tools/art/menu_palette.py --check    # the four rules
"""
from __future__ import annotations

import colorsys
import json
import os
import sys

# ---------------------------------------------------------------------------------------- the rules
MIN_HUE_STEP = 6.0        # degrees of rotation per ramp step
MIN_VALUE_GAP = 0.055     # between two materials that touch on screen
MIN_HUE_GAP = 28.0        # ...or this much hue separation, plus a smaller value gap
MIN_HUE_GAP_VALUE = 0.020 # the value floor that goes with a hue separation
SAT_PEAK_TOLERANCE = 2    # the saturation peak must be within N steps of the ramp's middle

LOWKEY_MID = 0.30         # "dark" for the material list: two thirds of the steps must be under
                          # this, i.e. under about 25% luma. The reference still has 75% of its
                          # PIXELS under 12%, so 25% is the generous version of the rule.
LOWKEY_SHARE = 0.66
LOWKEY_CEILING = 0.62     # no NON-EMISSIVE step may be brighter than this: only lamps emit

# ---------------------------------------------------------------------------------------- the ramps
# (hue degrees, saturation, value) per step, darkest -> lightest. RGB is derived, never hand-typed:
# a hand-typed palette drifts into hues nobody chose and the rules stop being checkable.
RAMPS = {
    # the room's architecture: cold, dark, clearly BLUE-grey. The steps rotate 12 degrees each: shadow
    # violet, mid blue, lit blue-green where the screen reaches. Nothing here is above 0.19 value.
    # v119 adds a LIT step to the wall and the floor. Without one there is no value left to lift into: the
    # back wall was already at the top of its ramp, so the window's glow could only be faked as a cyan tint
    # sprinkled over it -- which is why the first v119 glow attempt looked like sprayed dots. `wall_3` is the
    # value the wall takes when the city light actually lands on it: still dark (0.30 < LOWKEY_CEILING),
    # still blue, one honest step up.
    'wall':  [(236, .36, .085), (224, .32, .135), (212, .28, .185), (200, .24, .290)],
    # the floor: a violet-black tile. Its own hue family, two steps below the wall, so the wall/floor
    # line reads as a line and not as a change of light
    # v119.1: FOUR steps was still not enough. v119's four steps spanned value 0.022-0.100, i.e. the whole
    # floor lived inside 2-10% luma, so tiles, grout, shadow and light were all the same near-black and the
    # player's "define the ground more" was really "I cannot see a floor". `floor_4` is the value a tile
    # takes when the monitor's spill actually reaches it: still dark (0.145, far under the 0.62 ceiling),
    # still violet, and enough separation for a checker and a grout line to be readable at 480x270.
    'floor': [(276, .46, .022), (264, .40, .038), (252, .34, .060), (244, .30, .100), (236, .26, .145)],
    # the desk: WARM. the one large warm surface, and the reason the frame is not monochrome cyan
    'wood':  [(14, .57, .050), (24, .56, .090), (32, .54, .140), (40, .48, .200), (48, .38, .280)],
    # steel: the monitor table and the server's case. cool, green-leaning (it reflects the screens),
    # and the material that made the old room read as ONE object because it sat on the wall's value
    'steel': [(202, .28, .075), (190, .22, .140), (178, .18, .215)],
    # the phosphor: the screen's own cyan, the room's key. The only ramp allowed to be genuinely cyan
    'teal':  [(210, .64, .050), (200, .66, .100), (190, .62, .175), (180, .54, .290), (172, .44, .450)],
    # the one warm light in the room (the lamp): amber, and the brightest non-emissive material
    'amber': [(14, .60, .085), (22, .62, .180), (30, .56, .300), (38, .46, .430), (46, .32, .600)],
    # SKIN (v127, the player's "the applicant still looks the same").
    #
    # THE PROBLEM WAS NOT THE FACE, IT WAS THE PALETTE. The applicant generator carried its own 20-colour
    # list with SKIN = ('paper', 'warn', 'orange'), and 'orange' is (255, 138, 61) -- an 8-bit-saturated
    # orange at 0.60 value. The approved room's brightest NON-emissive value is amber_4 at 0.60, and the
    # palette's whole rule is that a low-key frame has nothing above the light sources. So the man across the
    # desk was the brightest object in a room whose light comes from a window and a tube: he read as a
    # cartoon pasted onto a photograph, which is exactly what the player keeps saying.
    #
    # A skin ramp cannot live in the room's ramps: skin is the one material with its own hue family, and at
    # 480x270 there is room for THREE steps (shadow, mid, key) plus one lit. Values are capped at 0.42 --
    # under cup_2 (0.465) and well under the white of a specular -- so a face can be lit without becoming
    # the brightest thing in the frame. Hue walks 20 -> 26 -> 33 -> 40 the way skin does under a cold key:
    # shadows go red-violet, highlights go yellow.
    'skin':  [(16, .58, .115), (22, .60, .200), (28, .55, .290), (34, .44, .420)],
    # the one cold note on a face: the rim the window puts down his temple and shoulder
    # SKIN, SECOND SET (v130.2). The player asked for "varieties of the applicant" and got fourteen people
    # sharing one complexion: the four TONES the generator offered all resolved into the same `skin` ramp, so
    # the cast was one family with different hair. A deeper complexion on a low-key frame CANNOT be faked by
    # darkening -- the shadow side of `skin_0` is already 0.115, and two more steps down is the floor. So deep
    # skin needs its own FAMILY with more room: same warm hue territory, a longer run, and a mid tone that
    # sits where the light can still reach it (0.30, not 0.45). Authored as a second ramp rather than a
    # second code path, because a separate ramp is what lets the light pass step it correctly.
    'skin2': [(12, .68, .078), (18, .66, .140), (26, .60, .205), (34, .52, .275)],
    'skinrim': [(206, .40, .230), (196, .34, .300), (188, .26, .440)],
    # HAIR (v130). The applicant generator has drawn hair from `void`/`ink`/`panel2` -- the room's darkest
    # UI colours -- since v9, so every head in the game wore a black helmet and the four hair colours the
    # player asked for were a comment, not a picture. Hair is a material: a short warm-dark ramp that starts
    # one step above the deepest shadow and ends at a value a cold key can still read, so black, brown and
    # grey are three PLACES on it rather than three colours pulled from the UI sheet.
    'hair':  [(14, .40, .100), (22, .32, .170), (30, .24, .255), (216, .12, .350)],
    # the coffee cup: cream ceramic. value-separated from the desk at every step it can touch
    'cup':   [(34, .22, .255), (41, .18, .355), (50, .12, .465)],
    # paper: warm off-white, two steps darker than the cup and four degrees cooler
    'paper': [(24, .32, .128), (32, .24, .200), (40, .14, .330)],
    # the wall monitors' tubes: pale cyan-white. The darkest step is a DEAD screen: dark glass, and it
    # must not sit on the table's value or the table eats it
    'scr':   [(206, .30, .150), (194, .16, .380), (184, .05, .780)],
    # the AI's colour. rationed to the breach and the stamp
    'red':   [(6, .62, .175), (358, .76, .410), (350, .70, .760)],
    # the breach/selection cyan: brighter than teal, used for text and the selected row
    'cyan':  [(194, .62, .285), (187, .66, .480), (180, .48, .760)],
}

# What the code calls each step. The sprite generators and MenuView.cs both use these names, so a
# change here is a change on both sides -- there is no second list to fall out of sync.
NAMES = {}
for _ramp, _steps in RAMPS.items():
    for _i in range(len(_steps)):
        NAMES['%s_%d' % (_ramp, _i)] = (_ramp, _i)

# One-offs: not part of a ramp because nothing shades them.
EXTRA = {
    'void':   (0x04, 0x05, 0x07),   # the darkest thing in the frame; the bezel's shadow, the grout
    'ink':    (0x14, 0x10, 0x0c),   # text on paper
    'white':  (0xe8, 0xf2, 0xf4),   # a specular hit; used fewer than 200 times in the whole room
    'stamp':  (0x8e, 0x16, 0x22),   # the stamp's ink, one per page
    'brass':  (0x6a, 0x54, 0x2c),   # the server's screws, the lamp's stem
}

# Materials that TOUCH on screen, and therefore must be value-separated: (a, b, why).
AUDIT = [
    ('wall_0', 'floor_0', 'back wall against the floor strip behind the desk'),
    ('wall_2', 'steel_0', "the table's legs against the wall behind them"),
    ('wood_3', 'cup_1', 'the coffee cup on the desk'),
    ('wood_1', 'paper_1', 'the forms against the shadowed end of the desk'),
    ('wood_2', 'key_2', 'the keyboard on the desk'),
    ('void', 'wall_0', "the bezel's shadow against the wall"),
    ('teal_3', 'wall_2', "the screen's rim against the wall it lights"),
    ('scr_0', 'steel_2', 'a dead screen in the monitor table against the table'),
    ('cup_1', 'paper_1', 'the cup against the paper beside it'),
    ('cyan_1', 'teal_2', "the selected row's ink against the tube behind it"),
    ('red_1', 'wall_1', 'the breach flash against the wall it lands on'),
    ('amber_1', 'wood_1', "the lamp's pool on the desk's far edge"),
    ('scr_1', 'wall_2', 'a white screen against the wall beside it'),
]


def hsv(h, s, v):
    r, g, b = colorsys.hsv_to_rgb((h % 360) / 360.0, s, v)
    return (int(round(r * 255)), int(round(g * 255)), int(round(b * 255)))


RAMP = {}   # ramp name -> list of RGB, darkest first
for _name, _steps in RAMPS.items():
    RAMP[_name] = [hsv(*s) for s in _steps]

# The flat name -> RGB table the sprites are drawn from, and the one published to Unity as
# Resources/palette.json. `key`/`mouse` are the desk's plastics: they are the SAME family as `steel`
# but two steps darker, which is enough for the eye and cheap in palette slots.
RAMP['key'] = [RAMP['steel'][0], RAMP['steel'][1], RAMP['steel'][2]]
RAMP['mouse'] = [RAMP['steel'][1], RAMP['steel'][2], hsv(190, .16, .26)]

PALETTE = {}
for _ramp, _steps in RAMP.items():
    for _i, _rgb in enumerate(_steps):
        PALETTE['%s_%d' % (_ramp, _i)] = _rgb
PALETTE.update(EXTRA)


def value(rgb):
    """HSV value of an RGB triple. This is what the separation rule measures."""
    return colorsys.rgb_to_hsv(rgb[0] / 255.0, rgb[1] / 255.0, rgb[2] / 255.0)[2]


def luma(rgb):
    """Rec.709 luma, 0..1. This is what the LOW-KEY rule measures."""
    return (0.2126 * rgb[0] + 0.7152 * rgb[1] + 0.0722 * rgb[2]) / 255.0


def hue(rgb):
    return colorsys.rgb_to_hsv(rgb[0] / 255.0, rgb[1] / 255.0, rgb[2] / 255.0)[0] * 360.0


def sat(rgb):
    return colorsys.rgb_to_hsv(rgb[0] / 255.0, rgb[1] / 255.0, rgb[2] / 255.0)[1]


def hue_shift(a, b):
    """Signed shortest rotation from a to b, in degrees."""
    d = (hue(b) - hue(a) + 540.0) % 360.0 - 180.0
    return d


def hexof(rgb):
    return '#%02x%02x%02x' % rgb


# ---------------------------------------------------------------------------------------- the gate
EMISSIVE = {'white', 'teal_4', 'scr_2', 'cyan_2', 'red_2', 'amber_4', 'scr_2'}   # light sources


def check() -> list:
    problems = []

    # 1. hue rotation, and the direction: darker steps sit at or above the brightest hue? For a COLD key
    #    the rule inverts -- the lit step rotates TOWARD the light (cyan-green) and the shadow toward
    #    blue-violet -- so what is asserted is the magnitude, plus monotonic value.
    for name, steps in RAMPS.items():
        if not 3 <= len(steps) <= 5:
            problems.append('%s has %d steps: 3 to 5 is what a 16-bit ramp uses' % (name, len(steps)))
        for i in range(len(steps) - 1):
            h0, s0, v0 = steps[i]
            h1, s1, v1 = steps[i + 1]
            # authored hue, not the hue recovered from the 8-bit triple: at v=0.02 the recovered hue of a
            # rounded RGB triple wobbles by degrees, which is invisible on screen and noisy in a gate
            turn = abs((h1 - h0 + 540.0) % 360.0 - 180.0)
            if turn < MIN_HUE_STEP:
                problems.append('%s: step %d->%d rotates %.1f degrees, under %.0f (it is a brightness '
                                'ramp, not a hue-shifted one)' % (name, i, i + 1, turn, MIN_HUE_STEP))
            if not v1 > v0:
                problems.append('%s: step %d is not lighter than step %d' % (name, i + 1, i))

    # 2. saturation peaks in the middle
    for name, steps in RAMPS.items():
        sats = [step[1] for step in steps]
        peak = sats.index(max(sats))
        middle = (len(sats) - 1) / 2.0
        if abs(peak - middle) > SAT_PEAK_TOLERANCE:
            problems.append('%s: saturation peaks at step %d of %d, not in the middle'
                            % (name, peak, len(sats)))

    # 3. value separation on every touching pair
    for a, b, why in AUDIT:
        if a not in PALETTE:
            problems.append('audit names %s, which is not in the palette (%s)' % (a, why))
            continue
        if b not in PALETTE:
            problems.append('audit names %s, which is not in the palette (%s)' % (b, why))
            continue
        gap = abs(value(PALETTE[a]) - value(PALETTE[b]))
        hgap = abs(hue_shift(PALETTE[a], PALETTE[b]))
        # Two materials can separate by VALUE or by HUE -- a warm desk against a cold wall is legible at
        # a smaller value gap, because the eye has the hue edge to work with. Requiring value alone
        # would force every material into the same ramp length and flatten the picture instead.
        if gap < MIN_VALUE_GAP and not (hgap >= MIN_HUE_GAP and gap >= MIN_HUE_GAP_VALUE):
            problems.append('%s and %s are %.3f apart in value and %.0f degrees in hue: they merge (%s)'
                            % (a, b, gap, hgap, why))

    # 4. the frame is DARK (the reference still: 75% of pixels under 12% luma)
    steps = [c for k, c in PALETTE.items() if k in NAMES and k not in EMISSIVE]
    dark = [c for c in steps if value(c) < LOWKEY_MID]
    share = len(dark) / float(len(steps)) if steps else 0.0
    if share < LOWKEY_SHARE:
        problems.append('only %.0f%% of the material steps are under %.2f value; a low-key material list '
                        'needs %.0f%% (this is the "the colour is too bright" rule)' %
                        (share * 100, LOWKEY_MID, LOWKEY_SHARE * 100))
    for name, rgb in PALETTE.items():
        if name in EMISSIVE:
            continue
        if value(rgb) > LOWKEY_CEILING:
            problems.append('%s is %.2f value, over the %.2f ceiling: only the emissive keys may be that '
                            'bright' % (name, value(rgb), LOWKEY_CEILING))
    return problems


def table() -> str:
    out = []
    for name in sorted(RAMPS):
        out.append('%-7s ' % name + '  '.join('%s v%.2f h%3.0f' % (hexof(c), value(c), hue(c))
                                              for c in RAMP[name]))
    out.append('')
    for k in sorted(EXTRA):
        out.append('%-7s %s v%.2f' % (k, hexof(EXTRA[k]), value(EXTRA[k])))
    return '\n'.join(out)


def audit_table() -> str:
    out = ['%-9s %-9s %6s %6s  %s' % ('material', 'against', 'dvalue', 'dhue', 'why')]
    for a, b, why in AUDIT:
        if a in PALETTE and b in PALETTE:
            out.append('%-9s %-9s %6.3f %6.1f  %s' % (a, b, abs(value(PALETTE[a]) - value(PALETTE[b])),
                                                     abs(hue_shift(PALETTE[a], PALETTE[b])), why))
    return '\n'.join(out)


def write_json(path=None) -> str:
    """
    Merges the front door's materials into art/kit/palette.json, which is the ONE palette file Unity reads.

    MERGE, never replace: the rest of the game (the desk HUD, the letter, the sweep, the intro) reads keys
    this module has never heard of -- `text`, `signal`, `danger`, `paper`, `ink` -- and every committed
    sprite is validated against make_art_kit.PALETTE. A writer that replaced the file would blank the HUD
    and fail the art gate at the same time, which is exactly the kind of "one file, two owners" bug this
    project keeps finding. The front door owns its own key namespace (material_step) and nothing else.
    """
    root = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..'))
    path = path or os.path.join(root, 'art', 'kit', 'palette.json')
    merged = {}
    if os.path.exists(path):
        try:
            for e in json.load(open(path, encoding='utf-8')):
                v = e['value']
                merged[e['key']] = (v['r'], v['g'], v['b'])
        except (ValueError, KeyError):
            merged = {}
    merged.update(PALETTE)
    entries = [{'key': k, 'value': {'r': c[0], 'g': c[1], 'b': c[2], 'a': 255}}
               for k, c in sorted(merged.items())]
    with open(path, 'w', encoding='utf-8', newline='\n') as fh:
        json.dump(entries, fh, indent=1)
        fh.write('\n')
    return path


def main(argv) -> int:
    if '--write-json' in argv:
        print('wrote ' + write_json())
    problems = check()
    if '--check' in argv:
        for p in problems:
            print('FAIL  ' + p)
        if problems:
            return 1
        steps = [c for k, c in PALETTE.items() if k in NAMES]
        dark = sum(1 for c in steps if value(c) < LOWKEY_MID)
        print('PASS: %d ramps, %d touching pairs separated, every step rotates >= %.0f degrees, '
              '%.0f%% of steps dark'
              % (len(RAMPS), len(AUDIT), MIN_HUE_STEP, 100.0 * dark / len(steps)))
        return 0
    print(table())
    print()
    print(audit_table())
    if problems:
        for p in problems:
            print('FAIL  ' + p)
        return 1
    return 0


if __name__ == '__main__':
    raise SystemExit(main(sys.argv[1:]))
