#!/usr/bin/env python3
"""
make_menu_composite.py -- the front door, composed from the menuFrame in desk_layout.json.

This is the same frame the game will build: one 480x270 canvas made of

    background (room, night city)      the wall, window, moon, boards, clock, cabinet, plant
    server     (2x, right of the window)
    monitor    (2x) + stand (2x)       the bezel the menu lives inside
    desk       (480x64)                coffee, keyboard, applicant folders, clipboard, notepad
    halo       (fx/screen_halo)        the cyan light the screen throws, if the room asks for it

...with the menu painted into the monitor glass at 2x, so the text is twice the size of gameplay's
record view and fits the concept's proportions.

The geometry is read, never guessed: tools/art/make_menu_composite.py --write-frame rewrites
desk_layout.json's menuFrame block from the constants below, and --check fails if the menu cannot fit
the glass. Two uses:
    python tools/art/make_menu_composite.py                 # render art/kit/reference/menu_room_2x.png
    python tools/art/make_menu_composite.py --check          # geometry gate
"""
import json
import os
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
sys.path.insert(0, os.path.join(ROOT, 'tools', 'comp'))

from PIL import Image, ImageDraw  # noqa: E402

LAYOUT = os.path.join(ROOT, 'unity', 'Assets', 'Content', 'desk_layout.json')

# the composition, in canvas px, y DOWN (frame convention). Derived from the concept image.
MON_X, MON_Y, MON_SCALE = 14, 30, 1
SRV_X, SRV_Y, SRV_SCALE = 436, 109, 1
# inside monitor_big.png, y DOWN from its top-left (glass is authored there, so no y-up flip is needed)
GLASS_OFF = (12, 12, 236, 144)
DESK_BAND_H = 64
W, H = 480, 270


def frame():
    gw, gh = GLASS_OFF[2] * MON_SCALE, GLASS_OFF[3] * MON_SCALE
    left = MON_X + GLASS_OFF[0] * MON_SCALE
    top = MON_Y + GLASS_OFF[1] * MON_SCALE
    return dict(w=W, h=H,
                monitor=dict(x=MON_X, y=MON_Y, scale=MON_SCALE),
                server=dict(x=SRV_X, y=SRV_Y, scale=SRV_SCALE),
                screenOffset=dict(x=GLASS_OFF[0], y=GLASS_OFF[1], w=GLASS_OFF[2], h=GLASS_OFF[3]),
                monitorSprite='art/kit/desk/monitor_big.png',
                deskBandH=DESK_BAND_H,
                glass=dict(x=left, y=top, w=gw, h=gh))


def kit(rel):
    return Image.open(os.path.join(ROOT, 'art', 'kit', rel)).convert('RGBA')


def scaled(rel, scale):
    im = kit(rel)
    return im.resize((im.width * scale, im.height * scale), Image.NEAREST)


def compose(with_halo=True):
    img = kit('backgrounds/office_L0.png').copy()
    srv = scaled('desk/server_rack.png', SRV_SCALE)
    img.alpha_composite(srv, (SRV_X, SRV_Y))
    return img


def compose_monitor(img):
    mon = scaled('desk/monitor_big.png', MON_SCALE)
    img.alpha_composite(mon, (MON_X, MON_Y))
    return img


def compose_desk(img):
    desk = kit('desk/desk_foreground.png')
    img.alpha_composite(desk, (0, H - desk.height))
    return img


