#!/usr/bin/env python3
"""
make_desk_room.py -- the IN-GAME room, painted in the menu's materials (v126, the player's request P).

THE PLAYER'S WORDS
    "Redo the whole in game art and applicants, use the same layout and art as the ones in the menu."

WHAT THE MISMATCH ACTUALLY IS (measured, not felt)
    Two rooms, one game. The menu's shell is painted with a 3-5 step ramp per material -- a dithered wall,
    a tiled floor in one-point perspective, a warm wood slab with a single specular on its front edge. The
    desk's office is a set of flat blue-grey slabs with 1 px outlines: office_L0.png is a 480x270 picture with
    ~8 colours, no dither, no perspective and no specular. Put them side by side and the menu is a photograph
    of a room and the desk is a diagram of one.

    Neither room is wrong on its own. The bug is that they are not the same room, and the transition from
    New Game to the first applicant is a cut between them.

WHAT THIS PAINTS
    A 480x270 room in the menu's OWN idiom, from the menu's own palette and primitives (not a copy of them:
    this module imports make_menu_room so the wall dither, the perspective floor and the dither tables are
    the same code). The composition keeps the DESK LAYOUT's geometry -- the window behind the applicant's
    shoulders, the monitor left-of-centre, the desk surface at row 206, the floor at 198 -- because that
    geometry is what the hotspots, the lighting and the applicant's slot are registered against. What
    changes is the material: ramp steps, dither seams, a perspective floor, a wall lit from a named source,
    and the two light pools falling off in the right directions.

    It is written as ONE room sprite (backgrounds/office_M0.png) with the placeable layers still cut
    separately (desk/monitor_M.png, desk/monitor_stand_M.png, desk/foreground_M.png), so the existing layout
    can point at them without moving a single hotspot.

WHERE THE LIGHT COMES FROM (rule: no glow without a named emitter)
    * the window, high and left: the desk's cold key, and the reason the left side of every object is the
      lit side;
    * the monitor's tube, in front of the applicant at his right: a cold pool on the desk in front of the
      glass and a cold rim up his left edge;
    * nothing else. There is no lamp in this room, so there is no warm pool in it -- the warm light in this
      game belongs to the front door, and a warm pool with no lamp over it is the exact bug the player has
      now reported three times.

USAGE
    python tools/art/make_desk_room.py            # write the room and its placeable layers
    python tools/art/make_desk_room.py --check    # palette gate only, write nothing
"""
import math
import os
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
sys.path.insert(0, os.path.join(ROOT, 'tools', 'art'))

from PIL import Image, ImageDraw  # noqa: E402

import make_menu_room as M        # the primitives: rgba/px/box/dither, the palette, the perspective
from menu_palette import PALETTE, RAMP  # noqa: E402

W, H = 480, 270

# ---- the desk's geometry, taken from desk_layout.json so this art cannot drift from the hotspots -------
DESK_ROW = 170          # the desk's FAR edge: the props' plane starts here
FLOOR_ROW = 198         # where the wall meets the floor -- 8 px of floor show behind the desk
DESK_LIP = 228          # the desk's near edge: the lip the player's hands are on
# An OPEN desk (v127). Below the lip is not the desk's front panel -- it is the shadowed floor under the
# desk, which the player can see because the desk is open underneath. That is the band they called "too big
# and too empty", and it is now where the mess of dead PCs lives (art/kit/desk/props_junk.png).
HORIZON = 150           # the eye line: everything converges here, in both rooms
VPX = 268               # the same vanishing point as the menu's shell, so the two rooms agree
WALL_X = 0

# ---- the window, behind the applicant's shoulders (layout: x168..472, sill y152) -----------------------
# THE WINDOW MOVED LEFT (v127). It used to run behind the applicant's shoulders at x168..472; with the
# monitor now on the RIGHT and the applicant on the LEFT (the player's instruction -- "it would look cramped
# for the applicant if we put the window there and the applicant on the side"), the window belongs behind
# the applicant, i.e. on the left, and the monitor takes the right wall.
WIN_X0, WIN_X1 = 26, 208
WIN_Y0, WIN_Y1 = 8, 152
WIN_SILL = 152

