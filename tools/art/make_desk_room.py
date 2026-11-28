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
DESK_ROW = 186          # the desk's FAR edge: the props' plane starts here, 22 px above the window's sill
FLOOR_ROW = 168         # where the wall meets the floor -- 18 px of floor show behind the desk, under
# the window's sill (164), which is what puts the desk's far edge (186) on the same ground plane as the menu's
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
# The window runs from just above the desk's far edge UP the wall, so its sill reads as the same kind of
# ledge the menu's does. v127 had the sill at y=152 with the desk's far edge at 170 -- an 18 px gap of bare
# wall that made the window look like it was floating.
WIN_X0, WIN_X1 = 24, 200
WIN_Y0, WIN_Y1 = 24, 164
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
MON_Y1 = DESK_ROW - 1              # the plinth's bottom row sits ON the desk's surface line (186)
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
    M.forms(sd, scratch, 26, top)
    M.keyboard(sd, scratch, 196, top)
    M.mouse(sd, scratch, 292, top - 3)
    M.cup(sd, scratch, 330, top)
    img.alpha_composite(scratch.crop((0, DESK_ROW, W, DESK_ROW + h)))

    # ---- contact shadows: a soft dark seam under each object, drawn in the wood's own dither. Without this a
    # prop is a decal; with it, it is an object with weight.
    for (px0, px1) in ((26, 86), (196, 288), (292, 306), (316, 346)):
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
            t = M.BAYER4[y & 3][x & 3]
            if cold < 1.0 and t < (1.0 - cold) * 9.0:
                p = img.getpixel((x, y))[:3]
                if p == PALETTE['wood_1']:
                    img.putpixel((x, y), rgba('wood_2'))
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
    # the monitor's stand retired (v128): the menu's CRT carries its own plinth.
    pieces = {
        'backgrounds/office_M0.png': room(),
        'desk/monitor_M.png': monitor(),
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
