#!/usr/bin/env python3
"""
make_applicant_M.py -- the person across the desk: HALF BODY, in the menu's room. ONE painter, fourteen faces.

--------------------------------------------------------------------------------------------
v130.2 -- WHY THIS FILE WAS REWRITTEN (BUG-032, the player's "the applicant's art seems to have
reverted back to the older version")
--------------------------------------------------------------------------------------------
    It had. There were TWO applicant painters in tools/art:

        make_applicants.py    the older one (waist-up bust with fists on a drawn desk edge)
        make_applicant_M.py   the v127 one, approved after four rounds of "he still looks the same"

    Both wrote the SAME filenames. In the v130 round I ran make_applicants.py to add skin-tone variety to
    the palette, and it overwrote every strip with the older drawing -- desk edge, fists, cartoon orange.
    Nothing failed, because both painters satisfy the palette gate and both produce a valid 4-cell strip.
    That is the whole lesson: TWO GENERATORS OF ONE FILE IS A BUG WAITING FOR SOMEONE WHO IS IN A HURRY.

    So this file is now the only applicant painter. make_applicants.py refuses to paint and says so. The
    variant table below is the single list of who exists.

--------------------------------------------------------------------------------------------
THE PLAYER'S TWO INSTRUCTIONS IN THE SAME MESSAGE
--------------------------------------------------------------------------------------------
    "Applicant's art seems to have reverted back to the older version"
        -> restore the v127 bust: half body, three planes on the face, no drawn desk, no fists.

    "can you add make the new applicant art to be half body rather than torso and up"
        -> HALF BODY is now the form: it is cut at the chest by the frame's bottom edge, the desk is the
           room's own slab in front of him, and the only things in the sprite are head, neck, shoulders,
           chest, lanyard and badge. Nothing is drawn ON the desk any more, which is also why the "hands on
           the desk" tell moved to the CUFF (see TRAITS below).

    The head is the same measured skull as v127 -- the half-width table below is the portrait, and it does
    not change between variants. What changes is identity: skin, hair, glasses, facial hair, build, badge
    side, earring, age. What ALSO changes, and matters more, is the TELL: each synthetic carries one drawing
    defect, and the same defect must never appear on a human variant.

--------------------------------------------------------------------------------------------
THE FOUR FRAMES ARE FOUR DRAWINGS, NOT ONE COPIED FOUR TIMES
--------------------------------------------------------------------------------------------
    v127 wrote the same bust into all four cells, so the idle did nothing -- which quietly disabled the one
    mechanic that lives in the animation (`no_blink`: a synthetic that never blinks). The frames are now
        neutral | breathe (chest and shoulders rise 1 px) | fidget (a shoulder drops, the lanyard swings)
        | blink (the lids close -- UNLESS the variant carries `no_blink`)
    and a gate asserts that frames 0 and 3 differ, and that a `no_blink` variant differs in a different way.

--------------------------------------------------------------------------------------------
USAGE
--------------------------------------------------------------------------------------------
    python tools/art/make_applicant_M.py              # write all 14 strips + 14 busts + 14 portraits
    python tools/art/make_applicant_M.py --check      # palette gate only, writes nothing
    python tools/art/make_applicant_M.py --one human_solid_a --png /tmp/a.png    # one cell, for review
"""
import argparse
import os
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
sys.path.insert(0, os.path.join(ROOT, 'tools', 'art'))

from PIL import Image          # noqa: E402

import make_menu_room as M     # noqa: E402  (rgba / BAYER4 -- the room's own dither)
from menu_palette import PALETTE  # noqa: E402

W, H = 192, 216
CX = 96                     # the bust's centre line
CELLS = 4                   # the idle's four frames


def rgba(key):
    return M.rgba(key)


def put(img, x, y, key):
    """putpixel with bounds: every coordinate below is hand-placed, and a stray pixel must not abort a build."""
    if 0 <= int(x) < W and 0 <= int(y) < H:
        img.putpixel((int(x), int(y)), rgba(key))


def bayer(x, y):
    return M.BAYER4[int(y) & 3][int(x) & 3] / 16.0


def out(rel):
    return os.path.join(ROOT, 'art', 'kit', rel)


# ===================================================================================== the skull
# Rows are measured DOWN from the top of the head. Half-widths in px, from the centre line. These numbers
# are the portrait: the brow is the widest part of the upper skull, the cheekbone is the widest part of the
# middle face, and the jaw narrows to the chin over the last 20 rows. Everything else is shading.
CROWN_Y = 14          # where the hair's silhouette starts
HAIRLINE = 40         # the fringe's edge in the middle of the forehead
BROW_Y = 54
EYE_Y = 62
NOSE_Y = 82
MOUTH_Y = 94
CHIN_Y = 108
NECK_Y = 132          # where the collar takes over
BODY_TOP = CHIN_Y + 20   # 128 -- the chest starts here and the frame cuts it at 216: HALF BODY

