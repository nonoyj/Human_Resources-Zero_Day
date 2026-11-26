#!/usr/bin/env python3
"""
Desk background study -- builds the *candidate* office you sit in at night.

Direction (user, 2026-11-26): keep the CRT monitor look we already have; replace the background with a
believable office (no server racks, no wall of monitors) that keeps the menu concept's mood -- dark,
mysterious, mint-on-navy, one warm lamp.  Palette is the locked kit palette (make_art_kit.PALETTE) so
these can be promoted into art/kit with no colour drift.

Writes to art/concepts/study_*.png and a side-by-side contact sheet. Nothing here is shipped art.
"""
import os, sys
from PIL import Image, ImageDraw

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
sys.path.insert(0, os.path.join(ROOT, 'tools', 'art'))
from make_art_kit import PALETTE as PAL, rgba, new, save as _save  # noqa: E402

OUT = os.path.join(ROOT, 'art', 'concepts')
W, H = 480, 270
PANEL_TOP = 40        # far wall starts here (above = ceiling shadow)
FLOOR_Y = 168         # wall meets floor
DESK_Y = 214          # desk front edge


def px(img, x, y, c):
    if 0 <= x < img.width and 0 <= y < img.height:
        img.putpixel((x, y), rgba(c))


def base_room(img, d):
    d.rectangle([0, 0, W - 1, H - 1], fill=rgba('void'))
    # ceiling shadow band + skirting
    d.rectangle([0, 0, W - 1, PANEL_TOP - 1], fill=rgba('void'))
    d.rectangle([0, PANEL_TOP, W - 1, FLOOR_Y - 1], fill=rgba('panel'))
    # wall panel seams (vertical, like the kit's existing background)
    for x in range(0, W, 48):
        d.line([x, PANEL_TOP, x, FLOOR_Y - 1], fill=rgba('panel2'))
    d.line([0, PANEL_TOP, W - 1, PANEL_TOP], fill=rgba('panel2'))
    # floor
    d.rectangle([0, FLOOR_Y, W - 1, H - 1], fill=rgba('void'))
    d.line([0, FLOOR_Y, W - 1, FLOOR_Y], fill=rgba('panel2'))