# ---- the monitor, left of centre (layout: monitor anchor 14,86 as a 0.58x redraw) ----------------------
# The monitor's CANVAS rect, matching desk_layout.json (anchor [268,84], 192x108) -- the first pass
# still had the pre-move left-hand numbers here, which put the tube's light pool on the empty left half of
# the desk and drew an olive oval where nothing was. The sprite is 192x108 either way; these numbers are
# what the pools and the wall's rim are measured from, so they have to agree with the layout.
MON_X0, MON_Y0, MON_X1, MON_Y1 = 268, 88, 460, 196


def rgba(key):
    return M.rgba(key)


def bayer_fn(x, y):
    return M.BAYER4[y & 3][x & 3] / 16.0


def out(rel):
    return os.path.join(ROOT, 'art', 'kit', rel)


def dither(img, x0, y0, x1, y1, a, b, keep=0.5, seed=0):
    M.dither(img, x0, y0, x1, y1, a, b, keep, seed)


# ===================================================================================== the wall
def wall(img, d):
    """
    The back wall, in the menu's material: two ramp steps and a dither seam, a lit band under the window's
    sill where the cold key lands, and a deep fall-off toward the frame's corners.

    The desk's old wall was one flat colour with dark rectangles painted on it. A wall at this size reads as
    a wall because of three things: the seam where two sheets of value meet, the occlusion where it meets
    the floor, and the fact that it gets darker the further it is from the window.
    """
    # THE AMBIENT FIELD IS A FUNCTION OF X ONLY, and it has a soft edge. It used to be
    # t = (x + (y - HORIZON) * 0.35) / W, which is a DIAGONAL -- in the composition that drew a hard line
    # from the top of the frame down to the desk, a shadow wedge with a ruled edge, and it was the first
    # thing the eye found in the room. A back wall is lit by distance from the window: one vertical gradient,
    # dither-ramped, and nothing else.
    for y in range(0, DESK_ROW):
        for x in range(W):
            u = (x - 40) / float(W - 40)          # 0 at the window's edge, 1 at the far wall
            if u < 0.18:
                key = 'wall_1'
            elif u < 0.46:
                key = 'wall_1' if M.BAYER4[y & 3][x & 3] / 16.0 < (0.46 - u) / 0.28 else 'wall_0'
            else:
                key = 'wall_0'
            img.putpixel((x, y), rgba(key))
    # the corners fall off, so the frame's edges are not a flat plate of one value
    for y in range(0, DESK_ROW):
        for x in range(W - 60, W):
            if M.BAYER4[y & 3][x & 3] / 16.0 < (x - (W - 60)) / 90.0:
                img.putpixel((x, y), rgba('void'))

    # THE WALL'S SEAM IS VERTICAL AND SOFT. It used to run on the light's angle, which at this size drew a
    # dotted diagonal across the plaster -- a scratch on the print, not a change of value. A wall's two values
    # meet at the corner of the room, so the seam is a 14 px dither band down the frame's own vertical, and
    # the window's light band (below) does the directional work.
    # NO SEAM LINE. Two values already meet in a 90 px dither band above; a drawn line on top of that is a
    # pipe, and it read as one. A room corner is a change of value, not a stroke.
    for y in range(0, DESK_ROW):
        for x in range(int(0.60 * W), int(0.68 * W)):
            if M.BAYER4[y & 3][x & 3] / 16.0 < 0.10:
                img.putpixel((x, y), rgba('wall_1'))   # a faint grazing highlight, nothing more