HALF = {
    14: 8, 16: 18, 18: 25, 20: 30, 24: 34, 30: 36, 38: 37, 46: 37, 54: 37,
    62: 36, 70: 35, 78: 33, 84: 31, 90: 30, 96: 29, 100: 27, 104: 22, 108: 10,
}


def half_at(y):
    keys = sorted(HALF)
    if y < keys[0] or y > keys[-1]:
        return 0
    for i in range(len(keys) - 1):
        a, b = keys[i], keys[i + 1]
        if a <= y <= b:
            t = (y - a) / float(b - a)
            return HALF[a] + (HALF[b] - HALF[a]) * t
    return 0


# ===================================================================================== the cast
#
# ONE LIST OF WHO EXISTS. `Appearances.cs` mirrors these keys; `make_sprite_manifest.py` reads the files;
# `variety_every_variant_ships` holds the three together, so a key here with no PNG on disk fails the build
# instead of showing an empty chair.
#
# traits are the TELLS (docs/07). A human never carries one.
#   seam          a dead-straight line where the hair meets the background, no tufts
#   mirror        perfectly symmetric catchlights, and both eyes identical to the pixel
#   ear           a rectangular ear slab spanning hairline to jaw, no fold, no taper
#   glasses_merge glasses arms run into the temples with no hinge and no shadow
#   skin_band     a hard four-row posterised band across the jaw instead of a turned plane
#   cuffs         both cuffs are one solid block, identical, with no seam and no wrist gap
#   straight_badge the badge hangs machine-level, its strap ends in one clean straight cut
#   shadow        NO contact shadow: the neck floats off the collar with nothing under the jaw
#   no_blink      the eyes never close, so frame 3 is the same as frame 0
VARIANTS = {
    # ---------------- humans: identity variety, no tells
    'human_solid_a':     dict(seed=3,   syn=False, traits=(), female=False, tone='warm',  hair=0, style=0, outfit='suit'),
    'human_solid_b':     dict(seed=17,  syn=False, traits=(), female=False, tone='light', hair=1, style=1, outfit='sweater'),
    'human_solid_c':     dict(seed=58,  syn=False, traits=(), female=True,  tone='dark',  hair=2, style=2,
                              glasses=True, age=True, outfit='blouse'),
    'human_nervous_a':   dict(seed=29,  syn=False, traits=(), female=True,  tone='tan',   hair=2, style=3, outfit='shirt'),
    'human_nervous_b':   dict(seed=71,  syn=False, traits=(), female=True,  tone='light', hair=0, style=7, outfit='jacket'),
    'human_bait_a':      dict(seed=41,  syn=False, traits=(), female=False, tone='dark',  hair=2, style=0,
                              beard=2, outfit='coat'),
    'human_bait_b':      dict(seed=86,  syn=False, traits=(), female=False, tone='warm',  hair=1, style=8,
                              beard=1, outfit='suit'),
    # ---------------- synthetics: each carries a specific tell set. They span the tones too: a tell must
    # never be "the dark ones", which would teach the wrong lesson and read as a slur on the cast.
    'synthetic_polished_a':  dict(seed=101, syn=True, traits=('seam', 'mirror'), female=False, tone='tan',
                                  hair=2, style=1, outfit='suit'),
    'synthetic_polished_b':  dict(seed=131, syn=True, traits=('cuffs', 'skin_band'), female=True, tone='light',
                                  hair=0, style=3, outfit='blouse'),
    'synthetic_polished_c':  dict(seed=163, syn=True, traits=('glasses_merge', 'mirror'), female=False,
                                  tone='dark', hair=1, style=0, glasses=True, outfit='suit'),
    'synthetic_cloned_a':    dict(seed=113, syn=True, traits=('ear', 'shadow'), female=True, tone='warm',
                                  hair=2, style=2, outfit='blouse'),
    'synthetic_cloned_b':    dict(seed=149, syn=True, traits=('cuffs', 'seam'), female=False, tone='tan',
                                  hair=2, style=0, outfit='sweater'),
    'synthetic_collusion_a': dict(seed=127, syn=True, traits=('straight_badge', 'mirror'), female=True,
                                  tone='light', hair=1, style=2, outfit='shirt'),
    'synthetic_collusion_b': dict(seed=173, syn=True, traits=('shadow', 'no_blink'), female=False, tone='dark',
                                  hair=2, style=6, outfit='coat'),
}

