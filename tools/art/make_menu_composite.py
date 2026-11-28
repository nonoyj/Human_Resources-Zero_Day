#!/usr/bin/env python3
"""
make_menu_composite.py -- render the front door exactly as MenuView composes it.

v114 rebuild: the player pointed out that v113 followed their PROSE and not the CONCEPT IMAGE. The concept
is a specific frame, so this file and MenuLayout.cs now carry that frame's numbers, and this script draws
them so the preview and the game cannot drift:

    background (night office)     the desk scene's own backdrop, darkened one palette step
    wall_monitors  x300 y46       the bank of three wall screens with cyan data on it
    server_rack   x424 y100       the tall cabinet at the far right
    menu_monitor   x16 y14        the big monitor: 300x220, glass 272x170, 3x title
    screen_halo                   cyan ring around the case + pool on the desk
    desk_foreground      y206     coffee, keyboard, applicant folders, clipboard, notepad
    clearance card x410 y6        the ID panel, top right

    python tools/art/make_menu_composite.py            # writes art/kit/reference/menu_composite_3x.png
    python tools/art/make_menu_composite.py --check    # geometry gate
"""
import os
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
sys.path.insert(0, os.path.join(ROOT, 'tools', 'comp'))
sys.path.insert(0, os.path.join(ROOT, 'tools', 'art'))

from PIL import Image, ImageDraw  # noqa: E402
from preview_intro import load_font, draw_text, GLYPH_W as GW, GLYPH_H as GH, LETTER_GAP as LG  # noqa: E402
from make_art_kit import PALETTE  # noqa: E402

W, H = 480, 270

# ---- mirrored from MenuLayout.cs -- one source of truth, two renderers ------------------------------
MON_X, MON_Y, MON_W, MON_H = 4, 10, 340, 224
GLASS_X, GLASS_Y, GLASS_W, GLASS_H = 18, 24, 312, 172
WALL_X, WALL_Y = 326, 40
LABEL_X, LABEL_Y = 350, 150
SRV_X, SRV_Y = 430, 92
DESK_Y = 206
WIDGET_X, WIDGET_Y = 412, 6
TITLE_SCALE = 3
TITLE_TOP, SUB_TOP, TAG_TOP = GLASS_Y + 6, GLASS_Y + 30, GLASS_Y + 56
PILL_X, PILL_W, PILL_H, PILL_STEP = 108, 156, 15, 17
PILL_TOP = GLASS_Y + 70

DARKEN = {'panel2': 'panel', 'panel': 'void', 'line': 'panel', 'linehi': 'line', 'muted': 'line',
          'textdim': 'muted', 'text': 'textdim', 'white': 'textdim', 'paper': 'paperdim',
          'paperdim': 'ink', 'ink': 'void', 'signal': 'signaldim', 'ok': 'signaldim', 'synth': 'synthdim'}


def kit(rel):
    return Image.open(os.path.join(ROOT, 'art', 'kit', rel + '.png')).convert('RGBA')


def compose(dim_room=True):
    img = kit('backgrounds/office_L0').copy()
    img.alpha_composite(kit('desk/wall_monitors'), (WALL_X, WALL_Y))
    img.alpha_composite(kit('desk/wall_labels'), (LABEL_X, LABEL_Y))
    img.alpha_composite(kit('desk/server_rack'), (SRV_X, SRV_Y))
    img.alpha_composite(kit('desk/menu_monitor'), (MON_X, MON_Y))
    halo(img)
    img.alpha_composite(kit('desk/desk_foreground'), (0, DESK_Y))
    widget(img)
    if dim_room:
        dim(img)
    return img


