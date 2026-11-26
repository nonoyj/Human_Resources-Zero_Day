#!/usr/bin/env python3
"""
preview_letter.py -- render the full-screen letter page exactly as LetterView will.

Mirrors LetterLayout.Wrap / LetterPaginator / LetterPage.ToLines from
unity/Assets/Scripts/Game/Desk/LetterView.cs. If this looks wrong here, it will look wrong in Unity.

    python tools/comp/preview_letter.py
    python tools/comp/preview_letter.py --scam --zoom 3 --out letter_scam.png
"""
from __future__ import annotations
import argparse, json, os, random, sys

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
KIT = os.path.join(ROOT, 'art', 'kit')
sys.path.insert(0, os.path.join(ROOT, 'tools', 'art'))

try:
    from PIL import Image
    HAVE_PIL = True
except ImportError:
    Image = None
    HAVE_PIL = False

GLYPH_W, GLYPH_H, LETTER_GAP, LINE_GAP = 5, 7, 2, 3
MARGIN, INSET, BODY_ROWS, TOTAL_ROWS = 12, 6, 16, 23
PAGE_W, PAGE_H = 480 - 2 * MARGIN, 270 - 2 * MARGIN           # 456 x 246 paper
TEXT_W, TEXT_H = PAGE_W - 2 * INSET, PAGE_H - 2 * INSET       # 444 x 234 text area
COLS = (TEXT_W + LETTER_GAP) // (GLYPH_W + LETTER_GAP)        # 63

CLEAN = ("Dear Ms Santos,\n\nWe reviewed your application for the data entry post and would like to "
         "invite you for an interview on Monday at nine in the morning. Please bring one government "
         "identification card.\n\nWe look forward to meeting you.")
SCAM = ("Dear Sir/Madam,\n\nYour application was selected immediately. To activate your slot you must "
        "pay a processing fee of 500 pesos and send a deposit today. This offer expires in two hours, "
        "so reply now or lose the position. Training starts the same day with no further review of your "
        "documents or your references.")


def wrap(text, cols=COLS):
    out = []
    for para in text.replace('\r\n', '\n').split('\n'):
        if not para:
            out.append(''); continue
        line = ''
        for word in para.split(' '):
            if not word: continue
            if not line: line = word
            elif len(line) + 1 + len(word) <= cols: line += ' ' + word
            else: out.append(line); line = word
            while len(line) > cols:
                out.append(line[:cols - 1] + '~'); line = line[cols - 1:]
        if line: out.append(line)
    return out


def page_lines(body, title, signature='Regards,'):
    lines = [(title, 'warn'), ('-' * COLS, 'line')]
    lines += [(l, 'text') for l in body]
    lines.append((signature, 'textdim'))
    wanted = TOTAL_ROWS - 2
    while len(lines) < wanted:
        lines.append(('', 'text'))
    lines.append(('> FLAG WHAT IS WRONG        X BACK TO DESK', 'warn'))
    return lines


def load():
    with open(os.path.join(KIT, 'fonts', 'monitor_font_5x7.json'), encoding='utf-8') as fh:
        metrics = json.load(fh)
    import make_art_kit
    pal = {k: (v[0], v[1], v[2], 255) for k, v in make_art_kit.PALETTE.items()}
    return metrics, pal


# ON PAPER, the ink is dark and the stock is light. The monitor's palette colours are for a DARK tube,
# so using 'text' (near-white) on 'paper' (near-white) produced an unreadable pale page -- caught in the
# first preview render. These are the paper-side inks, and they are still palette values.
PAPER_INKS = {'text': 'ink', 'textdim': 'ink', 'paperdim': 'paperdim', 'warn': 'paperdim',
              'line': 'paperdim', 'muted': 'paperdim', 'signal': 'ink'}


def render(scam=False, title='COVER LETTER  MARIA SANTOS'):
    metrics, pal = load()
    atlas = Image.open(os.path.join(KIT, 'fonts', 'monitor_font_5x7.png')).convert('RGBA')
    rects = {g['ch']: (g['x'], g['y']) for g in metrics['glyphs']}
    body = wrap(SCAM if scam else CLEAN)[:BODY_ROWS]
    img = Image.new('RGBA', (PAGE_W, PAGE_H), pal['paper'])
    y = INSET
    for text, colour in page_lines(body, title):
        if not text:
            y += GLYPH_H + LINE_GAP; continue
        if y + GLYPH_H > PAGE_H - INSET: break
        pen = INSET
        ink = pal.get(PAPER_INKS.get(colour, 'ink'), pal['ink'])
        for ch in text[:COLS]:
            o = rects.get(ch) or rects.get(ch.upper())
            if o:
                g = atlas.crop((o[0], o[1], o[0] + GLYPH_W, o[1] + GLYPH_H))
                img.paste(Image.new('RGBA', (GLYPH_W, GLYPH_H), ink), (pen, y), g)
            pen += GLYPH_W + LETTER_GAP
        y += GLYPH_H + LINE_GAP
    # page frame on the paper stock -- 2 px, ragged on the bottom edge only (bible 6b: soft goods keep
    # torn edges, read-critical objects stay straight). Deterministic: same seed, same tear.
    from PIL import ImageDraw
    d = ImageDraw.Draw(img)
    d.rectangle([0, 0, PAGE_W - 1, PAGE_H - 1], outline=pal['ink'])
    rng = random.Random(0x5EED)
    x = 3
    while x < PAGE_W - 3:
        if rng.random() < 0.35:
            step = rng.choice((1, 2, 3))
            d.rectangle([x, PAGE_H - 3 - step, x + step - 1, PAGE_H - 2], fill=pal['paper'])
            x += step + 2
        else:
            x += 1
    return img


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('--scam', action='store_true')
    ap.add_argument('--zoom', type=int, default=2)
    ap.add_argument('--out', default='letter_preview.png')
    a = ap.parse_args()
    if not HAVE_PIL:
        print('Pillow required'); return 2
    img = render(scam=a.scam)
    dest = os.path.join(KIT, 'reference', a.out)
    img.resize((img.width * a.zoom, img.height * a.zoom), Image.NEAREST).convert('RGB').save(dest)
    print('wrote', os.path.relpath(dest, ROOT), f'({img.width}x{img.height} @ {a.zoom}x)')
    return 0


if __name__ == '__main__':
    raise SystemExit(main())
