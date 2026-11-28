#!/usr/bin/env python3
"""
make_desk_props.py -- the things on and under the desk, painted in the menu's materials (v127).

THE PLAYER'S LIST, VERBATIM
    "the coffee, the keyboards, the mouse, the clipboard, the applicant, and every other features ... Also
     footer room looks too big maybe we can do something about it like random junk of pc and servers in a
     mess, PC displaying error messages and cyber security related texts. make sure to mess them around
     underneath the table but don't completely fill it."

WHAT EACH SPRITE IS, AND WHERE IT GOES
    props_coffee.png     30x38   at canvas (236, 214) -- left of the applicant, right of the keyboard
    props_keyboard.png   96x20   at canvas (168, 206) -- in front of the monitor
    props_mouse.png      18x24   at canvas (280, 208)
    props_clipboard.png  38x46   at canvas (330, 212) -- the day's paperwork, under the applicant's hand
    props_papers.png     44x30   at canvas (14, 210)  -- the loose sheets on the desk's far left
    props_junk.png      480x62   at canvas (0, 208)   -- the MESS under the desk, bottom of the frame

    The junk layer is the "footer room" the player said was too big and too empty. It is deliberately NOT
    full: three clusters and two gaps, so the eye reads clutter with somewhere to rest -- the same rule the
    room's own composition follows. Everything in it is a monitor, a tower or a cable, drawn at the size
    those things are at this scale, and the CRTs carry error text from the game's own vocabulary.

MATERIAL RULES (the whole point of this round: the desk must look like the menu)
    * every surface is a RAMP: steel_0..2 for metal, wood_0..3 for the desk, paper_0..2 / cup_0..2 for
      ceramics and sheets, teal/cyan for anything emitting;
    * one dither seam per surface, bayer 4x4, never a gradient;
    * a specular is a SINGLE line on ONE edge and is rationed: the cup's lip, the mouse's shell, the
      keyboard's top rail, the clipboard's clip;
    * every object casts a contact shadow of 2-3 px toward the front, because the tube is in front of it.

USAGE
    python tools/art/make_desk_props.py            # write the six sprites
    python tools/art/make_desk_props.py --check    # palette gate only
"""
import math
import os
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
sys.path.insert(0, os.path.join(ROOT, 'tools', 'art'))

from PIL import Image, ImageDraw  # noqa: E402

import make_menu_room as M        # rgba, box, hline, vline, dither, BAYER4 -- one set of primitives
from menu_palette import PALETTE  # noqa: E402

W, H = 480, 270
# The desk's surface starts at its far edge; the props' shared layer is a band above it.
DESK_ROW = 186
# THE BAND IS 160..227, and that is the fix. The menu's prop functions draw DOWN the canvas from the point
# they are given (cup: body from cy, saucer at cy+24; keyboard: ky..ky+kh; forms: fy..fy+26), so a prop placed
# "on the desk" has its point INSIDE the slab's depth and its body running down into the wood -- which is how
# the menu's own desk places the cup (cy = the slab's far edge + 6). Cropping a band ABOVE the far edge
# therefore captured nothing but the cup's steam. The band is the slab and the air above it.
BAND_TOP = 160              # canvas row of this sprite's row 0
PROP_BAND = 68              # canvas rows 160..227: the slab's 42 px plus 26 px of steam room


def rgba(key):
    return M.rgba(key)


def out(rel):
    return os.path.join(ROOT, 'art', 'kit', rel)


def bayer(x, y):
    return M.BAYER4[y & 3][x & 3] / 16.0


def shadow(img, x0, y0, x1, y1, depth=3):
    """A contact shadow: solid under the object, dithered out over `depth` rows below it."""
    for y in range(y0, y1):
        for x in range(x0, x1):
            if 0 <= x < img.width and 0 <= y < img.height:
                t = (y - y0) / float(max(1, y1 - y0))
                if bayer(x, y) < 0.55 * (1.0 - t):
                    img.putpixel((x, y), rgba('void'))


