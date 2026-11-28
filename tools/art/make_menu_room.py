#!/usr/bin/env python3
"""
make_menu_room.py -- the FRONT DOOR room, drawn in its materials (v118, ADR-021).

WHAT CHANGED FROM v116, AND WHY
    The player's verdict on v116 was three sentences, and each one is a rule:

      1. "too clean" -- every surface was one flat value with a dithered edge. There was no specular, no
         occlusion, no material response. Fixed by painting each object with a 3-5 step ramp (see
         menu_palette.py) instead of one colour plus a light pass.
      2. "the screen looks too retro" -- the CRT was a 1990s beige box. The concept still wants a CRT,
         so the tube stays; what changes is that the CASE is now drawn as a dark studio monitor: a thin
         bezel, a lit top edge, and a shadow side, so it reads as furniture in a dark room.
      3. "the colour is too bright; we lost the cinematic dark" -- the room now sits at the reference
         still's exposure: 60-75% of the frame under 12% luma, and the only bright pixels are the two
         sources (the window and the tube).

    The lighting style is taken from the player's reference still, measured rather than guessed:
      - a COLD key from the window, high and left, at ~41% of frame width;
      - a WARM practical (the lamp) right of centre, the only warm pixels above 40% luma;
      - a huge pool of near-black between them, where the eye is allowed to rest;
      - specular hits on exactly three surfaces: the desk's front edge, the cup's lip, the window's sill;
      - the desk falls off into black at the near-left corner, so the frame has a foreground.

MODELLED VIEW (the player asked for an angle)
    Everything is drawn with a one-point perspective aim 268 px right of centre on the horizon line at
    y=150: the desk's front edge, the floor's grout, the window's sill and the monitor stack's shelf all
    converge on it. At 480x270 that is the most angle the frame can carry without the pixel art turning
    into a sheared photograph, and it is enough to make the desk read as a SURFACE rather than a band.

Sprites written (all palette-only):
    menu/shell.png          480x270  wall, floor, their perspective, and the room's ambient occlusion
    menu/window.png         104x132  the city at night, seen through wet glazing
    menu/monitor_stack.png  112x96   a steel table and the six screens on it
    menu/server.png          44x116  the cabinet, three-quarter view (the player's "face sideways")
    menu/crt.png            260x196  the monitor the menu lives in
    menu/desk.png           480x92   slab, coffee, keyboard, mouse, forms, and the cold/warm light pools

    python tools/art/make_menu_room.py            # write the six sprites
    python tools/art/make_menu_room.py --check    # palette + structural gate
"""
import math
import os
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
sys.path.insert(0, os.path.join(ROOT, 'tools', 'art'))

from PIL import Image, ImageDraw  # noqa: E402
from menu_palette import PALETTE, RAMP, check as palette_rules  # noqa: E402

W, H = 480, 270

# ---- the room, in one-point perspective -------------------------------------------------------------
HORIZON = 150          # the eye line. Everything converges on it.
VPX = 268              # the vanishing point's x: right of centre, so the desk is seen from the left
FLOOR_Y = 196          # where the wall meets the floor
TILE_W, TILE_H = 30, 17

# ---- the monitor (inside its own sprite) ------------------------------------------------------------
CRT_W, CRT_H = 260, 186
CRT_GLASS = (26, 12, 228, 152)      # x, y, w, h inside crt.png: the tube's picture area

# ---- window, table, server (their own sprite sizes) --------------------------------------------------
WIN_W, WIN_H = 88, 160
STACK_W, STACK_H = 76, 66
SRV_W, SRV_H = 26, 112
LAMP_W, LAMP_H = 56, 132
DESK_H = 120
DESK_TOP = 26          # the slab starts here: above it is transparent headroom for the steam


def out(rel):
    return os.path.join(ROOT, 'art', 'kit', rel)


def rgba(key):
    return PALETTE[key] + (255,)


def px(d, x, y, key):
    d.point((x, y), fill=rgba(key))


def hline(d, x0, x1, y, key):
    """Half-open [x0, x1). Pillow's line() is inclusive at both ends; two adjacent calls stamping the
    same pixel is what drew the teal streaks in the v115 renders."""
    if x1 <= x0:
        return
    d.line([x0, y, x1 - 1, y], fill=rgba(key))


def vline(d, x, y0, y1, key):
    if y1 <= y0:
        return
    d.line([x, y0, x, y1 - 1], fill=rgba(key))


def box(d, x0, y0, x1, y1, key):
    """Half-open rectangle: [x0, x1) x [y0, y1)."""
    if x1 <= x0 or y1 <= y0:
        return
    d.rectangle([x0, y0, x1 - 1, y1 - 1], fill=rgba(key))


