#!/usr/bin/env python3
"""
preview_intro.py -- render the intro card exactly as IntroView paints it.

This is a TWIN, not an illustration. It mirrors

    unity/Assets/Scripts/Game/Interaction/IntroView.cs   (card rect, padding, pitch, rows)
    unity/Assets/Scripts/Core/Rules/IntroScriptRules.cs  (wrap, line composition, caps)
    content/intro/intro_01.json                          (the actual beats the game will show)

and it fails loudly if the shipping script breaks a rule the game enforces (too long, unpaintable, over
the 25 s budget). If a beat reads badly here, it reads badly in Unity.

    python tools/comp/preview_intro.py                  # every beat, in order
    python tools/comp/preview_intro.py --zoom 2 --out intro_2x.png
    python tools/comp/preview_intro.py --check           # verify only, write nothing
"""
from __future__ import annotations
import argparse, json, os, sys

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
KIT = os.path.join(ROOT, 'art', 'kit')
SCRIPT = os.path.join(ROOT, 'content', 'intro', 'intro_01.json')
sys.path.insert(0, os.path.join(ROOT, 'tools', 'art'))

try:
    from PIL import Image, ImageDraw
    HAVE_PIL = True
except ImportError:
    Image = None
    HAVE_PIL = False

# ---- mirrored from MonitorScreen (5x7 atlas) ------------------------------------------------
GLYPH_W, GLYPH_H, LETTER_GAP, LINE_GAP = 5, 7, 2, 3
ROW_STEP = GLYPH_H + LINE_GAP
# ---- mirrored from IntroView ----------------------------------------------------------------
PAGE_W, PAGE_H = 480, 270
CARD_X, CARD_Y, CARD_W, CARD_H = 40, 60, 400, 150
PAD_X, PAD_Y = 12, 12
COLS = 56
# ---- mirrored from IntroScriptRules ---------------------------------------------------------
TEXT_CHARS, MAX_BEATS, MAX_TOTAL = 90, 8, 25.0
PUNCT = " !\"#%&'()*+,-./0123456789:;<=>?_[]~"


def paintable(s: str) -> bool:
    return all(('A' <= c <= 'Z') or c in PUNCT for c in s)


def load_font():
    import make_art_kit
    pal = {k: (v[0], v[1], v[2], 255) for k, v in make_art_kit.PALETTE.items()}
    atlas = Image.open(os.path.join(KIT, 'fonts', 'monitor_font_5x7.png')).convert('RGBA')
    with open(os.path.join(KIT, 'fonts', 'monitor_font_5x7.json'), encoding='utf-8') as fh:
        metrics = json.load(fh)
    rects = {g['ch']: (g['x'], g['y']) for g in metrics['glyphs']}
    return pal, atlas, rects


def draw_text(img, rects, atlas, pal, x0, y0, text, colour_key):
    ink = pal.get(colour_key, pal['text'])
    pen = x0
    for ch in text:
        o = rects.get(ch) or rects.get(ch.upper())
        if o:
            g = atlas.crop((o[0], o[1], o[0] + GLYPH_W, o[1] + GLYPH_H))
            img.paste(Image.new('RGBA', (GLYPH_W, GLYPH_H), ink), (pen, y0), g)
        pen += GLYPH_W + LETTER_GAP


def wrap(text, cols=COLS):
    out, line = [], ''
    for word in text.split(' '):
        if not word:
            continue
        if not line:
            line = word
        elif len(line) + 1 + len(word) <= cols:
            line += ' ' + word
        else:
            out.append(line)
            line = word
    if line:
        out.append(line)
    return out


def load_beats():
    with open(SCRIPT, encoding='utf-8') as fh:
        data = json.load(fh)
    return data.get('beats', [])


