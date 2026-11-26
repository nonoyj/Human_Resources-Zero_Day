#!/usr/bin/env python3
"""
RAGGED -- the "used and hand-drawn" pass for kit art.

Reference: art/concepts/menu_concept_A.png. Nothing there is a perfect rectangle: bezels bulge, edges
wobble a pixel, corners are chipped, surfaces are scuffed. The first attempt at this (v1) deleted
random edge pixels and produced *static*, not age -- so the rules below are:

  1. SHAPE not NOISE. Wobble whole row/column runs by 1 px so the outline stops being a perfect run.
     Chipping is limited to corners. Speckling is sparse and low-contrast.
  2. NEVER warp a 9-slice border. Unity stretches those bands; a wobbling border smears in-game.
     For 9-slices pass `nine_slice=True` -- the pass then only scuffs the fill.
  3. Small sprites get less. Wear is proportional to area; a 16x16 icon does not get 50 scuffs.
  4. Deterministic + palette-safe. Same seed, same pixels, solid palette colours, alpha 0 or 255.

Usage:
    from ragged import distress
    distress(img, seed=1337, level=2)              # ordinary sprite
    distress(img, seed=1337, level=2, nine_slice=True)   # UI panel/button (borders untouched)
"""
import sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from make_art_kit import PALETTE, rgba   # noqa: E402


class LCG:
    """Reproducible PRNG: the same seed must give the same art on every machine, forever."""

    def __init__(self, seed: int):
        self.s = (seed * 6364136223846793005 + 1442695040888963407) & 0xFFFFFFFFFFFFFFFF
        if self.s == 0:
            self.s = 0x9E3779B97F4A7C15

    def next(self) -> float:
        self.s = (self.s * 6364136223846793005 + 1442695040888963407) & 0xFFFFFFFFFFFFFFFF
        return ((self.s >> 33) & 0xFFFFFF) / 0x1000000

    def below(self, p: float) -> bool:
        return self.next() < p

    def pick(self, seq):
        return seq[int(self.next() * len(seq)) % len(seq)]

    def irange(self, lo, hi):
        return lo + int(self.next() * (hi - lo + 1))


def _row(img, y):
    W = img.width
    px = img.load()
    return [px[x, y] for x in range(W)]


def _set_row(img, y, cells):
    W = img.width
    px = img.load()
    for x in range(W):
        px[x, y] = cells[x]


def _col(img, x):
    H = img.height
    px = img.load()
    return [px[x, y] for y in range(H)]


def _set_col(img, x, cells):
    H = img.height
    px = img.load()
    for y in range(H):
        px[x, y] = cells[y]


