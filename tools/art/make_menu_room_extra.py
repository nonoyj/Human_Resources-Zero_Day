#!/usr/bin/env python3
"""
make_menu_room_extra.py -- the props the FRONT DOOR frame needs (v114).

The player said, correctly, that v113 followed their prose and not the concept image. The concept is a
specific frame, so this file draws that frame's parts:

    desk/menu_monitor.png   300x220  the big monitor: near-black case, rounded corners, two amber corner
                                     LEDs, the glass, an LED row under it, a stand on the desk
    desk/wall_monitors.png  150x104  the bank of three wall screens behind the desk, with cyan data
                                     on them (TRACE / PAYROLL.SYNC). Static: no animation in this pass.
    desk/server_rack.png     48x160  a tall cabinet at the far right, red/green drive LEDs
    fx/screen_halo.png      340x250  the cyan the monitor throws, drawn as a ring AROUND the case

All palette-only (art bible §3): alpha 0 or 255, colours from PALETTE, no blending. Kept out of the desk
LAYER list on purpose -- this is the front door's own frame, composed by MenuView, not gameplay art.

    python tools/art/make_menu_room_extra.py
    python tools/art/make_menu_room_extra.py --check     # palette gate only
"""
import os
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
sys.path.insert(0, os.path.join(ROOT, 'tools', 'art'))

from PIL import Image, ImageDraw  # noqa: E402
from make_art_kit import PALETTE, rgba  # noqa: E402

# ---- menu monitor ---------------------------------------------------------------------------------
MM_W, MM_H = 340, 224
MM_GLASS = (14, 14, 312, 172)          # x, y, w, h  -- 40 characters on 17 lines at 1x
MM_RADIUS = 10

# ---- wall monitor bank ----------------------------------------------------------------------------
WM_W, WM_H = 150, 104

# ---- server ---------------------------------------------------------------------------------------
SRV_W, SRV_H = 48, 160

# ---- halo -----------------------------------------------------------------------------------------
HL_W, HL_H = 370, 254


def out(rel):
    return os.path.join(ROOT, 'art', 'kit', rel)


def rounded(d, box, fill=None, outline=None, r=8, width=1):
    x0, y0, x1, y1 = box
    d.rounded_rectangle([x0, y0, x1, y1], radius=r, fill=fill, outline=outline, width=width)


