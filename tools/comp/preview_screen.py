#!/usr/bin/env python3
"""
preview_screen.py -- render the CRT screen exactly as MonitorScreen + MonitorScreenRaster will.

This is the fast feedback loop for the screen: it composes the real monitor sprite with the real
text layout and the real glyph atlas, so what you see here is what the phone gets. It is NOT a mock.

    python tools/comp/preview_screen.py                  # default sample applicant
    python tools/comp/preview_screen.py --zoom 4 --out screen_preview.png
    python tools/comp/preview_screen.py --trust 12 --day 5 --name "BARTOLOME VILLANUEVA-DELA CRUZ"
"""
from __future__ import annotations

import argparse
import json
import os
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
KIT = os.path.join(ROOT, 'art', 'kit')
sys.path.insert(0, os.path.join(ROOT, 'tools', 'art'))

try:
    from PIL import Image
    HAVE_PIL = True
except ImportError:                                            # pragma: no cover
    Image = None
    HAVE_PIL = False

# READABILITY (user, 2026-11-26: "text too close to each other"). Two changes, both measurable:
#   letterGap 1 -> 2 : glyphs stop touching, so a word reads as a word instead of a bar of pixels
#   lineGap   2 -> 3 : descenders and the caps of the next row no longer visually merge
# Glyph metrics live in ONE place: this constant block is mirrored by MonitorScreen in C#, and
# ScreenMetricsTests asserts the two agree, so the preview can never drift from the game.
GLYPH_W, GLYPH_H = 5, 7
LETTER_GAP, LINE_GAP = 2, 3
COLUMNS, ROWS = 20, 9            # 20*7-2 = 138 <= 142 glass width; 9*7+8*3 = 87 <= 88 glass height


def load_metrics():
    with open(os.path.join(KIT, 'fonts', 'monitor_font_5x7.json'), 'r', encoding='utf-8') as fh:
        return json.load(fh)


def load_palette():
    import make_art_kit
    return {k: (v[0], v[1], v[2], 255) for k, v in make_art_kit.PALETTE.items()}


def glass_rect(monitor_img):
    """Same formula as MonitorScreen.GlassForSprite: x0=6, yTop=6, right 6, bottom 12."""
    w, h = monitor_img.width, monitor_img.height
    return 6, 12, w - 12, h - 18            # x, y(bottom, y-up), w, h


def trust_bar(trust):
    t = max(0, min(100, trust))
    filled = (t + 5) // 10
    return '[' + '#' * filled + '-' * (10 - filled) + '] ' + str(t).rjust(3)


def build_lines(name, role, idnum, uni, grad, emp, exp, ref, day, trust, show_trust=True):
    """Mirror of MonitorScreen.BuildLines: a CARD, not a document.

    The full record (university, employer, letter) lives in the letter view -- user decision (c),
    2026-11-26. What fits on the glass is: day, trust, who is in front of you, and the prompt.
    """
    lines = [(f'DAY {day:02d}', 'textdim')]
    lines.append((trust_bar(trust) if show_trust else 'TRUST  --', 'signal'))
    lines.append((name.upper(), 'text'))
    if role:
        lines.append((role, 'textdim'))
    lines.append(('ID ' + idnum, 'paperdim'))
    facts = []
    if grad:
        facts.append(f'GRAD {grad}')
    if exp:
        facts.append(f'EXP {exp}Y')
    if facts:
        lines.append(('  '.join(facts), 'textdim'))
    if ref:
        lines.append(('REF ' + ref, 'paperdim'))
    lines.append(('> READ LETTER', 'warn'))
    return lines


def layout(lines, glass):
    """Mirror of MonitorScreen.LinesToRects: y-up, line 1 at the TOP, clip to rows."""
    gx, gy, gw, gh = glass
    cols = (gw + LETTER_GAP) // (GLYPH_W + LETTER_GAP)
    rows = (gh + LINE_GAP) // (GLYPH_H + LINE_GAP)
    out = []
    emitted = 0
    for text, colour in lines:
        if not text:
            continue
        if emitted >= rows:
            break
        fitted = text if len(text) <= cols else text[:cols - 1] + '~'
        y = gy + gh - GLYPH_H - emitted * (GLYPH_H + LINE_GAP)
        out.append((fitted, colour, gx, y))
        emitted += 1
    return out