# ===================================================================================== the wardrobe
# v130.4 (the player: "Btw varieties for the applicants outfit and look").
#
# Until now every one of the fourteen variants wore the SAME grey suit: paint_body wrote 'wall_2' for the
# shoulders, 'steel_1' for collar and cuffs, and the lanyard was always cyan. Fourteen people, one uniform.
# That is the "a variant table is not variety" bug (H6 in the orders) wearing clothes.
#
# A garment is a PLACE on a ramp, never a colour table, so a jacket still lights correctly under the desk
# lamp and still passes the one-palette gate:
#   jacket  shoulders + chest          `site`: the shoulder/chest ramp
#   collar  collar + cuffs + placket   one step lighter than the jacket
#   strap   the lanyard                `site`: the badge's own accent
#   shirt   bare V of shirt under it   light, because a shirt is the lightest thing in a wardrobe
#
# Six looks over fourteen people. Humans get all six; synthetics share them, because a synthetic is BUILT to
# pass, so on average it dresses more conventionally than a human does -- that irony is the point.
OUTFITS = {
    'suit':  dict(jacket=('wall', 2),  collar=('steel', 1), shirt=('steel', 2), strap=('cyan', 1), tie_mask=False),
    'sweater': dict(jacket=('steel', 1), collar=('steel', 2), shirt=('paper', 1), strap=('cyan', 1), tie_mask=False),
    'shirt':  dict(jacket=('cup', 1),   collar=('cup', 2),   shirt=('paper', 0), strap=('hair', 2),  tie_mask=False),
    'blouse': dict(jacket=('paper', 1), collar=('paper', 2), shirt=('cup', 2),   strap=('red', 1),   tie_mask=False),
    'coat':   dict(jacket=('wood', 1),  collar=('wood', 2),  shirt=('paper', 1), strap=('amber', 2), tie_mask=False),
    'jacket': dict(jacket=('scr', 0),   collar=('scr', 1),   shirt=('steel', 2), strap=('cyan', 2),  tie_mask=False),
}

# hair colours, by ramp place. Black/brown/grey are three PLACES on the `hair` ramp, never three tables.
HAIR_KEYS = {
    0: ('hair_0', 'hair_1', 'hair_2'),      # black
    1: ('hair_0', 'hair_1', 'hair_2'),      # dark brown
    2: ('hair_1', 'hair_2', 'hair_3'),      # brown / greying
}

# THE COMPLEXION MAP (v130.2). `tone` names a PLACE, and the place is a ramp plus a shift up it:
#   light  -> skin steps 2..3   (the lightest family in the cast)
#   warm   -> skin steps 1..3   (the default)
#   tan    -> skin 0..2 and skin2 2..3     (the middle: warm brown)
#   dark   -> skin2 steps 1..3  (the deepest family; see menu_palette's note on why it needs its own ramp)
#
# v130.1's bug in one line: all four tones resolved into `skin`, so fourteen named variants were one
# complexion. A cast where everybody has the same skin is not a cast, it is a uniform.
TONES = {
    'light': ('skin', 1),
    'warm':  ('skin', 0),
    'tan':   ('skin2', 1),
    'dark':  ('skin2', 0),
}

# roles within a ramp: 0 shadow, 1 turned-away plane, 2 front plane, 3 key-lit plane. A tone's shift
# raises the whole set, so "light" and "warm" are the same family at different values, and dark skin keeps
# its own key light instead of being one flat brown.
def skin_keys(st):
    ramp, shift = TONES.get(st.get('tone', 'warm'), ('skin', 0))
    def k(role):
        return '%s_%d' % (ramp, max(0, min(3, role + shift)))
    return dict(dark=k(0), mid=k(2), lit=k(3), turned=k(1))