def check(beats):
    problems = []
    if not beats:
        problems.append('the intro has no beats (the desk will start immediately)')
    if len(beats) > MAX_BEATS:
        problems.append('%d beats -- more than %d is not an intro' % (len(beats), MAX_BEATS))
    total = 0.0
    for i, b in enumerate(beats, 1):
        text = (b.get('text') or '').strip().upper()
        speaker = (b.get('speaker') or '').strip().upper()
        hold = b.get('hold') or 2.4
        total += hold
        if not text:
            problems.append('beat %d has no text' % i)
            continue
        if len(text) > TEXT_CHARS:
            problems.append('beat %d is %d chars (max %d) -- the game will drop it'
                            % (i, len(text), TEXT_CHARS))
        if not paintable(text):
            bad = sorted({c for c in text if not (('A' <= c <= 'Z') or c in PUNCT)})
            problems.append('beat %d has characters the atlas cannot draw: %r' % (i, bad))
        if speaker and not paintable(speaker):
            problems.append('beat %d speaker has characters the atlas cannot draw' % i)
        if hold < 0.5 or hold > 8:
            problems.append('beat %d holds %.1fs -- clamped to 0.5..8' % (i, hold))
        line = (speaker + ': ' + text) if speaker else text
        rows = wrap(line)
        if CARD_Y + PAD_Y + (2 + len(rows)) * ROW_STEP > CARD_Y + CARD_H - PAD_Y - GLYPH_H:
            problems.append('beat %d needs %d rows -- it will not fit the card' % (i, len(rows)))
    if total > MAX_TOTAL:
        problems.append('the intro runs %.1fs, over the %.0fs budget' % (total, MAX_TOTAL))
    return problems, total


def render(beats, pal, atlas, rects):
    shots = []
    for i, b in enumerate(beats):
        img = Image.new('RGBA', (PAGE_W, PAGE_H), pal['void'])
        d = ImageDraw.Draw(img)
        # the card, exactly as IntroView draws it: void fill, 1 px line border
        d.rectangle([CARD_X, CARD_Y, CARD_X + CARD_W - 1, CARD_Y + CARD_H - 1],
                    fill=pal['void'], outline=pal['line'])

        text = (b.get('text') or '').strip().upper()
        speaker = (b.get('speaker') or '').strip().upper()
        line = (speaker + ': ' + text) if speaker else text
        rows = ['HUMAN RESOURCES // ZERO DAY', '']
        rows += wrap(line)[:6]

        y = CARD_Y + PAD_Y
        for r, row in enumerate(rows):
            draw_text(img, rects, atlas, pal, CARD_X + PAD_X, y, row, 'signal' if r == 0 else 'text')
            y += ROW_STEP

        # the prompt is pinned to the card's bottom edge, exactly as IntroView pins it
        draw_text(img, rects, atlas, pal, CARD_X + PAD_X, CARD_Y + CARD_H - PAD_Y - GLYPH_H,
                  'BEAT %d / %d     [SPACE] NEXT   [X] SKIP' % (i + 1, len(beats)), 'textdim')
        shots.append(img)
    return shots


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('--zoom', type=int, default=2)
    ap.add_argument('--out', default=None)
    ap.add_argument('--check', action='store_true')
    a = ap.parse_args()
    if not HAVE_PIL:
        print('Pillow required'); return 2

    beats = load_beats()
    problems, total = check(beats)

    if a.check:
        if problems:
            print('FAIL: the intro script cannot be shown as written:')
            for p in problems:
                print('  - ' + p)
            return 1
        print('PASS: the intro script is showable: %d beats, %.1fs, all rows fit the card'
              % (len(beats), total))
        return 0

    if problems:
        print('WARNING: the game will change this script:')
        for p in problems:
            print('  - ' + p)

    pal, atlas, rects = load_font()
    shots = render(beats, pal, atlas, rects)
    sheet = Image.new('RGBA', (PAGE_W, PAGE_H * len(shots) + 6 * (len(shots) - 1)), pal['void'])
    for i, shot in enumerate(shots):
        sheet.alpha_composite(shot, (0, i * (PAGE_H + 6)))
    out = a.out or os.path.join(KIT, 'reference', 'intro_beats_%dx.png' % a.zoom)
    if a.zoom != 1:
        sheet = sheet.resize((sheet.width * a.zoom, sheet.height * a.zoom), Image.NEAREST)
    sheet.save(out)
    print('wrote %s (%dx%d @ %dx)' % (os.path.relpath(out, ROOT), PAGE_W, PAGE_H * len(shots), a.zoom))
    print('  %d beats, %.1fs total' % (len(beats), total))
    return 0


if __name__ == '__main__':
    sys.exit(main())