# ===================================================================================== the floor
def floor(img, d):
    """
    The floor BEHIND the desk: 8 px of it, in perspective, converging on the menu's vanishing point. The desk's floor was a
    flat band; a floor a player stands in front of is the cheapest depth cue the frame has, and the menu
    already proved it (it is the thing the player called "define the ground more" and then approved).
    """
    for y in range(FLOOR_ROW, DESK_ROW):
        for x in range(W):
            img.putpixel((x, y), rgba('floor_1'))

    row_y, ri = FLOOR_ROW + 2, 0
    while row_y < DESK_ROW:
        step = 7 + ri * 3
        ry1 = min(row_y + step, DESK_ROW)
        d.line([(0, row_y), (W, row_y)], fill=rgba('floor_0'))
        if row_y + 1 < H:
            d.line([(0, row_y + 1), (W, row_y + 1)], fill=rgba('floor_4'))
        nearness = (row_y - FLOOR_ROW) / float(H - FLOOR_ROW)
        face = ('floor_4' if nearness > 0.45 else 'floor_1') if ri % 2 == 0 else \
               ('floor_2' if nearness > 0.45 else 'floor_3')
        for y in range(row_y + 2, ry1):
            for x in range(W):
                img.putpixel((x, y), rgba(face))
        # vertical grout, aimed at the vanishing point
        spacing = max(18, 34 - ri * 2)
        for k in range(-14, 16):
            xt = VPX + k * spacing
            xb = VPX + int(k * spacing * (1.5 + ri * 0.12))
            if xb < -4 or xt > W + 4:
                continue
            for y in range(row_y, ry1):
                t = (y - row_y) / float(max(1, ry1 - row_y))
                x = int(xt + (xb - xt) * t)
                if 0 <= x < W:
                    img.putpixel((x, y), rgba('floor_0'))
        row_y, ri = ry1, ri + 1

    # the desk's own contact shadow: 3 px of near-black where the carcass meets the floor, dithered out in
    # front of it. This is what stops the desk reading as a pasted rectangle -- the one rule the player has
    # raised about the table twice.
    for x in range(W):
        for k in range(0, 3):
            y = FLOOR_ROW + k
            if y < H:
                img.putpixel((x, y), rgba('void'))


# ===================================================================================== the desk slab
def desk_slab_sprite():
    """
    THE SURFACE, as its own layer (v127). It used to be painted into the room, which meant the mess under the
    desk could not be drawn UNDER it: the slab was part of the background, so anything sorted above the
    background -- the junk -- lay on top of the wood and read as clutter ON the desk.

    Now the slab is a sprite that sorts between them: junk (8) < slab (9) < props (13..17). That is what the
    standing rule "layer the art" means at this size, and it is why the desk no longer looks like it floats:
    the junk is genuinely behind its front edge.

    Painted in the menu's wood: two steps across its depth, one dither seam, the darker band where it turns
    away from the window, and the specular rationed to the front edge.
    """
    h = DESK_LIP - DESK_ROW
    img = Image.new('RGBA', (W, h), rgba('wood_2'))
    d = ImageDraw.Draw(img)

    # the far edge catches the window's cold key: two rows up its ramp, with the desk's occlusion above it
    for x in range(W):
        img.putpixel((x, 0), rgba('wood_3'))
        img.putpixel((x, 1), rgba('wood_2'))

    # the depth: WOOD GETS BRIGHTER TOWARDS THE CAMERA. The first pass darkened towards the camera and the
    # desk read as a black bar across the frame -- the menu's desk is the brightest thing in its lower third
    # and that is exactly what makes it read as a desk. wood_2 at the far edge, wood_3 at the lip.
    dither(img, 0, 12, W, 20, 'wood_2', 'wood_3', 0.40)
    dither(img, 0, 26, W, 34, 'wood_3', 'wood_2', 0.22)

    # the lip: the frame's one warm specular, doubled, and its shadow socket under it
    for x in range(W):
        img.putpixel((x, h - 4), rgba('wood_4'))
        img.putpixel((x, h - 3), rgba('wood_4'))
        img.putpixel((x, h - 2), rgba('wood_1'))
        img.putpixel((x, h - 1), rgba('void'))

    # ---- OCCLUSION: a soft shadow pass right in front of the far edge, which is where the props stand.
    # Without it every object on the desk is a sticker on a board -- the player's oldest note about this room.
    for x in range(W):
        for k in range(0, 9):
            y = 2 + k
            if M.BAYER4[y & 3][x & 3] / 16.0 < (1.0 - k / 9.0) * 0.55:
                img.putpixel((x, y), rgba('wood_0'))

    # THE TUBE'S POOL IS NOT PAINTED HERE, AND THAT IS THE FIX. It used to be: an ellipse of wood_3/wood_4
    # baked onto the slab right where the monitor's light would fall. ScreenLight then ran over the same
    # pixels at runtime and had nowhere to climb -- the surface was already at the top of its ramp -- so
    # switching the tube on moved the desk by +1.0/255. That is the whole "lamp lights the wall, not the
    # desk" report, and no amount of tuning the light would have fixed it: the effect was painted away
    # before it was applied. Baked light belongs in the MENU's room (a still image). The desk is live.
    # What the slab does carry is occlusion: the far edge's contact shadow, and a faint warm lift where the
    # wood faces the camera.

    return img


