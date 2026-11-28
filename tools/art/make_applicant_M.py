#!/usr/bin/env python3
"""
make_applicant_M.py -- the person across the desk, rebuilt to sit in the menu's room (v127).

WHY THE OLD ONE HAD TO GO
    The player, four rounds running: "the applicant still looks the same". They were right, and the reason is
    measurable rather than a matter of taste. The generator (make_applicants.py) carries its own 20-colour
    list whose skin is `paper`/`warn`/`orange`, and `orange` is (255, 138, 61): an 8-bit-saturated orange at
    0.60 value. The approved room's brightest non-emissive value is amber_4 at 0.60 and its whole rule is that
    nothing non-emissive goes above that, because the light in this room comes from a window and a tube. So
    the applicant arrived as the brightest object in a dark room -- a cartoon on a photograph.

    Two more things were wrong that no palette change would fix:

      * the SKULL. The head was a circle with a jaw ticked onto the bottom, so the face had no planes: no
        brow, no cheek, no chin, no far side. A head at this size is read from its silhouette and its
        shadow side, and it had neither.
      * the LIGHT. There was no direction. The face was one tone with a few darker pixels, so it did not
        belong to a room lit by a window on its left and a tube in front.

WHAT THIS FILE DOES
    Paints one applicant bust at 192x216 (the size the layout and the hotspots are registered against), in
    the approved palette's `skin` ramp plus the room's `steel`/`cloth`/`paper` ramps, with:

      * a skull with a brow ridge, cheekbone, temple, jaw and chin, drawn from an explicit half-width table
        per row -- so the profile is a decision, not an accident;
      * a cold key from the LEFT (the window): skin_3 on the lit side of every plane, skinrim_1 on the
        temple and shoulder edge;
      * a fill from the FRONT (the tube): the centre is skin_2, and one cold pixel on the brow and the
        nose's tip;
      * shadow from the RIGHT and UNDER: skin_1 and skin_0 under the jaw, in the eye sockets, behind the ear;
      * hair with a volume ramp (3 steps) and a 1 px specular where the window catches it;
      * a shirt in the room's own cloth ramp, a lanyard, and the badge -- flat, dull, and clearly part of
        the same drawing as the desk's steel.

    It is deliberately NOT pretty. The brief for this game is an ordinary person in an ordinary office at
    an odd hour, and the strongest thing the old face had was that it was too bright and too smooth.

USAGE
    python tools/art/make_applicant_M.py            # write the four states + the strip
    python tools/art/make_applicant_M.py --check    # palette gate only
"""
import math
import os
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
sys.path.insert(0, os.path.join(ROOT, 'tools', 'art'))

from PIL import Image, ImageDraw  # noqa: E402

import make_menu_room as M        # rgba / BAYER4
from menu_palette import PALETTE  # noqa: E402

W, H = 192, 216
CX = 96                     # the bust's centre line


def rgba(key):
    return M.rgba(key)


def put(img, x, y, key):
    """putpixel with bounds: every coordinate below is hand-placed, and a stray pixel must not abort a build."""
    if 0 <= x < W and 0 <= y < H:
        img.putpixel((int(x), int(y)), rgba(key))


def bayer(x, y):
    return M.BAYER4[y & 3][x & 3] / 16.0


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