def menu_monitor():
    img = Image.new('RGBA', (MM_W, MM_H), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    # case: near-black, one step off void so it reads as a surface and not a hole
    rounded(d, [0, 0, MM_W - 1, MM_H - 8], fill=rgba('void'), outline=rgba('line'), r=MM_RADIUS)
    rounded(d, [2, 2, MM_W - 3, MM_H - 10], fill=rgba('panel'), outline=rgba('panel2'), r=MM_RADIUS - 2)
    # the two amber corner LEDs the concept has, top-left and top-right
    for cx in (16, MM_W - 20):
        d.rectangle([cx, 13, cx + 4, 17], fill=rgba('orange'))
    # glass: void inside a 1 px panel2 bevel
    gx, gy, gw, gh = MM_GLASS
    d.rectangle([gx - 2, gy - 2, gx + gw + 1, gy + gh + 1], fill=rgba('panel2'))
    d.rectangle([gx - 1, gy - 1, gx + gw, gy + gh], fill=rgba('line'))
    d.rectangle([gx, gy, gx + gw - 1, gy + gh - 1], fill=rgba('void'))
    # LED row under the glass: two amber, two teal -- the concept's cluster bottom-right of the case
    ly = gy + gh + 9
    for i, tone in enumerate(('linehi', 'orange', 'orange', 'signal', 'signal')):
        x = MM_W - 62 + i * 9
        d.rectangle([x, ly, x + 4, ly + 3], fill=rgba(tone))
    # a vent strip on the left of that row
    for x in range(gx, gx + 90, 4):
        d.rectangle([x, ly, x + 1, ly + 3], fill=rgba('panel2'))
    # stand: a trapezoid foot on the desk line
    cx = MM_W // 2
    for i in range(10):
        half = 26 + i * 3
        y = MM_H - 8 + i
        d.line([cx - half, y, cx + half, y], fill=rgba('panel' if i < 4 else 'void'))
    return img


def wall_monitors():
    img = Image.new('RGBA', (WM_W, WM_H), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    panels = [
        (0, 0, 64, 46, 'lines'),          # three rows of soft bars
        (70, 0, 148, 46, 'trace'),        # the big one with cyan text
        (0, 52, 64, 103, 'bars'),         # vertical bars
        (70, 52, 148, 103, 'sync'),       # the payroll panel
    ]
    for x0, y0, x1, y1, kind in panels:
        rounded(d, [x0, y0, x1, y1], fill=rgba('panel2'), outline=rgba('line'), r=4)
        rounded(d, [x0 + 3, y0 + 3, x1 - 3, y1 - 3], fill=rgba('void'), outline=None, r=3)
        if kind == 'lines':
            for i in range(4):
                d.line([x0 + 7, y0 + 12 + i * 8, x0 + 7 + (34 if i % 2 else 46), y0 + 12 + i * 8],
                       fill=rgba('panel2'))
        elif kind == 'trace':
            for i in range(4):
                d.line([x0 + 6, y0 + 10 + i * 8, x0 + 6 + (60 if i < 2 else 44), y0 + 10 + i * 8],
                       fill=rgba('signaldim'))
            d.rectangle([x1 - 12, y1 - 10, x1 - 8, y1 - 6], fill=rgba('orange'))
        elif kind == 'bars':
            for i in range(5):
                h = 6 + (i * 7) % 22
                d.rectangle([x0 + 8 + i * 10, y1 - 8 - h, x0 + 11 + i * 10, y1 - 8], fill=rgba('signaldim'))
        else:
            for i in range(3):
                d.rectangle([x0 + 8, y0 + 12 + i * 10, x0 + 52, y0 + 14 + i * 10], fill=rgba('signaldim'))
            d.rectangle([x1 - 12, y0 + 8, x1 - 8, y0 + 12], fill=rgba('orange'))
    return img


def server_rack():
    img = Image.new('RGBA', (SRV_W, SRV_H), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    w, h = SRV_W, SRV_H
    d.rectangle([0, 0, w - 1, h - 1], fill=rgba('panel'))
    d.rectangle([0, 0, w - 1, 2], fill=rgba('linehi'))
    d.line([0, 0, 0, h - 1], fill=rgba('line'))
    d.line([w - 1, 0, w - 1, h - 1], fill=rgba('line'))
    d.line([w - 6, 0, w - 6, h - 1], fill=rgba('line'))     # a side rail, like the concept
    for i in range(13):
        y = 6 + i * 11
        d.rectangle([4, y, w - 9, y + 6], fill=rgba('panel2'))
        d.line([5, y + 2, w - 10, y + 2], fill=rgba('void'))
        tone = 'danger' if i in (1, 4, 9) else ('orange' if i in (2, 7, 11) else 'linehi')
        d.point((w - 3, y + 3), fill=rgba(tone))
        d.point((w - 3, y + 5), fill=rgba('signal' if i % 3 == 0 else 'line'))
    return img


def wall_labels():
    """
    The concept's LABELS/MENUS column: tiny cyan words under the wall bank -- MICROSOFT, GRID-3, MENUS.
    Story furniture, not UI: they are what makes the wall read as a working office instead of decoration.
    Drawn with the same 5x7 atlas the game uses, so there is no second typeface on screen.
    """
    img = Image.new('RGBA', (WM_W, 30), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    words = ('MICROSOFT', 'GRID-3', 'MENUS')
    for i, word in enumerate(words):
        y = i * 10 + 1
        # a 1 px rule under each word, like a label on a machine
        d.line([0, y + 8, 2 + len(word) * 6, y + 8], fill=rgba('glow_teal'))
        for j, ch in enumerate(word):
            _glyph(d, ch, 2 + j * 6, y, rgba('signaldim'))
    return img


# 5x7 glyph rows for the handful of letters the labels need. Kept here, not imported, because this file
# must run before the font pipeline in a fresh clone.
_FONT = {
    'M': ('#...#', '##.##', '#.#.#', '#...#', '#...#', '#...#', '#...#'),
    'E': ('#####', '#....', '#....', '####.', '#....', '#....', '#####'),
    'N': ('#...#', '##..#', '#.#.#', '#..##', '#...#', '#...#', '#...#'),
    'U': ('#...#', '#...#', '#...#', '#...#', '#...#', '#...#', '.###.'),
    'S': ('.####', '#....', '#....', '.###.', '....#', '....#', '####.'),
    'I': ('#####', '..#..', '..#..', '..#..', '..#..', '..#..', '#####'),
    'C': ('.####', '#....', '#....', '#....', '#....', '#....', '.####'),
    'R': ('####.', '#...#', '#...#', '####.', '#.#..', '#..#.', '#...#'),
    'O': ('.###.', '#...#', '#...#', '#...#', '#...#', '#...#', '.###.'),
    'F': ('#####', '#....', '#....', '####.', '#....', '#....', '#....'),
    'T': ('#####', '..#..', '..#..', '..#..', '..#..', '..#..', '..#..'),
    'G': ('.###.', '#...#', '#...#', '#.###', '....#', '....#', '.###.'),
    'D': ('####.', '#...#', '#...#', '#...#', '#...#', '#...#', '####.'),
    '3': ('####.', '....#', '....#', '.###.', '....#', '....#', '####.'),
    '-': ('.....', '.....', '.....', '#####', '.....', '.....', '.....'),
    ' ': ('.....', '.....', '.....', '.....', '.....', '.....', '.....')
}


def _glyph(d, ch, x, y, colour):
    rows = _FONT.get(ch)
    if not rows:
        return
    for gy, row in enumerate(rows):
        for gx, c in enumerate(row):
            if c == '#':
                d.point((x + gx, y + gy), fill=colour)


def halo():
    img = Image.new('RGBA', (HL_W, HL_H), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    # a ring around the case: two flat steps, nothing inside (the menu texture lives there)
    inner = [10, 8, HL_W - 11, HL_H - 34]
    for i in range(1, 4):
        col = rgba('glow_teal') if i == 1 else rgba('signaldim')
        rounded(d, [inner[0] - i, inner[1] - i, inner[2] + i, inner[3] + i], outline=col,
                r=MM_RADIUS + i, width=1)
    # the pool on the desk below the case
    for i, tone in enumerate(('glow_teal', 'signaldim')):
        y = inner[3] + 6 + i * 4
        half = (inner[2] - inner[0]) // 2 - i * 30
        cx = (inner[0] + inner[2]) // 2
        d.line([cx - half, y, cx + half, y], fill=rgba(tone))
    return img


def palette_gate(images):
    vals = set(PALETTE.values())
    bad = 0
    for im in images:
        for y in range(im.height):
            for x in range(im.width):
                px = im.getpixel((x, y))
                if px[3] == 0:
                    continue
                if px[3] != 255 or px[:3] not in vals:
                    bad += 1
    return bad


def main():
    mon, wall, srv, hl = menu_monitor(), wall_monitors(), server_rack(), halo()
    labels = wall_labels()
    bad = palette_gate((mon, wall, srv, hl, labels))
    if '--check' in sys.argv:
        print('%s: %d off-palette pixels' % ('PASS' if bad == 0 else 'FAIL', bad))
        return 0 if bad == 0 else 1

    mon.save(out('desk/menu_monitor.png'))
    wall.save(out('desk/wall_monitors.png'))
    srv.save(out('desk/server_rack.png'))
    hl.save(out('fx/screen_halo.png'))
    labels.save(out('desk/wall_labels.png'))
    for name in ('desk/menu_monitor.png', 'desk/wall_monitors.png', 'desk/wall_labels.png',
                 'desk/server_rack.png', 'fx/screen_halo.png'):
        print('wrote art/kit/%s' % name)
    print('glass %dx%d = %d chars x %d lines; off-palette px: %d'
          % (MM_GLASS[2], MM_GLASS[3], (MM_GLASS[2] - 16) // 7, (MM_GLASS[3] + 3) // 10, bad))
    return 0 if bad == 0 else 1


if __name__ == '__main__':
    raise SystemExit(main())