# ===================================================================================== the head
def paint_head(img, st, frame):
    """
    THE FACE: three flat planes, hard edges, and no dither across the skin.

    Every wrong draft before this one made the same mistake: reaching for the room's ordered dither to
    separate planes on a surface 74 px wide. Dither that makes a wall feel like a wall draws a dotted line
    down a face. Skin at this size is three values with hard edges; the value change IS the plane change.
    """
    breath = 1 if frame == 1 else 0
    dy = breath                      # the head rises a pixel on the in-breath
    A = st['traits']
    S = skin_keys(st)
    KEY, PLANE, TURN, SHADE = S['lit'], S['mid'], S['turned'], S['dark']

    band = (CHIN_Y - 6, CHIN_Y + 1) if 'skin_band' in A else None

    for y in range(CROWN_Y + dy, CHIN_Y + 1 + dy):
        hw = half_at(y - dy)
        if hw <= 0:
            continue
        for x in range(int(CX - hw), int(CX + hw) + 1):
            u = (x - CX) / max(1.0, hw)
            # the planes sit at 0.58, not 0.30: at 0.30 the lit edge ran down the middle of his forehead and
            # the shadow edge cut through his right eye -- two hard vertical stripes on one face.
            if u < -0.58:
                put(img, x, y, KEY)            # the window's side, in the key light
            elif u > 0.74:
                put(img, x, y, TURN)           # the far side, in the room's shadow
            else:
                put(img, x, y, PLANE)          # the front plane, lit by the tube
            if -0.62 < u < -0.54 and bayer(x, y) < 0.5:
                put(img, x, y, PLANE)
            elif 0.70 < u < 0.78 and bayer(x, y) < 0.5:
                put(img, x, y, PLANE)
            # TELL skin_band: one hard four-row step across the jaw, dead level, on every column
            if band and band[0] <= y - dy <= band[1] and -0.9 < u < 0.9:
                put(img, x, y, KEY)

    # ---- a cold rim on the far edge: without it the shadowed side of his head merges into the shadowed
    # wall behind him, and the whole bust loses its silhouette at game scale.
    for y in range(CROWN_Y + 10 + dy, CHIN_Y - 2 + dy):
        hw = half_at(y - dy)
        put(img, int(CX + hw), y, 'skinrim_0')
        if bayer(int(CX + hw), y) < 0.4:
            put(img, int(CX + hw - 1), y, 'skinrim_0')

    # ---- the jaw turns away over the last ten rows
    if 'skin_band' not in A:
        for y in range(CHIN_Y - 7 + dy, CHIN_Y + 1 + dy):
            hw = half_at(y - dy)
            for x in range(int(CX - hw), int(CX + hw) + 1):
                u = abs(x - CX) / max(1.0, hw)
                if u > 0.45 and bayer(x, y) < 0.6:
                    put(img, x, y, TURN)

    # ---- the brows: two short bars, angled down towards the nose. A full-width band is a headband.
    for side in (-1, 1):
        bx = int(CX + side * 17)
        tilt = 0 if (side > 0 or 'mirror' not in A) else 1     # humans are asymmetric; a synthetic is not
        for x in range(bx - 9, bx + 8):
            put(img, x, BROW_Y + dy - (tilt if x < bx else 0), 'wood_0')
            put(img, x, BROW_Y + 1 + dy - (tilt if x < bx else 0),
                'wood_0' if abs(x - bx) < 7 else TURN)

    # ---- the eyes: an almond 13 px wide, sclera-iris-sclera, a heavy upper lid, one catchlight.
    blink = (frame == 3) and ('no_blink' not in A)
    for side in (-1, 1):
        ex = int(CX + side * 17)
        for x in range(ex - 6, ex + 7):
            put(img, x, EYE_Y + dy, KEY)
            put(img, x, EYE_Y + 1 + dy, KEY)
        for x in range(ex - 5, ex + 6):
            put(img, x, EYE_Y + 2 + dy, PLANE)
        if blink:
            # a closed eye is one darker line with the lid's crease above it -- not an empty socket
            for x in range(ex - 5, ex + 6):
                put(img, x, EYE_Y + 1 + dy, SHADE)
                put(img, x, EYE_Y + 2 + dy, TURN)
            put(img, ex - 2, EYE_Y + 1 + dy, PLANE)
            put(img, ex + 2, EYE_Y + 1 + dy, PLANE)
        else:
            for x in range(ex - 2, ex + 2):
                put(img, x, EYE_Y + dy, 'ink')
                put(img, x, EYE_Y + 1 + dy, 'ink')
            # TELL mirror: the catchlight is on the same pixel on both sides, so both eyes are identical
            if 'mirror' in A:
                put(img, ex - 1, EYE_Y + dy, 'skinrim_1')
            else:
                put(img, ex - 1 if side < 0 else ex + 1, EYE_Y + dy, 'skinrim_1')
        for x in range(ex - 6, ex + 7):
            put(img, x, EYE_Y - 1 + dy, TURN)
            put(img, x, EYE_Y + 3 + dy, TURN)

    # ---- the nose: a shadow down the far side, a lit edge on the near side, a ball, two nostrils
    for y in range(EYE_Y + 8 + dy, NOSE_Y + dy):
        put(img, CX + 2, y, TURN)
        put(img, CX + 3, y, TURN)
        put(img, CX - 2, y, KEY)
    for x in range(CX - 4, CX + 5):
        put(img, x, NOSE_Y + dy, KEY)
    for x in range(CX - 3, CX + 4):
        put(img, x, NOSE_Y + 1 + dy, TURN)
    put(img, CX - 4, NOSE_Y + 1 + dy, SHADE)
    put(img, CX + 4, NOSE_Y + 1 + dy, SHADE)

    # ---- the mouth: shadow above, a straight line, a lit lower lip, a shadow under it.
    for x in range(CX - 11, CX + 12):
        put(img, x, MOUTH_Y - 1 + dy, TURN)
    for x in range(CX - 10, CX + 11):
        put(img, x, MOUTH_Y + dy, SHADE)
    for x in range(CX - 8, CX + 9):
        put(img, x, MOUTH_Y + 1 + dy, PLANE)
    for x in range(CX - 7, CX + 8):
        put(img, x, MOUTH_Y + 2 + dy, TURN)

    # ---- the cheekbone: one lit pixel where the plane turns, on the window's side
    put(img, CX - 24, 74 + dy, KEY)
    put(img, CX - 28, 88 + dy, PLANE)

    if st.get('beard'):
        paint_facial_hair(img, st, dy)
    if st.get('glasses'):
        paint_glasses(img, st, dy, blink)
    if st.get('age'):
        for x in range(CX - 22, CX - 14):
            if bayer(x, 76) < 0.6:
                put(img, x, 76 + dy, TURN)
        for x in range(CX + 12, CX + 21):
            if bayer(x, 78) < 0.6:
                put(img, x, 78 + dy, TURN)

    # ---- the ear: 6x14 flat against the skull, in shadow, one lit rim.
    ey = 66
    ex0 = int(CX - half_at(ey))
    if 'ear' in A:
        # TELL: one perfectly rectangular slab, uniform colour, spanning hairline to jaw, no fold
        for y in range(HAIRLINE + 8 + dy, CHIN_Y - 6 + dy):
            for x in range(ex0 - 3, ex0 + 2):
                put(img, x, y, PLANE)
        for x in range(ex0 - 3, ex0 + 2):
            put(img, x, HAIRLINE + 8 + dy, KEY)
    else:
        for y in range(ey - 5 + dy, ey + 11 + dy):
            for x in range(ex0 + 1, ex0 + 7):
                put(img, x, y, TURN)
        for y in range(ey - 3 + dy, ey + 9 + dy):
            put(img, ex0 + 3, y, SHADE)
        put(img, ex0 + 5, ey + 2 + dy, PLANE)
        if st.get('earring'):
            put(img, ex0 + 4, ey + 11 + dy, 'cyan_2')
            put(img, ex0 + 4, ey + 12 + dy, 'cyan_1')


