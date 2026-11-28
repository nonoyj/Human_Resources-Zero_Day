#!/usr/bin/env python3
"""
Wear overlays -- RAGGED level drift driven by corruption (ADR-003, user decision 2026-11-26).

The kit is *authored* at RAGGED level 2 ("office surplus"). Repainting three rooms for three
corruption states was agreed against months ago; the approved method is one overlay sprite on top
of the room. These are those overlays.

CorruptionLevel already exists in ShiftController (0..3 = synthetics hired). The mapping is:

    corruption 0  -> no overlay                (the room as drawn: level 2)
    corruption 1  -> fx/wear_a.png             (level 2.5: hairline cracks, more scuffs)
    corruption 2  -> fx/wear_b.png             (level 3 "salvage")
    corruption 3  -> fx/wear_b.png + runtime red tint (already implemented)

Overlays are FULL-SCREEN 480x270 RGBA, transparent where untouched, palette colours only,
alpha strictly 0 or 255 (no partial alpha -- the palette gate forbids it).

    python tools/art/make_wear_overlays.py
    python tools/art/make_wear_overlays.py --check
"""
from __future__ import annotations

import argparse
import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from make_art_kit import PALETTE, rgba                       # noqa: E402
from pngcheck import read_rgba                               # noqa: E402

try:
    from PIL import Image
    HAVE_PIL = True
except ImportError:                                          # pragma: no cover
    Image = None
    HAVE_PIL = False

OUT = os.path.join(os.path.dirname(__file__), '..', '..', 'art', 'kit', 'fx')
W, H = 480, 270

PALETTE_SET = set(PALETTE.values())


def _one_palette():
    """The merged palette (v120). The overlays are painted with the kit's colours and then pass through
    art_palette.py --apply, which moves the structural navy onto the front door's ramp steps; both sets are
    legal pixels, and validating against the kit alone made the alias pass fail this gate."""
    try:
        import art_palette
        return {tuple(rgb) for rgb in art_palette.PALETTE.values()}
    except Exception:                                   # noqa: BLE001
        return set()


ONE_PALETTE_SET = _one_palette()

# damage inks: a crack is a dark void line with a lit edge; a scuff is one flat panel tone
CRACK = ('void', 'line')
SCUFF_INK = ('panel', 'panel2', 'line')
BURN = ('dangerdim', 'panel')


class LCG:
    """Same PRNG family as ragged.py -- deterministic, no dependency, identical every run."""

    def __init__(self, seed):
        self.s = (seed * 6364136223846793005 + 1442695040888963407) & 0xFFFFFFFFFFFFFFFF

    def next(self):
        self.s = (self.s * 6364136223846793005 + 1442695040888963407) & 0xFFFFFFFFFFFFFFFF
        return ((self.s >> 33) & 0xFFFFFF) / 0x1000000

    def below(self, p):
        return self.next() < p

    def pick(self, seq):
        return seq[int(self.next() * len(seq)) % len(seq)]

    def irange(self, lo, hi):
        return lo + int(self.next() * (hi - lo + 1))


def _put(img, x, y, tone):
    if 0 <= x < W and 0 <= y < H:
        img.putpixel((x, y), rgba(tone))


def crack(img, rnd, x, y, length=60, branch_p=0.18, side=1):
    """A hairline crack: travels mostly DIAGONALLY (a vertical run with branches reads as ivy),
    forks at most twice, and always ends in a single line rather than a spray."""
    for i in range(length):
        if rnd.below(0.28):
            x += side
        if rnd.below(0.60):
            y += 1
        _put(img, x, y, CRACK[0])
        if rnd.below(0.45):
            _put(img, x + 1, y, CRACK[1])          # lit edge, so the crack reads as depth
        if rnd.below(branch_p) and i < length - 20:
            bx, by = x, y
            for _k in range(rnd.irange(4, 9)):
                bx += side * rnd.pick((0, 1, 1))
                if rnd.below(0.5):
                    by += 1
                _put(img, bx, by, CRACK[0])


