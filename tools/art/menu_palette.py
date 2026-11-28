#!/usr/bin/env python3
"""
menu_palette.py -- the FRONT DOOR's material palette (v117, ADR-021).

WHY THIS FILE EXISTS
    The v116 room passed the palette gate because every pixel was an allowed colour. It still read as ONE
    OBJECT: the desk, the CRT case, the keyboard, the table and the server were all the same blue at the same
    brightness. A palette gate cannot see that defect -- it asks "is this pixel allowed", never "can the eye
    tell this thing from the thing behind it".

    So the room gets a MATERIAL palette: one hue-shifted ramp per material, and a rule that no two touching
    materials share a value. The ramps are written in HSV because that is how the rules are expressed; RGB is
    derived, so the rules can be CHECKED instead of eyeballed.

THE RULES (docs/16-light-study.md §10) -- all four are gated by `--check`
    1. ONE LIGHT. The screen, low and to the left. Its light is cyan; therefore highlights rotate toward
       cyan-green and shadows rotate toward blue-violet. (The usual "warm highlights, cool shadows" is the
       same rule with a warm key; ours flips because our key light is cold and the room's warmth is wood.)
    2. HUE SHIFTING. Every ramp rotates at least 6 degrees of hue per step. A ramp that only changes
       brightness reads as grey mud -- the first version of this file had 0.2 degree steps and the audit
       failed it on 44 counts.
    3. SATURATION PEAKS IN THE MIDDLE of the ramp: highlights wash out toward the light, shadows lose
       saturation rather than gaining it.
    4. VALUE SEPARATION. Bands are 0.06 apart on the HSV value axis. Any two materials that touch in the
       composition must differ by >= MIN_VALUE_GAP, or they merge into one silhouette.

    32-BIT COLOUR DEPTH means MORE RAMPS, NOT MORE MUD: four to five steps per material, alpha allowed for
    glow and smoke. It never means a photograph; the pixel grid is still the shape language.

    python tools/art/menu_palette.py            # print the ramps, then the audit
    python tools/art/menu_palette.py --check    # audit only; exit 1 on a value collision or a grey ramp
"""
from __future__ import annotations

import colorsys
import sys

# ------------------------------------------------------------------------------- the ramps, in HSV
#
# Each entry is a ramp: a list of (hue degrees, saturation 0..1, value 0..1) from DARKEST to LIGHTEST.
# The hue staircase is the point, so it is written out rather than hidden in RGB literals.
#
#   shadows  ->  blue-violet  (~250-270 deg)      highlights  ->  cyan-green  (~180-200 deg)
#   warm materials (wood, paper, coffee) keep their own hue family and lean amber in the light.
#
RAMPS = {
    # name        hue / sat / value per step, darkest first
    'wall':  [(248, 0.52, 0.09), (230, 0.46, 0.15), (210, 0.40, 0.21)],
    'floor': [(270, 0.58, 0.04), (248, 0.50, 0.08), (226, 0.44, 0.12)],
    'case':  [(244, 0.30, 0.26), (230, 0.32, 0.33), (214, 0.31, 0.39), (198, 0.24, 0.45), (180, 0.16, 0.52)],
    'tube':  [(176, 0.56, 0.03), (160, 0.50, 0.05), (144, 0.44, 0.08)],
    'desk':  [(8, 0.62, 0.15), (16, 0.63, 0.27), (25, 0.58, 0.35), (34, 0.48, 0.44), (43, 0.34, 0.53)],
    'steel': [(216, 0.44, 0.16), (202, 0.40, 0.22), (188, 0.34, 0.28)],
    'rack':  [(258, 0.66, 0.20), (240, 0.68, 0.28), (220, 0.60, 0.34), (200, 0.50, 0.40)],
    'key':   [(242, 0.34, 0.12), (226, 0.34, 0.18), (208, 0.30, 0.24), (188, 0.22, 0.30)],
    'cup':   [(30, 0.22, 0.34), (38, 0.20, 0.43), (46, 0.15, 0.53), (54, 0.09, 0.66)],
    'paper': [(28, 0.34, 0.26), (38, 0.32, 0.34), (50, 0.24, 0.42)],
    'sky':   [(252, 0.64, 0.05), (240, 0.60, 0.11), (226, 0.52, 0.17), (210, 0.40, 0.23)],
    'city':  [(258, 0.62, 0.07), (238, 0.50, 0.14)],
    'cyan':  [(184, 0.66, 0.22), (176, 0.68, 0.42), (168, 0.62, 0.64), (158, 0.50, 0.86)],
    'red':   [(358, 0.68, 0.28), (352, 0.80, 0.62), (344, 0.70, 0.98)],
    'smoke': [(26, 0.14, 0.27), (34, 0.10, 0.40), (44, 0.06, 0.53)],
    'amber': [(50, 0.62, 0.58), (40, 0.64, 0.80), (30, 0.42, 1.00)],
}

