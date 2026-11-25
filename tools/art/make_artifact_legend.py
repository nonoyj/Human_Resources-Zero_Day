#!/usr/bin/env python3
"""
Builds art/kit/reference/artifact_legend.png -- a side-by-side HUMAN vs AI index of every tell.

Why this file matters: it is the single visual reference for three people at once.
  - FE: knows exactly which sprite region gets an inspectable hotspot
  - Content: writes the Field Manual wording against a picture, not a table
  - Design review: you approve the tells visually in one look

Each row = same seed, same body, same pose. Left crop is the human, right crop is the
synthetic carrying exactly one artifact. Zoom is 4x with NEAREST so pixels stay honest.

    python tools/art/make_artifact_legend.py
"""
from __future__ import annotations

import os
import sys

from PIL import Image, ImageDraw

sys.path.insert(0, os.path.dirname(__file__))
from make_applicants import Applicant  # noqa: E402

OUT = os.path.normpath(os.path.join(os.path.dirname(__file__), '..', '..', 'art', 'kit', 'reference'))

# artifact -> (seed, crop box (x0, y0, x1, y1) on the 192x216 sprite, label, what the player is told)
ROWS = [
    ('seam', 101, (64, 0, 128, 40),
     'HAIR BLEND SEAM',
     'hairline meets the air on a dead-straight edge -- no strand, no taper'),
    ('mirror', 101, (64, 32, 128, 74),
     'PERFECT SYMMETRY',
     'both catchlights identical; beauty mark on BOTH cheeks'),
    ('ear', 30, (52, 30, 106, 68),
     'FEATURELESS EAR',
     'one ear is a flat slab -- no inner fold, no shadow'),
    ('glasses_merge', 163, (64, 34, 128, 66),
     'GLASSES MERGE',
     'temple arm dissolves into the temple of the head, no gap'),
    ('skin_band', 131, (70, 84, 126, 108),
     'POSTERISED SKIN STEP',
     'a hard band at the jaw where a gradient belongs'),
    ('hands', 131, (28, 172, 64, 202),
     'MERGED FINGERS',
     'the hand is a smear -- no separable digits'),
    ('straight_badge', 127, (74, 114, 122, 164),
     'PERFECTLY CENTRED BADGE',
     'lanyard dead vertical and centred -- no human slump'),
    ('shadow', 113, (24, 186, 192, 216),
     'IMPOSSIBLE SHADOW',
     'its cast shadow runs TOWARD the monitor light, hard-edged, and stops dead'),
]

ZOOM = 4
PAD = 14
LABEL_W = 186
ROW_LABEL_H = 32
TAG_H = 12


def main() -> int:
    cells = []
    for name, seed, box, label, note in ROWS:
        human = Applicant(seed, synthetic=False, artifacts=()).draw().crop(box)
        synth = Applicant(seed, synthetic=True, artifacts=(name,)).draw().crop(box)

        def prep(im, outline):
            bg = Image.new('RGBA', im.size, (19, 25, 38, 255))
            bg.alpha_composite(im)
            big = bg.resize((im.width * ZOOM, im.height * ZOOM), Image.NEAREST)
            framed = Image.new('RGBA', (big.width + 2, big.height + 2), outline)
            framed.alpha_composite(big, (1, 1))
            return framed

        cells.append((label, note, prep(human, (24, 224, 176, 255)), prep(synth, (255, 71, 87, 255))))

    pair_w = max(c[2].width + c[3].width + 10 for c in cells)
    pair_h = max(max(c[2].height, c[3].height) for c in cells)
    row_h = pair_h + ROW_LABEL_H + TAG_H + PAD
    W = LABEL_W + pair_w + PAD * 2
    H = row_h * len(cells) + PAD + 30

    sheet = Image.new('RGBA', (W, H), (11, 14, 19, 255))
    d = ImageDraw.Draw(sheet)
    d.text((PAD, 9), 'HR//0D  ·  AI ARTIFACT LEGEND  ·  same seed, one artifact added', fill=(24, 224, 176, 255))
    d.text((PAD, 21), 'green = human applicant      red = the synthetic that slipped through', fill=(122, 152, 178, 255))

    y = PAD + 30
    for label, note, himg, simg in cells:
        d.text((PAD, y), label, fill=(255, 209, 102, 255))
        # wrap the note to two short lines
        words, line, lines = note.split(), '', []
        for wd in words:
            if len(line) + len(wd) + 1 > 25:
                lines.append(line)
                line = wd
            else:
                line = (line + ' ' + wd).strip()
        lines.append(line)
        for i, ln in enumerate(lines[:3]):
            d.text((PAD, y + 13 + i * 11), ln, fill=(169, 199, 228, 255))

        x = PAD + LABEL_W
        d.text((x, y + 2), 'HUMAN', fill=(24, 224, 176, 255))
        sheet.alpha_composite(himg, (x, y + TAG_H + 2))
        x += himg.width + 10
        d.text((x, y + 2), 'AI', fill=(255, 71, 87, 255))
        sheet.alpha_composite(simg, (x, y + TAG_H + 2))
        y += row_h

    os.makedirs(OUT, exist_ok=True)
    path = os.path.join(OUT, 'artifact_legend.png')
    sheet.save(path)
    print(f"wrote {path}  ({sheet.width}x{sheet.height})")
    return 0


if __name__ == '__main__':
    sys.exit(main())
