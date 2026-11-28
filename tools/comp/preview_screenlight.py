#!/usr/bin/env python3
"""HR//0D -- the tube's light, as a picture, on the twin render.

WHY THIS EXISTS (v120)
    `ScreenLight.cs` bakes the monitor's light into the room sprite in Unity, using only operations the
    palette allows: walk a pixel UP its own ramp, dither the fraction, and push the shadow side DOWN. That
    is a rule, not an image -- so this script applies the *same rule* to the same composition
    `tools/comp/twin_desk.py` renders, offline, at 3x, where it can be looked at without opening Unity.

    If the picture and the game disagree, the RULE is wrong, not the picture. Keep them in step:
    `python3 tools/comp/preview_screenlight.py --state on --out /tmp/tube_on.png`

    The numbers are the ones in ScreenLight.cs and MenuLayout.MonitorLight():
        rx = glassRx * (1.15 + 0.55 * level)     the ellipse is ~2.3x the tube wide at full
        ry = glassRy * (0.80 + 0.30 * level)
        cy = glassY + 26                         biased DOWN onto the desk
        desk rows are lit at 1.0, wall rows at 0.55
        shadows run to the RIGHT of each prop (the monitor is on the left), length = height * 0.55
"""
from __future__ import annotations

import argparse
import os
import sys

from PIL import Image

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), '..', '..'))
sys.path.insert(0, os.path.join(ROOT, 'tools', 'art'))

from make_art_kit import PALETTE          # noqa: E402
from twin_desk import render, load        # noqa: E402  (sys.path is fixed by twin_desk itself)

DESK_ROW = 206
# MenuLayout.MonitorLight(): the glass centre and a 232x196 ellipse, strength 1.70, falloff 1
GLASS_X, GLASS_Y, GLASS_RX, GLASS_RY = 91, 140, 116, 98

# the props that stand on the desk: (left, width, top, height). Same list as ScreenLight.Props.
PROPS = [(152, 90, 232, 26), (248, 12, 236, 10), (300, 26, 214, 44), (370, 24, 222, 34), (196, 14, 234, 12)]


def ramps():
    """The SAME ladder PaletteLadder.cs builds at runtime: `material_N` -> `material_N+1`.

    NOT a luminance guess. Sorting the palette by brightness and pairing neighbours is what turned an
    orange into a gold and a blue-grey into a cream -- a cold material lit with a warm one is exactly the
    mistake the palette gate exists to stop. The relationship is authored in the names, so it is read from
    the names. A key that does not parse ('brass', 'stamp') has no step, which is correct: nothing lights it.
    """
    by_key = dict(PALETTE)
    up, down = {}, {}
    for key, rgb in by_key.items():
        cut = key.rfind('_')
        if cut <= 0:
            continue
        n = key[cut + 1:]
        if not n.isdigit():
            continue
        nxt = '%s_%d' % (key[:cut], int(n) + 1)
        if nxt in by_key:
            up[rgb] = by_key[nxt]
            down[by_key[nxt]] = rgb
    return up, down


def apply(img: Image.Image, level: float, dither=True) -> Image.Image:
    out = img.convert('RGB').copy()
    px = out.load()
    w, h = out.size
    up, down = ramps()
    # THE POOL IS ON THE DESK, not on the wall (v120, second pass). A screen faces the player, so its light
    # lands on the surface BETWEEN the tube and the viewer and on the props standing there. Centring the
    # ellipse on the glass lit the wall behind the monitor -- the same mistake as the lamp -- and left the
    # keyboard, the mug and the folders at their authored value, which is why the first pass looked off.
    rx = GLASS_RX * (1.30 + 0.75 * level)
    ry = GLASS_RY * (0.86 + 0.34 * level)
    cy = GLASS_Y + 104
    lit = 0
    for y in range(h):
        for x in range(w):
            dx = (x - GLASS_X) / rx
            dy = (y - cy) / ry
            r = (dx * dx + dy * dy) ** 0.5
            if r >= 1.0:
                continue
            on_desk = 1.0 if y >= DESK_ROW else 0.30
            amount = (1.0 - r) ** 2 * level * on_desk
            whole = int(amount * 2.4)
            frac = amount * 2.4 - whole
            if dither and (frac > 0.62 or (frac > 0.30 and ((x + y) & 1) == 0)):
                whole += 1
            c = px[x, y]
            for _ in range(whole):
                if c not in up:
                    break
                c = up[c]
            if c != px[x, y]:
                px[x, y] = c
                lit += 1
    # shadows: to the RIGHT of each prop (the tube is on the left)
    for (pxt, pw, ptop, ph) in PROPS:
        length = int(ph * 0.55 * level)
        for i in range(length):
            x = pxt + pw + i
            if x >= w:
                break
            k = 1.0 - i / max(1, length)
            if k < 0.35:
                continue
            for y in range(DESK_ROW, min(h, DESK_ROW + ph)):
                if k < 0.7 and ((x + y) & 1):
                    continue
                c = px[x, y]
                if c in down:
                    px[x, y] = down[c]
    return out


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument('--state', choices=('on', 'idle', 'off'), default='on')
    ap.add_argument('--out', default='/tmp/tube.png')
    ap.add_argument('--applicant', default='human_solid_a')
    ap.add_argument('--once', action='store_true', help='skip the side-by-side sheet')
    args = ap.parse_args()

    _manifest, layout, by_path = load()
    base = render(layout, by_path, applicant=args.applicant)
    flat = bytearray()
    for (r, g, b, a) in base.px:
        flat += bytes((r, g, b, a))
    raw = Image.frombytes('RGBA', (base.w, base.h), bytes(flat)).convert('RGB')
    level = {'on': 1.0, 'idle': 0.45, 'off': 0.0}[args.state]
    lit = apply(raw, level)
    if args.once or args.state == 'off':
        lit.resize((lit.width * 3, lit.height * 3), Image.NEAREST).save(args.out)
        print('wrote %s (%dx%d)' % (args.out, lit.width * 3, lit.height * 3))
        return 0
    sheet = Image.new('RGB', (raw.width, raw.height * 2 + 4), (255, 0, 255))
    sheet.paste(raw, (0, 0))
    sheet.paste(lit, (0, raw.height + 4))
    sheet.resize((sheet.width * 2, sheet.height * 2), Image.NEAREST).save(args.out)
    print('wrote %s -- top: no tube light, bottom: %s (level %.2f)' % (args.out, args.state, level))
    return 0


if __name__ == '__main__':
    raise SystemExit(main())
