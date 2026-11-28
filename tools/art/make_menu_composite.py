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

SCREENS = {
    'root': [
        ("HUMAN RESOURCES", 'text', 8),
        ("ZERO DAY", 'danger', 24),
        ("NOT EVERY APPLICANT IS A PERSON", 'warn', 42),
        ("> CONTINUE", 'signal', 66),
        ("  NEW SHIFT", 'text', 83),
        ("  FIELD MANUAL", 'text', 100),
        ("  SETTINGS", 'text', 117),
        ("  CREDITS", 'text', 134),
    ],
    'settings': [
        ("SETTINGS", 'text', 10),
        ("", 'text', 26),
        ("> ROOM VOLUME   ########-- 80%", 'signal', 40),
        ("  SOUND VOLUME  #########- 90%", 'text', 56),
        ("  SOUND: ON", 'text', 72),
        ("  BACK", 'text', 88),
        ("", 'text', 104),
        ("[LEFT/RIGHT] CHANGE  [ESC] BACK", 'textdim', 122),
    ],
    'consent': [
        ("DATA & CONSENT", 'text', 8),
        ("RECORDS: DECISIONS, TIME TAKEN,", 'textdim', 24),
        ("THE SEED OF THE RUN.", 'textdim', 36),
        ("NEVER: YOUR NAME OR TYPE.", "textdim", 48),
        ("OFF UNTIL YOU SAY YES.", 'textdim', 60),
        ("> [ ] SHARE DATA", 'signal', 82),
        ("  BACK", 'text', 98),
        ("STATE: NOT ASKED YET", 'textdim', 116),
    ],
}


def paint_menu(img):
    """The concept's type hierarchy, in the game's own atlas: title, red subtitle, tagline, five rows."""
    from preview_intro import load_font, draw_text
    pal, atlas, rects = load_font()
    g = frame()['glass']
    cx = g['x'] + g['w'] // 2
    lines = [r for r in SCREENS[SCREEN] if r[0]]
    centred = [r for r in lines if len(r) < 4]
    blocked = [r for r in lines if len(r) == 4]
    for text, key, dy in centred:
        w = len(text) * (5 + 2) - 2
        draw_text(img, rects, atlas, pal, cx - w // 2, g['y'] + dy, text, key)
    if blocked:
        # ONE column: every option and every notice line starts at the same x, so the stack is a block
        widest = max(len(t) for t, _k, _d, _a in blocked) * (5 + 2) - 2
        left = cx - widest // 2
        for text, key, dy, _align in blocked:
            draw_text(img, rects, atlas, pal, left, g['y'] + dy, text, key)


def glass_capacity():
    g = frame()['glass']
    # 2x glyphs: 10 px wide, 14 tall, 4 px letter gap, 6 px line gap
    cols = (g['w'] - 18) // 7      # 1x cell: 5 px glyph + 2 px gap
    rows = (g['h'] + 3) // 10      # 1x rows: 7 px glyph + 3 px gap
    return cols, rows


SCREEN = 'root'


def main():
    global SCREEN
    if '--screen' in sys.argv:
        SCREEN = sys.argv[sys.argv.index('--screen') + 1]
    if '--write-frame' in sys.argv:
        data = json.load(open(LAYOUT, encoding='utf-8'))
        data['menuFrame'] = frame()
        json.dump(data, open(LAYOUT, 'w', encoding='utf-8'), indent=2)
        open(LAYOUT, 'a', encoding='utf-8').write('\n')
        print('wrote menuFrame into desk_layout.json:', frame()['glass'])

    cols, rows = glass_capacity()
    problems = []
    for name, lines in SCREENS.items():
        for row in lines:
            text, key, dy = row[0], row[1], row[2]
            if len(text) > cols:
                problems.append('%s: "%s" is %d cols, glass holds %d' % (name, text, len(text), cols))
            if dy + 7 > frame()['glass']['h']:
                problems.append('%s: "%s" runs past the glass bottom' % (name, text))
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
    paint_menu(img)

    out = os.path.join(ROOT, 'art', 'kit', 'reference', 'menu_composite_%s_1x.png' % SCREEN)
    img.save(out)
    big = img.resize((W * 3, H * 3), Image.NEAREST)
    big.save(os.path.join(ROOT, 'art', 'kit', 'reference', 'menu_composite_%s_3x.png' % SCREEN))
    print('wrote %s (glass %d,%d %dx%d; %d cols x %d rows at 2x)'
          % (os.path.relpath(out, ROOT), *[frame()['glass'][k] for k in ('x', 'y', 'w', 'h')], cols, rows))
    return 0


if __name__ == '__main__':
    raise SystemExit(main())
