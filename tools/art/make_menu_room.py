#!/usr/bin/env python3
"""
make_menu_room.py -- the FRONT DOOR room, drawn from the player's brief (2026-11-28, ADR-019).

The brief, in the player's words:
    old monitor on the left with the title on top and the menu under it; on the desk a coffee with smoke,
    a keyboard and mouse beside it, and a pile of application forms; a cyan light fade coming out of the
    monitor; behind it in the furthest part of the room a table with six monitors (3 under, 3 above) on
    white screens, and two more glowing under the table; beside the table a window with a city silhouette,
    lit windows, night sky and moon; on the right of the window in the corner, a database server; tiled
    floor with black outlines. Dark and eerie, cyan, pixel. Inspiration: That's Not My Neighbor.

WHAT THE RESEARCH CHANGED (see docs/16-light-study.md)
    1. ONE dominant light source: the monitor. Every other glow is subordinate to it -- the white screens
       across the room are bright but small, the window is cold and far.
    2. Falloff is BANDS, and only the band edges are dithered. v113 dimmed the whole frame with a checker,
       which read as noise; here the checker appears only in the 1-px seam between two light steps.
    3. Bright cores stay clean and solid. Dither belongs at the boundary, never on the light itself.
    4. Shadows are lifted toward blue-teal, not crushed to black: the room is legible, which is what makes
       it feel wrong rather than merely dark.
    5. Warm colours are rationed -- the coffee's steam and one amber LED are the only warmth in frame.

Sprites written (all palette-only, alpha 0 or 255):
    menu/shell.png          480x270  back wall, tiled floor with black grout, skirting, ambient vignette
    menu/window.png         104x132  night sky, moon, city silhouette, lit windows
    menu/monitor_stack.png  176x136  the table and its eight white screens
    menu/server.png          56x150  the database cabinet in the right corner
    menu/crt.png            240x186  the old monitor the menu lives in
    menu/desk.png           480x74   desk: coffee + smoke, keyboard, mouse, forms

    python tools/art/make_menu_room.py            # write the six sprites
    python tools/art/make_menu_room.py --check    # palette gate only
"""
import os
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
sys.path.insert(0, os.path.join(ROOT, 'tools', 'art'))

from PIL import Image, ImageDraw  # noqa: E402
from make_art_kit import PALETTE, rgba  # noqa: E402

W, H = 480, 270

# ---- the room's structure ---------------------------------------------------------------------------
FLOOR_Y = 196                 # where the wall meets the floor
TILE_W, TILE_H = 26, 18       # floor tiles, black grout
SKIRT_H = 6

# ---- the monitor the menu lives in ------------------------------------------------------------------
CRT_W, CRT_H = 240, 172
CRT_GLASS = (14, 14, 212, 126)   # x, y, w, h inside crt.png -- 30 chars x 12 lines at 1x

# ---- window, table, server (positions inside their own sprites) ---------------------------------------
WIN_W, WIN_H = 82, 108
STACK_W, STACK_H = 100, 100
SRV_W, SRV_H = 26, 112
DESK_H = 64
DESK_TOP = 18          # the slab starts this far down: the rest is transparent headroom for the steam


def out(rel):
    return os.path.join(ROOT, 'art', 'kit', rel)


def px(d, x, y, key):
    d.point((x, y), fill=rgba(key))


def hline(d, x0, x1, y, key):
    d.line([x0, y, x1, y], fill=rgba(key))


def vline(d, x, y0, y1, key):
    d.line([x, y0, x, y1], fill=rgba(key))


def dither_band(d, x0, y0, x1, y1, a, b, density=2):
    """
    Bayer-ish 2x2 ordered dither between two palette colours inside a rect.
    Used ONLY at the seam between two light steps -- see docs/16-light-study.md §2.
    """
    pat = ((0, 2), (3, 1))
    for y in range(y0, y1 + 1):
        for x in range(x0, x1 + 1):
            d.point((x, y), fill=rgba(b if pat[y % 2][x % 2] < density else a))


