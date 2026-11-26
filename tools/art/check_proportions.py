#!/usr/bin/env python3
"""HR//0D -- structural gate for the applicant sprites.

WHY: v7 shipped with the head floating 24 px above the torso and a jaw three times too long, and nobody
noticed until it was in front of the player. Those are measurable, so measure them.

    python tools/art/check_proportions.py          # check every sprite
    python tools/art/check_proportions.py --fix    # print the numbers only

Exits 1 on any violation. Run by scripts/Test-ArtPipeline.ps1.
"""
import os
import sys

ROOT = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..'))
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from pngcheck import read_rgba      # stdlib decoder, no Pillow needed


def rows_of(w, h, px):
    out = []
    for y in range(h):
        xs = [x for x in range(w) if px[y * w + x][3] > 0]
        out.append((xs[0], xs[-1], len(xs)) if xs else (None, None, 0))
    return out


def check(path):
    w, h, px = read_rgba(path)
    rows = rows_of(w, h, px)
    filled = [y for y in range(h) if rows[y][2] > 0]
    if not filled:
        return ['empty sprite']
    problems = []

    # 1. no hole in the figure -- a floating head is a hole
    gaps = [y for y in range(filled[0], filled[-1]) if rows[y][2] == 0]
    if gaps:
        problems.append('empty row(s) inside the figure: %s' % gaps[:5])

    # 2. head must be roughly round: width of the widest head row vs its height down to the neck
    neck = [y for y in filled if 0 < rows[y][2] < 22]
    if neck:
        head_rows = [y for y in filled if y < neck[0]]
        if head_rows:
            widest = max(head_rows, key=lambda y: rows[y][1] - rows[y][0])
            hw = rows[widest][1] - rows[widest][0] + 1
            hh = neck[0] - head_rows[0]
            ratio = hh / float(hw)
            if ratio > 1.6:
                problems.append('head is %.2f:1 tall:wide -- that is a cone, not a skull' % ratio)

    # 3. the neck must be wide enough to read as a neck, not a stalk
    if neck:
        widths = [rows[y][2] for y in neck]
        if sum(widths) / float(len(widths)) < 14:
            problems.append('neck averages %.0f px wide -- too thin' % (sum(widths) / float(len(widths))))

    # 4. head and body must touch
    if neck and neck[-1] + 1 < filled[-1] and rows[neck[-1] + 1][2] == 0:
        problems.append('neck does not reach the shoulders')

    return problems


def main():
    directory = os.path.join(ROOT, 'art', 'kit', 'applicants')
    failures = 0
    files = sorted(f for f in os.listdir(directory) if f.endswith('.png'))
    for f in files:
        problems = check(os.path.join(directory, f))
        if problems:
            failures += 1
            print('  FAIL %s' % f)
            for p in problems:
                print('       %s' % p)
    print('checked %d applicant sheets, %d with problems' % (len(files), failures))
    return 1 if failures else 0


if __name__ == '__main__':
    sys.exit(main())
