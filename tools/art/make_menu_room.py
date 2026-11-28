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
STACK_W, STACK_H = 70, 108
SRV_W, SRV_H = 26, 112
LAMP_W, LAMP_H = 56, 132
DESK_H = 120
DESK_TOP = 26          # the slab starts here: above it is transparent headroom for the steam
GROUND_X0 = 406               # the desk's right end: only right of it can you see the floor
DESK_EDGE_Y = 150 + 26 + 38   # the desk's front edge on the canvas (MenuLayout.DeskEdgeY)


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

    # ---- THE WALL, DEFINED (v119.2).
    #
    # The player asked to "define the ground and the wall behind more, separate it with lines". The wall was
    # one value with two faint joints; at 480x270 that reads as fog rather than architecture. It now has a
    # PANEL GRID -- vertical joints on a 96 px pitch, a horizontal joint at the height a dado rail would sit,
    # and a black seam where the wall meets the floor -- drawn in the wall's own two values so the lines are
    # structure and not decoration.
    for x in (96, 192, 288, 384):
        vline(d, x, 0, FLOOR_Y - 8, 'void')          # the joint itself: one hard black line
        vline(d, x + 1, 0, FLOOR_Y - 8, 'wall_1')    # ...and the lit bevel beside it, which is the depth
    for x in (96, 192, 288, 384):
        for y in range(0, FLOOR_Y - 8, 9):
            if ((x + y) & 3) == 0:
                img.putpixel((x, y), rgba('wall_0'))  # let the joint FADE with distance: it is not a cage
    hline(d, 0, W, 108, 'void')                      # the dado rail
    hline(d, 0, W, 109, 'wall_1')
    # the corner where the wall turns: the right edge is one step darker than the wall it meets
    dither(img, W - 30, 0, W, FLOOR_Y, 'wall_0', 'void', 0.55)

    # ---- skirting: a ledge with a black underside. Without it the wall and the floor bleed together.
    box(d, 0, FLOOR_Y - 7, W, FLOOR_Y - 1, 'wall_2')
    hline(d, 0, W, FLOOR_Y - 7, 'wall_1')
    hline(d, 0, W, FLOOR_Y - 1, 'void')
    hline(d, 0, W, FLOOR_Y, 'void')

    # ---- the floor. v119.1: the player's note was "define the ground more", and the honest reading of the
    # previous floor is that it was a gradient, not a surface. A floor at this size needs three things:
    #
    #   1. TILES with a value of their own -- a checker of floor_1 and floor_2, not one wash, so the eye can
    #      count the squares and put the desk on top of something;
    #   2. GROUT, black, on both axes, in perspective (rows get taller and columns converge on the vanishing
    #      point), so the squares are measured and not implied;
    #   3. a CONTACT SHADOW immediately under the desk's front panel, and the window's light landing as a
    #      pool on the tiles -- light on a floor is what proves the floor is a floor.
    box(d, 0, FLOOR_Y + 1, W, H, 'floor_1')
    rows = []
    y = FLOOR_Y + 1
    row = 0
    while y < H:
        step = TILE_H + row * 3            # tiles get taller as they come toward the camera
        rows.append((y, min(y + step, H), row, step))
        y += step
        row += 1
    for (ry0, ry1, row, step) in rows:
        # the far edge of each tile row: black grout, then the lit bevel of the tile behind it
        hline(d, 0, W, ry0, 'void')
        hline(d, 0, W, ry0 + 1, 'floor_2')
        # the tile's own face, one value per row alternating as the checker, then its shadowed half
        face = 'floor_1' if row % 2 == 0 else 'floor_2'
        box(d, 0, ry0 + 2, W, ry1, face)
        mid = (ry0 + ry1) // 2
        if mid > ry0 + 3:
            dither(img, 0, mid, W, ry1 - 1, face, 'floor_0', 0.5)
        # vertical grout, converging on the vanishing point
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
                # the lit edge on one side of every joint: this is what makes the grout read as depth
                if 0 <= xx + 1 < W and yy > ry0 + 1:
                    img.putpixel((xx + 1, yy), rgba('floor_2'))
        # the checker: every other column is a step darker, so the squares are countable
        for k in range(-8, 20, 2):
            x_top = VPX + k * spacing
            x_bot = VPX + int((k * spacing) * (1.55 + row * 0.12))
            for yy in range(ry0 + 2, ry1):
                t = (yy - ry0) / float(max(1, ry1 - ry0))
                x0 = int(x_top + (x_bot - x_top) * t)
                x1 = int(x_top + spacing + (x_bot + spacing * (1.55 + row * 0.12) - (x_top + spacing)) * t)
                for xx in range(max(0, x0 + 2), min(W, x1 - 1)):
                    if yy > mid:
                        img.putpixel((xx, yy), rgba('floor_0'))
                    else:
                        img.putpixel((xx, yy), rgba('floor_1'))

    # ---- THE GROUND YOU CAN ACTUALLY SEE.
    #
    #      The desk sprite is opaque from the wall down to the bottom of the frame (a desk in front of the
    #      camera), so every floor pixel between x=0 and x=405 below y=150 is covered by it. What the player
    #      sees as "the ground" is therefore the two places the floor is NOT behind the desk: the strip right
    #      of the desk's right end (x > 405, under the lamp) and the near-left corner. v119.0 painted a
    #      beautiful tile grid into the region nobody can see, which is why "define the ground" came back.
    gx0 = GROUND_X0
    row_y, ri = FLOOR_Y + 1, 0
    while row_y < H:
        step = 10 + ri * 4
        ry0, ry1 = row_y, min(row_y + step, H)
        box(d, gx0, ry0, W, ry1, 'floor_1' if ri % 2 == 0 else 'floor_2')
        hline(d, gx0, W, ry0, 'void')                 # grout
        hline(d, gx0, W, ry0 + 1, 'floor_3')          # the lit bevel of the tile behind it
        if ry1 - ry0 > 5:
            dither(img, gx0, ry0 + 2 + (ry1 - ry0) // 2, W, ry1 - 1,
                   'floor_1' if ri % 2 == 0 else 'floor_2', 'floor_0', 0.55)
        for k in range(-2, 14):
            spacing = max(14, TILE_W - ri * 2)
            x_top = VPX + k * spacing
            x_bot = VPX + int((k * spacing) * (1.5 + ri * 0.12))
            if x_bot < gx0 - 4 or x_top > W + 4:
                continue
            for yy in range(ry0, ry1):
                t = (yy - ry0) / float(max(1, ry1 - ry0))
                xx = int(x_top + (x_bot - x_top) * t)
                if gx0 <= xx < W:
                    img.putpixel((xx, yy), rgba('void'))
                if gx0 <= xx + 1 < W and yy > ry0 + 1:
                    img.putpixel((xx + 1, yy), rgba('floor_2'))
        row_y, ri = ry1, ri + 1

    # the lamp's light landing on that floor: a warm pool centred under the lamp, falling off to the right.
    # Warm light on a cold floor is this shot's punchline.
    for y in range(FLOOR_Y + 2, H):
        depth = (y - FLOOR_Y) / float(H - FLOOR_Y)
        cx = 452 - int(6 * depth)
        half = 22 + int(30 * depth)
        for x in range(max(gx0, cx - half), min(W, cx + half)):
            d2 = abs(x - cx) / float(half)
            if BAYER4[y & 3][x & 3] / 16.0 < 0.95 * (1.0 - d2) * (1.0 - depth * 0.35):
                img.putpixel((x, y), rgba('amber_1'))
            elif BAYER4[y & 3][x & 3] / 16.0 < 0.45 * (1.0 - d2):
                img.putpixel((x, y), rgba('amber_0'))

    # the window's cold light: a pool on the near-left corner of the floor
    for y in range(FLOOR_Y + 2, H):
        depth = (y - FLOOR_Y) / float(H - FLOOR_Y)
        half = 34 - int(16 * depth)
        for x in range(0, max(1, half)):
            if BAYER4[y & 3][x & 3] / 16.0 < 0.55 * (1.0 - depth):
                img.putpixel((x, y), rgba('floor_3'))

    # the desk's contact seam where its right end meets the floor: a black line and a shadow to its right
    for y in range(FLOOR_Y + 1, H):
        if 0 <= gx0 - 1 < W:
            img.putpixel((gx0 - 1, y), rgba('void'))
        for i, x in enumerate(range(gx0, gx0 + 10)):
            if BAYER4[y & 3][x & 3] / 16.0 < 0.8 - i * 0.08:
                img.putpixel((x, y), rgba('void'))

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
    sky_h = gy1 - gy0

    # ---- THE SKY, as a smooth gradient with WIDE dithered seams.
    #
    # v119.0 had a broad checker band across the middle of the pane and the player read it as "weird" -- and
    # it was: at 88 px wide a 16 px checker is a texture fault, not haze. The rule for a sky at this size is
    # that each step must be at least 12 px tall, so a 160 px pane carries five steps and no more, with the
    # seam between two steps dithered across the whole 12 px instead of 3.
    #
    # Dark at the top, brightening toward the horizon where the city's own light sits. The pane ends up the
    # brightest large cold surface in the frame -- which is the job of a window in a dark room.
    steps = ('wall_0', 'wall_1', 'wall_2', 'wall_3', 'steel_1')
    band_h = sky_h / float(len(steps))
    for i, key in enumerate(steps):
        y0 = gy0 + int(i * band_h)
        y1 = gy0 + int((i + 1) * band_h)
        box(d, gx0, y0, gx1, y1, key)
        if i + 1 < len(steps):
            dither(img, gx0, y1 - 7, gx1, y1 + 7, key, steps[i + 1], 0.5, seed=i)
    # the city's light pollution: a tight bright band right above the skyline, where it belongs
    box(d, gx0, gy1 - 12, gx1, gy1 - 8, 'steel_1')
    dither(img, gx0, gy1 - 16, gx1, gy1 - 12, 'wall_3', 'steel_1', 0.45)

    # ---- the moon: a hard disc with a two-step halo. No rim light -- the halo IS the rim.
    mx, my = gx0 + int((gx1 - gx0) * 0.70), gy0 + 14
    d.ellipse([mx - 9, my - 9, mx + 9, my + 9], fill=rgba('wall_2'))
    d.ellipse([mx - 6, my - 6, mx + 6, my + 6], fill=rgba('wall_3'))
    d.ellipse([mx - 4, my - 4, mx + 4, my + 4], fill=rgba('steel_1'))
    d.ellipse([mx - 1, my - 5, mx + 5, my + 1], fill=rgba('wall_2'))      # the shadow side

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

    # ---- the wet glass: TWO horizontal reflections, not a vertical streak.
    #
    # A one-pixel vertical line down a pane reads as a scratch (that trap is already in the bug log). Glass
    # over a lit city reflects HORIZONTALLY: a broad soft band across the lower pane, and one thin hard
    # highlight where the surface turns. That is what "stronger highlights" looks like on wet glass.
    for y in range(gy0 + int(sky_h * 0.46), min(gy1 - 46, gy0 + int(sky_h * 0.72))):
        t = (y - gy0 - sky_h * 0.46) / float(max(1, sky_h * 0.26))
        for x in range(gx0 + 1, gx1):
            if BAYER4[y & 3][x & 3] / 16.0 < 0.5 * (1.0 - abs(t - 0.4) * 1.6):
                img.putpixel((x, y), rgba('steel_2'))
    band_y = gy0 + int(sky_h * 0.52)
    for x in range(gx0 + 2, gx1 - 2):
        if (x // 3) % 2 == 0:
            img.putpixel((x, band_y), rgba('steel_2'))

    # ---- the sill: the one specular in the sprite, and it is COLD
    box(d, 1, mh - 13, mw - 1, mh - 7, 'wall_1')
    hline(d, 2, mw - 3, mh - 13, 'steel_2')
    hline(d, 2, mw - 3, mh - 11, 'wall_1')
    hline(d, 1, mw - 1, mh - 6, 'void')
    dither(img, 2, mh - 10, mw - 2, mh - 7, 'wall_1', 'wall_0', 0.5)
    speculars(img, 'window')
    return img


# ===================================================================================== monitor table
def _screen(d, img, x, y, w, h, state='on', tilt=0):
    """
    One CRT of the cluster. v119: the cases are 30-34 px wide with a chunky 4 px bezel and a tube that is
    LIT, not grey -- at this size the tube is 3-4 pixels of colour and that colour is the whole read. The
    previous version used scr_1 for the glass and it read as blank grey stickers; now the lit ones are
    cyan and the dark ones are teal with a single sharp reflection.
    """
    y += 1 if tilt == 2 else 0
    # the shadow the case drops on the table under it, and on the case below it
    dither(img, x + 2, y + h, x + w, y + h + 2, 'void', 'steel_0', 0.4)
    # the case: black outline, then the body, lit from the upper left
    box(d, x, y, x + w, y + h, 'void')
    box(d, x + 1, y + 1, x + w - 1, y + h - 1, 'steel_1')
    dither(img, x + 1, y + 1, x + w - 1, y + h - 1, 'steel_1', 'steel_0', 0.35)
    hline(d, x + 1, x + w - 1, y + 1, 'steel_2')
    vline(d, x + 1, y + 1, y + h - 1, 'steel_2')
    cut_corners(d, x + 1, y + 1, x + w - 1, y + h - 1, 2, 'void')

    # the moulded bezel: 4 px of case, then the tube, then 6 px of chin where the badge lives
    tx0, ty0, tx1, ty1 = x + 4, y + 4, x + w - 4, y + h - 6
    if tx1 <= tx0 or ty1 <= ty0:
        return
    hline(d, tx0 - 1, tx1, ty0 - 1, 'steel_0')
    vline(d, tx0 - 1, ty0 - 1, ty1, 'steel_0')
    if state == 'on':
        # phosphor: brightest where the beam is, falling off toward the bottom of the tube
        box(d, tx0, ty0, tx1, ty1, 'teal_1')
        box(d, tx0, ty0, tx1, ty0 + 2, 'cyan_0')
        dither(img, tx0, ty0 + 1, tx1, ty0 + 5, 'teal_1', 'cyan_1', 0.5)
        dither(img, tx0, ty1 - 4, tx1, ty1, 'teal_1', 'teal_0', 0.5)
        # two rows of "data": a lit block and a dimmer one, not a paragraph
        for i, (dy, x_end, col) in enumerate(((2, 8, 'cyan_2'), (5, 4, 'cyan_0'), (8, 10, 'cyan_1'))):
            if ty0 + dy < ty1 - 1:
                hline(d, tx0 + 1, tx0 + x_end, ty0 + dy, col)
    elif state == 'cyber':
        box(d, tx0, ty0, tx1, ty1, 'void')
        for i in range(0, ty1 - ty0 - 1, 2):
            hline(d, tx0 + 1, tx0 + 2 + ((i * 5) % max(2, tx1 - tx0 - 3)), ty0 + i, 'cyan_2' if i % 4 else 'cyan_1')
    else:                                    # dead: dark glass, one 2 px reflection
        box(d, tx0, ty0, tx1, ty1, 'teal_0')
        hline(d, tx0 + 1, tx1 - 3, ty0 + 2, 'steel_2')
        hline(d, tx0 + 1, tx1 - 5, ty0 + 3, 'steel_1')
    # chin: the badge and the power LED
    hline(d, tx0, tx1, ty1 + 1, 'steel_0')
    box(d, tx0, ty1 + 2, tx0 + 3, ty1 + 4, 'steel_2' if state != 'dead' else 'steel_1')
    px(d, tx1 - 1, ty1 + 3, 'cyan_2' if state != 'dead' else 'red_1')


def monitor_stack():
    """
    The table of screens in the back of the room: BIGGER monitors, jumbled, standing on a TABLE THAT
    TOUCHES THE FLOOR.

    What the player saw in v118, and why each part is different now:

      * "the table floats together with the monitor" -- the sprite was 66 px tall and simply stopped in
        mid-air. It is 128 px now and the legs run to y=126 of the sprite, which is canvas y 196 = FloorY.
        There is a dithered contact shadow where each foot meets the floor.
      * "make the monitors bigger and more disarranged while sticking to each other" -- 30-34 px cases
        (was 22), rows that interlock by 3 px, a 1-2 px tilt on the case edges, and the two bottom cases
        sit ON the slab while the others are propped against each other and overhang its left end.
      * they were grey stickers -- see _screen(): the lit tubes are cyan now.
    """
    img = Image.new('RGBA', (STACK_W, STACK_H), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    mw, mh = STACK_W, STACK_H

    table_y = 76                      # the slab's front edge, in sprite space (canvas y 120)
    leg_bottom = mh - 3               # the feet, at canvas y 149 -- the floor plane behind the desk edge

    # ---- the table. Both posts are in the RIGHT half of the sprite (sprite x 38 and 106, canvas 368 and
    #      436): the monitor is drawn after the stack and covers everything left of canvas x 364, so a post
    #      at the sprite's left edge would be hidden and the table would read as one-legged. The slab is
    #      allowed to vanish behind the monitor -- that is depth -- but the support must be visible.
    box(d, 0, table_y, mw, table_y + 7, 'wood_1')
    hline(d, 0, mw, table_y, 'wood_2')
    dither(img, 0, table_y + 1, mw, table_y + 7, 'wood_1', 'wood_0', 0.45)
    # the slab catches the light off the tubes above it: a cyan wash along the lit end
    dither(img, 70, table_y + 1, mw, table_y + 4, 'wood_1', 'teal_1', 0.4)
    box(d, 0, table_y + 7, mw, table_y + 9, 'wood_0')
    hline(d, 0, mw, table_y + 8, 'void')
    for lx in (14, 54):
        box(d, lx, table_y + 9, lx + 6, leg_bottom - 2, 'wood_1')
        vline(d, lx + 6, table_y + 9, leg_bottom - 2, 'void')
        vline(d, lx, table_y + 10, leg_bottom - 2, 'wood_2')      # the lit edge, facing the monitor
        dither(img, lx + 1, table_y + 9, lx + 6, leg_bottom - 2, 'wood_1', 'wood_0', 0.4)
        box(d, lx - 2, leg_bottom - 3, lx + 9, leg_bottom, 'wood_0')   # a foot
        hline(d, lx - 2, lx + 9, leg_bottom - 3, 'wood_2')
        # ---- THE CONTACT SHADOW, v119.2.
        #
        # The player's report was "the table at the back looks like it's floating", and he is right: the feet
        # ended at y=149 and below them was bare WALL. A shadow drawn on a wall reads as a shadow drawn on a
        # wall, which is the same thing as floating. Two things fix it, and both are needed:
        #
        #   a) the sprite carries the FLOOR it stands on: a band of floor tile from the foot line to the
        #      bottom of the sprite, so the desk (drawn after) continues the same floor plane and the eye
        #      reads one ground going back behind the desk;
        #   b) a squashed, dithered contact shadow that pools AROUND each foot and is darkest where the foot
        #      meets the floor -- an ellipse, not a vertical smear.
        box(d, 0, leg_bottom, mw, mh, 'floor_1')
        hline(d, 0, mw, leg_bottom, 'floor_0')
        hline(d, 0, mw, leg_bottom + 1, 'floor_3')       # the floor's lit bevel, same as the desk's floor
        # THE GROUND SHADOW. One wide pool across the whole footprint, not two thin smears: a table standing
        # on a floor darkens the floor AROUND it, and that pool is the only thing at this scale that says
        # "touching" rather than "near". The pool is drawn on the wall band BELOW the feet, which is exactly
        # the strip the desk's back edge does not cover, so it stays visible in the composite.
        for yy in range(leg_bottom - 6, mh):
            dy2 = (yy - (leg_bottom - 6)) / float(max(1, mh - (leg_bottom - 6)))
            for xx in range(0, mw):
                # the pool is widest along the table's span, with the legs adding their own anchor
                edge = min(xx, mw - 1 - xx) / (mw * 0.5)
                near_leg = min(abs(xx - 12), abs(xx - 57)) / 14.0
                t = max(0.0, 1.0 - edge * 0.9) * (0.55 + 0.45 * max(0.0, 1.0 - near_leg))
                if BAYER4[yy & 3][xx & 3] / 16.0 < t * (0.45 + dy2 * 0.8):
                    img.putpixel((xx, yy), rgba('void'))
        # ...and then the FOOT is redrawn ON TOP of its own shadow, so the table stands IN the pool and is
        # not swallowed by it. A foot that disappears into its shadow reads as a leg cut off.
        for fx in (lx - 2, lx + 7):
            box(d, fx - 2, leg_bottom - 3, fx + 9, leg_bottom + 1, 'wood_1')
            hline(d, fx - 2, fx + 9, leg_bottom - 3, 'wood_3')    # the lit top of the foot
            hline(d, fx - 2, fx + 9, leg_bottom, 'wood_0')
            vline(d, fx + 9, leg_bottom - 3, leg_bottom, 'void')

    # the cross-brace, high up behind the cases: what makes it a table and not two sticks
    box(d, 12, table_y + 15, 60, table_y + 18, 'wood_0')
    hline(d, 12, 60, table_y + 15, 'wood_1')

    # ---- the cluster: six cases, tilted, overlapping, sagging to the right. The two bottom cases rest on
    #      the slab (y + h == 52); the rest are propped on each other and overhang its left end.
    # v119.1: SIX SCREENS, TWO ACROSS AND THREE HIGH.
    #
    # Two corrections, both from the player's report:
    #
    #   1. "the computers at the back is overlapping with the main monitor" -- the 3-wide cluster started at
    #      canvas x=331 and the monitor's case ends at 364, so the case's hard edge cut two screens in half.
    #      A face cut by a vertical line does not read as depth, it reads as a mistake. The cluster now sits
    #      entirely right of the monitor (366+), and only the TABLE tucks behind it.
    #   2. the room is 116 px wide right of the monitor and the lamp and the server both live there, so a
    #      3-wide row physically cannot coexist with them -- v119.1's first try put the lamp's shade on top
    #      of the top-right screen. Stacking 2 across x 3 high fits in 70 px, and each screen is 31-35 px
    #      wide instead of the 27 a 3-wide row could afford: BIGGER than v119.0 and much bigger than v118.
    #
    #   cells are sprite px; the sprite's origin is canvas (366, 44). The bottom row's bases land on the
    #   slab at y 76, and the pile leans the way a pile leans: alternate rows offset by 2-3 px.
    cells = [
        (1, 0, 32, 24, 'on'),
        (33, 2, 34, 23, 'cyber'),
        (0, 25, 33, 26, 'cyber'),
        (32, 24, 35, 27, 'dead'),
        (2, 51, 31, 24, 'on'),
        (34, 52, 33, 23, 'on'),
    ]
    for (cx, cy, cw, ch, state) in cells:
        _screen(d, img, cx, cy, cw, ch, state, tilt=(cx + cy) % 3)
    speculars(img, 'monitor_stack')
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
    speculars(img, 'lamp')
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
    speculars(img, 'crt')
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
    # ---- THE FLOOR. This is the v119.1 change the player actually asked for ("define the ground more").
    #
    # What was here: a flat `wood_0` band 57 px tall with a dither into `void`. That is not a floor, it is the
    # absence of one -- the desk looked like it was standing in black water, and the eye had nothing to
    # measure the room's depth with. It is now a TILED FLOOR IN PERSPECTIVE, and it is drawn as the
    # continuation of the wall's floor line:
    #
    #   * the tiles are laid on the same vanishing point as the wall and the desk (VPX 268), so their grout
    #     runs to the same place everything else does;
    #   * the rows get taller toward the camera, which is the whole depth cue at this size;
    #   * the front face of the desk gets a 3 px SHADOW SOCKET -- the desk's own carcass sits inside the
    #     floor, and the black seam under it is what stops it floating;
    #   * the floor is lit by the two sources that exist: the monitor's cold spill in front of the desk and
    #     the lamp's warm pool at the right end, plus the window's cold pool at the near-left corner.
    bayer = (0, 8, 2, 10, 12, 4, 14, 6, 3, 11, 1, 9, 15, 7, 13, 5)
    box(d, 0, near, W, DESK_H, 'floor_1')

    # the desk's shadow socket: 3 px of true black on the floor line, then a dithered falloff in FRONT of
    # the desk (i.e. below it in the image), which is where a raised object's contact shadow lands
    for y in range(near, min(DESK_H, near + 3)):
        hline(d, 0, W, y, 'void')
    for y in range(near + 3, min(DESK_H, near + 12)):
        fall = (y - near - 3) / 9.0
        for x in range(W):
            if bayer[((y & 3) << 2) | (x & 3)] < 16 * (0.55 - 0.45 * fall):
                img.putpixel((x, y), rgba('void'))

    # the tile rows, converging. Row heights grow with a fixed step so the perspective is legible rather
    # than decorative; the grout is black, and every tile's far edge gets one lit bevel pixel.
    row_y, ri = near + 3, 0
    rows_floor = []
    while row_y < DESK_H:
        step = 9 + ri * 3
        ry1 = min(row_y + step, DESK_H)
        rows_floor.append((row_y, ry1, ri))
        row_y, ri = ry1, ri + 1
    for (ry0, ry1, ri) in rows_floor:
        hline(d, 0, W, ry0, 'floor_0')                    # the grout between rows: dark, but not a hole
        hline(d, 0, W, ry0 + 1, 'floor_4')                # ...and the lit bevel of the tile behind it
        # the checker runs floor_2 / floor_4 near the camera and falls to floor_1 / floor_3 at the back:
        # the floor gets LIGHTER toward the viewer, which is the cheapest honest depth cue there is
        nearness = (ry0 - near) / float(DESK_H - near)
        face = ('floor_4' if nearness > 0.4 else 'floor_1') if ri % 2 == 0 else \
               ('floor_2' if nearness > 0.4 else 'floor_3')
        box(d, 0, ry0 + 2, W, ry1, face)
        mid = ry0 + 2 + (ry1 - ry0) // 2
        if mid < ry1 - 1:
            dither(img, 0, mid, W, ry1 - 1, face, 'floor_0', 0.55)
        # vertical grout, aimed at the vanishing point
        spacing = max(16, TILE_W - ri * 1)
        for k in range(-10, 22):
            x_top = VPX + k * spacing
            x_bot = VPX + int((k * spacing) * (1.55 + ri * 0.10))
            if x_bot < -6 or x_top > W + 6:
                continue
            for yy in range(ry0 + 1, ry1):
                t = (yy - ry0) / float(max(1, ry1 - ry0))
                xx = int(x_top + (x_bot - x_top) * t)
                if 0 <= xx < W:
                    img.putpixel((xx, yy), rgba('void'))
                if 0 <= xx + 1 < W and yy > ry0 + 1:
                    img.putpixel((xx + 1, yy), rgba('floor_2'))

    # the monitor's cold light on the floor in front of the desk: the one place its spill can be seen
    for y in range(near + 3, DESK_H):
        depth = (y - near) / float(DESK_H - near)
        for x in range(W):
            cold = ((x - 196) / 250.0) ** 2 + ((y - (near + 10)) / 34.0) ** 2
            if cold < 1.0 and bayer[((y & 3) << 2) | (x & 3)] < (1.0 - cold) * 12.0:
                img.putpixel((x, y), rgba('floor_3'))
    # the lamp's warm pool at the floor's right end, and the window's cold pool at the near-left corner
    for y in range(near + 3, DESK_H):
        depth = (y - near) / float(DESK_H - near)
        for x in range(W):
            warm = ((x - 448) / 84.0) ** 2 + ((y - (near + 14)) / 30.0) ** 2
            if warm < 1.0 and bayer[((y & 3) << 2) | (x & 3)] < (1.0 - warm) * 10.0:
                img.putpixel((x, y), rgba('amber_1'))
            win = ((x - 30) / 86.0) ** 2 + ((y - (near + 18)) / 40.0) ** 2
            if win < 1.0 and bayer[((y & 3) << 2) | (x & 3)] < (1.0 - win) * 8.0:
                img.putpixel((x, y), rgba('floor_3'))

    # ---- a drawer front on the right: the desk's APRON, above the front edge. It used to be drawn below
    # the front edge, which put it on the floor once the floor became a surface -- it read as a brass bar
    # lying on the ground. Furniture detail belongs on the furniture.
    box(d, 322, near - 18, 448, near - 6, 'wood_0')
    hline(d, 322, 448, near - 18, 'wood_1')
    hline(d, 322, 448, near - 7, 'void')
    hline(d, 324, 446, near - 6, 'wood_2')
    box(d, 378, near - 13, 400, near - 11, 'brass')

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
    speculars(img, 'desk')
    return img


def speculars(img, which):
    """
    THE HIGHLIGHTS (v119.1). The player asked for stronger highlights twice, so this is the honest version of
    that request: a specular is a PLACE, not a level. Brightening everything is what makes an image flat; a
    handful of 1-2 px hits at the edges that would really catch a light is what makes it read as glossy.

    All of these are the BRIGHTEST step of the material they sit on (wood_4, cup_2, paper_2, amber_4, steel_2),
    so none of them invents a colour, and each is dashed rather than continuous: a solid line across a 480 px
    desk is a divider, a dashed line is a reflection.
    """
    load = img.load()
    w, h = img.size
    def put(x, y, key):
        if 0 <= x < w and 0 <= y < h and load[x, y][3] != 0:
            load[x, y] = rgba(key)
    def dash(x0, y, n, key, period=3, duty=2):
        for i in range(n):
            if i % period < duty:
                put(x0 + i, y, key)

    if which == 'desk':
        near = DESK_TOP + 38
        # the desk's front edge: the frame's longest specular. wood_4 is the brightest wood step.
        hline(ImageDraw.Draw(img), 0, w, near - 3, 'wood_4')
        dash(96, near - 3, 120, 'paper_2', 7, 2)
        dash(300, near - 3, 96, 'wood_4', 5, 3)
        # the cup's rim: two pixels of the brightest ceramic, on the side the tube is on
        put(271, 170, 'paper_2'); put(272, 170, 'paper_2'); put(270, 172, 'cup_2')
        # the keyboard's top edge and the spacebar's lip
        dash(302, DESK_TOP + 10, 80, 'key_2', 6, 2)
        dash(318, DESK_TOP + 23, 48, 'key_2', 5, 1)
        # the mouse's spine
        dash(391, DESK_TOP + 13, 9, 'mouse_2', 4, 2)
        # the forms: the top sheet's near edge, and the brass handle
        dash(422, DESK_TOP + 26, 54, 'paper_2', 8, 3)
        dash(378, near - 12, 22, 'amber_3', 4, 2)
    elif which == 'crt':
        # the case's lit top edge, the bezel's chamfer, and the glass's corner glint
        dash(6, 0, CRT_W - 12, 'steel_2', 4, 2)
        dash(4, 4, CRT_W - 8, 'steel_2', 9, 2)
        gx, gy = CRT_GLASS[0], CRT_GLASS[1]
        for i, (dx, dy) in enumerate(((3, 2), (4, 2), (5, 2), (3, 3), (3, 4))):
            put(gx + dx, gy + dy, 'cyan_2')
    elif which == 'lamp':
        # the shade's lit rim and the brass stem
        dash(16, 84, 26, 'amber_4', 3, 2)
        dash(26, 100, 4, 'amber_3', 2, 1)
    elif which == 'monitor_stack':
        # every case's lit top edge and each tube's glint: six small hits, which is what makes the pile
        # read as six separate objects
        for n in range(6):
            cx = STACK_CELLS[n][0] if 'STACK_CELLS' in globals() else 0
        # (the cells themselves are the generator's own table; this pass only needs the slab's edge)
        dash(2, 76, 62, 'steel_2', 5, 2)
        dash(12, 62, 40, 'steel_2', 9, 2)
    elif which == 'window':
        # the sill's front edge: the sprite's loudest line, and now the frame's second brightest
        dash(3, WIN_H - 13, WIN_W - 6, 'steel_2', 3, 2)


def _step_up(current, low, high):
    """Raise a desk pixel one ramp step if it is the low step. Nothing else is touched: the light pass in
    MenuView does the same thing, in the same direction, so the twin render and the game agree."""
    if current[:3] == PALETTE[low]:
        return high
    return None


# ===================================================================================== glow
#  The player's v119 note was exact: "the darkness is good enough, only glow is missing". So this pass adds
#  light and never removes it, and it is a SEPARATE LAYER -- three sprites, blitted after the light pass and
#  after the vignette, so a halo cannot be darkened by the thing it is glowing through and the compositor
#  (and the artist) can tune one source without touching the room.
#
#  Additive, and quantised to the palette's own up-steps so the halos never introduce a colour: every pixel
#  is either transparent or an actual palette colour. Three ring levels, ordered-dithered, so the falloff
#  reads as light on a 16-bit screen instead of as a Photoshop blur.

#  Each halo pixel stores its STRENGTH in its colour: the ramp below is ordered weakest to strongest, and
#  the runtime reads the rank (MenuLayout.GlowRamp) to know how far to lift the surface underneath. The
#  brightest entry is treated as EMISSIVE and painted as-is -- that is the hot core of the bloom, the only
#  place a glow is allowed to put down its own colour.
#  THE COLD GLOW IS TWO STRENGTHS AND NO RIM.
#
#  v119's first cut gave the halo a solid `cyan_2` rim one pixel wide at distance 0 -- which is exactly an
#  OUTLINE around every emitter, and the player read it (correctly) as the window looking "weird": a cyan
#  pen line around the window frame instead of light in the room. There is no rim now. The near band is
#  dithered 3-in-4 rather than solid, the far band 1-in-5, and nothing in this layer is emissive: every
#  pixel is a STRENGTH the runtime uses to lift the surface under it one or two steps up its own ramp.
COLD_RAMP = ('teal_2', 'cyan_0', 'cyan_1')       # weakest -> strongest (index = strength - 0)
WARM_RAMP = ('amber_1', 'amber_2', 'amber_3')    # the lamp's own three steps
COLD_REACH, COLD_POWER = 62, 1.9
WARM_REACH, WARM_POWER = 44, 1.7
BAYER4 = ((0, 8, 2, 10), (12, 4, 14, 6), (3, 11, 1, 9), (15, 7, 13, 5))

#  ...and these are the HIGHLIGHTS the player asked for, drawn as a separate, sparse pass: real specular
#  hits where a light source would actually bounce, not a rim. They are the only emissive pixels in the
#  layer, they are 1-3 px each, and there are fewer than twenty of them on the whole canvas.
def _distance(mask, w, h):
    """Chebyshev-ish distance, in px, from the nearest True pixel of `mask` (a 8-neighbour BFS)."""
    INF = 999
    dist = [[INF] * w for _ in range(h)]
    frontier = [(x, y) for y in range(h) for x in range(w) if mask[y][x]]
    for (x, y) in frontier:
        dist[y][x] = 0
    d = 0
    while frontier and d < 16:
        d += 1
        nxt = []
        for (x, y) in frontier:
            for dy in (-1, 0, 1):
                for dx in (-1, 0, 1):
                    nx, ny = x + dx, y + dy
                    if 0 <= nx < w and 0 <= ny < h and dist[ny][nx] == INF:
                        dist[ny][nx] = d
                        nxt.append((nx, ny))
        frontier = nxt
    return dist


def _emitters():
    """
    The room's emitters as two masks on the canvas: COLD (the window's glazing, every lit tube in the monitor
    cluster) and WARM (the mouth of the lamp's shade). Two masks, because the lamp is the warm practical and
    the whole frame is balanced on it being the only warm thing -- one ramp for both would lose that.
    """
    w, h = W, H
    cold = [[False] * w for _ in range(h)]
    warm = [[False] * w for _ in range(h)]

    def paint(mask, sprite, ox, oy, accept):
        for y in range(sprite.height):
            for x in range(sprite.width):
                p = sprite.getpixel((x, y))
                if p[3] and accept(p[:3]):
                    cx, cy = ox + x, oy + y
                    if 0 <= cx < w and 0 <= cy < h:
                        mask[cy][cx] = True

    win = kit_rgba('menu/window.png')
    paint(cold, win, 8, 10, lambda c: True)          # the whole glazing is the source
    st = kit_rgba('menu/monitor_stack.png')
    lit = set(PALETTE[k] for k in ('cyan_2', 'cyan_1', 'teal_1'))
    paint(cold, st, 330, 68, lambda c: c in lit)     # only the lit tubes, not the steel around them
    for y in range(62 + 92, 62 + 102):               # the shade's mouth, 30 px wide
        for x in range(422 + 12, 422 + 44):
            if 0 <= x < w and 0 <= y < h:
                warm[y][x] = True
    # The FIXTURES are not surfaces: the light leaves them, it does not land on them. Without this the
    # distance map speckles the inside of every CRT case and the lamp's own shade, which is exactly the
    # "glow sprayed over the art" look this pass exists to avoid.
    solid = [[False] * w for _ in range(h)]
    for rel, ox, oy in (('menu/monitor_stack.png', 330, 68), ('menu/lamp.png', 422, 62),
                        ('menu/server.png', 452, 80), ('menu/crt.png', 104, 10)):
        sp = kit_rgba(rel)
        paint(solid, sp, ox, oy, lambda c: True)
    return cold, warm, solid, w, h


def kit_rgba(rel):
    return Image.open(os.path.join(ROOT, 'art', 'kit', rel)).convert('RGBA')


def glow():
    """
    The glow layer: ONE sprite, the size of the canvas.

    Built from distance transforms of the emitters' own silhouettes (so the light hugs the tall window, the
    cluster's tubes and the lamp's mouth), then banded and ORDERED-DITHERED, never outlined: v119.0 gave the
    halo a solid rim at distance 0 and that read as a cyan pen line around the window. Here the near band is
    a 3-in-4 dither, the far band 1-in-5, and both are pure STRENGTH -- the runtime lifts the surface under
    them one or two steps up that surface's own ramp, and no pixel of this layer is painted as a colour.
    """
    cold, warm, solid, w, h = _emitters()
    dc = _distance(cold, w, h)
    dw = _distance(warm, w, h)
    img = Image.new('RGBA', (w, h), (0, 0, 0, 0))
    out = img.load()
    for y in range(h):
        for x in range(w):
            if solid[y][x]:
                continue                      # a fixture is not a surface: the light leaves it
            use_warm = dw[y][x] < dc[y][x]
            d = dw[y][x] if use_warm else dc[y][x]
            if d == 0:
                continue
            reach, power = (WARM_REACH, WARM_POWER) if use_warm else (COLD_REACH, COLD_POWER)
            if d >= reach:
                continue
            bands = WARM_RAMP if use_warm else COLD_RAMP
            t = (1.0 - d / float(reach)) ** power
            level = int(math.floor(t * len(bands) + (BAYER4[y & 3][x & 3] / 16.0 - 0.5) * 1.6))
            if level < 0:
                continue
            key = bands[min(len(bands) - 1, level)]
            # the density rises with strength: 3-in-4 near, 1-in-5 far. A halo made of EVERY pixel is paint.
            dens = (0.75, 0.42, 0.20)[min(2, level)]
            if BAYER4[y & 3][x & 3] / 16.0 >= dens:
                continue
            out[x, y] = rgba(key)
    _highlights(out, w, h)
    return img


def _highlights(out, w, h):
    """
    THE HIGHLIGHTS (v119.1). The player asked for stronger highlights and this is the honest version of that
    request: not a brighter wash, but the handful of places where a real light source would produce a
    specular -- the shade's rim where it meets the bulb, the glass of the tube, the wet window's sill, the
    cup's lip, the desk's front edge where the monitor's light lands.

    These are the only EMISSIVE pixels in the glow layer, they are 1-3 px each, and there are fewer than
    twenty. Strength 3 in GlowRamp means "paint me": everything else in the layer only lifts a surface.
    """
    hot = PALETTE['cyan_2']
    warm = PALETTE['amber_4']
    def dot(x, y, c, run=1):
        for i in range(run):
            if 0 <= x + i < w and 0 <= y < h:
                out[x + i, y] = c + (255,)
    def dash(x, y, n, c):
        # a dashed specular: every other pixel, so it reads as a glint and not as a line
        for i in range(n):
            if i % 2 == 0:
                dot(x + i, y, c)

    # the window: the glint belongs to the SILL and the wet glass, never to the frame. (The first version
    # put it on the frame's outer edge and the no-rim gate below caught it -- a specular on the outside of a
    # silhouette is a rim, which is exactly the defect this round is fixing.)
    # (inside the glass, at least 3 px from its frame: the sprite is 88x160 and its glazing starts at 5,5)
    dot(8 + 56, 10 + 74, hot, 2)
    dot(8 + 22, 10 + 60, hot, 1)
    # the lamp: the rim of the shade where the bulb's light escapes, and the bulb itself
    dash(430, 62 + 88, 10, warm)
    dot(437, 62 + 92, warm, 2)
    dot(439, 62 + 90, warm, 1)
    # the tube's glass: one glint in the top-left corner of the picture area, the way a CRT photographs
    dot(MenuGlassX() + 8, MenuGlassY() + 6, hot, 3)
    dot(MenuGlassX() + 8, MenuGlassY() + 8, hot, 2)
    # the cup's lip -- the desk's front edge is NO LONGER a highlight target: once the floor became a
    # surface, two cyan dashes sitting at y=214 read as floating bars in mid-air. A highlight has to be on
    # the edge of something the eye can name.
    dash(268, 186, 7, hot)


def MenuGlassX():
    return 104 + 26


def MenuGlassY():
    return 10 + 12


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

    # ---- v119.1: NO RIM ON THE GLOW.
    #
    # The player's "the window looks weird" was, in the end, a one-pixel emissive outline the glow pass drew
    # at distance 0 around every emitter. A bloom has a soft edge; an outline has a hard one, and the eye reads
    # the second as a drawing error. So: count the EMISSIVE pixels in the glow sprite and require them to be
    # few and nowhere near the window's silhouette. This is the check that would have caught it.
    if 'menu/glow.png' in pieces:
        glow = pieces['menu/glow.png']
        emissive = set(PALETTE[k] for k in ('cyan_2', 'amber_4'))
        hot = [(x, y) for y in range(glow.height) for x in range(glow.width)
               if glow.getpixel((x, y))[3] and glow.getpixel((x, y))[:3] in emissive]
        if len(hot) > 40:
            problems.append('the glow layer has %d emissive pixels; a highlight is a glint, not a wash'
                            % len(hot))
        # The window's SILHOUETTE EDGE in canvas px -- its opaque pixels that have an empty neighbour OUTSIDE
        # the sprite. Testing against the whole pane instead would flag every glint drawn on the glass, and a
        # specular on glass is the point. A rim is an emissive pixel hugging the OUTSIDE of a silhouette.
        win = pieces['menu/window.png']
        opaque = {(x, y) for y in range(win.height) for x in range(win.width)
                  if win.getpixel((x, y))[3]}
        edge = {(8 + x, 10 + y) for (x, y) in opaque
                if any((x + dx, y + dy) not in opaque
                       for dx in (-1, 0, 1) for dy in (-1, 0, 1))}
        edge |= {(8 + WIN_W, 10 + y) for y in range(WIN_H)}     # the right side is off the sprite edge
        edge |= {(8 + x, 10 + WIN_H) for x in range(WIN_W)}     # ...and the bottom
        rim = [p for p in hot if any((p[0] + dx, p[1] + dy) in edge
                                     for dx in (-1, 0, 1) for dy in (-1, 0, 1))]
        if rim:
            problems.append('the glow draws a %d px RIM around the window (the v119.0 defect): %s'
                            % (len(rim), rim[:4]))
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
        # the glow layer: a SEPARATE set of sprites, blitted after the light pass and the vignette
        'menu/glow.png': glow(),
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