def round_rect(d, box, fill=None, outline=None, r=3, width=1):
    d.rounded_rectangle(box, radius=r, fill=fill, outline=outline, width=width)


# ---------------------------------------------------------------------------------------- shell
# ---------------------------------------------------------------------------------------- shell
def shell():
    """Back wall + tiled floor. Nothing in here is a light: the ambience pass owns the light."""
    img = Image.new('RGBA', (W, H), rgba('void'))
    d = ImageDraw.Draw(img)

    # ---- back wall: flat, with a skirting and two vertical seams so it has a scale
    d.rectangle([0, 0, W - 1, FLOOR_Y - 1], fill=rgba('panel'))
    for x in (150, 340):
        vline(d, x, 0, FLOOR_Y - 1, 'panel2')
    # one step of value where the wall meets the ceiling shadow
    dither_band(d, 0, 0, W - 1, 10, 'void', 'panel', density=2)
    hline(d, 0, W - 1, 0, 'void')

    # ---- skirting: a real ledge, black underneath, so the floor starts with a hard line
    d.rectangle([0, FLOOR_Y - SKIRT_H, W - 1, FLOOR_Y - 1], fill=rgba('panel2'))
    hline(d, 0, W - 1, FLOOR_Y - SKIRT_H, 'line')
    hline(d, 0, W - 1, FLOOR_Y - 1, 'void')

    # ---- floor: tiles with BLACK grout, in two rows of perspective (the brief asks for outlines)
    d.rectangle([0, FLOOR_Y, W - 1, H - 1], fill=rgba('panel2'))
    row_y = FLOOR_Y
    row = 0
    while row_y < H:
        h = TILE_H if row == 0 else TILE_H
        # horizontal grout
        hline(d, 0, W - 1, row_y, 'void')
        # vertical grout, offset every other row so it reads as tiling rather than a grid
        off = 0 if row % 2 == 0 else TILE_W // 2
        x = off
        while x < W:
            vline(d, x, row_y, min(row_y + h - 1, H - 1), 'void')
            x += TILE_W
        # the tile face catches a little of the ambient, its lower edge is darker
        if row_y + h - 1 < H:
            hline(d, 0, W - 1, row_y + h - 1, 'panel')
        row_y += h
        row += 1
    return img