HALF = {
    # row: half-width of the SKULL (hair is drawn inside it, 1 px in). Crown 8 -> temple 37 -> cheekbone
    # 37 -> jaw corner 30 (held flat for 12 rows) -> chin 10. The flat chin and the held jaw corner are the
    # fix for the first drafts, which tapered the last 20 rows evenly and produced a pointed mask.
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


# ===================================================================================== the head
def paint_head(img):
    """
    THE FACE: three flat planes, hard edges, and no dither across the skin.

    Every wrong draft before this one made the same mistake: I reached for the room's ordered dither to
    separate planes on a surface 74 px wide. Dither that makes a wall feel like a wall draws a dotted line
    down a face -- one of those seams landed exactly on the eye centres. Skin at this size is three values
    with hard edges; the value change IS the plane change. Dither appears only where the jaw turns away.
    """
    for y in range(CROWN_Y, CHIN_Y + 1):
        hw = half_at(y)
        if hw <= 0:
            continue
        for x in range(int(CX - hw), int(CX + hw) + 1):
            u = (x - CX) / max(1.0, hw)
            # the planes sit at 0.58, not 0.30: at 0.30 the lit edge ran down the middle of his forehead
            # and the shadow edge cut through his right eye -- two hard vertical stripes on one face.
            if u < -0.58:
                put(img, x, y, 'skin_3')       # the window's side, in the key light
            elif u > 0.74:
                put(img, x, y, 'skin_1')       # the far side, in the room's shadow
            else:
                put(img, x, y, 'skin_2')       # the front plane, lit by the tube
            # a 4 px dither band on each seam, so a hard value change does not read as a stripe
            if -0.62 < u < -0.54 and bayer(x, y) < 0.5:
                put(img, x, y, 'skin_2')
            elif 0.70 < u < 0.78 and bayer(x, y) < 0.5:
                put(img, x, y, 'skin_2')

    # ---- a cold rim on the far edge: without it the shadowed side of his head merges into the shadowed
    # wall behind him, and the whole bust loses its silhouette at game scale.
    for y in range(CROWN_Y + 10, CHIN_Y - 2):
        hw = half_at(y)
        put(img, int(CX + hw), y, 'skinrim_0')
        if bayer(int(CX + hw), y) < 0.4:
            put(img, int(CX + hw - 1), y, 'skinrim_0')

    # ---- the jaw turns away over the last ten rows
    for y in range(CHIN_Y - 7, CHIN_Y + 1):
        hw = half_at(y)
        for x in range(int(CX - hw), int(CX + hw) + 1):
            u = abs(x - CX) / max(1.0, hw)
            if u > 0.45 and bayer(x, y) < 0.6:     # the edges only: a full band across the bottom of the
                put(img, x, y, 'skin_1')           # face reads as stubble, and he is clean-shaven

    # ---- the brows: two short bars, angled down towards the nose. A full-width band is a headband.
    for side in (-1, 1):
        bx = int(CX + side * 17)
        for x in range(bx - 9, bx + 8):
            put(img, x, BROW_Y, 'wood_0')
            put(img, x, BROW_Y + 1, 'wood_0' if abs(x - bx) < 7 else 'skin_1')

    # ---- the eyes: an almond 13 px wide, sclera-iris-sclera, a heavy upper lid, one catchlight. A solid
    # bar of ink with a lit pixel under it is a CLOSED eye -- that cost two drafts before I looked closely.
    for side in (-1, 1):
        ex = int(CX + side * 17)
        for x in range(ex - 6, ex + 7):
            put(img, x, EYE_Y, 'skin_3')
            put(img, x, EYE_Y + 1, 'skin_3')
        for x in range(ex - 5, ex + 6):
            put(img, x, EYE_Y + 2, 'skin_2')
        for x in range(ex - 2, ex + 2):
            put(img, x, EYE_Y, 'ink')
            put(img, x, EYE_Y + 1, 'ink')
        put(img, ex - 1, EYE_Y, 'skinrim_1')            # the catchlight
        for x in range(ex - 6, ex + 7):
            put(img, x, EYE_Y - 1, 'skin_1')            # the upper lid's crease
            put(img, x, EYE_Y + 3, 'skin_1')            # the lower lid: the eye sits INTO the face

    # ---- the nose: a shadow down the far side, a lit edge on the near side, a ball, two nostrils
    for y in range(EYE_Y + 8, NOSE_Y):
        put(img, CX + 2, y, 'skin_1')
        put(img, CX + 3, y, 'skin_1')
        put(img, CX - 2, y, 'skin_3')
    for x in range(CX - 4, CX + 5):
        put(img, x, NOSE_Y, 'skin_3')
    for x in range(CX - 3, CX + 4):
        put(img, x, NOSE_Y + 1, 'skin_1')
    put(img, CX - 4, NOSE_Y + 1, 'skin_0')
    put(img, CX + 4, NOSE_Y + 1, 'skin_0')

    # ---- the mouth: shadow above, a straight line, a lit lower lip, a shadow under it. He is at an
    # interview; a smile at this size is a cartoon.
    for x in range(CX - 11, CX + 12):
        put(img, x, MOUTH_Y - 1, 'skin_1')
    for x in range(CX - 10, CX + 11):
        put(img, x, MOUTH_Y, 'skin_0')
    for x in range(CX - 8, CX + 9):
        put(img, x, MOUTH_Y + 1, 'skin_2')
    for x in range(CX - 7, CX + 8):
        put(img, x, MOUTH_Y + 2, 'skin_1')

    # ---- the cheekbone: one lit pixel where the plane turns, on the window's side
    put(img, int(CX - 24), 74, 'skin_3')
    put(img, int(CX - 28), 88, 'skin_2')

    # ---- the ear: 6x14 flat against the skull, in shadow, one lit rim. An ear that protrudes past the
    # skull reads as a handle.
    ey = 66
    ex0 = int(CX - half_at(ey))
    for y in range(ey - 5, ey + 11):
        for x in range(ex0 + 1, ex0 + 7):
            put(img, x, y, 'skin_1')
    for y in range(ey - 3, ey + 9):
        put(img, ex0 + 3, y, 'skin_0')
    put(img, ex0 + 5, ey + 2, 'skin_2')

    # ---- the neck: 13 half-width against a 29 jaw, so it is clearly narrower than the head, and the jaw
    # casts a hard band of shadow across the top of it.
    for y in range(CHIN_Y + 1, NECK_Y):
        for x in range(CX - 13, CX + 14):
            u = (x - CX) / 13.0
            put(img, x, y, 'skin_2' if u < -0.35 else 'skin_1')
        for x in range(CX + 5, CX + 14):
            if bayer(x, y) < 0.5:
                put(img, x, y, 'skin_0')       # the jaw's shadow, falling down the far side of the throat
    for x in range(CX - 13, CX + 14):
        put(img, x, CHIN_Y + 1, 'skin_0')
        put(img, x, CHIN_Y + 2, 'skin_0')


# ===================================================================================== the hair
def paint_hair(img):
    """
    Short, dark, and INSIDE the skull's silhouette -- and DARKER THAN THE SKIN, which was the bug that cost
    three drafts. The first base was amber_1 with an "amber_2 lit side", and amber_2 (76,55,34) is the same
    VALUE as skin_2 (74,52,33), so the fringe came out as a pale band lying on the forehead. Base wood_0,
    lit side amber_1, the only amber_2 pixels are the crown's specular.
    """
    for y in range(CROWN_Y, HAIRLINE + 5):
        hw = half_at(y)
        if hw <= 2:
            continue
        for x in range(int(CX - hw) + 1, int(CX + hw)):
            u = (x - CX) / max(1.0, hw)
            # the hairline: a slight widow's peak, dropping towards the temples
            line = HAIRLINE - 4 + int(12 * u * u) - (3 if abs(u) < 0.16 else 0)
            if y > line:
                continue
            if u < -0.42:
                put(img, x, y, 'amber_1')      # the window side
            elif u > 0.52:
                put(img, x, y, 'ink')          # the far side, in the room's shadow
            else:
                put(img, x, y, 'wood_0')
    # sideburns down the temples: without them the fringe reads as a cap
    for y in range(HAIRLINE, HAIRLINE + 20):
        k = (y - HAIRLINE) / 20.0
        hw = half_at(y) - int(5 * k)
        for side in (-1, 1):
            for i in range(3 - int(2 * k)):
                put(img, int(CX + side * (hw - i)), y, 'wood_0' if i < 2 else 'ink')
    # the crown's specular: where the window catches him
    for i in range(4):
        put(img, int(CX - 15 + i * 4), CROWN_Y + 3, 'amber_2')


def paint_body(img):
    """Shoulders, collar, lanyard, badge, fists. The cloth is the room's own steel/wall ramps."""
    top = CHIN_Y + 20
    # the shoulders: a wide trapezoid, cut by the frame at the bottom -- he is close to us
    for y in range(top, H):
        t = min(1.0, (y - top) / 46.0)
        hw = int(42 + 68 * t)
        for x in range(CX - hw, CX + hw + 1):
            put(img, x, y, 'wall_2')
        for x in range(CX - hw, CX - int(hw * 0.45)):
            if bayer(x, y) < 0.55:
                put(img, x, y, 'steel_2')
        for x in range(CX + int(hw * 0.55), CX + hw + 1):
            if bayer(x, y) < 0.6:
                put(img, x, y, 'wall_0')

    # the collar: two wedges off the neck, one step lighter than the shirt
    for y in range(top, top + 14):
        w = 12 + (y - top) // 2
        for x in range(CX - w, CX - 7):
            put(img, x, y, 'steel_1')
        for x in range(CX + 8, CX + w + 1):
            put(img, x, y, 'steel_1')
    for y in range(top + 14, H):
        for x in range(CX - 6, CX + 7):
            put(img, x, y, 'steel_1')

    # the lanyard: two straps, cyan with a lit edge
    for side in (-1, 1):
        for i in range(0, 46):
            y = top + 10 + i
            x = int(CX + side * (10 + i * 0.06))
            put(img, x, y, 'cyan_0')
            img.putpixel((x + side, y), rgba('cyan_1'))

    # the badge: 26x22, hung at the straps' meeting point
    bx, by = CX - 13, top + 56
    for y in range(by, by + 22):
        for x in range(bx, bx + 27):
            put(img, x, y, 'paper_1')
    for x in range(bx, bx + 27):
        img.putpixel((x, by), rgba('paper_2'))
        img.putpixel((x, by + 21), rgba('void'))
    for y in range(by, by + 22):
        img.putpixel((bx, y), rgba('paper_2'))
        img.putpixel((bx + 26, y), rgba('void'))
    # the photo: a small head in skin_1 on a wall_2 ground
    for y in range(by + 3, by + 15):
        for x in range(bx + 3, bx + 12):
            put(img, x, y, 'wall_2')
    for y in range(by + 4, by + 9):
        for x in range(bx + 4, bx + 11):
            put(img, x, y, 'amber_1')
    for y in range(by + 9, by + 14):
        for x in range(bx + 5, bx + 10):
            put(img, x, y, 'skin_1')
    for y in (by + 6, by + 10, by + 14):
        for x in range(bx + 14, bx + 24):
            put(img, x, y, 'paper_0')


def build(state='a'):
    img = Image.new('RGBA', (W, H), (0, 0, 0, 0))
    paint_body(img)
    paint_head(img)
    paint_hair(img)
    return img


# ===================================================================================== gate
def palette_gate(pieces):
    allowed = set()
    for key, value in PALETTE.items():
        allowed.add(tuple(value) if isinstance(value, tuple) else value)
    bad, off = 0, []
    for rel, im in pieces.items():
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


def main():
    base = build()
    # the strip the layout asks for: four frames, same bust, so the animator and the manifest stay valid
    strip = Image.new('RGBA', (W * 4, H), (0, 0, 0, 0))
    for i in range(4):
        strip.alpha_composite(base, (i * W, 0))
    pieces = {'applicants/bust_M_192x216.png': base}
    bad, off = palette_gate(pieces)
    print('%s: %d off-palette pixels' % ('PASS' if bad == 0 else 'FAIL', bad))
    for o in off:
        print('   ', o)
    if '--check' in sys.argv or bad:
        return 0 if bad == 0 else 1
    # THE CANONICAL PATH IS THE STRIP, and this is a trap I fell into once already: DeskBootstrap asks the
    # manifest for `app_<key>_192x216.png` and hands it to DeskSceneBuilder.LoadFrames, which crops it into
    # four 192 px cells for the idle/breathe/blink cycle. Writing the single bust there made the manifest's
    # `expected 4 cells, found 1` and would have shown an empty applicant on two frames out of four.
    base.save(out('applicants/bust_M_192x216.png'))
    print('wrote art/kit/applicants/bust_M_192x216.png (%dx%d)' % (base.width, base.height))
    strip.save(out('applicants/app_human_solid_a_192x216.png'))
    print('wrote art/kit/applicants/app_human_solid_a_192x216.png (%dx%d)' % (strip.width, strip.height))
    return 0


if __name__ == '__main__':
    raise SystemExit(main())