def paint_facial_hair(img, st, dy):
    kind = st['beard']
    if kind in (1, 3):
        for y in range(MOUTH_Y - 7 + dy, MOUTH_Y - 4 + dy):
            for x in range(CX - 13, CX + 14):
                u = abs(x - CX) / 13.0
                if u < 0.95:
                    put(img, x, y, 'hair_1' if bayer(x, y) < 0.5 else 'hair_0')
    if kind in (2, 3):
        for y in range(CHIN_Y - 15 + dy, CHIN_Y + 1 + dy):
            hw = half_at(y - dy) - 1
            for x in range(int(CX - hw), int(CX + hw) + 1):
                u = abs(x - CX) / max(1.0, hw)
                # keep the mouth clear: a beard around a mouth, not over it
                if MOUTH_Y - 5 + dy <= y <= MOUTH_Y + 3 + dy and u < 0.62:
                    continue
                if y < CHIN_Y - 9 + dy and u < 0.5:
                    continue
                put(img, x, y, 'hair_1' if bayer(x, y) < 0.45 else 'hair_0')


def paint_glasses(img, st, dy, blink):
    A = st['traits']
    TURN = skin_keys(st)['turned']
    merge = 'glasses_merge' in A
    for side in (-1, 1):
        ex = int(CX + side * 17)
        x0, x1 = ex - 9, ex + 9
        y0, y1 = EYE_Y - 5 + dy, EYE_Y + 5 + dy
        for x in range(x0, x1 + 1):
            put(img, x, y0, 'steel_1')
            put(img, x, y0 + 1, 'steel_0' if not merge else 'steel_1')
        for y in range(y0, y1 + 1):
            put(img, x0, y, 'steel_1')
            put(img, x1, y, 'steel_0')
        for x in range(x0 + 1, x1):
            put(img, x, y1, 'steel_0')
        # the lens: one diagonal glint, and (human) a shadow on the cheek under the rim
        for i in range(4):
            put(img, x0 + 3 + i, y0 + 2 + i, 'steel_2')
        if not merge:
            for x in range(x0 + 2, x1 - 1):
                if bayer(x, y1 + 1) < 0.5:
                    put(img, x, y1 + 1, TURN)
        # the arm: TELL if it runs into the temple with no hinge and no shadow
        for x in range(x1 + 1, min(W, CX + int(half_at(EYE_Y)) + 3)):
            put(img, x, y0 + 3, 'steel_1')
    # bridge over the nose
    for x in range(CX - 6, CX + 7):
        put(img, x, EYE_Y - 4 + dy, 'steel_1')
    if merge:
        for x in range(CX - 6, CX + 7):
            put(img, x, EYE_Y - 3 + dy, 'steel_1')