# --------------------------------------------------------------------------- 1. ragged shapes (DRAW TIME)
def _steps(rnd, length, step_p, amp=1):
    """A piecewise offset for one edge: which pixels are pushed out/in by `amp`."""
    offs = [0] * length
    i = 0
    while i < length:
        run = rnd.irange(2, max(2, length // 6))
        off = rnd.pick((0, 0, amp, -amp))
        for k in range(i, min(length, i + run)):
            offs[k] = off
        i += run
    return offs


def ragged_rect(img, x0, y0, x1, y1, fill, edge=None, seed=0, amp=1, fill_alpha=True):
    """Draw a rectangle whose four edges step by 1 px -- a hand-drawn box, not a CAD one.

    This is the primitive the RAGGED style is built from: our sprites are 1 px structures, so wear has
    to be built in while drawing. Post-processing a finished 1 px frame either does nothing or turns it
    into dashes (ragged v1..v3)."""
    W, H = img.size
    px = img.load()
    rnd = LCG(seed)
    w = x1 - x0 + 1
    h = y1 - y0 + 1
    if w < 4 or h < 4:
        from PIL import ImageDraw
        ImageDraw.Draw(img).rectangle([x0, y0, x1, y1], fill=rgba(fill))
        return
    top = _steps(rnd, w, 0.3, amp)
    bot = _steps(rnd, w, 0.3, amp)
    left = _steps(rnd, h, 0.3, amp)
    right = _steps(rnd, h, 0.3, amp)

    # fill body: per row, between the left and right offsets
    for j in range(h):
        y = y0 + j
        if not (0 <= y < H):
            continue
        xa = x0 + left[j]
        xb = x1 + right[j]
        for x in range(max(0, xa), min(W, xb + 1)):
            px[x, y] = rgba(fill)
    if edge:
        for i in range(w):
            x = x0 + i
            if not (0 <= x < W):
                continue
            for y in (y0 + top[i], y1 + bot[i]):
                if 0 <= y < H:
                    px[x, y] = rgba(edge)
        for j in range(h):
            y = y0 + j
            if not (0 <= y < H):
                continue
            for x in (x0 + left[j], x1 + right[j]):
                if 0 <= x < W:
                    px[x, y] = rgba(edge)


def notch_corner(img, x, y, dx, dy, depth=3, tone=None):
    """Bite a stepped corner off -- the "dropped box" corner. dx/dy point inward."""
    W, H = img.size
    px = img.load()
    for k in range(depth):
        for j in range(depth - k):
            X, Y = x + dx * j, y + dy * k
            if 0 <= X < W and 0 <= Y < H:
                px[X, Y] = (0, 0, 0, 0)
    if tone:
        X, Y = x + dx * depth, y + dy * depth
        if 0 <= X < W and 0 <= Y < H:
            px[X, Y] = rgba(tone)


# --------------------------------------------------------------------------- 2. corner chips
def chip_corners(img, seed, per_corner=2):
    """Bite a small triangle out of up to `per_corner` pixels at each corner of the opaque bounding box.
    Corners are where wear shows first and where a chip is unmistakably intentional."""
    W, H = img.size
    px = img.load()
    xs = [x for x in range(W) if any(px[x, y][3] for y in range(H))]
    ys = [y for y in range(H) if any(px[x, y][3] for x in range(W))]
    if not xs or not ys:
        return 0
    x0, x1, y0, y1 = min(xs), max(xs), min(ys), max(ys)
    rnd = LCG(seed ^ 0xC4)
    removed = 0
    for (cx, cy, dx, dy) in ((x0, y0, 1, 1), (x1, y0, -1, 1), (x0, y1, 1, -1), (x1, y1, -1, -1)):
        if not rnd.below(0.7):
            continue
        size = rnd.irange(1, per_corner)
        for k in range(size):
            for j in range(size - k):
                X, Y = cx + dx * j, cy + dy * k
                if 0 <= X < W and 0 <= Y < H and px[X, Y][3]:
                    px[X, Y] = (0, 0, 0, 0)
                    removed += 1
    return removed


def close_notches(img, passes=2):
    """Close 1 px gaps created by wobbling: a transparent pixel whose left AND right neighbours share
    the same colour is a tear, not a hole -- fill it. Same vertically. This keeps a 1 px frame
    continuous and wavy instead of dashed (ragged v2 lesson)."""
    W, H = img.size
    px = img.load()
    for _ in range(passes):
        filled = 0
        for y in range(H):
            for x in range(1, W - 1):
                if px[x, y][3]:
                    continue
                left, right = px[x - 1, y], px[x + 1, y]
                if left[3] and right[3] and left == right:
                    px[x, y] = left
                    filled += 1
        for x in range(W):
            for y in range(1, H - 1):
                if px[x, y][3]:
                    continue
                up, down = px[x, y - 1], px[x, y + 1]
                if up[3] and down[3] and up == down:
                    px[x, y] = up
                    filled += 1
        if not filled:
            break
    return filled


# --------------------------------------------------------------------------- 3. surface scuffs
def scuff(img, seed, n, tones=('panel', 'void'), box=None):
    """Sparse low-contrast speckles on opaque pixels. Count is capped by area so small sprites stay clean."""
    W, H = img.size
    px = img.load()
    area = sum(1 for y in range(0, H, 2) for x in range(0, W, 2) if px[x, y][3]) * 4
    cap = max(2, min(n, area // 900))          # ~1 scuff per 900 opaque px, never more than n
    rnd = LCG(seed ^ 0xA11CE)
    x0, y0, x1, y1 = box if box else (0, 0, W - 1, H - 1)
    done = 0
    for _ in range(cap * 40):
        if done >= cap:
            break
        x, y = rnd.irange(x0, x1), rnd.irange(y0, y1)
        if not (0 <= x < W and 0 <= y < H) or not px[x, y][3]:
            continue
        tone = rnd.pick(tones)
        if px[x, y][:3] == PALETTE[tone]:
            continue
        px[x, y] = rgba(tone)
        done += 1
    return done


# --------------------------------------------------------------------------- 4. ragged torn edge
def torn_edge(img, seed, axis='bottom', depth=2):
    """Give a paper-like sprite a torn edge along one side (cover letters, documents)."""
    W, H = img.size
    px = img.load()
    rnd = LCG(seed ^ 7011)
    if axis in ('bottom', 'top'):
        edge_y = H - 1 if axis == 'bottom' else 0
        step = -1 if axis == 'bottom' else 1
        for x in range(W):
            if rnd.below(0.45):
                d = rnd.irange(1, depth)
                for k in range(d):
                    Y = edge_y + step * k
                    if 0 <= Y < H:
                        px[x, Y] = (0, 0, 0, 0)
    else:
        edge_x = W - 1 if axis == 'right' else 0
        step = -1 if axis == 'right' else 1
        for y in range(H):
            if rnd.below(0.45):
                d = rnd.irange(1, depth)
                for k in range(d):
                    X = edge_x + step * k
                    if 0 <= X < W:
                        px[X, y] = (0, 0, 0, 0)


# --------------------------------------------------------------------------- the one call artists use
# CALIBRATED DOWN 2026-11-26 (user: "the PC looks too ragged"). The original numbers turned a
# 154x106 monitor into debris. Wear is now sparse by default and only becomes obvious at level 3.
LEVELS = {
    1: dict(chip=1, scuff=4),    # owned for a year
    2: dict(chip=1, scuff=9),    # office surplus  <-- the kit is authored at this level
    3: dict(chip=2, scuff=16),   # salvage
}


def distress(img, seed: int, level=2, nine_slice=False, paper=False,
             scuff_tones=('panel', 'void')):
    """Make a finished sprite look used. Call LAST, after all drawing is done.

    nine_slice=True : do not touch the border bands (Unity stretches them) -- scuff the fill only.
    paper=True      : also tear one edge (documents, letters, sticky notes).
    """
    cfg = LEVELS[level]
    stats = {}
    if not nine_slice:
        stats['chipped'] = chip_corners(img, seed, per_corner=cfg['chip'])
    stats['scuffed'] = scuff(img, seed, n=cfg['scuff'], tones=scuff_tones)
    if paper and not nine_slice:
        torn_edge(img, seed, axis='bottom', depth=max(1, level))
    return stats


def verify(img, name='sprite'):
    vals = set(PALETTE.values())
    bad = 0
    px = img.load()
    for y in range(img.height):
        for x in range(img.width):
            p = px[x, y]
            if p[3] == 0:
                continue
            if p[3] != 255 or p[:3] not in vals:
                bad += 1
    if bad:
        raise SystemExit(f'{name}: {bad} off-palette / partial-alpha pixels')
    return True


if __name__ == '__main__':
    from PIL import Image
    root = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
    samples = [('art/kit/desk/monitor_small.png', 2, False, False),
               ('art/kit/ui/panel_9slice.png', 2, True, False),
               ('art/kit/desk/id_card.png', 2, False, True)]
    tiles = []
    for rel, lvl, ns, pap in samples:
        p = os.path.join(root, rel)
        if not os.path.exists(p):
            continue
        clean = Image.open(p).convert('RGBA')
        worn = clean.copy()
        st = distress(worn, 1337, level=lvl, nine_slice=ns, paper=pap)
        verify(worn, rel)
        gap = 10
        row = Image.new('RGBA', (clean.width * 2 + gap, max(clean.height, worn.height)), (11, 14, 19, 255))
        row.alpha_composite(clean, (0, 0))
        row.alpha_composite(worn, (clean.width + gap, 0))
        tiles.append(row.resize((row.width * 3, row.height * 3), Image.NEAREST))
        print(f'{rel:38s} level {lvl}  {st}')
    sheet = Image.new('RGB', (max(t.width for t in tiles), sum(t.height + 8 for t in tiles)), (11, 14, 19))
    y = 0
    for t in tiles:
        sheet.paste(t.convert('RGB'), (0, y))
        y += t.height + 8
    out = os.path.join(root, 'art', 'concepts', 'ragged_proof.png')
    sheet.save(out)
    print('wrote', os.path.relpath(out, root), sheet.size)
