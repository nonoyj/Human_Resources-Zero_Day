#!/usr/bin/env python3
"""
Builds art/kit/reference/composition_preview.png -- the real desk at 480x270 with an applicant
seated across the table, scaled 3x. This is the composition check the brief asks for:
left half = monitor, right half = the person across the table.

    python tools/art/make_composition_preview.py
"""
from __future__ import annotations

import os
import sys

from PIL import Image, ImageDraw

ROOT = os.path.normpath(os.path.join(os.path.dirname(__file__), '..', '..'))
KIT = os.path.join(ROOT, 'art', 'kit')
sys.path.insert(0, os.path.dirname(__file__))

ZOOM = 3
CASES = ['app_human_solid_a', 'app_synthetic_polished_a', 'app_human_nervous_b', 'app_synthetic_cloned_a']


def compose(name: str) -> Image.Image:
    bg = Image.open(os.path.join(KIT, 'backgrounds', 'office_L1.png')).convert('RGBA')
    app = Image.open(os.path.join(KIT, 'applicants', f'{name}_192x216.png')).convert('RGBA').crop((0, 0, 192, 216))
    bg.alpha_composite(app, (480 - 192 - 8, 270 - 216))                 # seated, right half
    bezel = Image.open(os.path.join(KIT, 'desk', 'monitor_bezel.png')).convert('RGBA')
    bg.alpha_composite(bezel, (14, 18))                                  # monitor, left half
    sheet = Image.open(os.path.join(KIT, 'desk', 'document_sheet.png')).convert('RGBA')
    bg.alpha_composite(sheet, (44, 148))                                 # the email on the desk
    return bg


def main() -> int:
    frames = []
    for name in CASES:
        f = compose(name)
        frames.append((name.replace('app_', ''), f.resize((f.width * ZOOM, f.height * ZOOM), Image.NEAREST)))

    PAD, LBL = 10, 14
    cw, ch = frames[0][1].size
    cols = 2
    rows = (len(frames) + cols - 1) // cols
    W = cols * (cw + PAD) + PAD
    H = rows * (ch + LBL + PAD) + PAD + 18
    out = Image.new('RGBA', (W, H), (11, 14, 19, 255))
    d = ImageDraw.Draw(out)
    d.text((PAD, 4), 'HR//0D  COMPOSITION  --  480x270 desk, applicant across the table (3x)', fill=(24, 224, 176, 255))
    for i, (name, im) in enumerate(frames):
        r, c = divmod(i, cols)
        x = PAD + c * (cw + PAD)
        y = 18 + PAD + r * (ch + LBL + PAD)
        d.text((x, y), name, fill=(230, 241, 255, 255))
        out.alpha_composite(im, (x, y + LBL))

    path = os.path.join(KIT, 'reference', 'composition_preview.png')
    out.save(path)
    print(f"wrote {path}  ({out.width}x{out.height})")
    return 0


if __name__ == '__main__':
    sys.exit(main())
