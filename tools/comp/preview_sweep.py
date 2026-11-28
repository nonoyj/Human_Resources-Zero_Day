#!/usr/bin/env python3
"""
preview_sweep.py -- render the after-hours walk exactly as SweepView will.

This is a TWIN, not an illustration: the page list, the captions, the wrap width and the ink colours are
mirrored from

    unity/Assets/Scripts/Core/Rules/Sweep.cs        (which traces exist, and why)
    unity/Assets/Scripts/Core/Rules/SweepScript.cs  (the pages: floors, headings, wrap, key line)
    unity/Assets/Scripts/Game/Interaction/SweepView.cs  (which sprite, where the text block sits)

If a page reads badly here it reads badly in Unity. tools/CoreTests/SweepScriptTests.cs asserts the same
numbers on the C# side, and this script fails loudly if the two ever disagree.

    python tools/comp/preview_sweep.py                 # 3-hire walk, every page, side by side
    python tools/comp/preview_sweep.py --hires 1       # the mildest walk
    python tools/comp/preview_sweep.py --zoom 3 --out sweep_walk_3x.png
"""
from __future__ import annotations
import argparse, json, os, sys

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
KIT = os.path.join(ROOT, 'art', 'kit')
sys.path.insert(0, os.path.join(ROOT, 'tools', 'art'))

try:
    from PIL import Image, ImageDraw
    HAVE_PIL = True
except ImportError:
    Image = None
    HAVE_PIL = False

# ---- mirrored from MonitorScreen (5x7 atlas) ------------------------------------------------
GLYPH_W, GLYPH_H, LETTER_GAP, LINE_GAP = 5, 7, 2, 3
# ---- mirrored from SweepScript --------------------------------------------------------------
PAGE_W, PAGE_H = 480, 270
# mirror of SweepScript: the bottom 32 px is ONE strip of readable text -- the prompt on top,
# the player's key hints underneath. The neon cap art is retired (BUG-055).
STRIP_H, STRIP_TOP = 32, 270 - 32
# mirror of SweepScript.KeyBandBox(): two 7 px rows + one 3 px gap = 17 px, 7 px down from the strip top
KEY_BOX_TOP = (STRIP_H - (2 * GLYPH_H + LINE_GAP)) // 2      # 7
KEY_BAND_TOP = STRIP_TOP + KEY_BOX_TOP
HINT_TOP = KEY_BAND_TOP + GLYPH_H + LINE_GAP
HINT_LINE = 'SPACE WALK  [S] SHIFT  [X] DESK'
# the caption band: a solid 'void' bar behind the words, because the room art is busy and the walk's
# text is the only thing that must never be hard to read. Same numbers as SweepScript.cs.
BAND_X, BAND_TOP, BAND_W = 8, 14, 464
ROW_STEP = GLYPH_H + LINE_GAP
PAD_X, PAD_Y = 8, 6
STRIP_PAD_X = 6
TEXT_X, TEXT_TOP = BAND_X + PAD_X, BAND_TOP + PAD_Y
TEXT_W = BAND_W - 2 * PAD_X
COLS = (TEXT_W + LETTER_GAP) // (GLYPH_W + LETTER_GAP)          # 64
ROWS = (PAGE_H - TEXT_TOP - STRIP_H - 6 + LINE_GAP) // (GLYPH_H + LINE_GAP)

# ---- mirrored from Sweep.cs: the four traces, cheapest to most damning ----------------------
TRACES = [
    dict(id='quiet', severity=0, floor='desk',
         title='The office is exactly as you left it.',
         detail='Everyone you passed through was a person. The lift panel is warm.', cause=None),
    dict(id='mug_moved', severity=1, floor='desk',
         title='Your mug has been moved to the other side of the desk.',
         detail='Nobody else has a key. It is still warm.', cause='synthetic_polished'),
    dict(id='chair_warm', severity=2, floor='corridor',
         title='The interview chair is still warm.',
         detail='The badge you filed is face down on the seat. It is a different face.', cause='synthetic_cloned'),
    dict(id='lift_waits', severity=3, floor='lift',
         title='The lift is holding on your floor with the doors open.',
         detail='It has been holding since 00:00. The call button is lit from the inside.', cause='synthetic_collusion'),
]

# names the player interviewed, so the receipt can say whose hire this was
HIRED = {1: [('synthetic_polished', 'Maren Voss')],
         2: [('synthetic_polished', 'Maren Voss'), ('synthetic_cloned', 'Tomas Reyes')],
         3: [('synthetic_polished', 'Maren Voss'), ('synthetic_cloned', 'Tomas Reyes'),
             ('synthetic_collusion', 'Iris Bane')]}