# ===================================================================================== coffee
def coffee():
    """
    38 px tall, 30 wide: a ceramic cup with a saucer, steam, and the day's evidence -- a ring where an
    earlier cup stood. Drawn on cup_0..2, which is a WARM grey ramp of its own, so it separates from the
    steel of the monitor without leaving the palette.

    The steam is two columns of dithered 'paper_1' going UP from the lip: it is the only thing in the desk's
    room that moves in the art, and it is the cheapest way to say "this cup is hot and this person is here".
    """
    w, h = 30, 38
    img = Image.new('RGBA', (w, h), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)

    # ---- the saucer (an ellipse of three values, lit from the front-left where the tube is)
    cx, sy = w // 2, h - 6
    for y in range(sy - 3, sy + 4):
        for x in range(2, w - 2):
            dx = (x - cx) / 13.0
            dy = (y - sy) / 3.4
            if dx * dx + dy * dy <= 1.0:
                img.putpixel((x, y), rgba('cup_0'))
    for x in range(5, w - 5):
        img.putpixel((x, sy - 2), rgba('cup_2'))
    shadow(img, 3, sy + 2, w - 3, sy + 5)

    # ---- the cup's body: a tapered cylinder, cup_1 face, cup_0 shadow side, cup_2 rim light on the left
    top, bot = sy - 20, sy - 3
    for y in range(top, bot):
        t = (y - top) / float(bot - top)
        halfw = 8 - int(t * 1.5)
        for x in range(cx - halfw, cx + halfw):
            img.putpixel((x, y), rgba('cup_1'))
        img.putpixel((cx - halfw, y), rgba('cup_2'))
        img.putpixel((cx - halfw + 1, y), rgba('cup_1'))
        for x in range(cx + halfw - 3, cx + halfw):
            img.putpixel((x, y), rgba('cup_0'))
    # the seam where the cylinder turns away from the light, dithered
    for y in range(top + 3, bot):
        for x in range(cx + 1, cx + 6):
            if bayer(x, y) < 0.35:
                img.putpixel((x, y), rgba('cup_0'))

    # ---- the rim: an ellipse, cup_2 on the near edge and void inside it (the coffee is nearly gone)
    for x in range(cx - 9, cx + 9):
        img.putpixel((x, top), rgba('cup_2'))
        img.putpixel((x, top + 1), rgba('cup_1'))
    for x in range(cx - 7, cx + 7):
        img.putpixel((x, top + 2), rgba('void'))
    # the coffee left in it: a thin crescent of amber_2, the room's only warm note on the desk
    for x in range(cx - 4, cx + 5):
        img.putpixel((x, top + 2), rgba('amber_2'))

    # ---- the handle: a ring on the right, two values
    for y in range(top + 4, top + 13):
        img.putpixel((cx + 8, y), rgba('cup_1'))
        img.putpixel((cx + 9, y), rgba('cup_0'))
    for x in range(cx + 6, cx + 10):
        img.putpixel((x, top + 4), rgba('cup_2'))
        img.putpixel((x, top + 12), rgba('cup_0'))

    # ---- the ring the earlier cup left: an arc of cup_0 on the wood, dithered. Evidence of a person.
    for i in range(0, 40):
        a = math.radians(i * 9)
        x = int(cx + 13 + 4 * math.cos(a))
        y = int(sy + 3 + 2 * math.sin(a))
        if 0 <= x < w and 0 <= y < h and bayer(x, y) < 0.6:
            img.putpixel((x, y), rgba('cup_0'))

    # ---- steam: two columns, rising, bit-sparse, FADING out with height
    for col, x0 in enumerate((cx - 3, cx + 3)):
        for y in range(top - 12, top - 1):
            t = (top - 1 - y) / 12.0
            xx = x0 + int(math.sin((y + col * 3) * 0.7) * 1.5)
            if 0 <= xx < w and bayer(xx + col, y) < 0.45 * t:
                img.putpixel((xx, y), rgba('paper_1'))
    return img


