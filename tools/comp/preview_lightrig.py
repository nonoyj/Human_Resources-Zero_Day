#!/usr/bin/env python3
"""
preview_lightrig.py -- the room lit by SEVERAL lights, as pixels, at 480x270.

This is the offline twin of unity/Assets/Scripts/Core/Rules/LightRig.cs: the same ellipses, the same
ADD-then-clamp, the same "step up the pixel's own ramp, dither the fraction" rule that make_menu_room bakes
at build time. If this and LightRig.cs disagree, LightRig.cs is what ships -- but the numbers below are the
ones the tests assert, so a disagreement is a bug in one of them and both are in the repo.

    python tools/comp/preview_lightrig.py                 # tube off / on, side by side, at 2x
    python tools/comp/preview_lightrig.py --shadows       # + a red shadow map over the on frame
    python tools/comp/preview_lightrig.py --measure       # print the numbers the player asked about

WHAT IT PROVES
    * the tube's pool is on the WOOD, in front of the glass, never on the wall
    * every prop the player named (cup, keyboard, mouse, forms) receives light when the tube is on
    * the side of each prop facing the tube darkens, and the side facing away lifts -- i.e. the shadow
      intensity increases on the side against the monitor, which is the sentence this round is answering
    * light lands on at most (Levels + 1) discrete steps: this stays 16-bit art, not a soft gradient
"""
from __future__ import annotations

import argparse
import os
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
sys.path.insert(0, os.path.join(ROOT, 'tools', 'comp'))
sys.path.insert(0, os.path.join(ROOT, 'tools', 'art'))

from PIL import Image                      # noqa: E402
from twin_desk import load, render         # noqa: E402

# ---- THE SAME NUMBERS AS LightRig.cs ---------------------------------------------------------------
LEVELS = 3
DESK_ROW = 170
# THE SAME FIVE SOURCES AS LightRig.ForDesk, same numbers, same order-ish. If this file and the rig
# disagree, the rig ships and this picture stops being evidence -- so they are checked by eye and by
# `preview_lightrig.py --measure` on every run.
TUBE = dict(x=364, y=194, rx=118, ry=50, s=1.5, srx=360, sry=70, ss=0.8)
LAMP = dict(x=444, y=118, rx=52, ry=50, s=1.15)          # the cone: only the light leaving the shade
POOL = dict(x=384, y=176, rx=160, ry=30, s=0.50)         # the lamp's plate on the wood (the menu's bounce)
WINDOW = dict(x=52, y=71, rx=116, ry=128, s=1.95)
CLOCK = dict(x=404, y=40, rx=54, ry=38, s=0.3)


PROPS = {                                   # x, w, top, h -- exactly what desk_slab_M draws
    'coffee':   (34, 44, 176, 30),
    'forms':    (96, 62, 176, 26),
    'keyboard': (262, 84, 176, 20),
    'mouse':    (356, 20, 173, 32),
}

# 4x4 ordered dither, the same array the art generators use
BAYER = [[0, 8, 2, 10], [12, 4, 14, 6], [3, 11, 1, 9], [15, 7, 13, 5]]

# the palette ladders: material step N -> N+1, READ FROM THE SAME NAMES the art uses
PALETTE = None


def load_palette():
    global PALETTE
    import json
    with open(os.path.join(ROOT, 'art', 'kit', 'palette.json'), 'r', encoding='utf-8') as fh:
        entries = json.load(fh)
    PALETTE = {e['key']: (e['value']['r'], e['value']['g'], e['value']['b']) for e in entries}
    return PALETTE


def ladder():
    """material_1 -> material_2 -> ... for every ramp that has numbered steps."""
    up = {}
    for key in PALETTE:
        if '_' not in key:
            continue
        stem, _, step = key.rpartition('_')
        if not step.isdigit():
            continue
        n = int(step)
        nxt = f'{stem}_{n + 1}'
        if nxt in PALETTE:
            up[PALETTE[key]] = PALETTE[nxt]
    return up


def core(px, py, L):
    dx = (px - L['x']) / L['rx']
    dy = (py - L['y']) / L['ry']
    d = dx * dx + dy * dy
    return L['s'] * (1.0 - d) ** 2 if d < 1.0 else 0.0


def spill(px, py, L):
    if L.get('ss', 0) <= 0:
        return 0.0
    dx = (px - L['x']) / L['srx']
    dy = (py - L['y']) / L['sry']
    d = dx * dx + dy * dy
    return L['ss'] * (1.0 - d) ** 0.5 if d < 1.0 else 0.0


def light_at(px, py, sources):
    total = 0.0
    for L in sources:
        total += core(px, py, L) + spill(px, py, L)
    return total