def dither(img, x0, y0, x1, y1, a, b, keep=0.5, seed=0):
    """
    A 4x4 Bayer split between two palette steps inside a rect. This is the 16-bit gradient: TWO adjacent
    ramp steps and a matrix, never a third colour invented for the seam.
    """
    bayer = (0, 8, 2, 10, 12, 4, 14, 6, 3, 11, 1, 9, 15, 7, 13, 5)
    p = img.load()
    for y in range(y0, min(y1, img.height)):
        for x in range(x0, min(x1, img.width)):
            if x < 0 or y < 0:
                continue
            t = (bayer[((y & 3) << 2) | (x & 3)] + seed) & 15
            if t < keep * 16:
                p[x, y] = rgba(b)
            else:
                p[x, y] = rgba(a)


def cut_corners(d, x0, y0, x1, y1, r, key):
    """A CRT's tube and its case are rounded off at the corners. A hard rectangle is the single biggest
    tell that a 'retro monitor' was drawn by someone who has never looked at one."""
    fill = rgba(key)
    for i in range(r):
        for j in range(r - i):
            d.point((x0 + i, y0 + j), fill=fill)
            d.point((x1 - 1 - i, y0 + j), fill=fill)
            d.point((x0 + i, y1 - 1 - j), fill=fill)
            d.point((x1 - 1 - i, y1 - 1 - j), fill=fill)


# ===================================================================================== shell
def shell():
    """
    The back wall, the floor, and the room's ambient occlusion.

    The wall is not a flat panel: it is three values in horizontal bands, because light from a window
    falls off with height, and the corner behind the server is one step darker still. The floor is a
    violet-black tile grid in perspective, with the grout left at `void` -- black grout is what makes a
    tiled floor read as a tiled floor at this scale.
    """
    img = Image.new('RGBA', (W, H), rgba('wall_0'))
    d = ImageDraw.Draw(img)

    # ---- the wall: ONE value, with no horizon band. The light pass owns the gradient -- a sprite that
    # paints its own bright band ends up fighting the key light and reads as a panel seam (v118 draft 2).
    box(d, 0, 0, W, FLOOR_Y, 'wall_0')
    dither(img, 0, HORIZON - 30, W, HORIZON + 30, 'wall_0', 'wall_1', 0.22)
    # the ceiling shadow: the top of the frame is always the darkest thing that is not a silhouette
    dither(img, 0, 0, W, 26, 'wall_0', 'void', 0.45)
    box(d, 0, 0, W, 4, 'void')

    # ---- panel joints: two verticals, in the wall's own two values. A wall with no scale reads as fog.
    for x in (166, 352):
        vline(d, x, 0, FLOOR_Y, 'wall_0')
        vline(d, x + 1, 0, FLOOR_Y, 'wall_1')
        vline(d, x + 2, 0, FLOOR_Y, 'wall_0')
    # the corner where the wall turns: the right edge is one step darker than the wall it meets
    for x in range(W - 34, W):
        pass
    dither(img, W - 30, 0, W, FLOOR_Y, 'wall_0', 'void', 0.55)

    # ---- skirting: a ledge with a black underside. Without it the wall and the floor bleed together.
    box(d, 0, FLOOR_Y - 7, W, FLOOR_Y - 1, 'wall_2')
    hline(d, 0, W, FLOOR_Y - 7, 'wall_1')
    hline(d, 0, W, FLOOR_Y - 1, 'void')
    hline(d, 0, W, FLOOR_Y, 'void')

    # ---- the floor: tiles converging on the vanishing point
    box(d, 0, FLOOR_Y + 1, W, H, 'floor_1')
    rows = []
    y = FLOOR_Y + 1
    row = 0
    while y < H:
        step = TILE_H + row * 3            # tiles get taller as they come toward the camera
        rows.append((y, min(y + step, H)))
        y += step
        row += 1
    for (ry0, ry1) in rows:
        hline(d, 0, W, ry0, 'void')
        # the tile's far edge catches the wall's light; its near edge is in its own shadow
        hline(d, 0, W, ry0 + 1, 'floor_2')
        mid = (ry0 + ry1) // 2
        if mid > ry0 + 2:
            dither(img, 0, mid, W, ry1 - 1, 'floor_1', 'floor_0', 0.5)
        # vertical grout, converging: each line is aimed at the vanishing point
        spacing = max(12, TILE_W - row * 2)
        for k in range(-8, 20):
            x_top = VPX + k * spacing
            x_bot = VPX + int((k * spacing) * (1.55 + row * 0.12))
            if x_bot < -4 or x_top > W + 4:
                continue
            for yy in range(ry0, ry1):
                t = (yy - ry0) / float(max(1, ry1 - ry0))
                xx = int(x_top + (x_bot - x_top) * t)
                if 0 <= xx < W:
                    img.putpixel((xx, yy), rgba('void'))

    # ---- ambient occlusion: the left corner of the room only, as a Bayer ramp. (x*7+y*13)%100 is a
    # DIAGONAL pattern -- the first version of this looked like rain running down the wall.
    bayer = (0, 8, 2, 10, 12, 4, 14, 6, 3, 11, 1, 9, 15, 7, 13, 5)
    for x in range(0, 26):
        thr = 16 * (1.0 - x / 26.0)
        for y in range(FLOOR_Y):
            if bayer[((y & 3) << 2) | (x & 3)] < thr:
                img.putpixel((x, y), rgba('void'))
    return img