# ---------------------------------------------------------------------------------------- window
def window():
    """
    Night sky, moon, city silhouette, lit windows. The one COLD, FAR light in the room.

    The sky is `panel` and the buildings are `void`: a silhouette only reads if the sky behind it is
    lighter than it is. The first version painted both in `void` and the entire city disappeared.
    """
    img = Image.new('RGBA', (WIN_W, WIN_H), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    Wm, Hm = WIN_W, WIN_H

    # ---- reveal: a concrete surround with a lit top edge and a black inner lip
    round_rect(d, [0, 0, Wm - 1, Hm - 1], fill=rgba('panel2'), r=2)
    hline(d, 2, Wm - 3, 0, 'line')
    vline(d, 0, 1, Hm - 2, 'line')
    vline(d, Wm - 1, 1, Hm - 2, 'void')
    hline(d, 2, Wm - 3, Hm - 1, 'void')

    gx0, gy0, gx1, gy1 = 5, 5, Wm - 6, Hm - 6
    d.rectangle([gx0, gy0, gx1, gy1], fill=rgba('panel'))          # the sky: one step up from black

    # ---- stars: seven, sparse. A pixel sky is made by restraint, not by noise.
    for (sx, sy) in ((gx0 + 9, gy0 + 10), (gx0 + 24, gy0 + 6), (gx0 + 46, gy0 + 16),
                     (gx0 + 12, gy0 + 30), (gx0 + 35, gy0 + 26), (gx0 + 62, gy0 + 34),
                     (gx0 + 5, gy0 + 46)):
        if sx <= gx1:
            d.point((sx, sy), fill=rgba('muted'))

    # ---- moon: small, high, crescent. Its lit side takes the only cold white in the room.
    mx, my, mr = gx0 + 52, gy0 + 20, 6
    d.ellipse([mx - mr, my - mr, mx + mr, my + mr], fill=rgba('textdim'))
    d.ellipse([mx - mr + 3, my - mr - 1, mx + mr + 3, my + mr + 1], fill=rgba('panel'))
    d.point((mx - 3, my + 2), fill=rgba('muted'))

    # ---- skyline: five masses of different heights, `void` against the `panel` sky
    horizon = gy1 - 22
    for (bx, bh, bw) in ((gx0, 15, 14), (gx0 + 15, 24, 12), (gx0 + 28, 11, 13),
                         (gx0 + 42, 20, 11), (gx0 + 54, 26, 13)):
        bx1 = min(bx + bw, gx1)
        if bx > gx1 or bx1 <= bx:
            continue
        d.rectangle([bx, gy1 - bh, bx1, gy1], fill=rgba('void'))
        # a lit parapet on the tallest two: a city at night is lit from above as well as inside
        if bh >= 20:
            hline(d, bx, bx1, gy1 - bh, 'line')
            d.point((bx + 2, gy1 - bh - 1), fill=rgba('danger'))
        # windows: one in four, warm and cold mixed -- never a single colour
        for wy in range(gy1 - bh + 4, gy1 - 2, 5):
            for wx in range(bx + 2, bx1 - 1, 4):
                seed = (wx * 7 + wy * 13) % 13
                if seed == 0:
                    d.point((wx, wy), fill=rgba('warn'))
                elif seed == 4:
                    d.point((wx, wy), fill=rgba('textdim'))
                elif seed == 8:
                    d.point((wx, wy), fill=rgba('line'))

    # ---- glazing bars, painted OVER the sky so the frame reads as glass, and a sill
    d.line([gx0 + (gx1 - gx0) // 2, gy0, gx0 + (gx1 - gx0) // 2, gy1], fill=rgba('panel2'))
    d.line([gx0, gy0 + (gy1 - gy0) // 3, gx1, gy0 + (gy1 - gy0) // 3], fill=rgba('panel2'))
    hline(d, gx0, gx1, gy1, 'void')

    # ---- the corner of the glass catches the sky: a dithered wedge in the top-left
    for y in range(gy0, gy0 + 16):
        for x in range(gx0, gx0 + 16 - (y - gy0)):
            if ((x + y) & 1) == 0:
                d.point((x, y), fill=rgba('panel2'))
    return img


# ---------------------------------------------------------------------------------------- monitor stack
def _screen(d, x, y, w, h):
    """One white screen: a case with a bezel, a glare streak in the top-left, black under the bottom lip."""
    round_rect(d, [x, y, x + w, y + h], fill=rgba('panel'), r=1)
    d.rectangle([x + 1, y + 1, x + w - 1, y + h - 2], fill=rgba('text'))
    # glare: a small stepped wedge in the top-LEFT corner only -- a glare, not a stripe. A full diagonal
    # lines up with the screen beside it and the whole stack reads as one scratch across the wall.
    for i, wlen in enumerate((4, 3, 2)):
        d.line([x + 2, y + 3 + i, x + 2 + wlen, y + 3 + i], fill=rgba('white'))
    d.line([x + 2, y + 3, x + 2, y + 6], fill=rgba('white'))
    # a dead pixel row along the top: it is what makes a white screen look like a machine
    hline(d, x + 1, x + w - 1, y + 1, 'textdim')
    d.line([x, y + h, x + w, y + h], fill=rgba('void'))


def monitor_stack():
    """
    The brief: a table with six monitors on it -- three underneath, three above -- all on white screens,
    and two more glowing under the table. One of the six is dead; a face of black is the eeriest thing
    in the frame and it stops the wall of white from becoming a lightbox.
    """
    img = Image.new('RGBA', (STACK_W, STACK_H), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    Wm, Hm = STACK_W, STACK_H

    SW, SH, GAP = 30, 22, 4
    xs = [2, 2 + SW + GAP, 2 + 2 * (SW + GAP)]
    upper_y, lower_y = 2, 26
    table_y = 50

    # ---- two cables down the back, drawn FIRST so they pass behind everything: the stack is installed,
    # not floating
    for x0 in (5, Wm - 6):
        for i in range(table_y + 6, Hm):
            d.point((x0, i), fill=rgba('void'))

    # ---- the table: a slab, then two legs in silhouette, then the shelf that carries the lower pair
    d.rectangle([0, table_y, Wm - 1, table_y + 3], fill=rgba('panel2'))
    d.line([0, table_y, Wm - 1, table_y], fill=rgba('line'))
    d.line([0, table_y + 3, Wm - 1, table_y + 3], fill=rgba('void'))
    for lx in (3, Wm - 5):
        d.rectangle([lx, table_y + 4, lx + 2, Hm - 1], fill=rgba('panel'))
        d.line([lx + 2, table_y + 4, lx + 2, Hm - 1], fill=rgba('void'))
    d.rectangle([6, 58, Wm - 7, 59], fill=rgba('panel'))

    # ---- two monitors under the table, on the shelf, glowing up at the slab
    for sx in (8, 54):
        _screen(d, sx, 60, 34, 26)

    # ---- three under, three above
    for sx in xs:
        _screen(d, sx, lower_y, SW, SH)
    for sx in xs:
        _screen(d, sx, upper_y, SW, SH)
    # the middle of the top row is dead
    d.rectangle([xs[1] + 1, upper_y + 1, xs[1] + SW - 1, upper_y + SH - 2], fill=rgba('void'))
    for i in range(6):
        d.point((xs[1] + 3 + i * 4, upper_y + 7), fill=rgba('panel2'))

    return img


# ---------------------------------------------------------------------------------------- server
def server():
    """The database cabinet in the corner: nine rack units, status LEDs, one big vent at the bottom."""
    img = Image.new('RGBA', (SRV_W, SRV_H), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    Wm, Hm = SRV_W, SRV_H

    # ---- body: black edges, a panel face, a lit right edge (it is in the corner, nearest the window)
    d.rectangle([0, 0, Wm - 1, Hm - 1], fill=rgba('void'))
    d.rectangle([1, 1, Wm - 2, Hm - 2], fill=rgba('panel2'))
    vline(d, 1, 1, Hm - 2, 'line')
    vline(d, Wm - 2, 1, Hm - 2, 'void')
    hline(d, 1, Wm - 2, 0, 'line')

    # ---- nine rack units, each a slot with two screws and an LED
    y = 4
    for unit in range(8):
        d.rectangle([3, y, Wm - 4, y + 10], fill=rgba('panel'))
        hline(d, 3, Wm - 4, y + 10, 'void')
        px(d, 5, y + 2, 'line')
        px(d, Wm - 6, y + 2, 'line')
        # LEDs: three green, two amber, one red. Warmth rationed means ONE amber in the whole cabinet
        if unit in (0, 3, 6):
            d.point((6, y + 6), fill=rgba('glow_teal'))
        if unit == 1:
            d.point((9, y + 6), fill=rgba('signal'))
        if unit == 4:
            d.point((9, y + 6), fill=rgba('danger'))
        y += 13

    # ---- vent grille at the bottom
    for i in range(4):
        hline(d, 4, Wm - 5, Hm - 10 + i * 2, 'void')
    return img


# ---------------------------------------------------------------------------------------- crt
def crt():
    """
    The old monitor the menu lives in: a deep plastic case, a thick bezel, a control strip at the bottom
    right. The glass is left empty here -- the composite paints its phosphor and the menu on top.
    """
    img = Image.new('RGBA', (CRT_W, CRT_H), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    Wm, Hm = CRT_W, CRT_H
    gx, gy, gw, gh = CRT_GLASS

    # ---- case: two values and a black outline, plus a chamfer at each corner
    round_rect(d, [0, 0, Wm - 1, Hm - 1], fill=rgba('panel2'), outline=rgba('void'), r=6)
    round_rect(d, [3, 3, Wm - 4, Hm - 4], fill=rgba('panel'), outline=rgba('void'), r=5)
    # the top edge catches the room; the bottom edge is in its own shadow
    hline(d, 6, Wm - 7, 3, 'line')
    hline(d, 6, Wm - 7, Hm - 5, 'void')

    # ---- bezel: the plastic lip that surrounds the tube, with a highlight on the top-left of the glass
    d.rectangle([gx - 4, gy - 4, gx + gw + 3, gy + gh + 3], fill=rgba('panel2'))
    vline(d, gx - 4, gy - 4, gy + gh + 3, 'void')
    hline(d, gx - 4, gx + gw + 3, gy - 4, 'void')
    d.rectangle([gx - 1, gy - 1, gx + gw, gy + gh], fill=rgba('void'))

    # ---- control strip: a speaker grille, a power LED, four small buttons
    sy = gy + gh + 6
    for i in range(9):
        vline(d, 12 + i * 3, sy, sy + 4, 'line')
    d.rectangle([80, sy + 2, 84, sy + 3], fill=rgba('line'))
    d.point((96, sy + 2), fill=rgba('glow_teal'))
    for i in range(4):
        d.rectangle([170 + i * 10, sy, 174 + i * 10, sy + 5], fill=rgba('panel2'))
        d.point((172 + i * 10, sy + 1), fill=rgba('line'))
    # three amber status dots, the only warmth on the case
    for i, key in enumerate(('signal', 'signal', 'danger')):
        d.rectangle([206 + i * 5, sy + 1, 208 + i * 5, sy + 3], fill=rgba(key))

    # ---- the case sits on a foot: a 2-px black line at the very bottom
    hline(d, 0, Wm - 1, Hm - 1, 'void')
    return img


# ---------------------------------------------------------------------------------------- desk
def desk():
    """The desk the CRT sits on: coffee + smoke, keyboard, mouse, a pile of application forms."""
    img = Image.new('RGBA', (W, DESK_H), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)

    # ---- desk top: a slab with a lit top edge and a dark front face
    d.rectangle([0, 0, W - 1, 30], fill=rgba('panel2'))
    hline(d, 0, W - 1, 0, 'line')
    dither_band(d, 0, 26, W - 1, 30, 'panel2', 'panel', density=2)
    d.rectangle([0, 31, W - 1, DESK_H - 1], fill=rgba('void'))
    hline(d, 0, W - 1, 31, 'panel')
    for y, x0, x1 in ((12, 20, 210), (18, 60, 300), (8, 330, 460)):
        hline(d, x0, x1, y, 'panel')

    # ---- coffee cup: left of the keyboard, handle to the right, three curls of steam (the one warmth)
    cx, cy = 74, DESK_TOP + 6
    d.rectangle([cx - 10, cy + 3, cx + 10, cy + 22], fill=rgba('panel'))
    vline(d, cx - 10, cy + 3, cy + 22, 'linehi')
    vline(d, cx + 10, cy + 3, cy + 22, 'line')
    hline(d, cx - 9, cx + 9, cy + 22, 'void')
    round_rect(d, [cx - 12, cy, cx + 12, cy + 4], fill=rgba('panel2'), outline=rgba('line'), r=2)
    hline(d, cx - 7, cx + 7, cy + 2, 'ink')
    hline(d, cx - 3, cx + 2, cy + 2, 'orange')
    for i in range(1, 8):
        d.point((cx + 12 + (1 if i in (1, 7) else 0), cy + 6 + i), fill=rgba('linehi'))
    for k, (x0, height, tone) in enumerate(((cx - 5, 14, 'textdim'), (cx, 20, 'muted'), (cx + 5, 11, 'linehi'))):
        for i in range(height):
            y = cy - 2 - i
            if y < 0:
                continue
            d.point((x0 + ((i // 5) % 2), y), fill=rgba(tone))

    # ---- keyboard: a wedge, key rows, a spacebar. Sits right of the coffee, under the CRT.
    kx, ky, kw, kh = 108, DESK_TOP + 8, 104, 24
    round_rect(d, [kx, ky, kx + kw, ky + kh], fill=rgba('panel'), outline=rgba('void'), r=2)
    hline(d, kx + 1, kx + kw - 1, ky, 'line')
    for row in range(4):
        y = ky + 4 + row * 4
        for col in range(21):
            x = kx + 3 + col * 5
            if row == 3 and 4 <= col <= 15:
                continue
            d.point((x, y), fill=rgba('line'))
            d.point((x, y + 1), fill=rgba('panel2'))
    # spacebar + two modifiers
    d.rectangle([kx + 22, ky + 18, kx + 78, ky + 20], fill=rgba('line'))
    d.rectangle([kx + 3, ky + 18, kx + 15, ky + 20], fill=rgba('panel2'))
    d.rectangle([kx + 84, ky + 18, kx + 100, ky + 20], fill=rgba('panel2'))

    # ---- mouse: right of the coffee, left of the forms, with a 2-px cable
    round_rect(d, [246, DESK_TOP + 8, 260, DESK_TOP + 24], fill=rgba('panel'), outline=rgba('line'), r=4)
    hline(d, 248, 258, DESK_TOP + 9, 'linehi')
    vline(d, 253, DESK_TOP + 11, DESK_TOP + 15, 'void')
    for i in range(6):
        d.point((254 + i // 3, DESK_TOP + 7 - i // 2), fill=rgba('panel'))

    # ---- pile of application forms: off-white, black outline, three sheets, a red stamp
    fx, fy = 292, DESK_TOP + 2
    for i in range(5):
        x = fx + i * 3
        y = fy + i * 2
        d.rectangle([x, y, x + 74, y + 28], fill=rgba('paper'))
        d.rectangle([x, y, x + 74, y + 28], outline=rgba('ink'))
        # ruled lines on the top sheet only
        for r in range(5):
            hline(d, x + 6, x + 68, y + 6 + r * 4, 'paperdim')
        d.point((x + 4, y + 4), fill=rgba('paperdim'))
    # the stamp: a small red mark on the top sheet, which is the whole story of the game
    d.rectangle([fx + 50, fy + 18, fx + 66, fy + 24], outline=rgba('danger'))
    hline(d, fx + 52, fx + 64, fy + 21, 'danger')
    # a pen, dropped beside the pile
    for i in range(14):
        d.point((fx + 68 + i, DESK_TOP + 26 - i // 4), fill=rgba('linehi'))
    d.point((fx + 82, DESK_TOP + 22), fill=rgba('signal'))

    # The desk does NOT paint its own light. The ambience pass in MenuView.Light() decides what the CRT
    # does to the desk -- a hand-painted wedge fought it and left the familiar green diagonal stripes.
    return img
def palette_gate(images):
    vals = set(PALETTE.values())
    bad = 0
    for im in images:
        for y in range(im.height):
            for x in range(im.width):
                p = im.getpixel((x, y))
                if p[3] == 0:
                    continue
                if p[3] != 255 or p[:3] not in vals:
                    bad += 1
    return bad


def main():
    pieces = {
        'menu/shell.png': shell(),
        'menu/window.png': window(),
        'menu/monitor_stack.png': monitor_stack(),
        'menu/server.png': server(),
        'menu/crt.png': crt(),
        'menu/desk.png': desk(),
    }
    bad = palette_gate(pieces.values())
    if '--check' in sys.argv:
        print('%s: %d off-palette pixels' % ('PASS' if bad == 0 else 'FAIL', bad))
        return 0 if bad == 0 else 1
    for rel, im in pieces.items():
        im.save(out(rel))
        print('wrote art/kit/%s (%dx%d)' % (rel, im.width, im.height))
    print('glass %dx%d; off-palette px: %d' % (CRT_GLASS[2], CRT_GLASS[3], bad))
    return 0 if bad == 0 else 1


if __name__ == '__main__':
    raise SystemExit(main())
