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

    python tools/art/make_menu_composite.py           # writes art/kit/reference/menu_v115_{1x,3x}.png
    python tools/art/make_menu_composite.py --check    # geometry gate
"""
import os
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
sys.path.insert(0, os.path.join(ROOT, 'tools', 'comp'))
sys.path.insert(0, os.path.join(ROOT, 'tools', 'art'))

from PIL import Image, ImageDraw  # noqa: E402
from preview_intro import load_font, draw_text, GLYPH_W as GW, GLYPH_H as GH, LETTER_GAP as LG  # noqa: E402
from make_art_kit import PALETTE  # noqa: E402

W, H = 480, 270

# ---- mirrored from MenuLayout.cs --------------------------------------------------------------------
# These MUST equal unity/Assets/Scripts/Core/Rules/MenuLayout.cs. check() asserts it against the sprite
# sizes, so a stale number here fails the gate instead of quietly painting the room wrong.
WIN_X, WIN_Y = 368, 34
STACK_X, STACK_Y = 260, 102
SRV_X, SRV_Y = 454, 90
CRT_X, CRT_Y = 14, 30
GLASS_X, GLASS_Y, GLASS_W, GLASS_H = 28, CRT_Y + 14, 212, 126
DESK_Y = 184          # DESK_TOP=18 of a 64 px sprite: the slab still meets the wall at 202
TITLE_SCALE = 2
TITLE_TOP, SUB_TOP, TAG_TOP = GLASS_Y + 8, GLASS_Y + 24, GLASS_Y + 40
PILL_X, PILL_W, PILL_H, PILL_STEP = GLASS_X + 52, 122, 11, 13
PILL_TOP = GLASS_Y + 52
FOOTER_TOP = GLASS_Y + GLASS_H - 9

# the ramp + the three lights, mirrored from MenuLayout.cs
RAMP = ['void', 'panel', 'panel2', 'line']
CYAN_NEAR = 14
FADE_REACH = 84
LIGHTS = [
    (STACK_X + 50, STACK_Y + 26, 96, 88, 2.10, 1),
    (WIN_X + 41, WIN_Y + 54, 92, 106, 1.90, 1),
    (GLASS_X + GLASS_W // 2, GLASS_Y + GLASS_H // 2, 232, 176, 1.55, 2),
]


def kit(rel):
    return Image.open(os.path.join(ROOT, 'art', 'kit', rel + '.png')).convert('RGBA')


def compose():
    img = kit('menu/shell').copy()
    # BACK TO FRONT, exactly MenuView.BuildRoom(): wall, window, table, server, monitor, desk.
    for rel, x, y in (('menu/window', WIN_X, WIN_Y),
                      ('menu/monitor_stack', STACK_X, STACK_Y),
                      ('menu/server', SRV_X, SRV_Y),
                      ('menu/crt', CRT_X, CRT_Y),
                      ('menu/desk', 0, DESK_Y)):
        img.alpha_composite(kit(rel), (x, y))
    return img


def light(img):
    """
    The same value-ramp lighting MenuView.Light() runs: a light adds ramp steps to a surface, it never
    replaces the surface with a light colour. The fractional step becomes the dither.
    """
    import math
    px = img.load()
    key_of = {v: k for k, v in PALETTE.items()}
    idx_of = {PALETTE[k]: i for i, k in enumerate(RAMP)}
    teal, teal_dim = PALETTE['glow_teal'], PALETTE['signaldim']

    for y in range(H):
        for x in range(W):
            c = px[x, y]
            if c[3] == 0:
                continue
            # the monitor's own case and tube are a light SOURCE: the light pass never touches them
            if CRT_X <= x < CRT_X + 240 and CRT_Y <= y < CRT_Y + 172:
                continue
            step = idx_of.get(c[:3])
            if step is None:
                continue                       # emitting colour (white, signal, paper): leave it alone
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
            if frac > 0.65:
                add += 1
            elif frac > 0.35 and pat < 2:
                add += 1
            if add <= 0:
                continue
            add = min(add, 1)          # MaxLift: one step, never two
            target = min(step + add, len(RAMP) - 1)
            dxg = max(GLASS_X - x, x - (GLASS_X + GLASS_W), 0)
            dyg = max(GLASS_Y - y, y - (GLASS_Y + GLASS_H), 0)
            near = dxg + dyg < CYAN_NEAR
            px[x, y] = teal if near else PALETTE[RAMP[target]]

            # the cyan fade, measured from the CASE: mirrors MenuView.Light()
            if step <= 1:
                fx = max(CRT_X - x, x - (CRT_X + 239), 0)
                fy = max(CRT_Y - y, y - (CRT_Y + 171), 0)
                fd = max(fx, fy)
                if fd < FADE_REACH:
                    thr = int(((1.0 - fd / FADE_REACH) ** 1.15) * 15.0)
                    pat4 = (x & 3) + ((y & 3) << 2)
                    if pat4 < thr:
                        px[x, y] = teal

    # the CRT's own spill: TWO rings, dark teal, the outer one dithered. Same as MenuView.Halo().
    for ring in range(1, 3):
        x0, y0 = CRT_X - ring, CRT_Y - ring
        x1, y1 = CRT_X + 240 + ring, CRT_Y + 172 + ring
        for x in range(x0, x1 + 1):
            for yy in (y0, y1):
                _halo_px(px, x, yy, ring, teal, teal_dim)
        for y in range(y0, y1 + 1):
            for xx in (x0, x1):
                _halo_px(px, xx, y, ring, teal, teal_dim)

    # vignette
    dark = PALETTE['void']
    for y in range(H):
        for x in range(W):
            c = px[x, y]
            if c[3] == 0:
                continue
            if CRT_X <= x < CRT_X + 240 and CRT_Y <= y < CRT_Y + 172:
                continue
            dx = (x - W / 2) / (W / 2)
            dy = (y - H / 2) / (H / 2)
            r = math.sqrt(dx * dx + dy * dy)
            if r > 1.25:
                px[x, y] = dark
            elif r >= 1.0 and ((x + y) & 1) == 0:
                px[x, y] = dark


def _halo_px(px, x, y, ring, teal, teal_dim):
    if not (0 <= x < W and 0 <= y < H):
        return
    c = px[x, y]
    if c[3] == 0:
        return
    if ring == 2 and ((x + y) & 3) != 0:
        return
    px[x, y] = teal


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


def paint_menu(img, rows):
    pal, atlas, rects = load_font()

    def ink(ch, x0, y0):
        o = rects.get(ch)
        if o is None:
            return
        for yy in range(GH):
            for xx in range(GW):
                if atlas.getpixel((o[0] + xx, o[1] + yy))[3] > 0:
                    yield x0 + xx, y0 + yy

    def big(text, top, key, scale):
        cell = (GW + LG) * scale
        width = len(text) * cell - LG * scale
        x0 = GLASS_X + (GLASS_W - width) // 2
        for i, ch in enumerate(text):
            if ch == ' ':
                continue
            for px_, py_ in ink(ch, x0 + i * cell, top):
                for sy in range(scale):
                    for sx in range(scale):
                        x, y = px_ + sx, py_ + sy
                        if GLASS_X <= x < GLASS_X + GLASS_W and GLASS_Y <= y < GLASS_Y + GLASS_H:
                            img.putpixel((x, y), pal[key])

    def small(text, top, key, left):
        draw_text(img, rects, atlas, pal, left, top, text, key)

    big('HUMAN RESOURCES', TITLE_TOP, 'text', TITLE_SCALE)
    big('ZERO DAY', SUB_TOP, 'danger', TITLE_SCALE)
    tag = 'NOT EVERY APPLICANT IS A PERSON'
    small(tag, TAG_TOP, 'warn', GLASS_X + (GLASS_W - (len(tag) * (GW + LG) - LG)) // 2)

    d = ImageDraw.Draw(img)
    for i, label in enumerate(rows):
        top = PILL_TOP + i * PILL_STEP
        sel = i == 0
        # unselected rows are a thin rule, selected is a filled bar -- the concept's hierarchy
        if sel:
            d.rectangle([PILL_X, top, PILL_X + PILL_W - 1, top + PILL_H - 1], fill=pal['signal'])
        else:
            d.rectangle([PILL_X, top, PILL_X + PILL_W - 1, top + PILL_H - 1],
                        fill=pal['panel'], outline=pal['line'])
        small(label, top + (PILL_H - GH) // 2, 'void' if sel else 'text',
              PILL_X + (PILL_W - (len(label) * (GW + LG) - LG)) // 2)

    foot = '[SPACE] CHOOSE   [S] SOUND'
    small(foot, FOOTER_TOP, 'textdim', GLASS_X + (GLASS_W - (len(foot) * (GW + LG) - LG)) // 2)


def geometry():
    """Every sprite must sit inside 480x270 and the four back-wall items must not overlap each other.
    This gate exists because v115 first shipped with the window tucked behind the monitor stack."""
    boxes = {
        'window': (WIN_X, WIN_Y, *kit('menu/window').size),
        'stack': (STACK_X, STACK_Y, *kit('menu/monitor_stack').size),
        'server': (SRV_X, SRV_Y, *kit('menu/server').size),
        'crt': (CRT_X, CRT_Y, *kit('menu/crt').size),
        'desk': (0, DESK_Y, *kit('menu/desk').size),
    }
    problems = []
    for name, (x, y, w, h) in boxes.items():
        if x < 0 or y < 0 or x + w > W or y + h > H:
            problems.append('%s spills off the frame (%d,%d %dx%d)' % (name, x, y, w, h))
    back = [k for k in boxes if k != 'desk']
    for i, a in enumerate(back):
        ax, ay, aw, ah = boxes[a]
        for b in back[i + 1:]:
            bx, by, bw, bh = boxes[b]
            if ax < bx + bw and bx < ax + aw and ay < by + bh and by < ay + ah:
                problems.append('%s overlaps %s' % (a, b))
    return problems


def check():
    problems = geometry()
    if PILL_TOP + 4 * PILL_STEP + PILL_H > FOOTER_TOP:
        problems.append('the pills run into the footer')
    if TITLE_TOP + TITLE_SCALE * GH > SUB_TOP:
        problems.append('the title runs into ZERO DAY')
    for label in labels():
        if len(label) * (GW + LG) - LG > PILL_W:
            problems.append('"%s" does not fit a %d px pill' % (label, PILL_W))
    title_w = len('HUMAN RESOURCES') * (GW + LG) * TITLE_SCALE - LG * TITLE_SCALE
    if title_w > GLASS_W:
        problems.append('the %dx title is %d px, the glass is %d' % (TITLE_SCALE, title_w, GLASS_W))
    glass_bottom = GLASS_Y + GLASS_H
    if FOOTER_TOP + GH > glass_bottom:
        problems.append('the footer runs below the glass')
    return problems


def main():
    problems = check()
    if problems:
        for p in problems:
            print('  ' + p)
        print('FAIL: the frame does not fit')
        return 1
    if '--check' in sys.argv:
        print('PASS: title, tagline, five pills and footer fit the CRT glass (%dx%d)' % (GLASS_W, GLASS_H))
        return 0

    img = compose()
    glass_material(img)
    light(img)
    paint_menu(img, labels())
    base = os.path.join(ROOT, 'art', 'kit', 'reference')
    img.save(os.path.join(base, 'menu_v115_1x.png'))
    img.resize((W * 3, H * 3), Image.NEAREST).save(os.path.join(base, 'menu_v115_3x.png'))
    print('wrote art/kit/reference/menu_v115_{1x,3x}.png')
    return 0


if __name__ == '__main__':
    raise SystemExit(main())