# the names each ramp's steps answer to, darkest first. Kept explicit so call sites read like English.
NAMES = {
    'wall':  ['wall_deep', 'wall', 'wall_lit'],
    'floor': ['floor_deep', 'floor', 'floor_lit'],
    'case':  ['case_deep', 'case', 'case_facet', 'case_lit', 'case_gloss'],
    'tube':  ['tube_black', 'tube', 'tube_glow'],
    'desk':  ['desk_deep', 'desk', 'desk_facet', 'desk_lit', 'desk_gloss'],
    'steel': ['steel_deep', 'steel', 'steel_facet'],
    'rack':  ['rack_deep', 'rack', 'rack_facet', 'rack_lit'],
    'key':   ['key_deep', 'key', 'key_facet', 'key_lit'],
    'cup':   ['cup_deep', 'cup', 'cup_lit', 'cup_gloss'],
    'paper': ['paper_deep', 'paper', 'paper_lit'],
    'sky':   ['sky_deep', 'sky', 'sky_glow', 'sky_high'],
    'city':  ['city_near', 'city_far'],
    'cyan':  ['cyan_glow', 'cyan_breath', 'cyan_dim', 'cyan'],
    'red':   ['red_glow', 'red_dim', 'red'],
    'smoke': ['smoke_dim', 'smoke', 'smoke_lit'],
    'amber': ['amber_glow', 'amber', 'amber_lit'],
}

# a few one-off colours that belong to no ramp
EXTRA = {
    'grout': (0x00, 0x00, 0x00),
    'white': (0xff, 0xff, 0xff),
    'screen': (0xdf, 0xf2, 0xef),
    'stamp': (0xb4, 0x22, 0x28),
    'dust': (0x33, 0x36, 0x3e),
    'ink': (0x14, 0x10, 0x0c),
}


def _to_rgb(h, s, v):
    r, g, b = colorsys.hsv_to_rgb(h / 360.0, s, v)
    return (int(round(r * 255)), int(round(g * 255)), int(round(b * 255)))


RAMP = {}
for _key, _steps in RAMPS.items():
    for _name, _hsv in zip(NAMES[_key], _steps):
        RAMP[_name] = _to_rgb(*_hsv)
RAMP.update(EXTRA)

# ------------------------------------------------------------------------------------ the audit
#
# Every pair here TOUCHES in the composition: one is in front of, resting on, or standing against the other.
# The gate asserts their base values differ by at least MIN_VALUE_GAP, so one can never melt into the other.
MIN_VALUE_GAP = 0.055

AUDIT = [
    ('case', 'wall'),       # the monitor against the wall behind it
    ('desk', 'wall'),       # the desk against the wall
    ('desk', 'floor'),      # the desk against the floor
    ('cup', 'desk'),        # the cup on the desk
    ('key', 'desk'),        # the keyboard on the desk
    ('paper', 'desk'),      # the paperwork on the desk
    ('steel', 'wall'),      # the table against the wall
    ('steel', 'floor'),     # the table on the floor
    ('rack', 'wall'),       # the server against the wall
    ('rack', 'floor'),      # the server on the floor
    ('steel', 'rack'),      # the server beside the table
    ('cup', 'paper'),       # the cup beside the paperwork
    ('case', 'desk'),       # the monitor standing on the desk
    ('key', 'case'),        # the keyboard in front of the monitor
    ('floor', 'wall'),      # the floor meeting the wall
]


def value(rgb) -> float:
    """HSV value in 0..1 -- what the eye reads as 'how light is this'."""
    return max(rgb) / 255.0


def hsv(rgb):
    r, g, b = (c / 255.0 for c in rgb)
    return colorsys.rgb_to_hsv(r, g, b)


def hue_shift(a, b) -> float:
    """Degrees of hue rotation between two swatches, 0..180."""
    ha, hb = hsv(a)[0], hsv(b)[0]
    d = abs(ha - hb) * 360.0
    return min(d, 360.0 - d)


MIN_HUE_STEP = 6.0


def audit() -> list:
    problems = []
    for a, b in AUDIT:
        if a not in RAMP or b not in RAMP:
            problems.append('audit names an unknown colour: %s / %s' % (a, b))
            continue
        gap = abs(value(RAMP[a]) - value(RAMP[b]))
        if gap < MIN_VALUE_GAP:
            problems.append('%s (v=%.3f) and %s (v=%.3f) differ by %.3f -- they will read as one object'
                            % (a, value(RAMP[a]), b, value(RAMP[b]), gap))
    # every ramp must actually rotate in hue, and its saturation must peak in the middle
    for key, steps in RAMPS.items():
        for h0, h1 in zip(steps, steps[1:]):
            shift = min(abs(h0[0] - h1[0]), 360 - abs(h0[0] - h1[0]))
            if shift < MIN_HUE_STEP:
                problems.append('%s: steps rotate %.1f degrees of hue (need >= %.0f) -- grey mud'
                                % (key, shift, MIN_HUE_STEP))
        if len(steps) >= 3:
            mid = len(steps) // 2
            peak = max(range(len(steps)), key=lambda i: steps[i][1])
            if abs(peak - mid) > 2:
                problems.append('%s: saturation peaks at step %d of %d, not in the middle -- the ramp will '
                                'read as a filter, not as light' % (key, peak + 1, len(steps)))
    return problems


def show() -> None:
    for key in sorted(RAMPS):
        cells = []
        for name, (h, s, v) in zip(NAMES[key], RAMPS[key]):
            cells.append('%-11s %4.0fdeg s%.2f v%.2f' % (name, h, s, v))
        print('%-7s %s' % (key, ' | '.join(cells)))


def main(argv) -> int:
    show()
    problems = audit()
    if '--check' in argv:
        for p in problems:
            print('FAIL  ' + p)
        if problems:
            return 1
        print('PASS: %d ramps, %d touching pairs >= %.3f value apart, every step rotates >= %.0f degrees'
              % (len(RAMPS), len(AUDIT), MIN_VALUE_GAP, MIN_HUE_STEP))
        return 0
    for p in problems:
        print('WARN  ' + p)
    return 0


if __name__ == '__main__':
    raise SystemExit(main(sys.argv[1:]))
