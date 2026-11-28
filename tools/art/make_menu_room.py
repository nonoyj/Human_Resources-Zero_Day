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
CRT_W, CRT_H = 252, 186
CRT_GLASS = (24, 16, 224, 148)   # x, y, w, h inside crt.png -- tube picture area

# ---- window, table, server (positions inside their own sprites) ---------------------------------------
WIN_W, WIN_H = 96, 108
STACK_W, STACK_H = 96, 104
SRV_W, SRV_H = 22, 112
DESK_H = 64
DESK_TOP = 18          # the slab starts this far down: the rest is transparent headroom for the steam


def out(rel):
    return os.path.join(ROOT, 'art', 'kit', rel)


def px(d, x, y, key):
    d.point((x, y), fill=rgba(key))


def hline(d, x0, x1, y, key):
    """
    A horizontal one-pixel line, half-open: [x0, x1). Pillow's d.line() is inclusive on both ends, so a
    caller that says `hline(d, 0, W - 1, y)` gets exactly W pixels -- and, more importantly, a line that
    stamps the SAME pixel as the line beside it is one pixel wider than it was asked to be. Those extra
    pixels were the teal streaks running across the desk and the wall in the v115 renders.
    """
    if x1 <= x0:
        return
    d.line([x0, y, x1 - 1, y], fill=rgba(key))


def vline(d, x, y0, y1, key):
    """Vertical one-pixel line, half-open: [y0, y1). Same rule as hline()."""
    if y1 <= y0:
        return
    d.line([x, y0, x, y1 - 1], fill=rgba(key))


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