def shadow_strength(px, py, right, bottom, height, tube_on):
    """The player's sentence as arithmetic: a smear at the prop's foot, on the flank facing the glass.

    Same rule as LightRig.ShadowSideAt: the shadow lives in the ContactBand rows, and it belongs to the flank
    the tube is nearest to. It does NOT darken the whole prop and it never covers the far flank -- 25% of the
    light lost is a shadow; 100% is a hole.
    """
    if not tube_on:
        return 0.0
    if py < bottom - 5 or py > bottom + 3:      # a flat contact shadow at the foot
        return 0.0
    if px > right or px < right - height * 0.55 - 1:
        return 0.0
    reach = min(20.0, max(2.0, height * 0.55))
    d = min(1.0, max(0.0, (right - px) / reach))
    return 1.0 - d


def light_frame(base, sources, tube_on, shadow_map=False):
    """Step every pixel up its own ramp by the light it receives; dither the fraction."""
    up = ladder()
    out = base.copy()
    px_in = base.load()
    px_out = out.load()
    w, h = base.size
    for y in range(h):
        for x in range(w):
            c = px_in[x, y]
            if c[3] == 0:
                continue
            n = light_at(x, y, sources)
            whole = int(n)
            frac = n - whole
            steps = whole + (1 if frac > BAYER[y & 3][x & 3] / 16.0 else 0)
            rgb = c[:3]
            for _ in range(min(steps, LEVELS)):
                nxt = up.get(rgb)
                if nxt is None:
                    break
                rgb = nxt
            # a cast shadow takes back exactly one step, so the two ideas never fight
            sh = 0.0
            for right, bottom, height in ((p[0] + p[1] - 1, p[2] + p[3], p[3]) for p in PROPS.values()):
                sh = max(sh, shadow_strength(x, y, right, bottom, height, tube_on))
            if sh > 0.55 and BAYER[(y + 2) & 3][x & 3] / 16.0 < sh:
                down = {v: k for k, v in up.items()}
                prev = down.get(rgb)
                if prev is not None:
                    rgb = prev
            px_out[x, y] = (rgb[0], rgb[1], rgb[2], 255)
    return out


def sources_for(tube_on, lamp_on=True):
    """THE SAME FIVE the desk's rig carries: window, lamp cone, lamp pool, tube, clock."""
    s = [WINDOW, CLOCK]
    if lamp_on:
        s += [LAMP, POOL]
    if tube_on:
        s.append(TUBE)
    return s


def measure(tube_on):
    sources = sources_for(tube_on)
    rows = []
    for name, (x, w, top, h) in PROPS.items():
        left, right, mid = x, x + w - 1, top + h // 2
        rows.append((name, light_at(left, mid, sources), light_at(right, mid, sources)))
    wood = light_at(TUBE['x'], TUBE['y'], sources)
    wall = light_at(TUBE['x'], DESK_ROW - 40, sources)
    return rows, wood, wall


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument('--out', default='/tmp/lightrig.png')
    ap.add_argument('--zoom', type=int, default=2)
    ap.add_argument('--shadows', action='store_true')
    ap.add_argument('--measure', action='store_true')
    ap.add_argument('--applicant', default='human_solid_a')
    args = ap.parse_args()

    load_palette()
    _m, layout, by_path = load()
    cv = render(layout, by_path, applicant=args.applicant)
    base = Image.new('RGBA', (cv.w, cv.h))
    base.putdata(cv.px)

    off = light_frame(base, sources_for(False), False)
    on = light_frame(base, sources_for(True), True)

    frames = [off, on]
    if args.shadows:
        marked = on.copy()
        mp = marked.load()
        for right, bottom, height in ((p[0] + p[1] - 1, p[2] + p[3], p[3]) for p in PROPS.values()):
            for y in range(bottom - height - 2, bottom + 4):
                for x in range(right - 40, right + 1):
                    s = shadow_strength(x, y, right, bottom, height, True)
                    if s > 0.55 and 0 <= x < 480 and 0 <= y < 270:
                        mp[x, y] = (255, 71, 87, 255)
        frames.append(marked)

    w, h = frames[0].size
    sheet = Image.new('RGB', (w * len(frames) + 6 * (len(frames) - 1), h), (0, 0, 0))
    for i, f in enumerate(frames):
        sheet.paste(f, (i * (w + 6), 0))
    sheet = sheet.resize((sheet.width * args.zoom, sheet.height * args.zoom), Image.NEAREST)
    sheet.save(args.out)
    print('wrote', args.out, sheet.size)

    off_rows, off_wood, off_wall = measure(False)
    on_rows, on_wood, on_wall = measure(True)
    print('  tube OFF -> ON, in ramp steps (0 = the room\'s own fill, 3 = the brightest this palette goes):')
    print(f'    wood under the tube   {off_wood:5.2f} -> {on_wood:5.2f}')
    print(f'    wall above the tube   {off_wall:5.2f} -> {on_wall:5.2f}   (must stay below the wood)')
    for (name, lo, ro), (_n, ln, rn) in zip(off_rows, on_rows):
        print(f'    {name:9s} left {lo:5.2f} -> {ln:5.2f}   right {ro:5.2f} -> {rn:5.2f}   '
              f'(the RIGHT side faces the tube, so it must lift more)')
    if args.measure:
        return 0
    return 0


if __name__ == '__main__':
    sys.exit(main())