def paint_hair(img, st, dy):
    """Hair is INSIDE the skull's silhouette and DARKER THAN THE SKIN -- one ramp, three places."""
    dark, mid, lit = HAIR_KEYS[st.get('hair', 0)]
    style = st.get('style', 0)
    A = st['traits']

    # NOTE (v130.2): styles 4 (headscarf) and 5 (beanie) were cut after the contact sheet showed both
    # reading as a dark CAP -- at 74 px of skull, cloth that follows the silhouette is indistinguishable
    # from a helmet, and a helmet on an interview chair is worse than no hat at all. The two identities they
    # carried are now 7 (low ponytail) and 8 (curls), drawn in the hair ramp like every other style, because
    # a material drawn in its own ramp cannot turn into a different material by accident.

    start = CROWN_Y + (6 if style == 6 else 0)      # style 6: receding -- the crown starts lower
    for y in range(start + dy, HAIRLINE + 5 + dy):
        hw = half_at(y - dy)
        if hw <= 2:
            continue
        for x in range(int(CX - hw) + 1, int(CX + hw)):
            u = (x - CX) / max(1.0, hw)
            line = HAIRLINE - 4 + int(12 * u * u) - (3 if abs(u) < 0.16 else 0)
            if style == 1:                           # side part: the fringe sweeps from the window side
                line += int(4 * max(0.0, u))
            if style == 3:                           # long: hangs past the ears
                line += 6
            if y > line:
                continue
            if u < -0.42:
                put(img, x, y, lit)
            elif u > 0.52:
                put(img, x, y, dark)
            else:
                put(img, x, y, mid)
    # sideburns: without them the fringe reads as a cap
    for y in range(HAIRLINE + dy, HAIRLINE + 20 + dy):
        k = (y - dy - HAIRLINE) / 20.0
        hw = half_at(y - dy) - int(5 * k)
        for side in (-1, 1):
            for i in range(3 - int(2 * k)):
                put(img, int(CX + side * (hw - i)), y, mid if i < 2 else dark)
    if style == 2:                                  # bun: a mass ABOVE the crown, not beside it
        for y in range(CROWN_Y - 12 + dy, CROWN_Y + 2 + dy):
            for x in range(CX - 11, CX + 12):
                t = (x - CX) / 11.0
                s = (y - (CROWN_Y - 5 + dy)) / 7.0
                if t * t + s * s <= 1.0:
                    put(img, x, y, mid if x < CX else dark)
    if style == 3:                                  # long: two falls down the shoulders
        for side in (-1, 1):
            for y in range(HAIRLINE + 10 + dy, BODY_TOP + 10):
                hw = half_at(min(y - dy, CHIN_Y)) + 1
                for i in range(4):
                    put(img, int(CX + side * (hw - i)), y, mid if i < 2 else dark)
    if style == 7:                                  # low ponytail: a mass at the nape, one side visible
        for y in range(CHIN_Y - 14 + dy, BODY_TOP + 14):
            t = (y - (CHIN_Y - 14)) / float(BODY_TOP + 14 - (CHIN_Y - 14))
            for x in range(int(CX + 24 - 3 * t), int(CX + 34 + 4 * t)):
                u = (x - (CX + 29)) / 6.0
                if abs(u) > 1.0:
                    continue
                put(img, x, y, mid if u < 0.1 else dark)
        put(img, int(CX + 27), CHIN_Y - 15 + dy, 'brass')      # the tie: one warm pixel
        put(img, int(CX + 28), CHIN_Y - 15 + dy, 'brass')
    if style == 8:                                  # curls: the silhouette itself is bumped
        for y in range(CROWN_Y + dy, HAIRLINE + 12 + dy):
            hw = half_at(y - dy)
            if hw <= 2:
                continue
            for x in range(int(CX - hw) - 1, int(CX + hw) + 2):
                u = (x - CX) / max(1.0, hw)
                if abs(u) > 1.0:
                    # an outline of bumps, not a smooth edge: this is what makes hair read as curls
                    if bayer(x * 3, y * 2) < 0.45:
                        put(img, x, y, dark)
                    continue
                if y > HAIRLINE - 4 + int(12 * u * u):
                    continue
                put(img, x, y, lit if u < -0.42 else (dark if u > 0.52 else mid))

    # the crown's specular: where the window catches him
    for i in range(4):
        put(img, int(CX - 15 + i * 4), CROWN_Y + 3 + dy, 'amber_1' if not A else lit)
    # TELL seam: a dead-straight line where the hair meets the background, no tufts, no variation
    if 'seam' in A:
        for x in range(int(CX - half_at(CROWN_Y + 4)) + 1, int(CX + half_at(CROWN_Y + 4))):
            put(img, x, CROWN_Y + 1 + dy, 'brass')