# ===================================================================================== keyboard
def keyboard():
    """
    96x20. A low-profile mechanical board: a dark tray, keys in a 1 px grid with a lit top face, and one
    specular along the top rail. The keys are TWO values -- cap and shadow -- because at this size a key is
    literally 5x3 px and a third value turns the board into noise.
    """
    w, h = 96, 20
    img = Image.new('RGBA', (w, h), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)

    # ---- the tray
    for y in range(3, h - 3):
        for x in range(1, w - 1):
            img.putpixel((x, y), rgba('steel_0'))
    for x in range(1, w - 1):
        img.putpixel((x, 3), rgba('steel_2'))      # the top rail: the board's one specular
        img.putpixel((x, 4), rgba('steel_1'))
        img.putpixel((x, h - 4), rgba('steel_0'))
    for y in range(3, h - 3):
        img.putpixel((1, y), rgba('steel_1'))
        img.putpixel((w - 2, y), rgba('void'))

    # ---- the keys: rows of caps on a 6x4 grid, with the space bar
    rows = [(6, 13), (9, 13), (12, 12)]
    for ri, (y, count) in enumerate(rows):
        for k in range(count):
            x = 5 + k * 7
            if x + 5 >= w - 3:
                break
            for yy in range(y, y + 2):
                for xx in range(x, x + 5):
                    img.putpixel((xx, yy), rgba('steel_1'))
            for xx in range(x, x + 5):
                img.putpixel((xx, y), rgba('steel_2'))       # the cap's lit face
    # the space bar and the two modifiers under it
    for xx in range(24, 60):
        img.putpixel((xx, 15), rgba('steel_1'))
        img.putpixel((xx, 16), rgba('steel_0'))
    for x0 in (12, 66):
        for xx in range(x0, x0 + 10):
            img.putpixel((xx, 15), rgba('steel_1'))

    # ---- the cable leaving the top-right, and the contact shadow
    for i in range(0, 6):
        cx_, cy_ = w - 4 + i, 3 - i // 3
        if 0 <= cx_ < w and 0 <= cy_ < h:
            img.putpixel((cx_, cy_), rgba('void'))
    shadow(img, 2, h - 3, w - 2, h)
    return img