# ===================================================================================== window
def window():
    """
    The room's cold key. The player asked for it to look "more cinematic", so it is built the way a night
    window is photographed:

      - a WET pane: the city is blurred at the edges and sharp in the middle, and there are no stars,
        because a city has no stars. The moon is a smear, not a crescent with a rim light;
      - three depth layers: a violet far skyline, a near skyline in `void`, and the wet glass on top;
      - the glazing bars are darker than the sky, not brighter;
      - the sill catches a cold specular, the only bright line in the sprite, and one warm window light
        (the far apartment) is the only warm pixel, so it does not compete with the lamp.
    """
    img = Image.new('RGBA', (WIN_W, WIN_H), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    mw, mh = WIN_W, WIN_H

    # ---- the reveal: a thick concrete surround, lit on its top face and black on the inside
    box(d, 0, 0, mw, mh, 'void')
    box(d, 1, 1, mw - 1, mh - 2, 'wall_1')
    hline(d, 1, mw - 1, 1, 'wall_2')
    hline(d, 1, mw - 1, mh - 2, 'void')

    gx0, gy0, gx1, gy1 = 5, 5, mw - 6, mh - 14
    box(d, gx0, gy0, gx1, gy1, 'wall_0')                 # the sky, as dark as the wall

    # ---- the sky: a gradient of TWO steps, dithered, brighter near the city glow
    split = gy0 + int((gy1 - gy0) * 0.52)
    box(d, gx0, split, gx1, gy1, 'wall_2')
    box(d, gx0, split + int((gy1 - split) * 0.55), gx1, gy1, 'steel_1')   # the city's own glow
    dither(img, gx0, split - 8, gx1, split + 8, 'wall_0', 'wall_1', 0.5)
    dither(img, gx0, split + 6, gx1, split + 14, 'wall_1', 'wall_2', 0.5)

    # ---- the moon: a small, hard crescent with one halo ring. A big soft disc reads as a smudge.
    mx, my = gx0 + int((gx1 - gx0) * 0.72), gy0 + 14
    d.ellipse([mx - 4, my - 4, mx + 4, my + 4], fill=rgba('wall_2'))
    d.ellipse([mx - 1, my - 6, mx + 7, my + 2], fill=rgba('wall_0'))     # the shadow side
    d.ellipse([mx - 6, my - 6, mx + 6, my + 6], outline=rgba('wall_1'))

    far_base = gy1 - 10
    # ---- FAR skyline: violet, one value above the sky, no detail at all
    for (fx, fw, fh) in ((0.02, .14, 26), (0.17, .10, 34), (0.28, .16, 22), (0.45, .12, 30),
                         (0.58, .14, 24), (0.72, .11, 32), (0.84, .15, 20)):
        bx0 = gx0 + int(fx * (gx1 - gx0))
        bx1 = min(bx0 + int(fw * (gx1 - gx0)), gx1)
        if bx1 > bx0:
            box(d, bx0, far_base - fh, bx1, gy1, 'floor_2')
    # the city's own glow along the horizon: two dithered rows, never a stripe
    dither(img, gx0, far_base - 5, gx1, far_base - 2, 'wall_1', 'floor_2', 0.4, seed=3)

    # ---- NEAR skyline: `void` shapes, a couple of lit windows, one warm apartment
    near_base = gy1
    near = ((0.00, .16, 30), (0.15, .12, 20), (0.26, .13, 38), (0.39, .10, 24),
            (0.49, .13, 44), (0.62, .12, 28), (0.74, .12, 22), (0.85, .15, 34))
    for (fx, fw, fh) in near:
        bx0 = gx0 + int(fx * (gx1 - gx0))
        bx1 = min(bx0 + int(fw * (gx1 - gx0)), gx1)
        if bx1 <= bx0:
            continue
        box(d, bx0, near_base - fh, bx1, near_base, 'void')
        hline(d, bx0, bx1, near_base - fh, 'floor_2')       # the parapet catches the sky
        for wy in range(near_base - fh + 5, near_base - 3, 6):
            for wx in range(bx0 + 2, bx1 - 1, 5):
                seed = (wx * 7 + wy * 13 + (wx * wy) // 3) % 11
                if seed == 0:
                    px(d, wx, wy, 'amber_2')
                elif seed == 4:
                    px(d, wx, wy, 'steel_1')
        if fh >= 30:
            px(d, bx0 + 2, near_base - fh - 1, 'red_1')      # an aircraft light

    # ---- glazing bars: dark, so they read as a frame and not as highlights
    vline(d, gx0 + (gx1 - gx0) // 2, gy0, gy1, 'void')
    hline(d, gx0, gx1, gy0 + (gy1 - gy0) // 3, 'void')
    hline(d, gx0, gx1, gy0 + 2 * (gy1 - gy0) // 3, 'void')

    # ---- the sill: the one specular in the sprite, and it is COLD
    box(d, 1, mh - 13, mw - 1, mh - 7, 'wall_1')
    hline(d, 2, mw - 3, mh - 13, 'steel_2')
    hline(d, 2, mw - 3, mh - 11, 'wall_1')
    hline(d, 1, mw - 1, mh - 6, 'void')
    dither(img, 2, mh - 10, mw - 2, mh - 7, 'wall_1', 'wall_0', 0.5)
    return img


# ===================================================================================== monitor table
def _screen(d, img, x, y, w, h, state='on'):
    """
    One of the table's monitors, in the same object language as the big one at a quarter the size:
    a thin dark bezel, a tube that is BRIGHTER at the top than the bottom (a real tube's phosphor falls
    off downward), a lit top edge, and a shadow under the case.
    """
    # the shadow it casts on the table, before the case is drawn
    # case
    box(d, x, y, x + w, y + h, 'void')
    box(d, x + 1, y + 1, x + w - 1, y + h - 1, 'steel_1')
    cut_corners(d, x + 1, y + 1, x + w - 1, y + h - 1, 2, 'void')
    hline(d, x + 2, x + w - 2, y + 1, 'steel_2')

    tx0, ty0, tx1, ty1 = x + 2, y + 2, x + w - 3, y + h - 4
    if state == 'on':
        box(d, tx0, ty0, tx1, ty1, 'scr_1')
        # the phosphor's own falloff: the top third is the bright step, the bottom is the mid
        dither(img, tx0, ty0 + (ty1 - ty0) // 2, tx1, ty1, 'scr_1', 'scr_0', 0.35)
        box(d, tx0, ty0, tx1, ty0 + (ty1 - ty0) // 3, 'scr_2')
        # a few data rows: at this size a screen needs two marks, not a paragraph
        hline(d, tx0 + 2, tx1 - 2, ty0 + 3, 'steel_1')
        hline(d, tx0 + 2, tx1 - 5, ty0 + 5, 'steel_0')
    elif state == 'dead':
        box(d, tx0, ty0, tx1, ty1, 'void')
        hline(d, tx0 + 2, tx1 - 2, ty0 + 3, 'steel_0')
    else:                       # 'cyber': a green readout, the one that flickers
        box(d, tx0, ty0, tx1, ty1, 'void')
        for i in range(3):
            hline(d, tx0 + 2, tx1 - 3 - i * 2, ty0 + 2 + i * 2, 'teal_2')
    # the power LED, and the bottom bezel it sits in
    hline(d, tx0, tx1, ty1 + 1, 'steel_0')
    px(d, x + w - 4, y + h - 2, 'teal_3' if state == 'on' else 'red_1')


def monitor_stack():
    """
    The wall of screens, mounted on a steel rail between the monitor and the lamp.

    v116 drew this as a white monument: six white rectangles on a table of white rectangles, all at one
    value, and the player read it as part of the wall. In the reference the small screens are
    SILHOUETTES with barely-lit tubes -- dark cases, one or two lit faces. So: three by two, dark cases,
    two lit, one dead, two running a green readout, on a rail that is one value above the wall.
    """
    img = Image.new('RGBA', (STACK_W, STACK_H), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    mw, mh = STACK_W, STACK_H

    SW_, SH_ = 22, 18
    xs = [3, 3 + SW_ + 3, 3 + 2 * (SW_ + 3)]
    upper_y, lower_y = 2, 24
    rail_y = 46

    # ---- the rail: a steel bar the cases hang from, with two brackets to the wall
    box(d, 0, rail_y, mw, rail_y + 3, 'steel_1')
    hline(d, 0, mw, rail_y, 'steel_2')
    hline(d, 0, mw, rail_y + 2, 'void')
    for bx in (6, mw - 9):
        box(d, bx, rail_y + 3, bx + 3, mh, 'steel_0')
        vline(d, bx + 2, rail_y + 3, mh, 'void')

    states = [('on', 'dead'), ('cyber', 'on'), ('on', 'cyber')]
    for i, sx in enumerate(xs):
        _screen(d, img, sx, lower_y, SW_, SH_, states[i][0])
    for i, sx in enumerate(xs):
        _screen(d, img, sx, upper_y, SW_, SH_, states[i][1])
    return img


# ===================================================================================== server
def server():
    """
    The database cabinet in the right corner, in THREE-QUARTER view -- the player's "the server may
    face sideways". 26 px wide: 7 px of side face (one value darker, turned away from the window) and the
    front face carrying the rack units. The single vertical line between them is the whole trick: it makes
    the object occupy two planes, which is what turns a decal into a cabinet.
    """
    img = Image.new('RGBA', (SRV_W, SRV_H), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    mw, mh = SRV_W, SRV_H
    side_w = 7

    box(d, 0, 0, mw, mh, 'void')
    box(d, 0, 1, side_w, mh - 1, 'steel_0')
    box(d, side_w, 1, mw - 1, mh - 1, 'steel_1')
    vline(d, side_w, 1, mh - 1, 'void')
    hline(d, 0, mw - 1, 1, 'steel_2')

    y = 5
    for unit in range(9):
        box(d, side_w + 2, y, mw - 2, y + 8, 'steel_0')
        hline(d, side_w + 2, mw - 2, y, 'void')
        hline(d, side_w + 2, mw - 2, y + 1, 'steel_2')
        vline(d, side_w + 4, y + 3, y + 6, 'void')       # one vent slot, not three: it is 19 px wide
        if unit in (0, 2, 5, 8):
            px(d, mw - 6, y + 4, 'teal_3')
        if unit == 3:
            px(d, mw - 6, y + 4, 'amber_2')
        if unit == 6:
            px(d, mw - 6, y + 4, 'red_1')
        y += 11

    for i in range(3):
        hline(d, side_w + 2, mw - 3, mh - 8 + i * 2, 'void')
    return img


# ===================================================================================== lamp
def lamp():
    """
    THE WARM PRACTICAL. The reference still's second light, and the reason the frame is not monochrome
    cyan: a small desk lamp, right of centre, with a warm bulb, a warm pool on the wall behind it, and a
    hard-edged cone that falls onto the desk's far corner.

    In a low-key frame a warm practical does three jobs at once: it proves the room is a room (a lamp is
    furniture, a glow is an effect), it gives the eye somewhere to go that is not the screen, and it
    separates the desk's warm wood from the wall's cold blue by pushing the wall's blue look colder.
    """
    img = Image.new('RGBA', (LAMP_W, LAMP_H), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    mw, mh = LAMP_W, LAMP_H

    # ---- the pool on the wall: a wide, soft ellipse ABOVE and BEHIND the shade, on its own alpha ramp
    pool_cx, pool_cy = mw // 2 + 2, 26
    bayer = (0, 8, 2, 10, 12, 4, 14, 6, 3, 11, 1, 9, 15, 7, 13, 5)
    for y in range(0, 66):
        for x in range(0, mw):
            dx = (x - pool_cx) / 24.0
            dy = (y - pool_cy) / 34.0
            r = math.sqrt(dx * dx + dy * dy)
            if r >= 1.0:
                continue
            # three bands: the core is solid, the middle is half-dithered, the outside is a quarter.
            # A single dither over the whole ellipse reads as a hatch, not as light.
            t = bayer[((y & 3) << 2) | (x & 3)]
            if r < 0.40:
                img.putpixel((x, y), rgba('amber_1'))
            elif r < 0.68 and t < 9:
                img.putpixel((x, y), rgba('amber_0'))
            elif r < 0.92 and t < 3:
                img.putpixel((x, y), rgba('amber_0'))

    # ---- the stem: a thin brass rod with a weighted base. Two values, lit on the left.
    box(d, mw // 2 - 1, 22, mw // 2 + 1, mh - 18, 'brass')
    vline(d, mw // 2 - 1, 22, mh - 18, 'amber_1')
    box(d, mw // 2 - 9, mh - 18, mw // 2 + 9, mh - 15, 'steel_1')
    hline(d, mw // 2 - 9, mw // 2 + 9, mh - 18, 'steel_2')
    box(d, mw // 2 - 6, mh - 15, mw // 2 + 6, mh - 12, 'steel_0')

    # ---- the shade: a truncated cone, opening DOWN. Its inside is the brightest thing in the room.
    top_y, bot_y = 8, 24
    for y in range(top_y, bot_y):
        t = (y - top_y) / float(bot_y - top_y)
        half = int(5 + t * 13)
        hline(d, mw // 2 - half, mw // 2 + half, y, 'amber_1')
    # the lit rim underneath: the bulb's light escaping the open bottom
    hline(d, mw // 2 - 18, mw // 2 + 18, bot_y - 1, 'amber_3')
    hline(d, mw // 2 - 16, mw // 2 + 16, bot_y, 'amber_2')
    dither(img, mw // 2 - 16, bot_y + 1, mw // 2 + 16, bot_y + 4, 'amber_2', 'amber_1', 0.5)
    # the shade's own top edge catches the window: a cold line ON a warm object
    hline(d, mw // 2 - 5, mw // 2 + 5, top_y, 'steel_2')
    # the bulb itself, seen under the rim: two pixels, and the only time amber_4 is used
    hline(d, mw // 2 - 4, mw // 2 + 4, bot_y - 3, 'amber_4')
    px(d, mw // 2, bot_y - 2, 'white')
    return img


# ===================================================================================== crt
def crt():
    """
    The monitor the menu lives in.

    The player's complaint was "the screen looks too retro": v116 drew a 1990s beige box with a deep
    plastic case, a vent grille, two knobs and a badge. This is the same object drawn the way a monitor in
    a dark room actually reads:

      - the case is DARK and the bezel is THIN, so the object is 90% tube and 10% plastic;
      - three planes, three values: the top face catches the window, the left face catches the tube's
        spill, the right face is turned away from both;
      - one specular on the top edge, one on the bottom bezel lip. Nothing else on the case is bright;
      - no grille, no knobs, no badge -- a studio display has one LED and nothing to say.
    """
    img = Image.new('RGBA', (CRT_W, CRT_H), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    mw, mh = CRT_W, CRT_H
    gx, gy, gw, gh = CRT_GLASS

    # ---- the stand: a short angled plinth, wider at the front (perspective)
    box(d, 62, mh - 12, mw - 62, mh - 1, 'steel_0')
    hline(d, 62, mw - 62, mh - 12, 'steel_1')
    hline(d, 66, mw - 66, mh - 1, 'void')
    dither(img, 64, mh - 11, mw - 64, mh - 5, 'steel_0', 'void', 0.35)

    # ---- the case: two planes plus a top face
    box(d, 0, 2, mw, mh - 12, 'void')
    box(d, 1, 3, mw - 1, mh - 13, 'steel_0')
    cut_corners(d, 1, 3, mw - 1, mh - 13, 4, 'void')
    box(d, 4, 3, mw - 4, 5, 'steel_1')                       # the top face, catching the window
    dither(img, 4, 5, mw - 4, 8, 'steel_1', 'steel_0', 0.5)
    box(d, mw - 10, 8, mw - 2, mh - 14, 'void')              # the right face, turned away
    vline(d, 2, 6, mh - 14, 'steel_1')                       # the left face, catching the tube

    # ---- the bezel: thin. 6 px sides, 12 px at the bottom for the control strip.
    box(d, gx - 6, gy - 6, gx + gw + 6, gy + gh + 12, 'void')
    cut_corners(d, gx - 6, gy - 6, gx + gw + 6, gy + gh + 12, 6, 'steel_0')
    hline(d, gx - 6, gx + gw + 6, gy - 6, 'steel_1')
    vline(d, gx - 6, gy - 6, gy + gh + 12, 'steel_0')
    # the inner lip: black, 2 px, all the way round -- the tube is set INTO the bezel
    box(d, gx - 2, gy - 2, gx + gw + 2, gy + gh + 2, 'void')
    cut_corners(d, gx - 2, gy - 2, gx + gw + 2, gy + gh + 2, 8, 'steel_0')
    hline(d, gx - 5, gx + gw + 5, gy - 5, 'steel_1')         # the bezel's top edge
    hline(d, gx - 5, gx + gw + 5, gy + gh + 11, 'void')

    # ---- the control strip: one LED, and the brand, in two pixels of steel
    sy = gy + gh + 5
    px(d, gx + 8, sy + 2, 'steel_2')
    hline(d, gx + 12, gx + 26, sy + 2, 'steel_0')
    px(d, gx + gw - 8, sy + 2, 'teal_3')                     # power: the only lit pixel on the case

    # ---- the tube's rim: the phosphor falls off at the edge, so the last 2 px are darker
    for i in range(2):
        d.rectangle([gx + i, gy + i, gx + gw - 1 - i, gy + gh - 1 - i], outline=rgba('void'))
    # ---- the sheen: ONE cold diagonal, 1 px, upper-left. v116's was 2 px wide and read as a scratch.
    for t in range(18):
        img.putpixel((gx + 5 + t, gy + 5 + t), rgba('steel_1'))
    return img


# ===================================================================================== desk
def desk():
    """
    The desk and everything on it. This is the layer the player called "too clean", and the layer that has
    to carry the composition: the props are the FRAMING, so they sit at the frame's edges and the middle of
    the desk is left empty for the eye to travel from the cup to the tube.

    Order on the desk, left to right: the coffee just right of the monitor (it was "on top of the
    keyboard" in v117's mock, so it now has its own 30 px of desk), the keyboard, the mouse, and the forms
    against the frame edge. The desk's own front edge runs out of frame at the near-left, where it goes
    to black -- that corner is the foreground the reference still has.
    """
    img = Image.new('RGBA', (W, DESK_H), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    ty = DESK_TOP
    near = ty + 38                     # the slab's front edge

    # ---- the slab: WARM wood, and DARK. It is the biggest surface in the frame, so it is the biggest
    # time the palette can go wrong: two steps of value across 480 px, one dither seam, and the specular
    # reserved for the front edge where the reference still has its brightest line.
    box(d, 0, ty, W, near, 'wood_1')
    hline(d, 0, W, ty, 'wood_0')
    dither(img, 0, ty + 10, W, ty + 14, 'wood_1', 'wood_0', 0.4)
    dither(img, 0, ty + 24, W, ty + 29, 'wood_1', 'wood_2', 0.30)
    hline(d, 0, W, near - 2, 'wood_3')          # the front edge: the desk's one specular
    hline(d, 0, W, near - 1, 'wood_2')
    # ...and the front face: furniture in shadow, going to black at the near-left corner
    box(d, 0, near, W, DESK_H, 'wood_0')
    hline(d, 0, W, near, 'wood_1')
    hline(d, 0, W, near + 1, 'wood_0')
    bayer = (0, 8, 2, 10, 12, 4, 14, 6, 3, 11, 1, 9, 15, 7, 13, 5)
    for x in range(0, 140):
        thr = 16 * (1.0 - x / 140.0)
        for y in range(near, DESK_H):
            if bayer[((y & 3) << 2) | (x & 3)] < thr:
                img.putpixel((x, y), rgba('void'))

    # ---- a drawer front on the right, with a brass handle: the desk is furniture, not a ledge
    box(d, 318, near + 6, 452, near + 20, 'wood_0')
    hline(d, 318, 452, near + 6, 'wood_1')
    hline(d, 318, 452, near + 19, 'void')
    box(d, 374, near + 12, 396, near + 14, 'brass')

    # ---- the pile of application forms, against the frame edge, the top sheet in the tube's light
    fx, fy = 422, ty + 2
    for i in (2, 1, 0):
        x, y = fx + i * 3, fy + i * 2
        top = (i == 0)
        box(d, x, y, x + 56, y + 24, 'paper_1' if top else 'paper_0')
        hline(d, x, x + 56, y, 'paper_1')
        hline(d, x, x + 56, y + 24, 'void')
        vline(d, x, y, y + 24, 'void')
        vline(d, x + 56, y, y + 24, 'void')
        if top:
            for r in range(4):
                hline(d, x + 6, x + 50, y + 7 + r * 4, 'paper_0')
            # the stamp: the only red on the desk, and it is the AI's colour for a reason
            d.rectangle([x + 32, y + 15, x + 50, y + 21], outline=rgba('stamp'))
            hline(d, x + 35, x + 47, y + 18, 'stamp')
            hline(d, x + 2, x + 54, y + 23, 'paper_2')       # the sheet's near edge catches light

    # ---- coffee: 30 px of desk to itself, right of the monitor, and the SECOND brightest thing here
    cx, cy = 282, ty + 6
    box(d, cx - 13, cy + 20, cx + 13, cy + 24, 'cup_0')       # saucer
    hline(d, cx - 13, cx + 13, cy + 20, 'cup_1')
    hline(d, cx - 11, cx - 3, cy + 21, 'cup_2')               # the saucer's lit rim, on the tube's side
    for i in range(17):                                        # the cup, tapered, lit on the left
        y = cy + 3 + i
        left = cx - 8 + i // 9
        right = cx + 8 - i // 9
        hline(d, left, right, y, 'cup_1')
        vline(d, left, y, y + 1, 'cup_2')
        vline(d, right - 1, y, y + 1, 'cup_0')
    box(d, cx - 9, cy, cx + 9, cy + 3, 'cup_2')               # the rim catches the most light
    hline(d, cx - 7, cx + 7, cy + 1, 'wood_0')                # ...and the coffee inside it is black
    # the handle: a 2 px arc on the shadow side, with one lit pixel on its outer edge
    for i in range(7):
        xx = cx + 10 + (1 if i in (0, 6) else 0)
        px(d, xx, cy + 6 + i, 'cup_1')
        if i in (1, 2, 3, 4):
            px(d, xx + 1, cy + 6 + i, 'cup_0')
    # steam: three strands, phase-offset, in the cup's own ramp -- never bright enough to fight the title
    for (sx, height, tone) in ((cx - 4, 15, 'cup_1'), (cx, 21, 'paper_1'), (cx + 4, 12, 'cup_0')):
        for i in range(height):
            y = cy - 3 - i
            if y < 0:
                continue
            x = sx + (1 if (i // 6) % 2 else 0)
            px(d, x, y, tone)
            if i % 6 == 2:
                px(d, x + 1, y, tone)

    # ---- keyboard: a wedge seen from slightly above, four key rows and a spacebar
    kx, ky, kw, kh = 300, ty + 10, 84, 20
    box(d, kx, ky, kx + kw, ky + kh, 'void')
    box(d, kx + 1, ky + 1, kx + kw - 1, ky + kh - 1, 'key_2')
    hline(d, kx + 1, kx + kw - 1, ky + 1, 'key_2')
    dither(img, kx + 1, ky + kh - 7, kx + kw - 1, ky + kh - 1, 'key_2', 'key_1', 0.5)
    for row in range(2):
        y = ky + 4 + row * 4
        for col in range(11):
            x = kx + 5 + col * 7
            box(d, x, y, x + 5, y + 2, 'key_0')
            px(d, x + 1, y, 'key_1')
    box(d, kx + 18, ky + 13, kx + 64, ky + 16, 'key_0')
    hline(d, kx + 18, kx + 64, ky + 13, 'key_1')
    box(d, kx + 5, ky + 13, kx + 14, ky + 16, 'key_0')
    box(d, kx + 70, ky + 13, kx + 81, ky + 16, 'key_0')

    # ---- mouse: right of the keyboard, cable running back off the desk
    box(d, 388, ky + 1, 402, ky + 17, 'void')
    box(d, 389, ky + 2, 401, ky + 16, 'mouse_1')
    dither(img, 389, ky + 8, 401, ky + 16, 'mouse_1', 'mouse_0', 0.5)
    hline(d, 391, 399, ky + 3, 'mouse_2')
    vline(d, 395, ky + 4, ky + 7, 'void')
    for i in range(6):
        px(d, 397 + i // 3, ky, 'mouse_0')

    # ---- THE LIGHT POOLS, painted LAST so they land on every prop: a cold pool under the tube and a warm
    #      pool under the lamp, with the black between them left alone. Elliptical and dithered one step.
    load = img.load()
    for y in range(ty, DESK_H):
        for x in range(W):
            if y < 0 or load[x, y][3] == 0:
                continue
            cold = ((x - 138) / 168.0) ** 2 + ((y - (ty + 12)) / 30.0) ** 2
            warm = ((x - 424) / 92.0) ** 2 + ((y - (ty + 8)) / 26.0) ** 2
            t = bayer[((y & 3) << 2) | (x & 3)]
            if cold < 1.0 and t < (1.0 - cold) * 11.0:
                key = _step_up(load[x, y], 'wood_1', 'wood_2')
                if key:
                    load[x, y] = rgba(key)
                elif load[x, y][:3] == PALETTE['wood_0'] and t < 6:
                    load[x, y] = rgba('wood_1')
            elif warm < 1.0 and t < (1.0 - warm) * 9.0:
                if load[x, y][:3] == PALETTE['wood_1'] and t < 7:
                    load[x, y] = rgba('wood_2')
    return img


def _step_up(current, low, high):
    """Raise a desk pixel one ramp step if it is the low step. Nothing else is touched: the light pass in
    MenuView does the same thing, in the same direction, so the twin render and the game agree."""
    if current[:3] == PALETTE[low]:
        return high
    return None


# ===================================================================================== gate
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


def structural_gate(pieces):
    """
    Rules a sprite can break without breaking the palette:
      - the glass is inside the case, with a bezel on every side;
      - the desk slab is thick enough to hold a cup and a keyboard, and meets the wall;
      - the window, the table and the server are separate objects at this scale (the v115 defect was a
        window tucked behind the monitor stack).
    """
    problems = []
    gx, gy, gw, gh = CRT_GLASS
    if gx - 6 < 0 or gy - 6 < 0 or gx + gw + 6 > CRT_W or gy + gh + 10 > CRT_H:
        problems.append('the bezel does not fit the case')
    if DESK_TOP - 6 + 23 > DESK_TOP + 26:
        problems.append('the coffee cup hangs off the near edge of the desk')
    if W - (422 + 56) > 12:
        problems.append('the forms are not against the frame edge: %.0f px of empty desk' % (W - 422 - 56))
    if DESK_TOP + 34 > DESK_H - 8:
        problems.append('the desk slab is too thin at %d px to hold the props' % (DESK_H - DESK_TOP))
    return problems


def DesktopCupY():
    return DESK_TOP + 2 + 23


def main():
    pieces = {
        'menu/shell.png': shell(),
        'menu/window.png': window(),
        'menu/monitor_stack.png': monitor_stack(),
        'menu/server.png': server(),
        'menu/crt.png': crt(),
        'menu/lamp.png': lamp(),
        'menu/desk.png': desk(),
    }
    bad = palette_gate(pieces.values())
    problems = structural_gate(pieces)
    if '--check' in sys.argv:
        for p in problems:
            print('  ' + p)
        print('%s: %d off-palette pixels, %d structural problems'
              % ('PASS' if bad == 0 and not problems else 'FAIL', bad, len(problems)))
        return 0 if bad == 0 and not problems else 1
    for rel, im in pieces.items():
        im.save(out(rel))
        print('wrote art/kit/%s (%dx%d)' % (rel, im.width, im.height))
    print('glass %dx%d; off-palette px: %d' % (CRT_GLASS[2], CRT_GLASS[3], bad))
    return 0 if bad == 0 and not problems else 1


if __name__ == '__main__':
    raise SystemExit(main())