def render(name='MARIA SANTOS', role='DATA ENTRY II', idnum='8814-2231',
           uni='CEBU NORMAL UNIVERSITY', grad=2019, emp='NORTHSTAR LOGISTICS',
           exp=6, ref='WALK-IN', day=1, trust=100):
    metrics = load_metrics()
    atlas = Image.open(os.path.join(KIT, 'fonts', metrics['atlasName'] if 'atlasName' in metrics
                                    else 'monitor_font_5x7.png')).convert('RGBA')
    pal = load_palette()
    monitor = Image.open(os.path.join(KIT, 'desk', 'monitor_small.png')).convert('RGBA')
    glass = glass_rect(monitor)

    screen = Image.new('RGBA', (glass[2], glass[3]), pal['void'])
    rects = {g['ch']: (g['x'], g['y']) for g in metrics['glyphs']}

    for text, colour, x, y_up in layout(build_lines(name, role, idnum, uni, grad, emp, exp, ref,
                                                    day, trust), glass):
        ink = pal.get(colour, pal['text'])
        pen = 0
        for ch in text:
            origin = rects.get(ch) or rects.get(ch.upper())
            if origin:
                glyph = atlas.crop((origin[0], origin[1], origin[0] + GLYPH_W, origin[1] + GLYPH_H))
                top_row = glass[3] - 1 - (y_up - glass[1]) - (GLYPH_H - 1)
                tinted = Image.new('RGBA', (GLYPH_W, GLYPH_H), ink)
                screen.paste(tinted, (pen, top_row), glyph)
            pen += GLYPH_W + LETTER_GAP

    # paste into the monitor: screen-local (0,0) is the glass top-left in image space
    out = monitor.copy()
    gx, gy, gw, gh = glass
    img_y = monitor.height - (gy + gh)
    out.alpha_composite(screen, (gx, img_y))
    return out


# --------------------------------------------------------------------------- codepage (no Pillow)
# The GATE must be able to run without Pillow (verifying art never requires a pip install -- BUG-016).
# So the preview can also emit a plain-text code page: one character per glyph cell, using '#' for ink
# and '.' for glass. It is not pretty, but it proves the layout, the clipping and the metrics on any
# Python 3 with no dependencies at all.
PALETTE_KEYS = {
    'void': ' ', 'text': '#', 'textdim': '+', 'signal': 'S', 'paperdim': 'p', 'muted': '-',
}


def codepage(**kw):
    metrics = load_metrics()
    glass = (6, 12, 142, 88)
    lines = layout(build_lines(kw.get('name', 'MARIA SANTOS'), kw.get('role', 'DATA ENTRY II'),
                              kw.get('idnum', '8814-2231'), kw.get('uni', 'CEBU NORMAL UNIVERSITY'),
                              kw.get('grad', 2019), kw.get('emp', 'NORTHSTAR LOGISTICS'),
                              kw.get('exp', 6), kw.get('ref', 'WALK-IN'),
                              kw.get('day', 1), kw.get('trust', 100)), glass)
    cols = (glass[2] + LETTER_GAP) // (GLYPH_W + LETTER_GAP)
    rows = (glass[3] + LINE_GAP) // (GLYPH_H + LINE_GAP)
    grid = [['.'] * cols for _ in range(rows)]
    for text, colour, _x, _y in lines:
        row = rows - 1 - (_y - glass[1]) // (GLYPH_H + LINE_GAP)
        if row < 0:
            continue
        for c, ch in enumerate(text[:cols]):
            grid[row][c] = PALETTE_KEYS.get(colour, '#') if ch != ' ' else PALETTE_KEYS.get(colour, '#')
    # The gate also asserts the text is really there, so append an ASCII dump of the lines. A grid of
    # '#' alone proves geometry but not content; this proves content without an image library.
    return metrics, cols, rows, grid, lines


def write_codepage(path, **kw):
    metrics, cols, rows, grid, lines = codepage(**kw)
    with open(path, 'w', encoding='ascii') as fh:
        fh.write(f"# monitor screen, {cols}x{rows} cells, glyph {metrics['cellW']}x{metrics['cellH']}\n")
        fh.write("# legend: # text  + textdim  S signal  p paperdim  - muted  . empty\n")
        for r in range(rows):
            fh.write('|' + ''.join(grid[r]).ljust(cols) + '|\n')
        fh.write('# text content\n')
        for text, colour, _x, _y in lines:
            fh.write(f'{colour:9s} {text}\n')
    return path


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('--zoom', type=int, default=4)
    ap.add_argument('--out', default='screen_preview.png')
    ap.add_argument('--name', default='MARIA SANTOS')
    ap.add_argument('--day', type=int, default=1)
    ap.add_argument('--trust', type=int, default=100)
    ap.add_argument('--codepage', metavar='PATH',
                    help='write a text-only code page (no Pillow needed) -- used by the gate')
    args = ap.parse_args()

    if args.codepage:
        write_codepage(args.codepage, name=args.name, day=args.day, trust=args.trust)
        print('wrote', args.codepage)
        return 0

    if not HAVE_PIL:
        print('preview needs Pillow; use --codepage for a dependency-free check')
        return 2

    img = render(name=args.name, day=args.day, trust=args.trust)
    dest = os.path.join(ROOT, 'art', 'kit', 'reference', args.out)
    img.resize((img.width * args.zoom, img.height * args.zoom), Image.NEAREST).convert('RGB').save(dest)
    print('wrote', os.path.relpath(dest, ROOT), f'({img.width}x{img.height} @ {args.zoom}x)')


if __name__ == '__main__':
    main()