def paint_body(img, st, frame):
    """
    HALF BODY: head, neck, shoulders, chest, lanyard, badge. The frame cuts the chest at row 216, and the
    desk in front of him is the ROOM's slab (make_desk_room.desk_slab_M), never a drawn edge in the sprite.
    """
    A = st['traits']
    W = OUTFITS.get(st.get('outfit', 'suit'), OUTFITS['suit'])
    JACKET = '%s_%d' % W['jacket']
    COLLAR = '%s_%d' % W['collar']
    SHIRT = '%s_%d' % W['shirt']
    STRAP = '%s_%d' % W['strap']
    build = 42 + 6 * (st['seed'] % 3)                   # shoulder half-width: 42 / 48 / 54
    breath = 1 if frame == 1 else 0
    fidget = 1 if frame == 2 else 0
    top = BODY_TOP + breath

    # ---- the neck (under the head, so the jaw's shadow is drawn ON the neck)
    for y in range(CHIN_Y + 1 + (1 if frame == 1 else 0), top + 2):
        S = skin_keys(st)
        for x in range(CX - 13, CX + 14):
            u = (x - CX) / 13.0
            put(img, x, y, S['mid'] if u < -0.35 else S['turned'])
        for x in range(CX + 5, CX + 14):
            if bayer(x, y) < 0.5:
                put(img, x, y, S['dark'])
    if 'shadow' not in A:
        # CONTACT: the jaw's hard band of shadow on the top of the neck. Without it the head floats.
        for x in range(CX - 13, CX + 14):
            put(img, x, CHIN_Y + 1, skin_keys(st)['dark'])
            put(img, x, CHIN_Y + 2, skin_keys(st)['dark'])
    else:
        # TELL: no contact shadow at all -- the neck and the jaw are the same tone where they meet
        for x in range(CX - 13, CX + 14):
            put(img, x, CHIN_Y + 1, skin_keys(st)['turned'])

    # ---- the shoulders/chest: a wide trapezoid, cut by the frame at the bottom
    for y in range(top, H):
        t = min(1.0, (y - top) / 46.0)
        hw = int(build + 30 * t)
        if fidget:
            hw += 1 if y < top + 30 else 0
        for x in range(CX - hw, CX + hw + 1):
            put(img, x, y, JACKET)
        for x in range(CX - hw, CX - int(hw * 0.45)):
            if bayer(x, y) < 0.55:
                put(img, x, y, COLLAR)
        for x in range(CX + int(hw * 0.55), CX + hw + 1):
            if bayer(x, y) < 0.6:
                put(img, x, y, JACKET)   # the far shoulder: same garment, in the room's shadow

    # ---- the shirt: the bare V between the lapels. Draw it FIRST, then the lapels over it, so the V is a
    # shape cut out of the jacket rather than a stripe painted on it.
    for y in range(top + 2, H):
        t = (y - top) / float(H - top)
        vw = max(2, int(11 - 7 * t))                    # the V narrows as it goes down
        for x in range(CX - vw, CX + vw + 1):
            put(img, x, y, SHIRT)

    # ---- the collar: two wedges off the neck, in the garment's own lighter step
    for y in range(top, top + 14):
        w = 12 + (y - top) // 2
        for x in range(CX - w, CX - 7):
            put(img, x, y, COLLAR)
        for x in range(CX + 8, CX + w + 1):
            put(img, x, y, COLLAR)
    for y in range(top + 14, H):
        for x in range(CX - 6, CX + 7):
            put(img, x, y, COLLAR)

    # ---- the cuffs, at the frame's bottom edge: the tell that replaced "fists on the desk"
    cuff_y = H - 16
    for side in (-1, 1):
        cw = 15
        cx0 = CX + side * int(build * 0.78)
        for y in range(cuff_y, H):
            for x in range(cx0 - cw, cx0 + cw):
                put(img, x, y, COLLAR if 'cuffs' not in A else JACKET)
        if 'cuffs' not in A:
            for x in range(cx0 - cw, cx0 + cw):        # a human cuff has a seam and a wrist gap
                put(img, x, cuff_y, SHIRT)
            put(img, cx0 - cw, cuff_y, 'wall_0')
            put(img, cx0 + cw - 1, cuff_y, 'wall_0')
        else:
            # TELL: one solid block, identical on both sides, no seam, no wrist gap
            for x in range(cx0 - cw, cx0 + cw):
                put(img, x, cuff_y, JACKET)

    # ---- the lanyard: two straps, cyan with a lit edge
    swing = 1 if frame == 2 else 0
    for side in (-1, 1):
        for i in range(0, 46):
            y = top + 10 + i
            x = int(CX + side * (10 + i * 0.06) + (swing if side > 0 else 0))
            put(img, x, y, STRAP)
            ramp_stem = STRAP.rsplit('_', 1)[0]
            put(img, x + side, y, '%s_%d' % (ramp_stem, min(4, int(STRAP.rsplit('_', 1)[1]) + 1)))

    # ---- the badge: 27x22, hung at the straps' meeting point
    bx, by = CX - 13, top + 56 - (1 if fidget else 0)
    if 'straight_badge' in A:
        by = top + 56                                  # machine level, never off by a pixel
    for y in range(by, by + 22):
        for x in range(bx, bx + 27):
            put(img, x, y, 'paper_1')
    for x in range(bx, bx + 27):
        put(img, x, by, 'paper_2')
        put(img, x, by + 21, 'void' if 'straight_badge' not in A else 'wall_0')
    for y in range(by, by + 22):
        put(img, bx, y, 'paper_2')
        put(img, bx + 26, y, 'void')
    # the photo: a small head on a wall ground
    for y in range(by + 3, by + 15):
        for x in range(bx + 3, bx + 12):
            put(img, x, y, 'wall_2')
    for y in range(by + 4, by + 9):
        for x in range(bx + 4, bx + 11):
            put(img, x, y, 'amber_1')
    for y in range(by + 9, by + 14):
        for x in range(bx + 5, bx + 10):
            put(img, x, y, skin_keys(st)['turned'])
    for y in (by + 6, by + 10, by + 14):
        for x in range(bx + 14, bx + 24):
            put(img, x, y, 'paper_0')


