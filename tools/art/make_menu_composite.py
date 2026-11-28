#!/usr/bin/env python3
"""
make_menu_composite.py -- the front door, exactly as MenuView composes and lights it (v115).

Reads the same numbers as MenuLayout.cs, uses the same sprites as the game (art/kit/menu/), and applies
the same three lights with the same bands and falloff, so this render is a twin and not a mock-up.

    background   menu/shell.png             tiled floor, panelled wall, black grout
    window       menu/window.png            city, moon, stars, lit windows
    table        menu/monitor_stack.png     6 white screens on a table + 2 glowing underneath
    server       menu/server.png            right corner
    CRT          menu/crt.png               the menu lives in its glass
    desk         menu/desk.png              coffee + smoke, keyboard, mouse, application forms
    light        MenuLayout lights          3 sources, banded falloff, dithered seams only

    python tools/art/make_menu_composite.py           # writes art/kit/reference/menu_v119_{1x,3x}.png
    python tools/art/make_menu_composite.py --check    # geometry gate
"""
import os
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
sys.path.insert(0, os.path.join(ROOT, 'tools', 'comp'))
sys.path.insert(0, os.path.join(ROOT, 'tools', 'art'))

from PIL import Image, ImageDraw  # noqa: E402
from preview_intro import load_font, draw_text, GLYPH_W as GW, GLYPH_H as GH, LETTER_GAP as LG  # noqa: E402
from menu_palette import PALETTE  # noqa: E402  (the v118 material ramps, one source of truth)

W, H = 480, 270

# ---- mirrored from MenuLayout.cs (v118) -----------------------------------------------------------
# These MUST equal unity/Assets/Scripts/Core/Rules/MenuLayout.cs. check() asserts it against the sprite
# sizes and the palette, so a stale number here fails the gate instead of quietly painting the room wrong.
WIN_X, WIN_Y = 8, 10
STACK_X, STACK_Y = 366, 44
LAMP_X, LAMP_Y = 422, 62
SRV_X, SRV_Y = 452, 80
CRT_X, CRT_Y = 104, 10
GLASS_X, GLASS_Y, GLASS_W, GLASS_H = CRT_X + 26, CRT_Y + 12, 228, 152
DESK_Y = 150
DESK_TOP = DESK_Y + 26
TITLE_SCALE = 2
TITLE_GAP, TITLE_BLEED = 1, 1
TITLE_TOP, SUB_TOP, TAG_TOP = GLASS_Y + 9, GLASS_Y + 26, GLASS_Y + 52
RULE_TOP = GLASS_Y + 66
PILL_X, PILL_W, PILL_H, PILL_STEP = GLASS_X + 40, 148, 13, 13
PILL_TOP = GLASS_Y + 72
FOOTER_TOP = GLASS_Y + GLASS_H - 12
TUBE_INSET = 4
TEXT_X, TEXT_Y = GLASS_X + TUBE_INSET, GLASS_Y + TUBE_INSET
TEXT_W, TEXT_H = GLASS_W - 2 * TUBE_INSET, GLASS_H - 2 * TUBE_INSET