# --------------------------------------------------------------------------- props
def window_night(img, d, x, y, w, h):
    """Night window: sky, moon, skyline, blinds half drawn."""
    d.rectangle([x, y, x + w, y + h], fill=rgba('panel2'), outline=rgba('line'))
    d.rectangle([x + 2, y + 2, x + w - 2, y + h - 2], fill=rgba('void'))
    # moon
    cx, cy, r = x + w - 22, y + 20, 7
    d.ellipse([cx - r, cy - r, cx + r, cy + r], fill=rgba('textdim'))
    d.ellipse([cx - r + 3, cy - r - 1, cx + r + 3, cy + r - 1], fill=rgba('void'))
    # skyline silhouette
    sky = [(x + 6, h - 34), (x + 18, h - 18), (x + 30, h - 42), (x + 44, h - 26),
           (x + 56, h - 48), (x + 68, h - 22), (x + 80, h - 38), (x + 92, h - 16),
           (x + 104, h - 30)]
    for bx, depth in sky:
        if bx + 10 < x + w - 4:
            bot = y + h - 3
            d.rectangle([bx, bot - depth, bx + 9, bot], fill=rgba('panel'))
            for wy in range(bot - depth + 4, bot - 2, 7):
                if (bx + wy) % 5 < 2:
                    d.point((bx + 3, wy), fill=rgba('warn'))
                    d.point((bx + 6, wy), fill=rgba('orange'))
    # blinds: horizontal slats over the top half
    for by in range(y + 3, y + h // 2, 5):
        d.line([x + 2, by, x + w - 2, by], fill=rgba('line'))
    d.rectangle([x, y, x + w, y + h], outline=rgba('linehi'))


def filing_cabinet(img, d, x, y, w=34, h=62):
    d.rectangle([x, y, x + w, y + h], fill=rgba('panel2'), outline=rgba('line'))
    for i in range(3):
        dy = y + 4 + i * 20
        d.rectangle([x + 3, dy, x + w - 3, dy + 16], fill=rgba('panel'), outline=rgba('line'))
        d.line([x + 10, dy + 8, x + w - 10, dy + 8], fill=rgba('linehi'))   # handle
    d.line([x, y + 2, x + w, y + 2], fill=rgba('linehi'))


def noticeboard(img, d, x, y, w=64, h=44):
    d.rectangle([x, y, x + w, y + h], fill=rgba('panel2'), outline=rgba('linehi'))
    d.rectangle([x + 2, y + 2, x + w - 2, y + h - 2], fill=rgba('panel'))
    papers = [(x + 6, y + 6, 20, 16), (x + 32, y + 5, 24, 12), (x + 10, y + 26, 22, 13), (x + 38, y + 22, 18, 16)]
    for (px_, py_, pw, ph) in papers:
        d.rectangle([px_, py_, px_ + pw, py_ + ph], fill=rgba('void'), outline=rgba('line'))
        d.point((px_ + pw // 2, py_ + 1), fill=rgba('danger'))          # pin
        for i in range(2, ph - 2, 4):
            d.line([px_ + 3, py_ + i, px_ + pw - 3, py_ + i], fill=rgba('panel2'))


def wall_clock(img, d, cx, cy, r=9):
    d.ellipse([cx - r, cy - r, cx + r, cy + r], fill=rgba('panel2'), outline=rgba('linehi'))
    d.line([cx, cy, cx, cy - 5], fill=rgba('textdim'))
    d.line([cx, cy, cx + 3, cy + 2], fill=rgba('textdim'))
    d.point((cx, cy), fill=rgba('signal'))


def plant(img, d, x, y):
    d.rectangle([x + 5, y + 22, x + 15, y + 32], fill=rgba('panel2'), outline=rgba('line'))
    for (dx, dy) in ((0, 0), (-6, 4), (6, 3), (-3, -6), (4, -4), (-8, -1), (8, -2)):
        d.line([x + 10, y + 22, x + 10 + dx, y + 22 - dy - 8], fill=rgba('signaldim'))
        d.point((x + 10 + dx, y + 22 - dy - 8), fill=rgba('signal'))


def water_cooler(img, d, x, y):
    d.rectangle([x + 3, y + 12, x + 15, y + 34], fill=rgba('panel2'), outline=rgba('line'))
    d.rectangle([x + 6, y + 2, x + 12, y + 12], fill=rgba('muted'))
    d.point((x + 7, y + 5), fill=rgba('textdim'))
    d.point((x + 8, y + 14), fill=rgba('signal'))
    d.point((x + 11, y + 14), fill=rgba('danger'))


def coat_stand(img, d, x, y):
    d.line([x, y, x, y + 46], fill=rgba('linehi'))
    d.line([x - 7, y + 2, x, y - 2], fill=rgba('linehi'))
    d.line([x, y - 2, x + 7, y + 2], fill=rgba('linehi'))
    d.line([x - 2, y + 48, x + 2, y + 48], fill=rgba('linehi'))
    # jacket hung on it
    d.rectangle([x + 2, y + 4, x + 14, y + 26], fill=rgba('panel2'), outline=rgba('line'))
    d.line([x + 4, y + 6, x + 12, y + 6], fill=rgba('line'))


def desk_lamp_glow(img, x, y, r=46, strength=14):
    """Cheap radial warm bloom using the kit's warm colours."""
    for i in range(strength, 0, -1):
        rr = int(r * i / strength)
        col = 'paperdim' if i < strength // 2 else 'warn'
        a = max(4, int(26 * (1 - i / strength)))
        d = ImageDraw.Draw(img, 'RGBA')
        d.ellipse([x - rr, y - rr // 2, x + rr, y + rr // 2], fill=rgba(col, a))


def desk_front(img, d):
    """The near desk edge you sit at -- dark, so the UI reads on top of it."""
    d.rectangle([0, DESK_Y, W - 1, H - 1], fill=rgba('panel'))
    d.line([0, DESK_Y, W - 1, DESK_Y], fill=rgba('linehi'))
    d.line([0, DESK_Y + 2, W - 1, DESK_Y + 2], fill=rgba('panel2'))
    for x in range(0, W, 32):
        d.line([x, DESK_Y + 3, x, H - 1], fill=rgba('panel2'))


# --------------------------------------------------------------------------- variants
def study_a():
    """'Shift' look: window right, cabinet + noticeboard left, clock, plant, lamp. Quiet and office-y."""
    img = new(W, H); d = ImageDraw.Draw(img)
    base_room(img, d)
    window_night(img, d, 292, 56, 168, 100)
    filing_cabinet(img, d, 22, 92)
    d.line([56, 96, 92, 96], fill=rgba('line'))                       # shelf on the wall
    noticeboard(img, d, 108, 62, 70, 48)
    wall_clock(img, d, 214, 74)
    plant(img, d, 236, 128)
    water_cooler(img, d, 268, 120)
    coat_stand(img, d, 36, 20)
    desk_lamp_glow(img, 90, 120)
    desk_front(img, d)
    return img


def study_b():
    """'After hours' look: solid blind wall, one warm lamp pooling light, everything else in shadow."""
    img = new(W, H); d = ImageDraw.Draw(img)
    base_room(img, d)
    # darken the wall further
    d.rectangle([0, PANEL_TOP, W - 1, FLOOR_Y - 1], fill=rgba('void'))
    for x in range(0, W, 48):
        d.line([x, PANEL_TOP, x, FLOOR_Y - 1], fill=rgba('panel'))
    d.line([0, PANEL_TOP, W - 1, PANEL_TOP], fill=rgba('panel'))
    d.line([0, FLOOR_Y, W - 1, FLOOR_Y], fill=rgba('panel'))
    # a single slot of window light on the right
    window_night(img, d, 352, 62, 108, 88)
    # desk lamp on the left + its bloom
    d.line([72, 150, 72, 196], fill=rgba('linehi'))
    d.polygon([(58, 148), (86, 148), (80, 136), (64, 136)], fill=rgba('panel2'))
    d.line([58, 148, 86, 148], fill=rgba('warn'))
    desk_lamp_glow(img, 72, 158, r=70, strength=18)
    # low filing cabinet in shadow, right of lamp
    filing_cabinet(img, d, 132, 118, w=30, h=52)
    wall_clock(img, d, 200, 78)
    coat_stand(img, d, 250, 24)
    plant(img, d, 300, 132)
    desk_front(img, d)
    # rim light along the desk edge on the lamp side
    d.line([0, DESK_Y, 150, DESK_Y], fill=rgba('paperdim'))
    return img


def study_c():
    """'Night shift' look: study_a props but the window is the hero, cool mint moonlight raking in."""
    img = new(W, H); d = ImageDraw.Draw(img)
    base_room(img, d)
    window_night(img, d, 250, 48, 210, 116)
    # moonlight shaft on the floor
    d.polygon([(258, FLOOR_Y), (452, FLOOR_Y), (470, H - 2), (300, H - 2)], fill=rgba('panel'))
    d.polygon([(262, FLOOR_Y), (300, FLOOR_Y), (330, H - 2), (300, H - 2)], fill=rgba('panel2'))
    filing_cabinet(img, d, 18, 96)
    noticeboard(img, d, 62, 66, 62, 42)
    wall_clock(img, d, 156, 74)
    plant(img, d, 178, 130)
    coat_stand(img, d, 214, 26)
    desk_lamp_glow(img, 40, 128, r=54, strength=12)
    desk_front(img, d)
    d.line([240, DESK_Y, 470, DESK_Y], fill=rgba('muted'))       # cool rim where light lands
    return img


VARIANTS = {'A_shift': study_a, 'B_after_hours': study_b, 'C_night_shift': study_c}


def main():
    os.makedirs(OUT, exist_ok=True)
    made = []
    for name, fn in VARIANTS.items():
        img = fn()
        # palette gate: every pixel must come from the kit palette (alpha 255 only)
        bad = 0
        for y in range(img.height):
            for x in range(img.width):
                p = img.getpixel((x, y))
                if p[3] != 255 or p[:3] not in {v for v in PAL.values()}:
                    bad += 1
        path = os.path.join(OUT, f'study_{name}.png')
        img.save(path)
        made.append((name, path, bad))
        print(f'wrote {path}  off-palette px: {bad}')

    # contact sheet at 2x, stacked
    sheet = Image.new('RGB', (W * 2, H * 2 * len(made)), (11, 14, 19))
    for i, (name, path, _) in enumerate(made):
        im = Image.open(path).convert('RGB').resize((W * 2, H * 2), Image.NEAREST)
        sheet.paste(im, (0, i * H * 2))
    sheet_path = os.path.join(OUT, 'study_contact_sheet.png')
    sheet.save(sheet_path)
    print(f'wrote {sheet_path}')
    worst = max(m[2] for m in made)
    print('RESULT:', 'PASS -- all variants on-palette' if worst == 0 else f'FAIL -- {worst} off-palette px')
    return 0 if worst == 0 else 1


if __name__ == '__main__':
    raise SystemExit(main())