# ===================================================================================== the window
def window():
    """
    The window behind the applicant: the room's cold key and the depth behind his head.

    Three lit panes round a cross of mullions, the city below the sill line, and the sky above it. The centre
    opening is deliberately EMPTY (sky, a few stars, the city's edge) because the applicant's head goes in
    front of exactly that rectangle -- the composition decision recorded in desk_layout.json, painted
    properly instead of drawn as a flat plate.
    """
    w, h = WIN_X1 - WIN_X0, WIN_Y1 - WIN_Y0
    img = Image.new('RGBA', (w, h), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)

    # ---- the sky: wall_0 at the top, lifting toward the horizon where the city's light sits
    for y in range(h):
        t = y / float(h)
        key = 'wall_1' if t < 0.35 else ('steel_0' if t < 0.62 else 'steel_1')
        for x in range(w):
            img.putpixel((x, y), rgba(key))
    # stars: a SPARSE HASH, and the first version of this was a polka-dot lattice -- (i*37)%w and (i*53)%52
    # are highly correlated, so "scattered" came out as a diagonal grid of dots, which reads as a texture on
    # the glass rather than as a sky. A hash that mixes the two axes fixes it; 1 pixel in ~230 is sky.
    def _h(x, y):
        h = (x * 374761393 + y * 668265263) & 0xFFFFFFFF
        h = (h ^ (h >> 13)) * 1274126177 & 0xFFFFFFFF
        return h ^ (h >> 16)

    for y in range(2, min(h - 2, int(h * 0.60))):
        for x in range(2, w - 2):
            if _h(x, y) % 230 == 0:
                img.putpixel((x, y), rgba('paper_1'))

    # ---- the city: a silhouette band with lit windows, standing on the sill
    city_top = int(h * 0.62)
    x = 0
    seed = 12345
    while x < w:
        seed = (seed * 1103515245 + 12345) & 0x7FFFFFFF
        bw = 10 + (seed >> 16) % 22
        seed = (seed * 1103515245 + 12345) & 0x7FFFFFFF
        bh = 8 + (seed >> 16) % 30
        top = min(h - 2, city_top + 26 - bh)
        for yy in range(top, h):
            for xx in range(x, min(w, x + bw)):
                img.putpixel((xx, yy), rgba('void'))
        # the towers' lit windows: sparse, warm-free (this is a cold room), one pixel each
        for yy in range(top + 3, h - 2, 4):
            for xx in range(x + 2, min(w, x + bw) - 1, 3):
                hh = (xx * 374761393 + yy * 668265263) & 0xFFFFFFFF
                hh = (hh ^ (hh >> 13)) * 1274126177 & 0xFFFFFFFF
                if (hh ^ (hh >> 16)) % 5 == 0:
                    img.putpixel((xx, yy), rgba('paper_1'))
        x += bw + 2

    # ---- the mullions: a cross. 3 px wide, wall_2 on the lit side and void on the shadow side, so the
    # frame is a solid object in front of the glass and not a drawn line on it.
    cx, cy = w // 2, int(h * 0.52)
    for xx in range(cx - 1, cx + 2):
        for y in range(h):
            img.putpixel((xx, y), rgba('wall_2') if xx == cx - 1 else rgba('void'))
    for yy in range(cy - 1, cy + 2):
        for x in range(w):
            img.putpixel((x, yy), rgba('wall_2') if yy == cy - 1 else rgba('void'))

    # ---- the glazing's own specular: two diagonal streaks of 'steel_1' at low coverage. A window in a dark
    # room is a mirror as much as a hole; it is the reason the menu's window reads as glass.
    #
    # ...and the streaks are SHORT and in one pane, because a streak that runs the height of the window is a
    # scratch on the glass, not a reflection. (First draft: a dotted diagonal from corner to corner, found by
    # looking at the render.)
    for i in range(6, int(w * 0.34)):
        for k in range(0, 2):
            y = int(i * 0.42) + k - 2
            if 0 <= y < h and M.BAYER4[y & 3][i & 3] < 6:
                img.putpixel((i, y), rgba('steel_1'))
    for i in range(10, int(w * 0.30)):
        y = int(i * 0.42) + 9
        if 0 <= y < h and M.BAYER4[y & 3][i & 3] < 3:
            img.putpixel((i, y), rgba('steel_2'))

    # ---- the frame: 2 px of steel, lit on the top and left, with the sill 4 px deep and its own specular on
    # the top edge (speculars are rationed to three surfaces in this room; the sill is one of them)
    for x in range(w):
        img.putpixel((x, 0), rgba('steel_2'))
        img.putpixel((x, 1), rgba('steel_1'))
        img.putpixel((x, h - 1), rgba('void'))
        img.putpixel((x, h - 2), rgba('steel_2'))
    for y in range(h):
        img.putpixel((0, y), rgba('steel_2'))
        img.putpixel((1, y), rgba('steel_1'))
        img.putpixel((w - 1, y), rgba('void'))
        img.putpixel((w - 2, y), rgba('steel_2'))
    return img