# the light: three sources, one step each, measured exactly as MenuView.Light() measures them
LIGHTS = [
    (WIN_X + 44, WIN_Y + 60, 116, 128, 1.95, 1),
    (LAMP_X + 28, LAMP_Y + 70, 108, 122, 1.45, 1),
    (GLASS_X + GLASS_W // 2, GLASS_Y + GLASS_H // 2, 232, 196, 1.70, 1),
]
MAX_LIFT = 1
BAYER4 = [0, 8, 2, 10, 12, 4, 14, 6, 3, 11, 1, 9, 15, 7, 13, 5]

# material ramps, mirrored from MenuLayout.Ramps: a light moves a surface ONE entry along its own ramp
RAMPS = [
    ("wall_0", "wall_1"), ("wall_1", "wall_2"),
    ("floor_0", "floor_1"), ("floor_1", "floor_2"),
    ("wood_0", "wood_1"), ("wood_1", "wood_2"), ("wood_2", "wood_3"), ("wood_3", "wood_4"),
    ("steel_0", "steel_1"), ("steel_1", "steel_2"),
    ("key_0", "key_1"), ("key_1", "key_2"),
    ("mouse_0", "mouse_1"), ("mouse_1", "mouse_2"),
    ("paper_0", "paper_1"), ("paper_1", "paper_2"),
    ("cup_0", "cup_1"), ("cup_1", "cup_2"),
]

# the cluster: (x, y, w, h) in canvas px, mirrored from MenuLayout.StackCells
STACK_CELLS = [
    (367, 44, 32, 24), (399, 46, 34, 23),
    (366, 69, 33, 26), (398, 68, 35, 27),
    (368, 95, 31, 24), (400, 96, 33, 23),
]
STACK_TUBE = 3
STACK_TABLE_TOP = STACK_Y + 76      # the slab under the tubes, in canvas px


def kit(rel):
    return Image.open(os.path.join(ROOT, 'art', 'kit', rel + '.png')).convert('RGBA')


def compose():
    img = kit('menu/shell').copy()
    # BACK TO FRONT, exactly MenuView.BuildRoom(): wall, window, the rail of screens, the lamp, the
    # monitor, the server, the desk.
    for rel, x, y in (('menu/window', WIN_X, WIN_Y),
                      ('menu/monitor_stack', STACK_X, STACK_Y),
                      ('menu/crt', CRT_X, CRT_Y),
                      ('menu/server', SRV_X, SRV_Y),
                      ('menu/desk', 0, DESK_Y),
                      ('menu/lamp', LAMP_X, LAMP_Y)):
        img.alpha_composite(kit(rel), (x, y))
    return img


def light(img):
    """
    MenuView.Light(), step for step: each light raises a surface ONE step along the surface's OWN ramp,
    the fractional part becomes a 2x2 dither, and emitters are never touched.
    """
    import math
    px = img.load()
    up = {}
    for a, b in RAMPS:
        up.setdefault(PALETTE[a], PALETTE[b])

    for y in range(H):
        for x in range(W):
            c = px[x, y]
            if c[3] == 0:
                continue
            if CRT_X <= x < CRT_X + 260 and CRT_Y <= y < CRT_Y + 186:
                continue                      # the tube is a SOURCE, not a surface
            target = up.get(c[:3])
            if target is None:
                continue                      # emitting colour: leave it alone

            lift = 0.0
            for (lx, ly, rx, ry, falloff, strength) in LIGHTS:
                dx = (x - lx) / rx
                dy = (y - ly) / ry
                r = math.sqrt(dx * dx + dy * dy)
                if r >= 1.0:
                    continue
                lift += ((1.0 - r) ** falloff) * strength

            add = int(lift)
            frac = lift - add
            pat = (x & 1) + ((y & 1) << 1)
            if frac > 0.62:
                add += 1
            elif frac > 0.30 and pat < 2:
                add += 1
            if add <= 0:
                continue
            add = min(add, MAX_LIFT)
            px[x, y] = target[:3] + (255,)


def vignette(img):
    """The outer corners only. A ring across the whole frame draws a visible dome (v115)."""
    import math
    px = img.load()
    dark = PALETTE['void'] + (255,)
    for y in range(H):
        for x in range(W):
            c = px[x, y]
            if c[3] == 0:
                continue
            if CRT_X <= x < CRT_X + 260 and CRT_Y <= y < CRT_Y + 186:
                continue
            dx = (x - W / 2) / (W / 2)
            dy = (y - H / 2) / (H / 2)
            r = math.sqrt(dx * dx + dy * dy)
            if r > 1.24:
                px[x, y] = dark
            elif r > 1.12 and BAYER4[((y & 3) << 2) | (x & 3)] < 5:
                px[x, y] = dark


GLASS_PAPER = PALETTE['void'] + (255,)
GLASS_WASH = PALETTE['teal_0'] + (255,)
GLASS_DIM = PALETTE['steel_0'] + (255,)


def glass_material(img):
    """The tube: an off-black phosphor with a scanline every third row, as MenuView.GlassMaterial()."""
    d = ImageDraw.Draw(img)
    d.rectangle([GLASS_X, GLASS_Y, GLASS_X + GLASS_W - 1, GLASS_Y + GLASS_H - 1], fill=GLASS_PAPER)
    for y in range(GLASS_Y, GLASS_Y + GLASS_H):
        if (y - GLASS_Y) % 3 == 2:
            shade = GLASS_WASH if y < GLASS_Y + GLASS_H // 3 else GLASS_DIM
            d.line([GLASS_X, y, GLASS_X + GLASS_W - 1, y], fill=shade)
    return img


def dust_hash(x, y):
    h = (x * 374761393 + y * 668265263) & 0xFFFFFFFF
    h = ((h ^ (h >> 13)) * 1274126177) & 0xFFFFFFFF
    return h ^ (h >> 16)


# the glow layer, mirrored from MenuLayout.GlowSprites / GlowPos. It runs AFTER the vignette, exactly as
# MenuView.Glow() does, and it only ever adds light.
BAYER_MENU = ((0, 8, 2, 10), (12, 4, 14, 6), (3, 11, 1, 9), (15, 7, 13, 5))
GLOW_POS = [('menu/glow', 0, 0)]


def glow(img):
    """
    MenuView.Glow(): the glow sprite's pixels are STRENGTHS, not colours. The sprite stores each strength as a
    palette colour, but the runtime recolours by VALUE rather than by key -- and so does this twin, because
    keying on the colour was the v119.0 bug that put cyan speckles on the window: the wall around the window
    is already at the top of its own ramp, so the fallback "dithered tint" had nowhere to lift and painted
    the glow's own cyan instead.

    The fix is a rule, not a number: A GLOW PIXEL ON A SURFACE WITH NO HEADROOM ADDS NOTHING. The wall and the
    floor gained lit steps (wall_3, floor_3/4) precisely so the headroom exists where the light lands.
    """
    from menu_palette import PALETTE
    up = {}
    for low, high in MenuLayoutRamps():
        up.setdefault(PALETTE[low], PALETTE[high])
    strength = {}
    for i, key in enumerate(COLD_WARM_RAMP):
        strength.setdefault(PALETTE[key], i)
    out = img.load()
    for rel, ox, oy in GLOW_POS:
        g = kit(rel)
        gp = g.load()
        for y in range(g.height):
            dy = oy + y
            if not (0 <= dy < H):
                continue
            for x in range(g.width):
                c = gp[x, y]
                if c[3] == 0:
                    continue
                dx = ox + x
                if not (0 <= dx < W):
                    continue
                if CRT_X <= dx < CRT_X + 260 and CRT_Y <= dy < CRT_Y + 186:
                    continue                      # never wash the tube
                st = strength.get(c[:3])
                if st is None:
                    continue
                if st >= GLOW_EMISSIVE:
                    out[dx, dy] = c          # a specular: painted, always
                    continue
                steps = 1 if st in (0, 3) else 2
                now = out[dx, dy][:3]
                for _ in range(steps):
                    nxt = up.get(now)
                    if nxt is None:
                        break                     # no headroom: the light adds nothing here
                    now = nxt
                out[dx, dy] = now + (255,)


COLD_WARM_RAMP = ['teal_2', 'cyan_0', 'cyan_1', 'amber_1', 'amber_2', 'amber_3', 'cyan_2', 'amber_4']
GLOW_EMISSIVE = 6


def MenuLayoutRamps():
    """The ramp table, read from MenuLayout.cs so the twin and the game cannot drift apart."""
    import re
    src = open(os.path.join(ROOT, 'unity', 'Assets', 'Scripts', 'Core', 'Rules', 'MenuLayout.cs'),
               encoding='utf-8').read()
    body = src[src.index('Ramps ='):]
    body = body[:body.index('};')]
    return re.findall(r"\{\s*\"([a-z_0-9]+)\"\s*,\s*\"([a-z_0-9]+)\"\s*\}", body)


def dust(img, clock=1.7):
    """
    MenuView.Dust(): motes that FALL. Deterministic in `clock`, so the still is the frame the game would
    draw at that moment -- not a random scatter nobody could reproduce.
    """
    import math
    px = img.load()
    grain = PALETTE['steel_1'] + (255,)
    faint = PALETTE['wall_2'] + (255,)
    for i in range(30):
        h1 = dust_hash(i, 3)
        h2 = dust_hash(i, 11)
        speed = 5.5 * (0.55 + (h2 & 0xFF) / 255.0 * 0.9)
        x0 = (h1 & 0xFFFF) / 65535.0 * W
        y0 = ((h1 >> 16) & 0xFFFF) / 65535.0 * H
        y = (y0 + clock * speed) % H
        x = (x0 - clock * 1.7 * (0.6 + ((h2 >> 8) & 0xFF) / 255.0) + math.sin(clock * 0.7 + i) * 1.6) % W
        xi, yi = int(x), int(y)
        if not (0 <= xi < W and 0 <= yi < H):
            continue
        if GLASS_X <= xi < GLASS_X + GLASS_W and GLASS_Y <= yi < GLASS_Y + GLASS_H:
            continue
        px[xi, yi] = grain if i % 3 == 0 else faint
        if i % 7 == 0 and xi + 1 < W:
            px[xi + 1, yi] = faint


def labels(has_save=True, in_progress=True):
    first = ('CONTINUE RUN' if in_progress else 'CONTINUE') if has_save else 'START'
    return [first, 'NEW SHIFT', 'FIELD MANUAL', 'SETTINGS', 'CREDITS']


GLASS_PAPER = (11, 14, 19, 255)     # `void`: the tube at rest, one step darker than the case
GLASS_WASH = (12, 61, 56, 255)      # `glow_teal`: the phosphor's own faint cyan


def glass_material(img):
    """The screen is a surface, not a hole: an off-black phosphor with a scanline every 3 rows and a
    soft vertical wash. Without this the glass matches the bezel and the monitor disappears."""
    d = ImageDraw.Draw(img)
    d.rectangle([GLASS_X, GLASS_Y, GLASS_X + GLASS_W - 1, GLASS_Y + GLASS_H - 1], fill=GLASS_PAPER)
    # scanlines: the phosphor's cyan lives in the top third of the tube, where the beam is brightest
    for y in range(GLASS_Y, GLASS_Y + GLASS_H):
        if (y - GLASS_Y) % 3 == 2:
            shade = GLASS_WASH if y < GLASS_Y + GLASS_H // 3 else (14, 28, 34, 255)
            d.line([GLASS_X, y, GLASS_X + GLASS_W - 1, y], fill=shade)
    # the tube's own edge: one emissive line just inside the bezel, on the top and left only
    d.line([GLASS_X, GLASS_Y, GLASS_X + GLASS_W - 1, GLASS_Y], fill=(15, 168, 138, 255))
    d.line([GLASS_X, GLASS_Y, GLASS_X, GLASS_Y + GLASS_H - 1], fill=(15, 168, 138, 255))
    # the bottom of a CRT tube is always a little dimmer
    for y in range(GLASS_Y + GLASS_H - 6, GLASS_Y + GLASS_H):
        for x in range(GLASS_X, GLASS_X + GLASS_W):
            if ((x + y) & 1) == 0:
                img.putpixel((x, y), GLASS_WASH)
    return img


def dust_hash(x, y):
    """Wang mix, mirrored from MenuLayout.DustHash(). The obvious (x*A ^ y*B) & 15 is periodic at 16 px and
    draws a screen-door mesh over the whole room."""
    h = (x * 374761393 + y * 668265263) & 0xFFFFFFFF
    h = ((h ^ (h >> 13)) * 1274126177) & 0xFFFFFFFF
    return h ^ (h >> 16)


READOUTS = [
    ("NODE 07/12", "LIVE 98.2%"),
    ("AUTH 0X4F2", "HANDSHAKE"),
    ("ICE BREACH", "TRACE FAILED"),
    ("PORT 22", "MONITOR"),
    ("SYS.ROOT", "?? INTRUDER"),
    ("KEY ROTATE", "DONE"),
]


def stack_screens(img, clock=2.4, breach=-1):
    """
    MenuView.StackScreens(), mirrored: the tube of EVERY cell is painted inside its own case, so the readout
    follows the art instead of sitting on a fixed 3x2 grid (that grid is what made the cluster look like grey
    stickers in v118). Cyan beam, brighter along the top of the tube, falling into teal at the bottom.
    """
    pal = PALETTE
    out = img.load()
    bezel, chin = 4, 6
    for n, (cx, cy, cw, ch) in enumerate(STACK_CELLS):
        sx, sy = cx + bezel, cy + bezel
        sw, sh = cw - bezel * 2, ch - bezel - chin
        if sw <= 0 or sh <= 0:
            continue
        breached = n == breach
        glass = pal['red_2'] if breached else pal['teal_1']
        beam = pal['white'] if breached else pal['cyan_2']
        for yy in range(sy, sy + sh):
            for xx in range(sx, sx + sw):
                if not (0 <= xx < W and 0 <= yy < H):
                    continue
                t = yy - sy
                c = glass
                if t == 0:
                    c = beam
                elif t == 1 and (xx + yy) % 3 != 0:
                    c = pal['cyan_1']
                elif t >= sh - 2:
                    c = pal['teal_0']
                out[xx, yy] = c + (255,)
        one, two = READOUTS[n % len(READOUTS)]
        for line, text in enumerate((one, two)):
            ly = sy + 2 + line * 3
            if ly >= sy + sh - 1:
                break
            _mark(out, sx + 1, ly, min(len(text), sw - 2), beam)


def _mark(out, x0, y, cells, colour):
    """One row of data on a 20 px screen: a bar of 'text', not a paragraph."""
    for i in range(cells):
        x = x0 + i
        if 0 <= x < W and 0 <= y < H:
            out[x, y] = colour + (255,)


def paint_menu(img, rows, clock=2.4):
    """
    The words, with the v118 type: a 2x title with a 1 px cell gap and a 1 px bleed (thicker and tighter),
    ZERO DAY 4 px under it, the tagline, a fading rule, five rows at 14 px pitch, and a footer with its own
    air. Mirrors MenuView.Text().
    """
    pal = PALETTE
    _, atlas, rects = load_font()

    def ink(ch, x0, y0, scale, gap, bleed, key):
        o = rects.get(ch)
        if o is None:
            return
        for yy in range(GH):
            for xx in range(GW):
                if atlas.getpixel((o[0] + xx, o[1] + yy))[3] == 0:
                    continue
                for sy in range(scale):
                    for sx in range(scale + bleed):
                        px_, py_ = x0 + xx * scale + sx, y0 + yy * scale + sy
                        if TEXT_X <= px_ < TEXT_X + TEXT_W and TEXT_Y <= py_ < TEXT_Y + TEXT_H:
                            out[px_, py_] = pal[key] + (255,)

    out = img.load()

    def line(text, top, key, scale, gap, bleed, centre_on_tube=True):
        cell = (GW + gap) * scale
        width = len(text) * cell - gap * scale + bleed
        x0 = TEXT_X + (TEXT_W - width) // 2
        for i, ch in enumerate(text):
            if ch == ' ':
                continue
            ink(ch, x0 + i * cell, top, scale, gap, bleed, key)

    # the title block
    line('HUMAN RESOURCES', TITLE_TOP, 'white', TITLE_SCALE, TITLE_GAP, TITLE_BLEED)
    line('ZERO DAY', SUB_TOP, 'red_2', TITLE_SCALE, TITLE_GAP, TITLE_BLEED)
    line('NOT EVERY APPLICANT IS A PERSON', TAG_TOP, 'amber_3', 1, 2, 0)

    # the rule: fades at both ends, so it reads as a highlight and not as a divider
    y = RULE_TOP
    x0, x1 = TEXT_X + 14, TEXT_X + TEXT_W - 14
    for x in range(x0, x1):
        d = min(x - x0, x1 - x)
        if d < 10 and ((x + y) & 1) == 0:
            continue
        if d < 4 and (x & 1) == 1:
            continue
        out[x, y] = pal['steel_1'] + (255,)

    # the rows
    for i, label in enumerate(rows):
        top = PILL_TOP + i * PILL_STEP
        sel = i == 0
        fill = pal['cyan_1'] + (255,) if sel else pal['void'] + (255,)
        edge = pal['cyan_2'] + (255,) if sel else pal['steel_1'] + (255,)
        for yy in range(top, top + PILL_H):
            for xx in range(PILL_X, PILL_X + PILL_W):
                border = yy in (top, top + PILL_H - 1) or (not sel and xx in (PILL_X, PILL_X + PILL_W - 1))
                out[xx, yy] = edge if border else fill
        # the selection's pulse: one row of the cap, brighter, at ~1 Hz
        if sel:
            for xx in range(PILL_X + 2, PILL_X + PILL_W - 2):
                out[xx, top + PILL_H - 1] = pal['cyan_2'] + (255,)
        tw = len(label) * (GW + 2) - 2
        line(label, top + (PILL_H - GH) // 2, 'void' if sel else 'paper_2', 1, 2, 0)

    foot = '[SPACE] CHOOSE   [S] SOUND'
    line(foot, FOOTER_TOP, 'paper_2', 1, 2, 0)


def geometry():
    """Every sprite inside 480x270, the back-wall pieces not overlapping each other, the desk meeting the
    wall, and the monitor standing on the desk. This gate exists because v115 shipped a window tucked
    behind the monitor stack."""
    boxes = {
        'window': (WIN_X, WIN_Y, *kit('menu/window').size),
        'stack': (STACK_X, STACK_Y, *kit('menu/monitor_stack').size),
        'lamp': (LAMP_X, LAMP_Y, *kit('menu/lamp').size),
        'server': (SRV_X, SRV_Y, *kit('menu/server').size),
        'crt': (CRT_X, CRT_Y, *kit('menu/crt').size),
        'desk': (0, DESK_Y, *kit('menu/desk').size),
    }
    problems = []
    for name, (x, y, w, h) in boxes.items():
        if x < 0 or y < 0 or x + w > W or y + h > H:
            problems.append('%s spills off the frame (%d,%d %dx%d)' % (name, x, y, w, h))
    # the backdrop pieces must not overlap. The desk is in FRONT, and the lamp's shade deliberately
    # hangs over the wall behind the server, so those two are excluded.
    # v119: the stack and the desk overlap ON PURPOSE -- the stack sprite now carries a table whose legs
    # stand on the floor, and the desk is the front plane that the table stands behind. What must hold is
    # that the cluster's TABLE TOP is in front of the monitor and clear of the glass.
    back = ['window', 'crt', 'server']
    for i, a in enumerate(back):
        ax, ay, aw, ah = boxes[a]
        for b in back[i + 1:]:
            bx, by, bw, bh = boxes[b]
            if ax < bx + bw and bx < ax + aw and ay < by + bh and by < ay + ah:
                problems.append('%s overlaps %s' % (a, b))
    stx, sty, stw, sth = boxes['stack']
    # the table stands BEHIND the monitor, so its slab must pass behind the glass, not across it
    if STACK_TABLE_TOP >= GLASS_Y + GLASS_H:
        problems.append('the cluster\'s table is level with the bottom of the glass, so it floats')
    # the stack sprite may start west of the monitor's right edge -- it is behind it -- but enough of it
    # must survive the monitor to read as a table: at least 50 px between the monitor and the server.
    visible = (stx + stw) - (CRT_X + 260)
    if visible < 50:
        problems.append('only %d px of the table clears the monitor' % visible)
    if sty + sth > DESK_Y + 6:
        problems.append('the table\'s legs run past the desk\'s far edge into the front plane')
    if sty + sth < DESK_Y - 2:
        problems.append('the table stops in mid-air %d px above the floor plane'
                        % (DESK_Y - (sty + sth)))
    if DESK_TOP > CRT_Y + 186:
        problems.append('the monitor floats above the desk')
    if DESK_TOP > 196:
        problems.append('the desk does not meet the wall at the floor line')
    return problems


def check():
    problems = geometry()
    if PILL_TOP + 4 * PILL_STEP + PILL_H > FOOTER_TOP:
        problems.append('the pills run into the footer')
    if TITLE_TOP + TITLE_SCALE * GH > SUB_TOP:
        problems.append('the title runs into ZERO DAY')
    if SUB_TOP + TITLE_SCALE * GH > TAG_TOP:
        problems.append('ZERO DAY runs into the tagline')
    if TAG_TOP + GH > RULE_TOP:
        problems.append('the tagline runs into the rule')
    for label in labels():
        if len(label) * (GW + 2) - 2 > PILL_W:
            problems.append('"%s" does not fit a %d px pill' % (label, PILL_W))
    for text, scale, gap, bleed, what in (
            ('HUMAN RESOURCES', TITLE_SCALE, TITLE_GAP, TITLE_BLEED, 'title'),
            ('ZERO DAY', TITLE_SCALE, TITLE_GAP, TITLE_BLEED, 'ZERO DAY'),
            ('NOT EVERY APPLICANT IS A PERSON', 1, 2, 0, 'tagline')):
        w = len(text) * (GW + gap) * scale - gap * scale + bleed
        if w > TEXT_W:
            problems.append('the %s is %d px and the tube holds %d' % (what, w, TEXT_W))
    if PILL_X < TEXT_X or PILL_X + PILL_W > TEXT_X + TEXT_W:
        problems.append('the rows run out of the tube sideways')
    if FOOTER_TOP + GH > TEXT_Y + TEXT_H:
        problems.append('the footer runs below the tube')
    if not os.path.exists(os.path.join(ROOT, 'art', 'kit', 'menu', 'lamp.png')):
        problems.append('menu/lamp.png is missing: the room has no warm key')
    return problems


def main():
    problems = check()
    if problems:
        for p in problems:
            print('  ' + p)
        print('FAIL: the frame does not fit')
        return 1
    if '--check' in sys.argv:
        print('PASS: title, tagline, five rows, footer and six props fit the %dx%d tube and the frame'
              % (GLASS_W, GLASS_H))
        return 0

    img = compose()
    glass_material(img)
    light(img)
    vignette(img)
    glow(img)          # the glow layer: after the vignette, and it only adds
    dust(img)
    stack_screens(img)
    paint_menu(img, labels())
    base = os.path.join(ROOT, 'art', 'kit', 'reference')
    img.convert('RGB').save(os.path.join(base, 'menu_v119_1x.png'))
    img.convert('RGB').resize((W * 3, H * 3), Image.NEAREST).save(os.path.join(base, 'menu_v119_3x.png'))
    print('wrote art/kit/reference/menu_v119_{1x,3x}.png')
    return 0


if __name__ == '__main__':
    raise SystemExit(main())