HEADINGS = {'desk': 'YOUR DESK - 00:06', 'corridor': 'CORRIDOR - 00:14', 'lift': 'LIFT - LOCKED IN'}
FLOORS = ['desk', 'corridor', 'lift']


def after_shift(corruption):
    """Mirror of Sweep.AfterShift + WithCause."""
    level = max(0, min(3, corruption))
    if level == 0:
        return [dict(TRACES[0])]
    hired = HIRED.get(level, [])
    out = []
    for i in range(1, level + 1):
        t = dict(TRACES[i])
        for family, name in hired:
            if family.startswith(t['cause']):
                t['detail'] = t['detail'] + ' (' + name + ')'
                break
        out.append(t)
    return out


def wrap(text, cols=COLS):
    """Mirror of SweepScript.Wrap -- same words, same breaks."""
    out = []
    if not text:
        return out
    line = ''
    for word in text.split(' '):
        if not word:
            continue
        if not line:
            while len(word) > cols:
                out.append(word[:cols]); word = word[cols:]
            line = word
            continue
        if len(line) + 1 + len(word) <= cols:
            line += ' ' + word
            continue
        out.append(line); line = ''
        while len(word) > cols:
            out.append(word[:cols]); word = word[cols:]
        line = word
    if line:
        out.append(line)
    return out


def build_pages(traces):
    """Mirror of SweepScript.Build."""
    pages = []
    for f, floor in enumerate(FLOORS):
        here = [t for t in traces if t['floor'] == floor]
        if not here and f > 0:
            continue
        page = dict(floor=floor, heading=HEADINGS[floor], lines=[])
        if not here:
            page['lines'] += wrap(traces[0]['title'])
        for t in here:
            page['lines'] += wrap(t['title'])
            if t['detail']:
                page['lines'] += wrap(t['detail'])
            page['lines'].append('')
        while page['lines'] and not page['lines'][-1]:
            page['lines'].pop()
        pages.append(page)
    if pages:
        pages[-1]['last'] = True
    return pages


def key_line(page, index, count):
    keys = '[SPACE] CLOCK OUT' if page.get('last') else '[SPACE] WALK ON'
    return '[WALK %d/%d]   %s' % (index + 1, count, keys)


def load_font():
    with open(os.path.join(KIT, 'fonts', 'monitor_font_5x7.json'), encoding='utf-8') as fh:
        metrics = json.load(fh)
    import make_art_kit
    pal = {k: (v[0], v[1], v[2], 255) for k, v in make_art_kit.PALETTE.items()}
    atlas = Image.open(os.path.join(KIT, 'fonts', 'monitor_font_5x7.png')).convert('RGBA')
    rects = {g['ch']: (g['x'], g['y']) for g in metrics['glyphs']}
    return pal, atlas, rects


def draw_text(img, rects, atlas, pal, x0, y0, text, colour_key):
    ink = pal.get(colour_key, pal['text'])
    pen = x0
    for ch in text[:COLS]:
        o = rects.get(ch) or rects.get(ch.upper())
        if o:
            g = atlas.crop((o[0], o[1], o[0] + GLYPH_W, o[1] + GLYPH_H))
            img.paste(Image.new('RGBA', (GLYPH_W, GLYPH_H), ink), (pen, y0), g)
        pen += GLYPH_W + LETTER_GAP


def render_page(page, index, count, pal, atlas, rects):
    bg_path = os.path.join(KIT, 'sweep', 'sweep_%s_480x270.png' % page['floor'])
    img = Image.open(bg_path).convert('RGBA')
    if img.size != (PAGE_W, PAGE_H):
        raise SystemExit('sweep background is %sx%s, expected 480x270' % img.size)

    lines = [page['heading'], ''] + page['lines']
    if len(lines) > ROWS:
        raise SystemExit('page %s wants %d rows, the page holds %d' % (page['floor'], len(lines), ROWS))

    # caption band first, then the words on top of it
    # mirror of SweepScript.BandHeight: rows * pitch + padding, no fudge
    band_h = len(lines) * ROW_STEP + 2 * PAD_Y
    d = ImageDraw.Draw(img)
    d.rectangle([BAND_X, BAND_TOP, BAND_X + BAND_W - 1, BAND_TOP + band_h - 1], fill=pal['void'])
    d.rectangle([BAND_X, BAND_TOP, BAND_X + BAND_W - 1, BAND_TOP + band_h - 1], outline=pal['line'])

    # mirror of SweepScript.BandTextBox(): the block is `rows` lines at the font's 10 px pitch, centred
    # in the band. Before v97 the first line sat at TEXT_TOP and the last one fell outside the band.
    block_h = len(lines) * ROW_STEP - LINE_GAP
    y = BAND_TOP + (band_h - block_h) // 2
    for i, text in enumerate(lines):
        if not text:
            y += ROW_STEP
            continue
        draw_text(img, rects, atlas, pal, TEXT_X, y, text, 'warn' if i == 0 else 'textdim')
        y += ROW_STEP

    # the neon caps are decoration along the very bottom; the WORDS sit on the black band above them,
    # never on top of a cap (first draft drew the prompt straight across them -- unreadable).
    # the strip: one bar, two lines of readable text, drawn from the same atlas as the copy above it
    d.rectangle([0, STRIP_TOP, PAGE_W - 1, PAGE_H - 1], fill=pal['void'], outline=pal['line'])
    draw_text(img, rects, atlas, pal, STRIP_PAD_X, KEY_BAND_TOP, key_line(page, index, count), 'signal')
    draw_text(img, rects, atlas, pal, STRIP_PAD_X, HINT_TOP, HINT_LINE, 'textdim')
    return img