def halo(img):
    d = ImageDraw.Draw(img)
    teal, dim_c = PALETTE['glow_teal'], PALETTE['signaldim']
    x0, y0, x1, y1 = MON_X, MON_Y, MON_X + MON_W, MON_Y + MON_H
    for i in range(1, 3):
        col = teal if i == 1 else dim_c
        for x in range(x0 - i, x1 + i):
            for y in (y0 - i, y1 - 1 + i):
                if 0 <= x < W and 0 <= y < H:
                    img.putpixel((x, y), col)
        for y in range(y0 - i, y1 + i):
            for x in (x0 - i, x1 - 1 + i):
                if 0 <= x < W and 0 <= y < H:
                    img.putpixel((x, y), col)
    for i, col in enumerate((teal, dim_c)):
        y = y1 + 3 + i * 4
        half = 150 - i * 40
        d.line([MON_X + MON_W // 2 - half, y, MON_X + MON_W // 2 + half, y], fill=col)


def widget(img):
    d = ImageDraw.Draw(img)
    w, h = 62, 30
    d.rectangle([WIDGET_X, WIDGET_Y, WIDGET_X + w - 1, WIDGET_Y + h - 1], fill=PALETTE['panel'],
                outline=PALETTE['line'])
    d.rectangle([WIDGET_X + 9, WIDGET_Y + 6, WIDGET_X + 13, WIDGET_Y + 11], fill=PALETTE['linehi'])
    d.rectangle([WIDGET_X + 7, WIDGET_Y + 13, WIDGET_X + 15, WIDGET_Y + 21], fill=PALETTE['linehi'])
    for i in range(3):
        d.line([WIDGET_X + 8 + i * 14, WIDGET_Y + h - 6, WIDGET_X + 18 + i * 14, WIDGET_Y + h - 6],
               fill=PALETTE['orange'] if i == 0 else PALETTE['linehi'])


def dim(img):
    """Surfaces drop one palette step; light-emitting colours stay. No checker: it reads as noise at 1x."""
    key_of = {v: k for k, v in PALETTE.items()}
    px = img.load()
    for y in range(H):
        for x in range(W):
            if GLASS_X <= x < GLASS_X + GLASS_W and GLASS_Y <= y < GLASS_Y + GLASS_H:
                continue
            c = px[x, y]
            if c[3] == 0:
                continue
            k = key_of.get(c[:3])
            target = DARKEN.get(k) if k else None
            if target:
                px[x, y] = PALETTE[target]


def labels(has_save=True, in_progress=True):
    first = ('CONTINUE RUN' if in_progress else 'CONTINUE') if has_save else 'START'
    return [first, 'NEW SHIFT', 'FIELD MANUAL', 'SETTINGS', 'CREDITS']


def paint_menu(img, rows):
    pal, atlas, rects = load_font()
    gx, gy, gw, gh = GLASS_X, GLASS_Y, GLASS_W, GLASS_H
    cx = gx + gw // 2

    def glyph_ink(ch, x0, y0):
        o = rects.get(ch)
        if o is None:
            return
        for yy in range(GH):
            for xx in range(GW):
                if atlas.getpixel((o[0] + xx, o[1] + yy))[3] > 0:
                    yield x0 + xx, y0 + yy

    def big(text, top, key, scale):
        cell = (GW + LG) * scale
        width = len(text) * cell - LG * scale
        x0 = cx - width // 2
        for i, ch in enumerate(text):
            if ch == ' ':
                continue
            for px_, py_ in glyph_ink(ch, x0 + i * cell, top):
                for sy in range(scale):
                    for sx in range(scale):
                        x, y = px_ + sx, py_ + sy
                        if gx <= x < gx + gw and gy <= y < gy + gh:
                            img.putpixel((x, y), pal[key])

    def small(text, top, key, left):
        draw_text(img, rects, atlas, pal, left, top, text, key)

    big('HUMAN RESOURCES', TITLE_TOP, 'text', TITLE_SCALE)
    big('ZERO DAY', SUB_TOP, 'danger', TITLE_SCALE)
    tag = 'NOT EVERY APPLICANT IS A PERSON'
    small(tag, TAG_TOP, 'warn', cx - (len(tag) * (GW + LG) - LG) // 2)

    d = ImageDraw.Draw(img)
    for i, label in enumerate(rows):
        top = PILL_TOP + i * PILL_STEP
        sel = i == 0
        d.rectangle([PILL_X, top, PILL_X + PILL_W - 1, top + PILL_H - 1],
                    fill=pal['signal'] if sel else pal['void'],
                    outline=None if sel else pal['linehi'])
        small(label, top + (PILL_H - GH) // 2, 'void' if sel else 'text',
              PILL_X + (PILL_W - (len(label) * (GW + LG) - LG)) // 2)

    foot = '[SPACE] CHOOSE   [S] SOUND'
    small(foot, gy + gh - 14, 'textdim', cx - (len(foot) * (GW + LG) - LG) // 2)


def check():
    problems = []
    cols = (GLASS_W - 16) // (GW + LG)
    rows = (GLASS_H + 3) // 10
    if TITLE_TOP + TITLE_SCALE * GH > SUB_TOP:
        problems.append('the title runs into ZERO DAY')
    if PILL_TOP + 4 * PILL_STEP + PILL_H > GLASS_Y + GLASS_H - 16:
        problems.append('the pills run into the footer')
    if PILL_X < GLASS_X or PILL_X + PILL_W > GLASS_X + GLASS_W:
        problems.append('a pill is outside the glass')
    for label in labels():
        if len(label) * (GW + LG) - LG > PILL_W:
            problems.append('"%s" does not fit a %d px pill' % (label, PILL_W))
    title_w = len('HUMAN RESOURCES') * (GW + LG) * TITLE_SCALE - LG * TITLE_SCALE
    if title_w > GLASS_W - 2:       # GameView centres it; 1 px of air each side is honest
        problems.append('the 3x title is %d px, the glass is %d' % (title_w, GLASS_W))
    print('glass %dx%d = %d cols x %d rows at 1x' % (GLASS_W, GLASS_H, cols, rows))
    return problems


def main():
    problems = check()
    if problems:
        for p in problems:
            print('  ' + p)
        print('FAIL: the concept frame does not fit')
        return 1
    if '--check' in sys.argv:
        print('PASS: title, tagline, pills and footer all fit the monitor glass')
        return 0

    img = compose()
    paint_menu(img, labels())
    base = os.path.join(ROOT, 'art', 'kit', 'reference')
    img.save(os.path.join(base, 'menu_composite_1x.png'))
    img.resize((W * 3, H * 3), Image.NEAREST).save(os.path.join(base, 'menu_composite_3x.png'))
    print('wrote art/kit/reference/menu_composite_{1x,3x}.png')
    return 0


if __name__ == '__main__':
    raise SystemExit(main())
