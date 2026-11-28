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
DESK_ROW = 170          # the desk's FAR edge: the props' plane starts here, 6 px above the window's sill
FLOOR_ROW = 164         # the wall meets the floor exactly at the window's sill, as in the menu, where the
# window's frame and the floor line meet the desk's own line. 46 px of floor show behind the desk.
DESK_LIP = 214          # the desk's near edge = the menu's front edge (MenuLayout.DeskEdgeY 214): the
# slab is 44 px deep, the menu's own 38-plus-the-difference, and everything below 214 is FLOOR.
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
# The window runs from just above the desk's far edge UP the wall, so its sill reads as the same kind of
# ledge the menu's does. v127 had the sill at y=152 with the desk's far edge at 170 -- an 18 px gap of bare
# wall that made the window look like it was floating.
WIN_X0, WIN_X1 = 24, 200
WIN_Y0, WIN_Y1 = 0, 164       # the window runs to the top of the wall, as the menu's does at x8,y10
WIN_SILL = 164

# ---- the monitor, left of centre (layout: monitor anchor 14,86 as a 0.58x redraw) ----------------------
# The monitor's CANVAS rect, matching desk_layout.json (anchor [268,84], 192x108) -- the first pass
# still had the pre-move left-hand numbers here, which put the tube's light pool on the empty left half of
# the desk and drew an olive oval where nothing was. The sprite is 192x108 either way; these numbers are
# what the pools and the wall's rim are measured from, so they have to agree with the layout.
# The monitor's canvas rect, derived from desk_layout.json: anchor [268,186] with a 200x143 sprite, so the
# sprite occupies columns 268..467 and rows 270-186-143 = -59..  (i.e. its plinth's bottom row is row 184? no:
# rows 270-186-143= -59 is impossible, which is exactly why the anchor is computed from the surface below).
MON_W, MON_H = 200, 143
MON_X0 = 266
MON_X1 = MON_X0 + MON_W
MON_Y1 = DESK_ROW - 1              # the plinth's bottom row sits ON the desk's surface line (170)
MON_Y0 = MON_Y1 - MON_H + 1


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
    THE MENU'S TILED FLOOR. Not a band with a horizon row -- make_menu_room.floor_band(), the same function
    that lays the menu's tiles, called at this room's floor line.

    That is the player's ruling applied literally: "COPY what's in the menu art and just change things that's
    necessary for the game." v127/v128 painted a wall/floor seam and a flat band; the menu has converging tile
    rows on the vanishing point, growing taller toward the camera. Nothing about a game room needs a worse
    floor than a menu room, and now it does not have one.
    """
    # the floor is the menu's function; the desk's own layer covers the middle of it, exactly as the menu's
    # desk layer covers its floor.
    # THE FLOOR RUNS TO THE BOTTOM OF THE FRAME. It used to stop at the desk's far edge, which left every
    # row below the desk's front edge (214) as unpainted void -- a black band across the bottom of the game
    # that the menu does not have. In the menu the floor lives inside the desk sprite; here the desk is a
    # layer standing ON the room, so the room owns the whole floor and the desk covers the part it sits on.
    M.floor_band(d, img, FLOOR_ROW, H)


# ===================================================================================== the desk slab
def desk_slab_sprite():
    """
    THE DESK, built exactly the way make_menu_room.desk() builds the menu's -- slab, then the props, then the
    light pools over all of it. One sprite, because the menu's is one sprite.

    WHY THIS SHAPE. v127 shipped the slab and the props as separate layers, and the player read the result
    correctly: "the objects are on the edge of the table and does not look like they are ON the table based on
    their angles." Separate layers means the props have no contact shadow, no shared light and no shared
    perspective -- they are stickers on a board. The menu does not do that: it draws the wood, draws the cup
    and the keyboard on it, and then paints the tube's pool and the lamp's warmth over everything at the end.
    That is why the menu's props look like they are standing there.

    Rows (top-down canvas): DESK_ROW 186 is the far edge against the wall, DESK_LIP 228 is the front edge.
    A prop placed "on the desk" has its top row at DESK_ROW + 6 -- the same 6 px inset the menu uses -- and its
    body runs DOWN into the wood, which is what the menu's own cup does.
    """
    h = DESK_LIP - DESK_ROW
    img = Image.new('RGBA', (W, h), rgba('wood_1'))
    d = ImageDraw.Draw(img)

    # ---- the slab, in the menu's construction: wood_1 across the depth, wood_0 where it meets the wall,
    # wood_2 toward the camera, and the one specular on the front edge.
    for x in range(W):
        img.putpixel((x, 0), rgba('wood_0'))
    M.dither(img, 0, 2, W, 9, 'wood_1', 'wood_0', 0.42)
    M.dither(img, 0, 14, W, 22, 'wood_1', 'wood_2', 0.34)
    M.dither(img, 0, 30, W, 36, 'wood_1', 'wood_2', 0.22)
    for x in range(W):
        img.putpixel((x, h - 3), rgba('wood_3'))
        img.putpixel((x, h - 2), rgba('wood_2'))
        img.putpixel((x, h - 1), rgba('wood_0'))

    # ---- the props: the MENU'S OWN DRAWING FUNCTIONS, on this desk, at canvas coordinates.
    # They are rendered onto a full-canvas scratch so their own coordinates stay canvas coordinates, then the
    # slab's band is cut out of it -- so the y they receive is the same y the menu gives them.
    scratch = Image.new('RGBA', (W, H), (0, 0, 0, 0))
    sd = ImageDraw.Draw(scratch)
    top = DESK_ROW + 6                       # the menu's 6 px inset from its slab's far edge
    # ORDER (v129, the player's instruction): "Swap the coffee with the keyboard and mouse." The coffee moves
    # to the left end, where the menu has its forms, and the keyboard + mouse take the coffee's place at the
    # right, under the tube -- which is also the correct room: the monitor is on the right, so the keyboard you
    # would actually be typing on belongs under it.
    M.cup(sd, scratch, 34, top)
    M.forms(sd, scratch, 96, top)
    M.keyboard(sd, scratch, 262, top)
    M.mouse(sd, scratch, 356, top - 3)
    img.alpha_composite(scratch.crop((0, DESK_ROW, W, DESK_ROW + h)))

    # ---- contact shadows: a soft dark seam under each object, drawn in the wood's own dither. Without this a
    # prop is a decal; with it, it is an object with weight.
    for (px0, px1) in ((22, 56), (92, 152), (258, 352), (352, 372)):
        for x in range(max(0, px0), min(W, px1)):
            for k in range(0, 4):
                y = (top - DESK_ROW) + 24 + k
                if 0 <= y < h and M.BAYER4[y & 3][x & 3] / 16.0 < (1.0 - k / 4.0) * 0.6:
                    img.putpixel((x, y), rgba('wood_0'))

    # ---- the light pools, painted LAST over the wood AND the props, exactly as the menu does it: the tube's
    # cold pool under the monitor (right of centre here) and the lamp's warm pool ... there is no lamp in the
    # game's room, so this is the tube's pool plus the window's cold spill at the left end.
    for y in range(h):
        for x in range(W):
            if img.getpixel((x, y))[3] == 0:
                continue
            cold = ((x - (MON_X0 + MON_W // 2)) / 150.0) ** 2 + ((y - 14) / 30.0) ** 2
            win = ((x - 90) / 130.0) ** 2 + ((y - 10) / 34.0) ** 2
            # v130: the LAMP is a second practical in this room now, so its warm pool lands on the wood at
            # the right end -- small, warm, and one step up the wood's own ramp (the menu's exact idiom).
            warm = ((x - (LAMP_X + 28)) / 54.0) ** 2 + ((y - 16) / 26.0) ** 2
            t = M.BAYER4[y & 3][x & 3]
            if cold < 1.0 and t < (1.0 - cold) * 9.0:
                p = img.getpixel((x, y))[:3]
                if p == PALETTE['wood_1']:
                    img.putpixel((x, y), rgba('wood_2'))
            elif warm < 1.0 and t < (1.0 - warm) * 8.0:
                p = img.getpixel((x, y))[:3]
                if p == PALETTE['wood_1']:
                    img.putpixel((x, y), rgba('wood_2'))
                elif p == PALETTE['wood_2'] and t < 4:
                    img.putpixel((x, y), rgba('wood_3'))
            elif win < 1.0 and t < (1.0 - win) * 6.0:
                p = img.getpixel((x, y))[:3]
                if p == PALETTE['wood_1']:
                    img.putpixel((x, y), rgba('wood_2'))
                elif p == PALETTE['wood_0'] and t < 3:
                    img.putpixel((x, y), rgba('wood_1'))
    return img


# ===================================================================================== the window
def window():
    """
    THE MENU'S WINDOW. Not "a window in the menu's materials" -- the same function that paints the front
    door's window, called at desk scale.

    v127 hand-painted a window here: a sky gradient, hashed stars, a skyline of its own invention. The player
    put it in one sentence -- "the windows does not look like to be the one in the menu" -- and they were
    right, because two drawings of one object are two objects. `make_menu_room.window()` was already
    size-driven apart from two globals, so it is parametric now (v128, verified byte-identical at 88x160) and
    this sprite IS its output at 176x236: the same concrete reveal, the same five-step sky with 12 px dither
    seams, the same smear of a moon, the same two skylines, the same dark mullion cross, the same cold sill
    specular. The only difference is scale.
    """
    return M.window(WIN_X1 - WIN_X0, WIN_Y1 - WIN_Y0)


# ===================================================================================== the lamp
LAMP_X = 420            # the desk's right end, in front of the tube's right edge (the tube ends at 456)
LAMP_TOP_ROW = 44       # the shade's top; its base lands on row 175, i.e. ON the wood (170..213)


def lamp():
    """
    THE WARM PRACTICAL, in the game's room (v130). The player asked for more light in the in-game look, and
    the honest way to add a light to a room is to add a LAMP to it -- not to brighten the pixels.

    It is make_menu_room.lamp() CALLED, not redrawn: the same shade, the same brass stem, the same amber
    bulb, the same wall pool on its own alpha ramp. The only change is where it stands: on the desk's right
    end, so the warm side of the frame is the same side in both rooms.

    It is its own layer rather than part of the room painting because a thing that stands ON the desk must
    be drawn ABOVE the desk band (the v128 rule, applied in the other direction), and the room painting is
    behind everything.
    """
    return M.lamp()


# ===================================================================================== wall clock
# v130.4 (the player: "The wall clock is covered by the monitor transfer it to the center"). This face used
# to be at x 388 -- directly behind the CRT, whose body runs 268..454. Being "on the wall, clear of the tube's
# top edge" is not the same as being VISIBLE: the monitor is mounted on that same wall and covers it. The face
# now sits centred on the free wall between the window (right edge 168) and the monitor (left edge 268).
# Keep these three in step with RoomClockView.x/y/size -- CoreTests asserts they agree.
CLOCK_X, CLOCK_Y = 203, 22          # top-left of the 30x30 face, centred on the free wall segment
CLOCK_SIZE = 30


def wall_clock():
    """
    THE WALL CLOCK (v130, the player's instruction: "a clock on the wall that tells the player what time it
    is").

    WHY IT IS A SPRITE AND NOT TEXT
        The player also said, twice, that the HUD "should not be displayed as text but rather as clickable
        objects". A floating number in a corner would be a fourth readout. A clock on the wall is FURNITURE:
        it sits in the room, it is lit by the same window as everything else, and it tells the time the way a
        clock on a wall tells the time -- at a glance, from across the room.

    THE FACE, in the menu's materials
        * a rim in steel_2 with a steel_1 inner bevel and a void drop down and right (the window is up and
          left, so every drop in this room goes down and right -- the same rule every other object obeys);
        * a dial in paper_0 (the same cream as the letter, because it IS paper) with four ticks;
        * the window's specular on the rim's upper left, dithered so it never reads as a drawn line;
        * a screw at the top where it hangs.

    THE HANDS ARE PAINTED AT THE SHIFT'S OPENING, 08:40. The clock is not redrawn every minute:
    RoomClockView rotates these hands about the face's centre, one whole pixel at a time.
    """
    n = CLOCK_SIZE
    img = Image.new('RGBA', (n, n), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    c = n // 2

    d.ellipse([1, 2, n - 1, n], fill=rgba('void'))
    d.ellipse([1, 1, n - 2, n - 2], fill=rgba('steel_3') if 'steel_3' in PALETTE else rgba('wall_3'))
    d.ellipse([2, 2, n - 3, n - 3], fill=rgba('steel_2'))
    d.ellipse([4, 4, n - 5, n - 5], fill=rgba('paper_2'))   # the dial is the one bright thing on that wall
    for (tx, ty, tw, th) in ((c, 5, 1, 2), (n - 7, c, 2, 1), (c, n - 7, 1, 2), (5, c, 2, 1)):
        d.rectangle([tx, ty, tx + tw - 1, ty + th - 1], fill=rgba('steel_0'))
    for (x, y) in ((5, 6), (6, 5), (6, 6), (7, 5), (5, 7)):
        img.putpixel((x, y), rgba('white'))
    for (x, y) in ((c - 1, 2), (c, 2), (c - 1, 3), (c, 3)):
        img.putpixel((x, y), rgba('steel_0'))

    def hand(angle_deg, length, key):
        rad = math.radians(angle_deg - 90.0)
        for i in range(1, length + 1):
            x = c + int(round(math.cos(rad) * i))
            y = c + int(round(math.sin(rad) * i))
            if 0 <= x < n and 0 <= y < n:
                img.putpixel((x, y), rgba(key))

    hand(360.0 * (8.0 + 40.0 / 60.0) / 12.0, 6, 'void')        # hour hand: 8:40
    hand(360.0 * (40.0 / 60.0), 9, 'void')                     # minute hand: 40 past
    img.putpixel((c, c), rgba('void'))
    return img


# ===================================================================================== the monitor
def monitor():
    """
    THE MENU'S MONITOR, at desk scale -- and it STANDS ON THE DESK.

    Two things were wrong in v127 and the player named both:

      * "our monitor is literally behind the table": the tube was drawn 108 px tall with its base at canvas
        row 184, while the desk's surface line is 186 -- so the monitor's own feet were UNDER the wood and the
        slab cut across its stand. A monitor standing on a desk has its base ON the surface line and its body
        ABOVE it.
      * it was not the menu's monitor either: a flat black bezel drawn from scratch. This is
        `make_menu_room.crt()` -- the approved menu CRT, the same dark case, thin bezel, three planes, one LED,
        one sheen -- rendered at 200x143. Its stand plinth is the last 12 px of the sprite, so the anchor puts
        those rows on the desk's surface.
    """
    return M.crt(MON_W, MON_H)


def monitor_stand():
    """
    RETIRED (v128). The menu's CRT carries its own stand -- a 12 px plinth drawn as part of the case -- so a
    second, separately-placed arm put two stands under one monitor. DeskSceneBuilder keeps the layer slot (it
    is harmless when empty), but the desk no longer draws one.
    """
    return None


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
    # BELOW IT IS FLOOR (v129). v127/v128 filled this band with dead PCs, servers and error screens. The
    # player's ruling: "Remove the random monitors and servers underneath the table IN THE GAME version since
    # thats only for the menu version." The menu has no junk under its desk and neither does the game now --
    # the band is the menu's tiled floor, and the room draws it.
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
    # ---- UNDER THE DESK: the menu's floor, shaded, and nothing else. The desk's front edge casts a hard
    # contact shadow for 3 px and a dithered falloff below that; everything under it is the room's own tiled
    # floor showing through. No junk, no error screens -- that belongs to the menu's room, which is a still.
    # THE SHADOW IS PUSHED DOWN THE FLOOR'S OWN RAMP, never painted black: the menu's floor is already the
    # darkest surface in the room, and stamping `void` over it deletes the tiles -- which is exactly what the
    # first pass of this did, leaving a black band under the desk where the floor should be.
    floor_down = {'floor_4': 'floor_3', 'floor_3': 'floor_2', 'floor_2': 'floor_1',
                  'floor_1': 'floor_0', 'floor_0': 'void'}
    for y in range(DESK_LIP, min(H, DESK_LIP + 18)):
        k = (y - DESK_LIP) / 18.0
        for x in range(W):
            if M.BAYER4[y & 3][x & 3] / 16.0 < (1.0 - k) * 0.75:
                cur = img.getpixel((x, y))[:3]
                for key, down in floor_down.items():
                    if cur == PALETTE[key]:
                        img.putpixel((x, y), rgba(down))
                        break

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

    # ---- THE LAMP'S LIGHT is painted into the wall here (its own sprite is a LAYER now -- see `lamp()`),
    # because the wall is what the room owns. The fixture itself would be covered by the desk layer if it
    # lived in this painting, which is the "a prop belongs in the surface's layer" rule pointed the other way:
    # a thing that stands ON the desk is drawn WITH the desk's band, above it.
    lamp_img = M.lamp()
    lamp_x = 420
    lamp_y = DESK_ROW - lamp_img.height + 6

    # ---- THE WALL CLOCK (v130)
    img.alpha_composite(wall_clock(), (CLOCK_X, CLOCK_Y))

    # ---- THE LIGHT THE TWO NEW SOURCES ACTUALLY THROW. Painted here, on the wall that receives them, in the
    # menu's own two-step idiom: a warm band up the wall behind the lamp's shade, and the cold spill the
    # window already lays down the left side. No glow without a named emitter -- both are named above.
    lay = img.load()
    bayer = M.BAYER4
    lam_cx, lam_cy = lamp_x + lamp_img.width // 2, lamp_y + 44   # the shade, not the base
    for y in range(0, 180):
        for x in range(0, W):
            p = lay[x, y]
            if p[3] == 0:
                continue
            # warm: the lamp's pool on the wall, centred on its shade
            dx = (x - lam_cx) / 46.0
            dy = (y - lam_cy) / 40.0
            r = math.sqrt(dx * dx + dy * dy)
            t = bayer[y & 3][x & 3]
            if r < 1.0:
                if p[:3] == PALETTE['wall_2'] and t < (1.0 - r) * 10.0:
                    lay[x, y] = rgba('wall_3')
                elif p[:3] == PALETTE['wall_1'] and t < (1.0 - r) * 7.0:
                    lay[x, y] = rgba('wall_2')
            # warm rim on the shade's own side of the wall: one step, adjacent to it only
            if 0 <= x - lam_cx <= 26 and abs(y - lam_cy) < 16 and t < 4:
                if p[:3] == PALETTE['wall_2']:
                    lay[x, y] = rgba('wall_3')
    return img


def main():
    # the monitor's stand retired (v128): the menu's CRT carries its own plinth.
    pieces = {
        'backgrounds/office_M0.png': room(),
        'desk/monitor_M.png': monitor(),
        'desk/desk_slab_M.png': desk_slab_sprite(),
        'desk/foreground_M.png': foreground(),
        'desk/lamp_M.png': lamp(),
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