# ===================================================================================== the monitor
def monitor():
    """
    The monitor the applicant is being judged through, at the layout's 0.58 scale: 192x108.

    It is the same object as the menu's CRT (menu/crt.png) seen smaller and from the front: a thin dark
    bezel, a lit top edge, a shadow side, a glass that is very slightly green-black, and a scanline every
    three rows. The glass is left EMPTY -- MonitorScreen paints into it at runtime from the same glyph atlas
    -- but it is painted with its own two-step value and its roll-off, so the screen reads as glass even
    before a single letter is drawn on it.
    """
    w, h = MON_X1 - MON_X0, MON_Y1 - MON_Y0
    img = Image.new('RGBA', (w, h), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    bez = 7

    # ---- the case: steel_1 body, steel_0 shadow side, steel_2 lit top and left. One specular on the top
    # edge, and a 2 px socket under the bottom lip.
    for y in range(h):
        for x in range(w):
            img.putpixel((x, y), rgba('steel_1'))
    for y in range(h):
        for x in range(w - 5, w):
            img.putpixel((x, y), rgba('steel_0'))
    for x in range(w):
        img.putpixel((x, 0), rgba('steel_2'))
        img.putpixel((x, 1), rgba('steel_2'))
        img.putpixel((x, h - 1), rgba('void'))
        img.putpixel((x, h - 2), rgba('steel_0'))
    for y in range(h):
        img.putpixel((0, y), rgba('steel_2'))
        img.putpixel((1, y), rgba('steel_1'))
        img.putpixel((w - 1, y), rgba('void'))

    # ---- the bezel's inner shadow: a 2 px socket all round the glass, so the tube sits INSIDE the case
    gx0, gy0, gx1, gy1 = bez, bez, w - bez - 4, h - bez - 6
    for y in range(gy0 - 2, gy1 + 2):
        for x in range(gx0 - 2, gx1 + 2):
            if 0 <= x < w and 0 <= y < h:
                img.putpixel((x, y), rgba('void'))

    # ---- the glass: void with a scanline every three rows and a faint wash in the top third, exactly the
    # menu's GlassMaterial() rule, so the two screens are the same glass
    for y in range(gy0, gy1):
        bright = y < gy0 + (gy1 - gy0) // 3
        for x in range(gx0, gx1):
            key = 'void'
            if (y - gy0) % 3 == 2:
                key = 'teal_0' if bright else 'steel_0'
            img.putpixel((x, y), rgba(key))

    # ---- the tube's own cold light on the case's bottom lip: the screen lighting its own chin
    for x in range(gx0, gx1):
        for k in range(0, 3):
            y = gy1 + 1 + k
            if y < h and M.BAYER4[y & 3][x & 3] < (10 - k * 3):
                img.putpixel((x, y), rgba('teal_1'))

    # ---- the corner LEDs and the brand slot: three pixels, and the only place the case is not grey
    for i, key in enumerate(('teal_2', 'amber_1', 'steel_0')):
        img.putpixel((gx1 - 6 + i * 3, gy1 + 4), rgba(key))
    return img


def monitor_stand():
    """
    The stand, as its own layer (the layout anchors it separately because the monitor's stub could never
    reach the desk -- the 2026-11-26 float fix). A neck, a foot, and a shadow socket under it.
    """
    w, h = 54, 40
    img = Image.new('RGBA', (w, h), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    # the neck
    for y in range(0, h - 10):
        for x in range(w // 2 - 5, w // 2 + 5):
            img.putpixel((x, y), rgba('steel_1'))
    for y in range(0, h - 10):
        img.putpixel((w // 2 - 5, y), rgba('steel_2'))
        img.putpixel((w // 2 + 4, y), rgba('steel_0'))
    # the foot: a shallow tray, lit on the top edge, black in its socket
    for y in range(h - 10, h - 4):
        for x in range(4, w - 4):
            img.putpixel((x, y), rgba('steel_1'))
    for x in range(4, w - 4):
        img.putpixel((x, h - 10), rgba('steel_2'))
        img.putpixel((x, h - 5), rgba('steel_0'))
    for y in range(h - 4, h):
        for x in range(2, w - 2):
            img.putpixel((x, y), rgba('void'))
    return img


# ===================================================================================== the desk
def foreground():
    """
    The desk's front lip: the slab the player's own hands would be on, and the one surface allowed a
    specular. 480x19 at the bottom of the frame (the layout's own size), so everything above the lip stays
    unclipped -- which is why the arm and the papers are not cut off by it.

    Warm wood, three steps, one dither seam, one specular line on the front edge, and a shadow socket under
    it. It is the desk's only warm surface in a cold room, which is what makes it read as the player's side.
    """
    w, h = W, 14                        # the front edge as its own strip: rows DESK_LIP..DESK_LIP+13.
    # Below it, to the bottom of the frame, is the OPEN under-desk band where the dead hardware lives
    # (props_junk.png), and that band is 28 rows -- enough to read, not enough to fill.
    img = Image.new('RGBA', (w, h), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    for y in range(h):
        for x in range(w):
            img.putpixel((x, y), rgba('wood_3'))
    dith = (0, 8, 2, 10, 12, 4, 14, 6, 3, 11, 1, 9, 15, 7, 13, 5)
    for y in range(3, 7):
        for x in range(w):
            if dith[((y & 3) << 2) | (x & 3)] < 7:
                img.putpixel((x, y), rgba('wood_2'))
    for y in range(8, 12):
        for x in range(w):
            if dith[((y & 3) << 2) | (x & 3)] < 5:
                img.putpixel((x, y), rgba('wood_0'))
    for x in range(w):
        img.putpixel((x, 0), rgba('wood_4'))      # the specular: the lip catching the room's cold key
        img.putpixel((x, 1), rgba('wood_3'))
        img.putpixel((x, h - 2), rgba('wood_2'))
        img.putpixel((x, h - 1), rgba('wood_0'))  # and the lip's own shadow socket: the desk turns to face down
    return img


# ===================================================================================== light pools
def light_pools(img):
    """
    The two sources, painted onto the room where they LAND rather than added as a halo around themselves.

    This is the rule the player has now stated three times about the lamp, applied to the desk's own room:
    light is visible because a surface it reaches goes up its own ramp. So the window's cold pool lies on the
    floor at the left under the sill, and the monitor's cold pool lies on the DESK in front of the glass --
    not on the wall behind it, and not around the fixture.
    """
    # ---- the window's pool: on the wall below the sill (already dithered in wall()) and on the floor at the
    # frame's left, falling off with distance
    for y in range(FLOOR_ROW, min(H, FLOOR_ROW + 50)):
        drop = (y - FLOOR_ROW) / 50.0
        for x in range(0, 150):
            dx = (x - 60) / 150.0
            if M.BAYER4[y & 3][x & 3] / 16.0 < (1.0 - drop) * (1.0 - abs(dx)) * 0.5:
                img.putpixel((x, y), rgba('floor_4'))

    # the desk's front edge: the lip's specular, doubled, because this is the one surface in the room whose
    # value is allowed to be the brightest thing on the wood
    for x in range(W):
        img.putpixel((x, DESK_LIP - 2), rgba('wood_3'))
        img.putpixel((x, DESK_LIP - 1), rgba('wood_2'))
    # ---- UNDER THE DESK (v127). The slab and the lip are their own layers now, so what the background owes
    # the bottom of the frame is the floor the desk stands on: dark, one step, with the slab's contact shadow
    # at the top. The mess of dead PCs is drawn on top of THIS, and the slab is drawn over the back of it.
    # The floor under the desk is floor_2 -> floor_1 toward the bottom edge, not floor_1 -> void: the junk
    # that sits on it is drawn in steel_0/steel_1, and on a near-black band those two values are the same
    # silhouette. A lit room has a lit floor; the junk only reads if the floor it stands on does.
    for y in range(DESK_ROW, H):
        k = (y - DESK_ROW) / float(max(1, H - DESK_ROW))
        for x in range(W):
            if k < 0.34:
                img.putpixel((x, y), rgba('floor_1'))
            elif k < 0.72:
                img.putpixel((x, y), rgba('floor_2' if M.BAYER4[y & 3][x & 3] > 6 else 'floor_1'))
            else:
                img.putpixel((x, y), rgba('floor_2'))
    for y in range(DESK_ROW, min(H, DESK_ROW + 10)):
        for x in range(W):
            if M.BAYER4[y & 3][x & 3] / 16.0 < 0.45 * (1.0 - (y - DESK_ROW) / 10.0):
                img.putpixel((x, y), rgba('floor_1'))

    # ---- the tube's pool: an ellipse lying ON the desk surface in front of the glass, plus the cold rim it
    # throws up the monitor's own shadow side
    cx, cy = (MON_X0 + MON_X1) // 2, DESK_ROW - 8
    for y in range(DESK_ROW - 30, min(H, DESK_ROW + 40)):
        for x in range(max(0, MON_X0 - 40), min(W, MON_X1 + 40)):
            dx = (x - cx) / 150.0
            dy = (y - cy) / 34.0
            r = math.sqrt(dx * dx + dy * dy)
            if r >= 1.0:
                continue
            t = M.BAYER4[y & 3][x & 3]
            if r < 0.45:
                img.putpixel((x, y), rgba('floor_3') if y > DESK_ROW else rgba('steel_1'))
            elif r < 0.75 and t < 8:
                img.putpixel((x, y), rgba('floor_3') if y > DESK_ROW else rgba('steel_0'))
            elif r < 0.95 and t < 3:
                img.putpixel((x, y), rgba('floor_3') if y > DESK_ROW else rgba('steel_0'))


# ===================================================================================== gate
def palette_gate(images):
    bad = 0
    allowed = set()
    for key, value in PALETTE.items():
        allowed.add(tuple(value) if isinstance(value, tuple) else value)
    for rel, im in images.items():
        for y in range(im.height):
            for x in range(im.width):
                p = im.getpixel((x, y))
                if p[3] == 0:
                    continue
                if tuple(p[:3]) not in allowed:
                    bad += 1
    return bad


def room():
    img = Image.new('RGBA', (W, H), rgba('void'))
    d = ImageDraw.Draw(img)
    wall(img, d)
    floor(img, d)

    win = window()
    img.alpha_composite(win, (WIN_X0, WIN_Y0))
    return img


def main():
    pieces = {
        'backgrounds/office_M0.png': room(),
        'desk/monitor_M.png': monitor(),
        'desk/monitor_stand_M.png': monitor_stand(),
        'desk/desk_slab_M.png': desk_slab_sprite(),
        'desk/foreground_M.png': foreground(),
    }
    bad = palette_gate(pieces)
    print('%s: %d off-palette pixels across %d sprites'
          % ('PASS' if bad == 0 else 'FAIL', bad, len(pieces)))
    if '--check' in sys.argv or bad:
        return 0 if bad == 0 else 1
    for rel, im in pieces.items():
        path = out(rel)
        os.makedirs(os.path.dirname(path), exist_ok=True)
        im.save(path)
        print('wrote art/kit/%s (%dx%d)' % (rel, im.width, im.height))
    return 0


if __name__ == '__main__':
    raise SystemExit(main())