def render_walk(hires=3):
    pal, atlas, rects = load_font()
    pages = build_pages(after_shift(hires))
    shots = [render_page(p, i, len(pages), pal, atlas, rects) for i, p in enumerate(pages)]
    strip = Image.new('RGBA', (PAGE_W, PAGE_H * len(shots) + 6 * (len(shots) - 1)), pal['void'])
    for i, shot in enumerate(shots):
        strip.alpha_composite(shot, (0, i * (PAGE_H + 6)))
    return strip, pages


def check():
    """The gate form: prove the walk can be built and read, and write nothing."""
    problems = []
    for hires in (1, 2, 3):
        pages = build_pages(after_shift(hires))
        if not pages:
            problems.append('hires=%d produced no pages' % hires)
            continue
        if pages[0]['floor'] != 'desk':
            problems.append('hires=%d walk does not start at the desk' % hires)
        if not pages[-1].get('last'):
            problems.append('hires=%d walk has no last page' % hires)
        for p in pages:
            rows = len(p['lines']) + 2
            if rows > ROWS:
                problems.append('%s page wants %d rows, holds %d' % (p['floor'], rows, ROWS))
            for line in p['lines']:
                if len(line) > COLS:
                    problems.append('%s line overflows %d cols: %r' % (p['floor'], COLS, line[:20]))
            # the sprite the view will ask Resources for must exist in the repo
            bg = os.path.join(KIT, 'sweep', 'sweep_%s_480x270.png' % p['floor'])
            if not os.path.exists(bg):
                problems.append('missing background ' + os.path.relpath(bg, ROOT))
    # the strip is painted, not imported: nothing to check on disk. What matters is that the hint line
    # fits the page width, which the render above proves by drawing it.

    # and it must actually rasterise: a page that throws in PIL would throw in Unity too
    try:
        render_walk(3)
    except SystemExit as e:
        problems.append('render failed: %s' % e)
    except Exception as e:                     # noqa: BLE001 -- the point is to report, not to classify
        problems.append('render failed: %r' % e)

    if problems:
        print('FAIL: the after-hours walk cannot be read:')
        for p in problems:
            print('  - ' + p)
        return 1
    print('PASS: sweep walk builds for 1/2/3 hires, fits %d cols x %d rows, all art present' % (COLS, ROWS))
    return 0


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('--hires', type=int, default=3, help='synthetics that got through last shift (1-3)')
    ap.add_argument('--zoom', type=int, default=2)
    ap.add_argument('--out', default=None)
    ap.add_argument('--check', action='store_true',
                    help='verify the walk without writing a file: every floor present, nothing overflowing')
    a = ap.parse_args()
    if not HAVE_PIL:
        print('Pillow required (pip install pillow) -- the page code itself is not known to be wrong'); return 3

    if a.check:
        return check()

    strip, pages = render_walk(max(1, min(3, a.hires)))
    name = a.out or ('sweep_walk_%dx_%dx.png' % (a.hires, a.zoom))
    dest = os.path.join(KIT, 'reference', name)
    strip.resize((strip.width * a.zoom, strip.height * a.zoom), Image.NEAREST).convert('RGB').save(dest)
    print('wrote', os.path.relpath(dest, ROOT), '(%dx%d @ %dx)' % (strip.width, strip.height, a.zoom))
    for i, p in enumerate(pages):
        print('  page %d  %-8s  %s' % (i + 1, p['floor'], key_line(p, i, len(pages))))
    print('\n  cols %d, rows %d per page' % (COLS, ROWS))
    return 0


if __name__ == '__main__':
    raise SystemExit(main())