def cut_corners(d, box, r, key):
    """
    Removes a quarter-disc of radius r from each corner of `box`. A CRT's tube and its plastic case are
    both rounded off at the corners; drawing them as plain rectangles is the single biggest tell that a
    "retro monitor" was drawn by someone who has never looked at one. See docs/16-light-study.md.
    """
    x0, y0, x1, y1 = box
    fill = rgba(key)
    for i in range(r):
        for j in range(r - i):
            # top-left, top-right, bottom-left, bottom-right
            d.point((x0 + i, y0 + j), fill=fill)
            d.point((x1 - i, y0 + j), fill=fill)
            d.point((x0 + i, y1 - j), fill=fill)
            d.point((x1 - i, y1 - j), fill=fill)


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
    Night sky, moon, a city in two depth layers, lit windows. The one COLD, FAR light in the room.

    The city is built as TWO layers, which is the atmospheric-perspective rule: the far skyline is only
    one value off the sky and almost no detail, the near skyline is `void` with lit windows. One flat row
    of buildings reads as wallpaper; two layers read as a city with depth behind glass.
    """
    img = Image.new('RGBA', (WIN_W, WIN_H), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    Wm, Hm = WIN_W, WIN_H

    # ---- reveal: a concrete surround, lit top edge, black inner lip, and a sill
    round_rect(d, [0, 0, Wm - 1, Hm - 1], fill=rgba('panel2'), r=2)
    hline(d, 2, Wm - 2, 0, 'line')
    vline(d, 0, 1, Hm - 1, 'line')
    vline(d, Wm - 1, 1, Hm - 1, 'void')
    hline(d, 2, Wm - 2, Hm - 1, 'void')

    gx0, gy0, gx1, gy1 = 5, 5, Wm - 7, Hm - 7
    d.rectangle([gx0, gy0, gx1, gy1], fill=rgba('panel'))          # the sky

    # ---- stars: sparse, up in the top third where the sky is darkest
    for (fx, fy) in ((0.10, 0.12), (0.28, 0.07), (0.46, 0.15), (0.63, 0.09),
                     (0.80, 0.18), (0.92, 0.10), (0.36, 0.26), (0.74, 0.30)):
        d.point((gx0 + int(fx * (gx1 - gx0)), gy0 + int(fy * (gy1 - gy0))), fill=rgba('muted'))

    # ---- moon, high and right: a crescent with the shadow side painted in the sky colour
    mx, my, mr = gx0 + int(0.72 * (gx1 - gx0)), gy0 + 13, 6
    d.ellipse([mx - mr, my - mr, mx + mr, my + mr], fill=rgba('textdim'))
    d.ellipse([mx - mr + 3, my - mr - 1, mx + mr + 3, my + mr + 1], fill=rgba('panel'))
    d.point((mx - 3, my + 2), fill=rgba('muted'))

    # ---- FAR skyline: one value off the sky, no windows, no detail. This is the layer that gives depth.
    far_base = gy1 - 12
    for (fx, fw, fh) in ((0.02, 0.13, 20), (0.16, 0.10, 30), (0.27, 0.14, 24),
                         (0.43, 0.11, 34), (0.55, 0.13, 26), (0.70, 0.12, 30), (0.83, 0.15, 22)):
        bx0 = gx0 + int(fx * (gx1 - gx0))
        bx1 = min(bx0 + int(fw * (gx1 - gx0)), gx1)
        if bx1 <= bx0:
            continue
        d.rectangle([bx0, far_base - fh, bx1, gy1], fill=rgba('panel2'))
    # a single pale band along the far horizon: the city's own glow, dithered so it is not a stripe
    for y in range(far_base - 4, far_base):
        for x in range(gx0, gx1 + 1):
            if ((x + y) & 1) == 0:
                d.point((x, y), fill=rgba('line'))

    # ---- NEAR skyline: `void` against the sky, lit windows, a few roofs and a red aircraft light
    near_base = gy1
    near = ((0.00, 0.15, 26), (0.14, 0.13, 18), (0.26, 0.12, 32),
            (0.38, 0.11, 22), (0.48, 0.14, 38), (0.61, 0.12, 26),
            (0.72, 0.13, 20), (0.84, 0.14, 30))
    for (fx, fw, fh) in near:
        bx0 = gx0 + int(fx * (gx1 - gx0))
        bx1 = min(bx0 + int(fw * (gx1 - gx0)), gx1)
        if bx1 <= bx0:
            continue
        d.rectangle([bx0, near_base - fh, bx1, near_base], fill=rgba('void'))
        hline(d, bx0, bx1, near_base - fh, 'line')              # the parapet catches the sky
        if fh >= 30:
            d.point((bx0 + 2, near_base - fh - 1), fill=rgba('danger'))   # aircraft light
        for wy in range(near_base - fh + 4, near_base - 2, 5):
            for wx in range(bx0 + 2, bx1 - 1, 4):
                seed = (wx * 7 + wy * 13 + (wx * wy) // 3) % 13
                if seed == 0:
                    d.point((wx, wy), fill=rgba('warn'))
                elif seed == 5:
                    d.point((wx, wy), fill=rgba('textdim'))
                elif seed == 9:
                    d.point((wx, wy), fill=rgba('line'))

    # ---- glazing bars over the whole thing, so it reads as glass and not as a picture
    d.line([gx0 + (gx1 - gx0) // 2, gy0, gx0 + (gx1 - gx0) // 2, gy1], fill=rgba('panel2'))
    d.line([gx0, gy0 + (gy1 - gy0) // 3, gx1, gy0 + (gy1 - gy0) // 3], fill=rgba('panel2'))
    hline(d, gx0, gx1, gy1, 'void')

    # ---- the top-left corner of the glass catches the sky: a dithered wedge, the only cool highlight
    for y in range(gy0, gy0 + 16):
        for x in range(gx0, gx0 + 16 - (y - gy0)):
            if ((x + y) & 1) == 0:
                d.point((x, y), fill=rgba('panel2'))
    return img


# ---------------------------------------------------------------------------------------- monitor stack
def _screen(d, x, y, w, h):
    """
    One of the table's monitors. Same object language as the big one on the desk, at a third of the size:
    a cut-corner plastic case, a thick bottom bezel holding an LED, and an inset white tube (not a white
    rectangle with a border). White is the light; the case around it is what makes it read as a monitor.
    """
    # case
    d.rectangle([x, y, x + w, y + h], fill=rgba('void'))
    d.rectangle([x + 1, y + 1, x + w - 1, y + h - 1], fill=rgba('panel'))
    cut_corners(d, [x + 1, y + 1, x + w - 1, y + h - 1], 2, 'void')
    hline(d, x + 3, x + w - 3, y + 1, 'line')

    # the tube: inset, with the white phosphor and a stepped glare in the top-left corner
    tx0, ty0 = x + 3, y + 3
    tx1, ty1 = x + w - 4, y + h - 6          # the bottom 3 px are bezel, not glass
    d.rectangle([tx0, ty0, tx1, ty1], fill=rgba('text'))
    for i, wlen in enumerate((3, 2)):
        d.line([tx0 + 1, ty0 + 1 + i, tx0 + 1 + wlen, ty0 + 1 + i], fill=rgba('white'))
    # a dead-pixel row along the top: a machine, not a lamp
    hline(d, tx0, tx1 + 1, ty0, 'textdim')

    # bottom bezel: an LED and two slots
    ly = y + h - 3
    d.point((x + w - 6, ly), fill=rgba('glow_teal'))
    vline(d, x + 4, ly, ly + 1, 'panel2')
    vline(d, x + 7, ly, ly + 1, 'panel2')


def monitor_stack():
    """
    The table in the furthest part of the room: six monitors in two rows of three -- three underneath,
    three above -- and two more on a shelf under the table. All of them retro cases, all of them white.
    One of the six is dead, because a face of black in a wall of white is the eeriest thing in the frame.
    """
    img = Image.new('RGBA', (STACK_W, STACK_H), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    Wm, Hm = STACK_W, STACK_H

    SW, SH, GAP = 30, 26, 3
    xs = [2, 2 + SW + GAP, 2 + 2 * (SW + GAP)]
    upper_y, lower_y = 2, 30
    table_y = 58

    # ---- cables first, so they pass behind everything: the stack is installed, not floating
    for x0 in (5, Wm - 6):
        for i in range(table_y + 4, Hm):
            d.point((x0, i), fill=rgba('void'))

    # ---- the table: a slab with a lit top edge, two legs, and a shelf under it
    d.rectangle([0, table_y, Wm - 1, table_y + 3], fill=rgba('panel2'))
    d.line([0, table_y, Wm - 1, table_y], fill=rgba('line'))
    d.line([0, table_y + 3, Wm - 1, table_y + 3], fill=rgba('void'))
    for lx in (3, Wm - 6):
        d.rectangle([lx, table_y + 4, lx + 2, Hm - 1], fill=rgba('panel'))
        d.line([lx + 2, table_y + 4, lx + 2, Hm - 1], fill=rgba('void'))

    # ---- two monitors on the shelf, glowing up at the underside of the table
    d.rectangle([7, 68, Wm - 8, 69], fill=rgba('panel'))
    for sx in (9, 55):
        _screen(d, sx, 70, 32, 26)

    # ---- six on the table, three under and three above
    for sx in xs:
        _screen(d, sx, lower_y, SW, SH)
    for sx in xs:
        _screen(d, sx, upper_y, SW, SH)
    # the middle of the top row is dead: case on, tube off
    dx = xs[1]
    d.rectangle([dx + 3, upper_y + 3, dx + SW - 4, upper_y + SH - 6], fill=rgba('void'))
    for i in range(5):
        d.point((dx + 5 + i * 4, upper_y + 8), fill=rgba('panel2'))
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
    The old monitor the menu lives in.

    A 1990s office CRT, read from the actual object rather than from a "retro" filter:

      - a DEEP case: the plastic is thicker at the bottom (the tube is heavy) and there are feet;
      - a THICK front bezel, wider at the bottom because the control strip lives there;
      - CUT CORNERS on both the case and the tube -- a CRT is a rounded box, never a rectangle;
      - a SHADOW SIDE: the right of the case is one value darker, because the light is on the left;
      - the tube is darker at its own edges than in the middle (phosphor falls off at the rim);
      - a moulded seam line across the case, a vent grille, a badge, two knobs, a power LED.

    The glass itself is left as flat `void` here: the composite paints the phosphor, and MenuView paints
    its own copy in GlassMaterial().
    """
    img = Image.new('RGBA', (CRT_W, CRT_H), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    Wm, Hm = CRT_W, CRT_H
    gx, gy, gw, gh = CRT_GLASS

    # ---- feet: two blocks under a case this heavy, one value darker than the case
    d.rectangle([26, Hm - 5, 62, Hm - 1], fill=rgba('void'))
    d.rectangle([Wm - 62, Hm - 5, Wm - 26, Hm - 1], fill=rgba('void'))

    # ---- the case: three values, cut corners, black outline
    round_rect(d, [0, 0, Wm - 1, Hm - 6], fill=rgba('void'), r=8)
    round_rect(d, [1, 1, Wm - 2, Hm - 7], fill=rgba('panel2'), r=8)
    cut_corners(d, [1, 1, Wm - 2, Hm - 7], 6, 'void')
    # the moulded face catches the room: `panel` on the left, `panel2` on the right
    round_rect(d, [3, 3, Wm - 4, Hm - 8], fill=rgba('panel'), r=7)
    cut_corners(d, [3, 3, Wm - 4, Hm - 8], 5, 'panel2')
    d.rectangle([Wm - 30, 8, Wm - 4, Hm - 10], fill=rgba('panel'))
    d.rectangle([Wm - 6, 10, Wm - 4, Hm - 12], fill=rgba('panel2'))    # shadow side
    hline(d, 10, Wm - 12, 3, 'line')
    hline(d, 4, Wm - 5, Hm - 8, 'void')

    # ---- the bezel around the tube: `panel` with a black inner lip all the way round, and a
    #      wider bottom band that carries the controls
    d.rectangle([gx - 7, gy - 7, gx + gw + 6, gy + gh + 15], fill=rgba('panel'))
    cut_corners(d, [gx - 7, gy - 7, gx + gw + 6, gy + gh + 15], 7, 'panel2')
    hline(d, gx - 7, gx + gw + 6, gy - 7, 'line')
    vline(d, gx - 7, gy - 7, gy + gh + 15, 'linehi')
    d.rectangle([gx - 2, gy - 2, gx + gw + 1, gy + gh + 1], fill=rgba('void'))
    cut_corners(d, [gx - 2, gy - 2, gx + gw + 1, gy + gh + 1], 8, 'panel')

    # ---- the control strip: vent grille, badge, two knobs, power LED, three status dots
    sy = gy + gh + 6
    for i in range(14):
        vline(d, 12 + i * 3, sy, sy + 4, 'line')
    # badge: a recessed plate with two glyph-ish marks
    d.rectangle([64, sy - 1, 90, sy + 6], fill=rgba('panel2'))
    d.rectangle([64, sy - 1, 90, sy + 6], outline=rgba('void'))
    d.rectangle([67, sy + 2, 76, sy + 4], fill=rgba('line'))
    d.point((80, sy + 2), fill=rgba('line'))
    d.point((84, sy + 2), fill=rgba('line'))
    # two knobs: a circle is a pixel cross, not a disc
    for kx in (108, 128):
        d.rectangle([kx, sy - 1, kx + 9, sy + 7], fill=rgba('void'))
        d.rectangle([kx + 1, sy, kx + 8, sy + 6], fill=rgba('panel2'))
        vline(d, kx + 4, sy + 1, sy + 5, 'line')
        hline(d, kx + 2, kx + 6, sy + 3, 'line')
    # power: an LED plus a button with a lit top edge
    d.point((152, sy + 3), fill=rgba('glow_teal'))
    d.rectangle([160, sy, 172, sy + 6], fill=rgba('panel2'))
    d.rectangle([160, sy, 172, sy + 6], outline=rgba('void'))
    hline(d, 161, 171, sy + 1, 'line')
    # three status dots, the only warmth on the case
    for i, key in enumerate(('signal', 'signal', 'danger')):
        d.rectangle([196 + i * 6, sy + 2, 199 + i * 6, sy + 4], fill=rgba(key))

    # ---- moulded seam across the case, and a screw at each corner
    hline(d, 4, gx - 9, 12, 'panel2')
    hline(d, gx + gw + 8, Wm - 5, 12, 'panel2')
    for (sx, sy2) in ((7, 7), (Wm - 9, 7), (7, Hm - 12), (Wm - 9, Hm - 12)):
        d.point((sx, sy2), fill=rgba('line'))

    # ---- the tube's own rim: darker at the very edge, the way phosphor falls off
    for i in range(2):
        d.rectangle([gx + i, gy + i, gx + gw - 1 - i, gy + gh - 1 - i], outline=rgba('void'))
    return img


# ---------------------------------------------------------------------------------------- desk
def desk():
    """
    The desk the monitor stands on: coffee with smoke, keyboard, mouse, a pile of application forms.
    Everything sits to the RIGHT of the monitor (8..260), because a prop that is entirely hidden behind
    the monitor is a prop that does not exist.
    """
    img = Image.new('RGBA', (W, DESK_H), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    ty = DESK_TOP

    # ---- desk top: a slab with a lit top edge and a dark front face
    d.rectangle([0, ty, W - 1, ty + 30], fill=rgba('panel2'))
    d.line([0, ty, W - 1, ty], fill=rgba('line'))
    dither_band(d, 0, ty + 26, W - 1, ty + 28, 'panel2', 'panel', density=2)
    d.rectangle([0, ty + 31, W - 1, DESK_H - 1], fill=rgba('void'))
    d.line([0, ty + 31, W - 1, ty + 31], fill=rgba('panel'))
    # a drawer front and its handle: the front of a desk is furniture, not a void band
    d.rectangle([300, ty + 34, 460, ty + 44], fill=rgba('void'))
    d.rectangle([300, ty + 34, 460, ty + 44], outline=rgba('panel'))
    d.rectangle([368, ty + 37, 392, ty + 38], fill=rgba('panel'))
    for x in range(0, W, 8):
        d.point((x, ty + 32), fill=rgba('panel'))

    # ---- coffee: a tapered cup with a handle, a saucer, and three curls of steam (the one warmth)
    cx, cy = 274, ty + 4
    d.rectangle([cx - 9, cy + 4, cx + 9, cy + 20], fill=rgba('panel'))
    vline(d, cx - 9, cy + 4, cy + 20, 'linehi')
    vline(d, cx + 9, cy + 4, cy + 20, 'line')
    hline(d, cx - 9, cx + 9, cy + 20, 'void')
    round_rect(d, [cx - 11, cy, cx + 11, cy + 4], fill=rgba('panel2'), outline=rgba('line'), r=2)
    hline(d, cx - 6, cx + 6, cy + 2, 'ink')
    hline(d, cx - 2, cx + 2, cy + 2, 'orange')
    for i in range(1, 8):
        d.point((cx + 11 + (1 if i in (1, 7) else 0), cy + 7 + i), fill=rgba('linehi'))
    for (x0, height, tone) in ((cx - 4, 13, 'textdim'), (cx, 19, 'muted'), (cx + 4, 10, 'linehi')):
        for i in range(height):
            y = cy - 2 - i
            if y < 0:
                continue
            d.point((x0 + ((i // 5) % 2), y), fill=rgba(tone))

    # ---- keyboard: a wedge, four key rows, a spacebar
    kx, ky, kw, kh = 292, ty + 8, 92, 22
    round_rect(d, [kx, ky, kx + kw, ky + kh], fill=rgba('panel'), outline=rgba('void'), r=2)
    hline(d, kx + 1, kx + kw - 1, ky, 'line')
    for row in range(3):
        y = ky + 4 + row * 4
        for col in range(18):
            x = kx + 3 + col * 5
            if row == 2 and 3 <= col <= 13:
                continue
            d.point((x, y), fill=rgba('line'))
            d.point((x, y + 1), fill=rgba('panel2'))
    d.rectangle([kx + 18, ky + 16, kx + 70, ky + 18], fill=rgba('line'))
    d.rectangle([kx + 3, ky + 16, kx + 13, ky + 18], fill=rgba('panel2'))
    d.rectangle([kx + 76, ky + 16, kx + 89, ky + 18], fill=rgba('panel2'))

    # ---- mouse, right of the keyboard, with a 2 px cable
    round_rect(d, [390, ty + 8, 403, ty + 23], fill=rgba('panel'), outline=rgba('line'), r=4)
    hline(d, 392, 401, ty + 9, 'linehi')
    vline(d, 396, ty + 11, ty + 15, 'void')
    for i in range(5):
        d.point((398 + i // 3, ty + 6 - i // 2), fill=rgba('panel'))

    # ---- the pile of application forms: three sheets, black outline, a red stamp, a pen
    # A pile of paper is not a white box: the sheets under the top one are in shadow, so they are
    # `paperdim` and only the top sheet is `paper`. Three sheets, each 3 px off the one below.
    fx, fy = 412, ty + 3
    for i in (2, 1, 0):
        x, y = fx + i * 3, fy + i * 2
        top = (i == 0)
        d.rectangle([x, y, x + 56, y + 22], fill=rgba('paper' if top else 'paperdim'),
                    outline=rgba('void'))
        if top:
            for r in range(4):
                hline(d, x + 5, x + 51, y + 6 + r * 4, 'paperdim')
            d.point((x + 3, y + 4), fill=rgba('paperdim'))
            # the stamp: a small red mark, and the only red on the desk
            d.rectangle([x + 34, fy + 14, x + 50, fy + 19], outline=rgba('danger'))
            hline(d, x + 36, x + 48, fy + 16, 'danger')
        else:
            hline(d, x, x + 56, y + 22, 'ink')

    # The desk does NOT paint its own light: the ambience pass owns that.
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
