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
except ImportError:                                            # pragma: no cover
    print('Pillow is required for the preview: pip install pillow')
    raise SystemExit(2)

GLYPH_W, GLYPH_H, LETTER_GAP, LINE_GAP = 5, 7, 1, 2


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
    lines = [(f'DAY {day:02d}   SHIFT LOG', 'textdim')]
    lines.append(('TRUST ' + trust_bar(trust), 'signal') if show_trust else ('TRUST --', 'muted'))
    lines.append(('', 'void'))
    lines.append((name.upper(), 'text'))
    if role:
        lines.append((role, 'textdim'))
    lines.append(('ID ' + idnum, 'paperdim'))
    if uni:
        lines.append(('UNI ' + uni, 'textdim'))
    if grad:
        lines.append((f'GRAD {grad}', 'textdim'))
    if emp:
        lines.append(('EMP ' + emp, 'textdim'))
    if exp:
        lines.append((f'EXP {exp}Y', 'textdim'))
    if ref:
        lines.append(('REF ' + ref, 'paperdim'))
    return lines


def layout(lines, glass):
    """Mirror of MonitorScreen.LinesToRects: y-up, line 1 at the TOP, clip to rows."""
    gx, gy, gw, gh = glass
    cols = gw // (GLYPH_W + LETTER_GAP)
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


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('--zoom', type=int, default=4)
    ap.add_argument('--out', default='screen_preview.png')
    ap.add_argument('--name', default='MARIA SANTOS')
    ap.add_argument('--day', type=int, default=1)
    ap.add_argument('--trust', type=int, default=100)
    args = ap.parse_args()

    img = render(name=args.name, day=args.day, trust=args.trust)
    dest = os.path.join(ROOT, 'art', 'kit', 'reference', args.out)
    img.resize((img.width * args.zoom, img.height * args.zoom), Image.NEAREST).convert('RGB').save(dest)
    print('wrote', os.path.relpath(dest, ROOT), f'({img.width}x{img.height} @ {args.zoom}x)')


if __name__ == '__main__':
    main()