def scuff_marks(img, rnd, n, box=None):
    """Sparse flat scuffs: 1-4 px strokes on the surface, biased to the frame edges."""
    x0, y0, x1, y1 = box or (0, 0, W - 1, H - 1)
    for _ in range(n):
        if rnd.below(0.6):                              # edges of the screen first -- that is
            x = rnd.irange(x0, x0 + 26) if rnd.below(0.5) else rnd.irange(x1 - 26, x1)
            y = rnd.irange(y0, y1)
        else:
            x, y = rnd.irange(x0, x1), rnd.irange(y0, y1)
        tone = rnd.pick(SCUFF_INK)
        for k in range(rnd.irange(1, 4)):
            _put(img, x + k, y, tone)


def dead_pixels(img, rnd, n):
    """Burned-out pixel clusters -- the screen itself is failing, not just the room."""
    for _ in range(n):
        x, y = rnd.irange(0, W - 2), rnd.irange(0, H - 2)
        tone = rnd.pick(BURN)
        _put(img, x, y, tone)
        if rnd.below(0.4):                          # a small clump, not a scattered dot
            _put(img, x + 1, y, tone)
        if rnd.below(0.4):
            _put(img, x, y + 1, tone)


def stain(img, rnd, cx, cy, r):
    """One irregular damp patch: concentric steps, never a circle."""
    for ring in range(r, 0, -1):
        tone = 'panel' if ring > r // 2 else 'panel2'
        for a in range(0, 360, 9):
            rad = math.radians(a)
            rr = ring + (1 if rnd.below(0.22) else 0)
            _put(img, int(cx + rr * math.cos(rad)), int(cy + rr * math.sin(rad) * 0.7), tone)


def build(stage):
    img = Image.new('RGBA', (W, H), (0, 0, 0, 0))
    rnd = LCG(0x57EA0 + (0 if stage == 'a' else 1))    # deterministic per stage
    if stage == 'a':
        scuff_marks(img, rnd, 80)
        for _ in range(2):
            crack(img, rnd, rnd.irange(30, W - 60), rnd.irange(8, 40),
                  length=rnd.irange(16, 26), side=rnd.pick((-1, 1)))
        dead_pixels(img, rnd, 7)
    else:
        scuff_marks(img, rnd, 170)
        # cracks start at structural seams (wall head, corners) and run down-diagonally
        for _ in range(4):
            crack(img, rnd, rnd.irange(24, W - 70), rnd.irange(0, 26),
                  length=rnd.irange(30, 52), side=rnd.pick((-1, 1)))
        for _ in range(2):
            crack(img, rnd, rnd.pick((6, W - 6)), rnd.irange(120, 200),
                  length=rnd.irange(20, 34), side=1 if rnd.below(0.5) else -1)
        dead_pixels(img, rnd, 18)
        stain(img, rnd, rnd.irange(300, 440), rnd.irange(60, 150), 9)
    return img


def check():
    import struct
    bad, count = [], 0
    for f in ('wear_a.png', 'wear_b.png'):
        p = os.path.join(OUT, f)
        if not os.path.exists(p):
            bad.append((f, 'missing'))
            continue
        count += 1
        w, h, pixels = read_rgba(p)
        if (w, h) != (W, H):
            bad.append((f, f'size {w}x{h}'))
            continue
        for px in pixels:
            if px[3] == 0:
                continue
            if (px[:3] not in PALETTE_SET and px[:3] not in ONE_PALETTE_SET) or 0 < px[3] < 255:
                bad.append((f, px))
                break
    print(f"checked {count} wear overlays")
    if bad:
        for row in bad[:8]:
            print("   ", row)
        print("FAIL: overlay problems above")
        return 1
    print("PASS: overlays are palette-conformant, alpha 0/255, 480x270")
    return 0


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('--check', action='store_true')
    args = ap.parse_args()
    if args.check:
        return check()
    if not HAVE_PIL:
        print("Pillow is required to regenerate overlays; --check needs only the stdlib.")
        return 2
    os.makedirs(OUT, exist_ok=True)
    for stage in ('a', 'b'):
        build(stage).save(os.path.join(OUT, f'wear_{stage}.png'))
    print(f"wrote 2 wear overlays to art/kit/fx")
    return 0


if __name__ == '__main__':
    sys.exit(main())