def halo_at(img):
    """
    The cyan light the screen throws ONTO THE ROOM: a two-step ring outside the BEZEL (not the glass --
    a ring inside the glass would be a border on the text) and a pool on the desk in front of it. Flat
    palette steps only, so it cannot band.
    """
    d = ImageDraw.Draw(img)
    m = frame()['monitor']
    bw, bh = 260 * m['scale'], 170 * m['scale']
    teal, dim = (12, 61, 56, 255), (15, 168, 138, 255)
    for i in range(1, 3):
        d.rectangle([m['x'] - i, m['y'] - i, m['x'] + bw - 1 + i, m['y'] + bh - 1 + i],
                    outline=dim if i == 1 else teal)
    for i, tone in enumerate((teal, dim)):
        y = m['y'] + bh + 3 + i * 3
        half = bw // 2 + 26 - i * 20
        d.line([m['x'] + bw // 2 - half, y, m['x'] + bw // 2 + half, y], fill=tone)


MENU_TITLE = "HUMAN RESOURCES"
MENU_SUB = "ZERO DAY"
MENU_TAG = "NOT EVERY APPLICANT IS A PERSON"

# ---- mirrored from MenuLayout.cs / MenuScreenText.cs (ADR-018). The twin draws what the view draws.
TITLE_TOP = 42 + 10
TITLE_SCALE = 2
SUBTITLE_TOP = TITLE_TOP + 9 * TITLE_SCALE
TAGLINE_TOP = SUBTITLE_TOP + 9 * TITLE_SCALE + 4
PILL_TOP = 42 + 65
PILL_X = 26 + 56
PILL_W = 236 - 112
PILL_H = 10
PILL_STEP = PILL_H + 3


def root_labels(has_save, in_progress):
    first = ('CONTINUE RUN' if in_progress else 'CONTINUE') if has_save else 'START'
    return [first, 'NEW SHIFT', 'FIELD MANUAL', 'SETTINGS', 'CREDITS']


DARKEN = [('panel2', 'panel'), ('panel', 'void'), ('line', 'panel'), ('linehi', 'line'), ('muted', 'line'), ('textdim', 'muted'), ('text', 'textdim'), ('white', 'textdim'), ('paper', 'paperdim'), ('paperdim', 'ink'), ('ink', 'void'), ('signal', 'signaldim'), ('ok', 'signaldim'), ('synth', 'synthdim')]


def dim(img):
    """
    The room at night. Every surface drops ONE palette step; anything that emits light (signal, warn,
    orange, the glow keys) stays lit. That is why the window and the LEDs still read while the walls fall
    back -- and it is a smooth darkening, not a checker, because a checker reads as noise at 1x.
    """
    from make_art_kit import PALETTE
    key_of = {v: k for k, v in PALETTE.items()}
    px = img.load()
    g = frame()['glass']
    for y in range(H):
        for x in range(W):
            if g['x'] <= x < g['x'] + g['w'] and g['y'] <= y < g['y'] + g['h']:
                continue
            c = px[x, y]
            if c[3] == 0:
                continue
            k = key_of.get(c[:3])
            if k is None:
                continue
            target = dict(DARKEN).get(k)
            if target:
                px[x, y] = PALETTE[target]
def paint_menu(img, labels):
    """
    Title at 3x (each atlas pixel repeated 3x3), red subtitle at 3x, amber tagline, then the option rows
    as pills: the selected one filled with signal and its label in void, the rest on panel2.
    """
    from preview_intro import load_font, draw_text, GLYPH_W as GW, GLYPH_H as GH, LETTER_GAP as LG, LINE_GAP
    pal, atlas, rects = load_font()
    g = frame()['glass']
    cx = g['x'] + g['w'] // 2

    def big(text, top, key, scale):
        cell_w = (GW + LG) * scale
        width = len(text) * cell_w - LG * scale
        x = cx - width // 2
        for i, ch in enumerate(text):
            if ch == ' ':
                continue
            for gy in range(GH):
                for gx in range(GW):
                    if not _ink(rects, atlas, ch, gx, gy):
                        continue
                    for sy in range(scale):
                        for sx in range(scale):
                            px = x + i * cell_w + gx * scale + sx
                            py = top + gy * scale + sy
                            if g['x'] <= px < g['x'] + g['w'] and g['y'] <= py < g['y'] + g['h']:
                                img.putpixel((px, py), pal[key])

    def small(text, top, key, left):
        draw_text(img, rects, atlas, pal, left, top, text, key)

    big('HUMAN RESOURCES', TITLE_TOP, 'text', TITLE_SCALE)
    big('ZERO DAY', SUBTITLE_TOP, 'danger', TITLE_SCALE)
    tag = 'NOT EVERY APPLICANT IS A PERSON'
    small(tag, TAGLINE_TOP, 'warn', cx - (len(tag) * (GW + LG) - LG) // 2)

    d = ImageDraw.Draw(img)
    for i, label in enumerate(labels):
        top = PILL_TOP + i * PILL_STEP
        sel = i == 0
        fill = pal['signal'] if sel else pal['panel2']
        d.rectangle([PILL_X, top, PILL_X + PILL_W - 1, top + PILL_H - 1], fill=fill)
        ink = 'void' if sel else 'text'
        small(label, top + (PILL_H - GH) // 2, ink, PILL_X + (PILL_W - (len(label) * (GW + LG) - LG)) // 2)

    foot = '[SPACE] CHOOSE   [S] SOUND'
    small(foot, g['y'] + g['h'] - 14, 'textdim', cx - (len(foot) * (GW + LG) - LG) // 2)


def _ink(rects, atlas, ch, gx, gy):
    o = rects.get(ch)
    if o is None:
        return False
    return atlas.getpixel((o[0] + gx, o[1] + gy))[3] > 0


def glass_capacity():
    g = frame()['glass']
    # 2x glyphs: 10 px wide, 14 tall, 4 px letter gap, 6 px line gap
    cols = (g['w'] - 18) // 7      # 1x cell: 5 px glyph + 2 px gap
    rows = (g['h'] + 3) // 10      # 1x rows: 7 px glyph + 3 px gap
    return cols, rows




def main():
    global SCREEN

    if '--write-frame' in sys.argv:
        data = json.load(open(LAYOUT, encoding='utf-8'))
        data['menuFrame'] = frame()
        json.dump(data, open(LAYOUT, 'w', encoding='utf-8'), indent=2)
        open(LAYOUT, 'a', encoding='utf-8').write('\n')
        print('wrote menuFrame into desk_layout.json:', frame()['glass'])

    cols, rows = glass_capacity()
    problems = []
    g = frame()['glass']
    if TITLE_TOP + TITLE_SCALE * 7 > g['y'] + g['h']:
        problems.append('the title runs past the glass bottom')
    for i, label in enumerate(root_labels(True, True)):
        w = len(label) * 7 - 2
        if w > PILL_W:
            problems.append('"%s" is %d px wide, the pill is %d' % (label, w, PILL_W))
    if PILL_TOP + 4 * PILL_STEP + PILL_H > g['y'] + g['h'] - 17:
        problems.append('the option pills run into the footer')
    if problems:
        for p in problems:
            print('  ' + p)
        print('FAIL: a menu line does not fit the monitor glass')
        return 1
    if '--check' in sys.argv:
        ok = cols >= 28 and rows >= 13
        print('%s: the glass holds %d cols x %d rows at 2x' % ('PASS' if ok else 'FAIL', cols, rows))
        return 0 if ok else 1

    img = compose()
    img = compose_monitor(img)
    halo_at(img)
    img = compose_desk(img)
    dim(img)
    paint_menu(img, root_labels(has_save=True, in_progress=True))

    out = os.path.join(ROOT, 'art', 'kit', 'reference', 'menu_composite_1x.png')
    img.save(out)
    img.resize((W * 3, H * 3), Image.NEAREST).save(
        os.path.join(ROOT, 'art', 'kit', 'reference', 'menu_composite_3x.png'))
    print('wrote %s (glass %d,%d %dx%d; %d cols x %d rows at 2x)'
          % (os.path.relpath(out, ROOT), *[frame()['glass'][k] for k in ('x', 'y', 'w', 'h')], cols, rows))
    return 0


if __name__ == '__main__':
    raise SystemExit(main())