def build(key):
    """One variant, four frames, in paint order: body (behind), head, hair (front)."""
    st = VARIANTS[key]
    if st.get('earring') is None:
        st['earring'] = st['seed'] % 4 == 1
    frames = []
    for frame in range(CELLS):
        img = Image.new('RGBA', (W, H), (0, 0, 0, 0))
        paint_body(img, st, frame)
        paint_head(img, st, frame)
        paint_hair(img, st, frame)
        frames.append(img)
    return frames


# ===================================================================================== gates
def palette_gate(images):
    allowed = set()
    for _key, value in PALETTE.items():
        allowed.add(tuple(value) if isinstance(value, tuple) else value)
    bad, off = 0, []
    for rel, im in images:
        for y in range(im.height):
            for x in range(im.width):
                p = im.getpixel((x, y))
                if p[3] == 0:
                    continue
                if tuple(p[:3]) not in allowed:
                    bad += 1
                    if len(off) < 6:
                        off.append((rel, x, y, p[:3]))
    return bad, off


def frame_gate(strips):
    """The idle must be four DIFFERENT drawings, and no_blink must differ from a blink in the right place."""
    problems = []
    for key, frames in strips.items():
        no_blink = 'no_blink' in VARIANTS[key]['traits']
        if frames[0].tobytes() == frames[3].tobytes() and not no_blink:
            problems.append(key + ': frame 3 is identical to frame 0 (the blink is not drawn)')
        if no_blink and frames[0].tobytes() == frames[3].tobytes():
            problems.append(key + ': a no_blink variant must still DIFFER from frame 0 somewhere -- '
                                   'otherwise "never blinks" and "the animator is broken" look the same')
        if frames[1].tobytes() == frames[0].tobytes():
            problems.append(key + ': frame 1 does not breathe')
    return problems


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('--check', action='store_true')
    ap.add_argument('--one', default='')
    ap.add_argument('--png', default='')
    ap.add_argument('--contact-sheet', action='store_true',
                    help='all variants side by side -- LOOK at this, do not count rows (orders/09 H2)')
    args = ap.parse_args()

    if args.contact_sheet:
        keys = sorted(VARIANTS)
        cols = 7
        rows = (len(keys) + cols - 1) // cols
        sheet = Image.new('RGB', (cols * W, rows * H), (10, 10, 14))
        for i, key in enumerate(keys):
            cell = build(key)[0]
            sheet.paste(cell, ((i % cols) * W, (i // cols) * H), cell)
        dest = args.png or os.path.join(ROOT, 'docs', 'images', 'v130', 'applicant_variants.png')
        sheet.save(dest)
        print('wrote %s (%d variants, %dx%d)' % (dest, len(keys), sheet.width, sheet.height))
        return 0

    if args.one:
        one = args.one if args.one in VARIANTS else 'human_solid_a'
        cell = build(one)[0]
        dest = args.png or os.path.join('/tmp', 'applicant_%s.png' % one)
        cell.save(dest)
        print('wrote %s (%dx%d)' % (dest, cell.width, cell.height))
        return 0

    strips, images = {}, []
    cells = {}
    for key in sorted(VARIANTS):
        frames = build(key)
        strips[key] = frames
        cells[key] = frames[0]
        images.append(('applicants/app_%s_192x216.png' % key, frames[0]))
        if 'no_blink' in VARIANTS[key]['traits']:
            # the no_blink difference lives in frame 3, which is where the blink would be
            images.append(('applicants/app_%s_192x216.png[3]' % key, frames[3]))

    bad, off = palette_gate(images)
    print('%s: %d off-palette pixels' % ('PASS' if bad == 0 else 'FAIL', bad))
    for o in off:
        print('   ', o)
    problems = frame_gate(strips)
    for p in problems:
        print('FAIL ', p)
    print('%s: the idle is four different drawings'
          % ('PASS' if not problems else 'FAIL'))
    if args.check:
        return 0 if (bad == 0 and not problems) else 1

    written = 0
    for key in sorted(VARIANTS):
        frames = strips[key]
        strip = Image.new('RGBA', (W * CELLS, H), (0, 0, 0, 0))
        for i, f in enumerate(frames):
            strip.alpha_composite(f, (i * W, 0))
        strip.save(out('applicants/app_%s_192x216.png' % key))
        frames[0].save(out('applicants/bust_M_192x216.png' if key == 'human_solid_a'
                           else 'applicants/bust_%s_192x216.png' % key))
        # the 96x96 document photo: head and shoulders, from the same drawing, never a second painting
        frames[0].crop((CX - 44, CROWN_Y - 4, CX + 44, CROWN_Y + 84)).resize(
            (96, 96), Image.NEAREST).save(out('portraits/portrait_%s_96x96.png' % key))
        written += 3
    print('wrote %d files for %d variants (strip + bust + portrait each)' % (written, len(VARIANTS)))
    return 0


if __name__ == '__main__':
    sys.exit(main())
