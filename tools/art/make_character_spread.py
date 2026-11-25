#!/usr/bin/env python3
"""
Proves how varied the applicant pool pulls. Renders 36 humans and 18 synthetics by
walking every identity slot (skin, hair, outfit, accent, headwear, glasses, facial hair,
build) in lockstep, then prints how many distinct visual combinations the generator can
produce so nobody has to take it on faith.

    python tools/art/make_character_spread.py
"""
from __future__ import annotations

import os
import sys

from PIL import Image, ImageDraw

sys.path.insert(0, os.path.dirname(__file__))
from make_applicants import ACCENT, HAIR, OUTFIT, SKIN, Applicant  # noqa: E402

OUT = os.path.normpath(os.path.join(os.path.dirname(__file__), '..', '..', 'art', 'kit', 'reference'))
ZOOM = 2
COLS = 6


def combos() -> int:
    """Upper bound on distinct looks, before artifacts."""
    return len(SKIN) * len(HAIR) * len(OUTFIT) * len(ACCENT) * 6 * 3 * 4 * 5


def build(index: int, synthetic: bool) -> Image.Image:
    # start from a seed, then stride through the identity slots so neighbours always differ
    a = Applicant(3 + index * 7, synthetic=synthetic, artifacts=())
    a.skin_light, a.skin_mid, a.skin_dark = SKIN[index % len(SKIN)]
    a.hair = HAIR[(index * 3 + 1) % len(HAIR)]
    a.outfit = OUTFIT[(index * 5 + 2) % len(OUTFIT)]
    a.accent = ACCENT[(index * 2) % len(ACCENT)]
    a.hair_style = index % 6
    a.glasses = (index % 3 == 1)
    a.facial_hair = 0 if synthetic else [0, 1, 2, 3][index % 4]
    a.build = 40 + (index % 5) * 4
    a.age_lines = (index % 7 == 3)
    return a.draw().crop((0, 0, 192, 216))


def block(items, title, tint):
    cells = []
    for label, im in items:
        bg = Image.new('RGBA', im.size, (19, 25, 38, 255))
        bg.alpha_composite(im)
        big = bg.resize((im.width * ZOOM, im.height * ZOOM), Image.NEAREST)
        cells.append((label, big))

    cw, ch = cells[0][1].width, cells[0][1].height
    rows = (len(cells) + COLS - 1) // COLS
    pad, lbl = 6, 12
    W = COLS * (cw + pad) + pad
    H = 20 + rows * (ch + lbl + pad) + pad
    blk = Image.new('RGBA', (W, H), (11, 14, 19, 255))
    d = ImageDraw.Draw(blk)
    d.text((pad, 4), title, fill=tint)
    for i, (label, im) in enumerate(cells):
        r, c = divmod(i, COLS)
        x = pad + c * (cw + pad)
        y = 20 + r * (ch + lbl + pad)
        d.text((x, y), label, fill=(169, 199, 228, 255))
        blk.alpha_composite(im, (x, y + lbl))
    return blk


def main() -> int:
    humans = [(f'h{i + 1:02d}', build(i, False)) for i in range(36)]
    synth = [(f's{i + 1:02d}', build(i, True)) for i in range(18)]

    top = block(humans, 'HUMANS 36 / 36 -- identity slots walked in lockstep, zero artifacts', (24, 224, 176, 255))
    bot = block(synth, 'SYNTHETICS 18 / 18 -- same slot walk (artifacts off here, see artifact_legend)', (255, 71, 87, 255))

    W = max(top.width, bot.width)
    out = Image.new('RGBA', (W, top.height + bot.height + 10), (11, 14, 19, 255))
    out.alpha_composite(top, (0, 0))
    out.alpha_composite(bot, (0, top.height + 10))

    os.makedirs(OUT, exist_ok=True)
    path = os.path.join(OUT, 'character_spread.png')
    out.save(path)
    print(f"wrote {path}  ({out.width}x{out.height})")
    print(f"distinct looks available to the spawner: {combos():,} "
          f"(skins {len(SKIN)} x hair {len(HAIR)} x outfits {len(OUTFIT)} x accents {len(ACCENT)} "
          f"x 6 headwear x 3 glasses x 4 facial-hair x 5 builds)")
    return 0


if __name__ == '__main__':
    sys.exit(main())