# ===================================================================================== mouse
def mouse():
    """
    18x24. A shell of steel_1 with one specular on its crown and a cable. The wheel is 2 px of teal_1 --
    the only place the mouse is allowed a colour, because it is an LED and this room's emitters are cyan.
    """
    w, h = 18, 24
    img = Image.new('RGBA', (w, h), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    cx = w // 2

    for y in range(6, h - 2):
        t = (y - 6) / float(h - 8)
        half = int(4 + t * 3)
        for x in range(cx - half, cx + half):
            img.putpixel((x, y), rgba('steel_1'))
        img.putpixel((cx - half, y), rgba('steel_2'))
        for x in range(cx + half - 2, cx + half):
            img.putpixel((x, y), rgba('steel_0'))

    # the crown: the specular runs along the top for 5 px and then stops
    for x in range(cx - 2, cx + 3):
        img.putpixel((x, 6), rgba('paper_2'))
        img.putpixel((x, 7), rgba('steel_2'))
    # the split between the buttons, and the wheel
    for y in range(8, 13):
        img.putpixel((cx - 1, y), rgba('void'))
        img.putpixel((cx, y), rgba('teal_1'))
        img.putpixel((cx + 1, y), rgba('void'))

    # the cable: a thin void line going up and left out of frame
    for i in range(0, 7):
        img.putpixel((cx + 1 + i // 2, 5 - i), rgba('void'))
    shadow(img, 3, h - 3, w - 3, h)
    return img


# ===================================================================================== clipboard
def clipboard():
    """
    38x46. The day's paperwork: a fibreboard pad of wood_1, a steel clip, one sheet of paper_1 with ruling
    in paper_0 and a red stamp box, and a pencil. This is the one prop that is about the JOB rather than the
    room, so it is the one prop allowed the warn colour.
    """
    w, h = 38, 46
    img = Image.new('RGBA', (w, h), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)

    # ---- the board
    for y in range(6, h - 2):
        for x in range(3, w - 3):
            img.putpixel((x, y), rgba('wood_1'))
    for x in range(3, w - 3):
        img.putpixel((x, 6), rgba('wood_3'))
        img.putpixel((x, h - 3), rgba('wood_0'))
    for y in range(6, h - 2):
        img.putpixel((3, y), rgba('wood_2'))
        img.putpixel((w - 4, y), rgba('wood_0'))
    for y in range(20, 24):
        for x in range(3, w - 3):
            if bayer(x, y) < 0.4:
                img.putpixel((x, y), rgba('wood_0'))

    # ---- the sheet: paper_1, ruling, a stamp box
    for y in range(11, h - 8):
        for x in range(6, w - 6):
            img.putpixel((x, y), rgba('paper_2'))
    for y in range(16, h - 11, 3):
        for x in range(8, w - 9):
            img.putpixel((x, y), rgba('paper_0'))
    for x in range(22, 30):
        img.putpixel((x, h - 15), rgba('red_2'))
        img.putpixel((x, h - 14), rgba('red_2'))
    for y in range(h - 15, h - 13):
        img.putpixel((22, y), rgba('red_2'))
        img.putpixel((29, y), rgba('red_2'))

    # ---- the clip: steel_2 with a hole, and the two rivets
    for y in range(0, 9):
        for x in range(12, w - 12):
            img.putpixel((x, y), rgba('steel_1') if y > 2 else rgba('steel_2'))
    for x in range(14, w - 14):
        img.putpixel((x, 2), rgba('void'))
    for x, y in ((14, 5), (w - 15, 5)):
        img.putpixel((x, y), rgba('steel_0'))
    shadow(img, 4, h - 2, w - 4, h)
    return img


# ===================================================================================== loose papers
def papers():
    """
    44x30: three sheets on the desk's far left, fanned. paper_2 faces, paper_1 edges, paper_0 ruling and a
    couple of lines of 'text' in void so they read as written on rather than blank.
    """
    w, h = 44, 30
    img = Image.new('RGBA', (w, h), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)

    def sheet(x0, y0, sw, sh, face):
        for y in range(y0, y0 + sh):
            for x in range(x0, x0 + sw):
                img.putpixel((x, y), rgba(face))
        for x in range(x0, x0 + sw):
            img.putpixel((x, y0), rgba('paper_2'))
        for y in range(y0, y0 + sh):
            img.putpixel((x0, y), rgba('paper_2'))
            img.putpixel((x0 + sw - 1, y), rgba('paper_0'))
        for x in range(x0, x0 + sw):
            img.putpixel((x, y0 + sh - 1), rgba('paper_0'))

    sheet(6, 12, 26, 16, 'paper_1')
    sheet(10, 8, 26, 16, 'paper_2')
    sheet(16, 4, 24, 16, 'paper_2')

    # the writing: two short ruled lines and a circle in the margin, all in void at half coverage
    for i, (x0, y) in enumerate(((20, 9), (20, 12), (20, 15))):
        for x in range(x0, x0 + 12 - i * 3):
            if bayer(x, y) < 0.8:
                img.putpixel((x, y), rgba('void'))
    for i in range(0, 12):
        a = math.radians(i * 30)
        img.putpixel((int(33 + 2 * math.cos(a)), int(20 + 2 * math.sin(a))), rgba('red_2'))
    shadow(img, 8, 25, 40, 28)
    return img


# ===================================================================================== the mess under the desk
def junk():
    """
    480x28 -- THE FLOOR UNDER THE DESK, and 28 is the whole story of this sprite.

    The first draft drew the mess 38 px tall on a 62 px canvas, which put the towers at canvas rows 232..270.
    The desk's lip covers rows 228..241 and sorts above the junk, so in the composed frame every tower was
    behind the lip and the band under the desk read as empty floor -- the exact complaint the player made
    ("footer room looks too big"), still true after the redraw. The band that actually exists is rows 242..270.
    So the junk is 28 rows, it is drawn bottom-up inside them, and nothing in it is taller than the desk.

    COMPOSITION, not a carpet -- three clusters with two gaps, because the player asked for a mess and a mess
    still needs somewhere for the eye to rest:
        x   4..142   a tower on its side, a CRT face-down, a stack of dead drives, one loose cable
        x 142..256   GAP: floor, and one cable crossing it
        x 258..384   a server box with a fault LED and a small CRT still ON, showing ERR 0x5D
        x 384..446   GAP with a cable
        x 448..478   one tower leaning against the frame's right edge
    Two screens are on and carry this game's vocabulary. A screen that is on is the room's emitter, so its
    text is cyan, and its glow is dithered onto the bezel around the glass -- light landing on a surface,
    which is the rule the player has stated about the lamp three times.
    """
    w, h = W, 28
    img = Image.new('RGBA', (w, h), (0, 0, 0, 0))

    def rect(x0, y0, x1, y1, key):
        for y in range(max(0, y0), min(h, y1)):
            for x in range(max(0, x0), min(w, x1)):
                img.putpixel((x, y), rgba(key))

    def cable(x0, y0, n, amp=2.0, freq=0.18):
        for i in range(n):
            x = x0 + i
            y = y0 + int(math.sin(i * freq) * amp)
            if 0 <= x < w and 0 <= y < h:
                img.putpixel((x, y), rgba('void'))

    def tower_on_side(x0, tw, th):
        """A tower lying on its side: its top face towards us, so the vents read as a hatch grid."""
        y0 = h - th
        rect(x0, y0, x0 + tw, h, 'steel_0')
        for x in range(x0, x0 + tw):
            img.putpixel((x, y0), rgba('steel_1'))          # the edge that catches what light there is
        for y in range(y0 + 3, h - 2, 2):
            for x in range(x0 + 4, x0 + tw - 4):
                if bayer(x, y) < 0.5:
                    img.putpixel((x, y), rgba('void'))
        for x in range(x0 - 1, x0 + tw + 1):
            if bayer(x, h - 1) < 0.8:
                img.putpixel((x, h - 1), rgba('void'))      # the contact shadow: it is ON the floor

    def crt_face(x0, y0, cw, ch, on, words):
        rect(x0, y0, x0 + cw, y0 + ch, 'steel_1')
        rect(x0, y0, x0 + cw, y0 + 2, 'steel_0')
        rect(x0 + cw - 2, y0, x0 + cw, y0 + ch, 'void')
        gx0, gy0 = x0 + 3, y0 + 3
        gx1, gy1 = x0 + cw - 3, y0 + ch - 3
        rect(gx0, gy0, gx1, gy1, 'teal_0' if on else 'void')
        if on:
            for y in range(gy0, gy1, 3):
                for x in range(gx0, gx1):
                    img.putpixel((x, y), rgba('steel_0'))
            for k in range(1, 3):                            # the glow, on the bezel around the glass
                for x in range(gx0 - k, gx1 + k):
                    if 0 <= gy0 - k < h and bayer(x, gy0 - k) < 0.45 / k:
                        img.putpixel((x, gy0 - k), rgba('teal_1'))
            pen = TextPen(img, gx0 + 1, gy0 + 1, 3, 5, 'cyan_1')
            for line in words:
                pen.write(line[:max(1, (gx1 - gx0) // 4)])
                pen.newline(1)

    # ---- CLUSTER 1: x 4..142
    tower_on_side(6, 30, 22)
    # a CRT face-down: the back shell, its vent slots, and a cable out of the neck
    rect(40, 10, 86, 28, 'steel_1')
    for x in range(40, 86):
        img.putpixel((x, 10), rgba('steel_2'))
    for y in range(14, 24, 3):
        for x in range(45, 81):
            if bayer(x, y) < 0.45:
                img.putpixel((x, y), rgba('void'))
    cable(82, 20, 12, 1.2, 0.35)
    # a stack of dead drives: three slabs, each with a lit top edge
    for i in range(3):
        y = 18 + i * 3
        rect(92, y, 124, y + 3, 'steel_1' if i % 2 == 0 else 'steel_0')
        for x in range(92, 124):
            img.putpixel((x, y), rgba('steel_2'))
    cable(126, 24, 14, 1.6, 0.28)

    # ---- CLUSTER 2: x 258..384. A server box and the CRT that is still ON.
    rect(258, 8, 336, 28, 'steel_0')
    for x in range(258, 336):
        img.putpixel((x, 8), rgba('steel_1'))
    for y in range(12, 26, 2):
        for x in range(262, 332):
            if bayer(x, y) < 0.35:
                img.putpixel((x, y), rgba('void'))
    for i in range(6):                                        # the fault LEDs: four healthy, one amber
        img.putpixel((264 + i * 2, 10), rgba('teal_1') if i < 4 else rgba('amber_3'))
    crt_face(340, 6, 34, 22, True, ['ERR', '0x5D'])
    cable(334, 24, 8, 1.0, 0.3)

    # ---- CLUSTER 3: a tower leaning on the frame's right edge
    for y in range(6, 28):
        for x in range(452, 478):
            img.putpixel((x, y), rgba('steel_0'))
    for y in range(6, 28):
        img.putpixel((452, y), rgba('steel_1'))
        img.putpixel((477, y), rgba('void'))
    for y in range(10, 24, 3):
        for x in range(456, 473):
            if bayer(x, y) < 0.4:
                img.putpixel((x, y), rgba('void'))
    img.putpixel((465, 8), rgba('amber_3'))

    # ---- the gaps: one crossing cable each, so they read as floor and not as holes
    cable(144, 22, 110, 2.0, 0.14)
    cable(386, 20, 60, 1.6, 0.22)
    return img


class TextPen:
    """
    A 3x5 block font, just for the junk's screens. The game's own atlas is 5x7, which is too coarse for a
    34 px CRT: at 3x5 with a 1 px gap, `ERR 0x5D` is 32 px wide and legible, and it is drawn from the same
    kind of bit rows as monitor_font_5x7 so the two typefaces belong to the same family.
    """
    GLYPHS = {
        '0': ('111', '101', '101', '101', '111'),
        '1': ('010', '110', '010', '010', '111'),
        '2': ('111', '001', '111', '100', '111'),
        '3': ('111', '001', '111', '001', '111'),
        '4': ('101', '101', '111', '001', '001'),
        '5': ('111', '100', '111', '001', '111'),
        '6': ('111', '100', '111', '101', '111'),
        '7': ('111', '001', '010', '010', '010'),
        '8': ('111', '101', '111', '101', '111'),
        '9': ('111', '101', '111', '001', '111'),
        'A': ('010', '101', '111', '101', '101'),
        'B': ('110', '101', '110', '101', '110'),
        'C': ('111', '100', '100', '100', '111'),
        'D': ('110', '101', '101', '101', '110'),
        'E': ('111', '100', '110', '100', '111'),
        'R': ('110', '101', '110', '101', '101'),
        'S': ('111', '100', '111', '001', '111'),
        'X': ('101', '101', '010', '101', '101'),
        'N': ('101', '111', '111', '111', '101'),
        'O': ('111', '101', '101', '101', '111'),
        'L': ('100', '100', '100', '100', '111'),
        'T': ('111', '010', '010', '010', '010'),
        'I': ('111', '010', '010', '010', '111'),
        'K': ('101', '101', '110', '101', '101'),
        'G': ('111', '100', '101', '101', '111'),
        'W': ('101', '101', '111', '111', '101'),
        'P': ('110', '101', '110', '100', '100'),
        ' ': ('000', '000', '000', '000', '000'),
        '.': ('000', '000', '000', '000', '010'),
        'E.': ('111', '100', '110', '100', '111'),
    }

    def __init__(self, img, x, y, gw, gh, key):
        self.img, self.x, self.y, self.gw, self.gh = img, x, y, gw, gh
        self.x0, self.key = x, key
        self.colour = rgba(key)

    def write(self, text):
        for ch in text.upper():
            rows = self.GLYPHS.get(ch)
            if rows is None:
                self.x += self.gw + 1
                continue
            for ry, row in enumerate(rows):
                for rx, bit in enumerate(row):
                    if bit == '1' and 0 <= self.x + rx < self.img.width and 0 <= self.y + ry < self.img.height:
                        self.img.putpixel((self.x + rx, self.y + ry), self.colour)
            self.x += self.gw + 1

    def newline(self, step):
        self.x = self.x0
        self.y += self.gh + step


# ===================================================================================== gate
def palette_gate(pieces):
    allowed = set()
    for key, value in PALETTE.items():
        allowed.add(tuple(value) if isinstance(value, tuple) else value)
    bad = 0
    off = []
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




# ===================================================================================== the desk's props
# THE MENU'S PROPS, ON THE GAME'S DESK (v128).
#
# v127 drew its own coffee, keyboard, mouse and papers. The player's note: "the objects are on the edge of the
# table and does not look like they are ON the table based on their angles." Two problems in one sentence --
# wrong objects, and wrong placement -- and both are fixed the same way: call the menu's own drawing functions
# (make_menu_room.cup / keyboard / mouse / forms), and place them so their BASES stand on the desk's surface.
#
# PLACEMENT, and this is the rule that was wrong: the canvas is top-down, so an object standing on a surface
# is drawn UP from the line it stands on. The desk's surface starts at its far edge, DESK_ROW = 186. Every
# prop's BASE row is 186 and its body rises into the rows above -- exactly how the menu's own desk places its
# cup (cy at the far edge of the slab, cup body above it).
def props_layer():
    """480 x PROP_BAND, one entry per prop, all of them the menu's drawings, all of them standing on 186."""
    import make_menu_room as M
    band_top = BAND_TOP
    img = Image.new('RGBA', (W, PROP_BAND), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)

    def at(canvas_x, canvas_top_row, fn, **kw):
        """Run a menu prop function at CANVAS coordinates, then crop the band out of it.

        `canvas_top_row` is where the function starts drawing -- its own (cy/ky/fy). The body runs DOWN from
        there into the slab, exactly as on the menu's desk.
        """
        sub = Image.new('RGBA', (W, H), (0, 0, 0, 0))
        sd = ImageDraw.Draw(sub)
        fn(sd, sub, canvas_x, canvas_top_row, **kw)
        return sub.crop((0, band_top, W, band_top + PROP_BAND))

    # left to right: the papers at the frame's left end, the keyboard in front of the viewer, the clipboard,
    # the coffee, and the mouse at the right -- the menu's own left-to-right reading, mirrored to this room's
    # monitor on the right so the props cluster under the tube's light.
    # Every top row is DESK_ROW + the same inset the MENU uses (6 px below its slab's far edge), so all four
    # objects stand on the same line of the same slab. Left to right: the forms at the frame's left end, the
    # keyboard in front of the viewer, the mouse, then the coffee under the tube's light.
    top = DESK_ROW + 6
    for piece in (
        at(26,  top, M.forms),
        at(196, top, M.keyboard),
        at(292, top - 3, M.mouse),
        at(330, top, M.cup),
    ):
        img.alpha_composite(piece)
    return img


def main():
    pieces = {
        'desk/props_coffee.png': coffee(),
        'desk/props_keyboard.png': keyboard(),
        'desk/props_mouse.png': mouse(),
        'desk/props_clipboard.png': clipboard(),
        'desk/props_papers.png': papers(),
        'desk/props_junk.png': junk(),
        'desk/props_desk_M.png': props_layer(),
    }
    bad, off = palette_gate(pieces)
    print('%s: %d off-palette pixels across %d sprites' % ('PASS' if bad == 0 else 'FAIL', bad, len(pieces)))
    for o in off:
        print('   ', o)
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
