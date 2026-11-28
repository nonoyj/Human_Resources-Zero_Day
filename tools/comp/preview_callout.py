#!/usr/bin/env python3
"""
preview_callout.py -- render the inspect read-out exactly as DeskEvidenceOverlay will.

The player, 2026-11-26: "Clicking the applicant's face, id or anywhere opens a text panel with very small
text in the middle." The panel was two TextMesh lines in the engine font at ~3 px, on a cream 9-slice
stretched over the face. It is now a painted panel: same 5x7 atlas as the CRT, dark fill, 1 px border,
8 px padding, wrapped body.

This twin mirrors TextPanel.Paint + DeskEvidenceOverlay.Wrap, so a panel that reads badly here reads
badly in Unity. It also draws the corner brackets (HotspotOutline) around the applicant, which is the
other half of the same complaint: "make what to click look more clickable".

    python tools/comp/preview_callout.py                 # 2x, one panel on the desk
    python tools/comp/preview_callout.py --zoom 3
    python tools/comp/preview_callout.py --check         # gate form: no file written
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

GLYPH_W, GLYPH_H, LETTER_GAP, LINE_GAP = 5, 7, 2, 3
PAD_X, PAD_Y = 8, 7
MAX_W = 176                                  # DeskEvidenceOverlay.MaxWidthPx
COLS = (MAX_W - 2 * PAD_X) // (GLYPH_W + LETTER_GAP)
SIGNAL = (24, 224, 176, 255)
PANEL = (19, 25, 38, 255)
LINE = (43, 58, 77, 255)

LABEL = "EYES AND CHEEKS"
BODY = ("The eyes are one shade too even, and the catchlight sits in the same place on both. "
        "A person's light lands differently on each eye.")


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
            out.append(line); line = word
    if line:
        out.append(line)
    return out


def panel_size(lines):
    h = 2 * PAD_Y + len(lines) * (GLYPH_H + LINE_GAP) - LINE_GAP
    return MAX_W, h


def load_font():
    with open(os.path.join(KIT, 'fonts', 'monitor_font_5x7.json'), encoding='utf-8') as fh:
        metrics = json.load(fh)
    atlas = Image.open(os.path.join(KIT, 'fonts', 'monitor_font_5x7.png')).convert('RGBA')
    rects = {g['ch']: (g['x'], g['y']) for g in metrics['glyphs']}
    return atlas, rects


def draw_text(img, rects, atlas, x0, y0, text, ink):
    pen = x0
    for ch in text:
        o = rects.get(ch) or rects.get(ch.upper())
        if o:
            g = atlas.crop((o[0], o[1], o[0] + GLYPH_W, o[1] + GLYPH_H))
            img.paste(Image.new('RGBA', (GLYPH_W, GLYPH_H), ink), (pen, y0), g)
        pen += GLYPH_W + LETTER_GAP


def paint_panel(lines, colours, w, h, atlas, rects):
    img = Image.new('RGBA', (w, h), PANEL)
    d = ImageDraw.Draw(img)
    d.rectangle([0, 0, w - 1, h - 1], outline=LINE)
    inks = {'warn': (255, 209, 102, 255), 'text': (230, 241, 255, 255), 'textdim': (169, 199, 228, 255)}
    for i, (text, colour) in enumerate(zip(lines, colours)):
        if not text:
            continue
        draw_text(img, rects, atlas, PAD_X, PAD_Y + i * (GLYPH_H + LINE_GAP), text, inks.get(colour, inks['text']))
    return img


def brackets(w, h, arm=5):
    """Mirror of HotspotOutline.Make: four corner Ls, no full rectangle."""
    img = Image.new('RGBA', (w, h), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    for (x0, y0, dx, dy) in ((0, 0, 1, 1), (w - 1, 0, -1, 1), (0, h - 1, 1, -1), (w - 1, h - 1, -1, -1)):
        d.line([(x0, y0), (x0 + dx * (arm - 1), y0)], fill=SIGNAL)
        d.line([(x0, y0), (x0, y0 + dy * (arm - 1))], fill=SIGNAL)
    return img


def render():
    atlas, rects = load_font()
    layout = json.load(open(os.path.join(ROOT, 'unity', 'Assets', 'Resources', 'desk_layout.json')))
    app = [l for l in layout['layers'] if l['id'] == 'applicant'][0]
    bg = Image.open(os.path.join(KIT, 'backgrounds', 'office_L0.png')).convert('RGBA')

    # the applicant, drawn where the layout puts it (anchor = bottom-left, scale 0.96)
    frame = Image.open(os.path.join(KIT, 'applicants', 'app_human_solid_a_192x216.png')).convert('RGBA')
    frame = frame.crop((0, 0, 192, 216))
    scale = app['scaleFactor']
    frame = frame.resize((int(192 * scale), int(216 * scale)), Image.NEAREST)
    bg.alpha_composite(frame, (int(app['anchor'][0]), int(270 - app['anchor'][1] - frame.height)))

    # brackets on every hotspot, exactly where HotspotHitTest puts them
    for hp in layout['hotspots']:
        x, y, w, h = hp['rect']
        cx = app['anchor'][0] + (x + w / 2) * scale
        cy = app['anchor'][1] + (y + h / 2) * scale
        bw, bh = int((w + 12) * scale), int((h + 12) * scale)
        patch = brackets(bw, bh)
        bg.alpha_composite(patch, (int(cx - bw / 2), int(270 - cy - bh / 2)))

    # and the read-out, top-right
    body = wrap(BODY)
    lines = [LABEL] + body
    colours = ['warn'] + ['text'] * len(body)
    w, h = panel_size(lines)
    panel = paint_panel(lines, colours, w, h, atlas, rects)
    px, py = 268, 196
    bg.alpha_composite(panel, (int(px - w / 2), int(270 - py - h / 2)))
    return bg, lines, w, h


def check():
    atlas, rects = load_font()
    problems = []
    body = wrap(BODY)
    lines = [LABEL] + body
    w, h = panel_size(lines)
    if w > 176:
        problems.append('panel is %d px wide, the cap is 176' % w)
    # The panel is anchored top-right and is allowed to reach most of the canvas height; what it must NOT
    # do is run off the page or swallow the whole subject. 100 px is 7 listed tells' worth of headroom.
    if h > 100:
        problems.append('panel is %d px tall -- it would cover the whole applicant' % h)
    if MAX_W + 0 > 200:
        problems.append('panel is %d px wide -- it would cross the desk' % MAX_W)
    for line in lines:
        if len(line) > COLS:
            problems.append('line overflows %d cols: %r' % (COLS, line[:24]))
    # every glyph the panel draws must exist in the atlas, or it renders as a gap
    for line in lines:
        for ch in line:
            if ch != ' ' and ch not in rects and ch.upper() not in rects:
                problems.append('glyph missing from the atlas: %r' % ch)
    # the panel must be legible against the desk: its fill is dark, its text light
    if sum(PANEL[:3]) >= sum((230, 241, 255)):
        problems.append('panel fill is not darker than its text')
    if problems:
        print('FAIL: the inspect read-out cannot be read:')
        for p in sorted(set(problems)):
            print('  - ' + p)
        return 1
    print('PASS: read-out panel %dx%d, %d lines, %d cols/line, every glyph present' %
          (w, h, len(lines), COLS))
    return 0


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('--zoom', type=int, default=2)
    ap.add_argument('--out', default='callout_preview.png')
    ap.add_argument('--check', action='store_true')
    a = ap.parse_args()
    if not HAVE_PIL:
        print('Pillow required (pip install pillow) -- the panel code itself is not known to be wrong'); return 3
    if a.check:
        return check()

    img, lines, w, h = render()
    dest = os.path.join(KIT, 'reference', a.out)
    img.resize((img.width * a.zoom, img.height * a.zoom), Image.NEAREST).convert('RGB').save(dest)
    print('wrote', os.path.relpath(dest, ROOT), '(%dx%d @ %dx)' % (img.width, img.height, a.zoom))
    for line in lines:
        print('  | ' + line)
    print('  panel %dx%d, %d cols' % (w, h, COLS))
    return 0


if __name__ == '__main__':
    raise SystemExit(main())
