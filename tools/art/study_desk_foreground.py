#!/usr/bin/env python3
"""
Composition studies F/G -- answers the notes on D/E:

  * "the computer looks too big, the applicant looks small"  -> monitor down, applicant up
  * "the desk is missing"                                    -> a real desk in the foreground with
                                                               keyboard, mouse, mug, pen, paper stack
  * "a floating plant"                                       -> every prop now stands on the floor line
  * "a ceiling light too low / too close to the table"       -> removed. Warm light enters from the
                                                               off-screen left and only ever shows as a
                                                               rim on the desk edge and the monitor's
                                                               left side.

Palette rule unchanged: solid kit-palette colours only, alpha 255, no blending. Reports off-palette px.
Nothing here touches art/kit or desk_layout.json.
"""
import os, sys
from PIL import Image, ImageDraw

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
sys.path.insert(0, os.path.join(ROOT, 'tools', 'art'))
from make_art_kit import PALETTE, rgba, new   # noqa: E402

OUT = os.path.join(ROOT, 'art', 'concepts')
KIT = os.path.join(ROOT, 'unity', 'Assets', 'Art', 'Kit')
W, H = 480, 270

FLOOR_ROW = 196      # wall meets floor (image rows)
DESK_ROW = 206       # top surface of the desk you sit at
DESK_EDGE = 218      # front lip of that desk


# --------------------------------------------------------------------------- props (grounded)
def window_night(img, d, x, row, w, h):
    d.rectangle([x, row, x + w, row + h], fill=rgba('panel2'), outline=rgba('linehi'))
    d.rectangle([x + 2, row + 2, x + w - 2, row + h - 2], fill=rgba('void'))
    base = row + h - 3
    depths = (16, 28, 10, 24, 32, 14, 22, 28, 12, 20, 26, 18)
    for i, bx in enumerate(range(x + 6, x + w - 12, 11)):
        depth = depths[i % len(depths)]
        d.rectangle([bx, base - depth, bx + 9, base], fill=rgba('panel'))
        for wy in range(base - depth + 4, base - 1, 7):
            d.point((bx + 3, wy), fill=rgba('warn'))
            if (bx + wy) % 3 == 0:
                d.point((bx + 6, wy), fill=rgba('orange'))
    cx, cy, r = x + w - 22, row + 15, 6
    d.ellipse([cx - r, cy - r, cx + r, cy + r], fill=rgba('textdim'))
    d.ellipse([cx - r + 3, cy - r - 1, cx + r + 3, cy + r - 1], fill=rgba('void'))
    for by in range(row + 3, row + h // 3, 5):
        d.line([x + 2, by, x + w - 2, by], fill=rgba('line'))
    d.line([x + w // 2, row + 2, x + w // 2, row + h - 2], fill=rgba('line'))


def filing_cabinet(img, d, x, floor_row, w=34, h=56):
    """Stands ON the floor: bottom of the cabinet is the floor row."""
    top = floor_row - h
    d.rectangle([x, top, x + w, floor_row], fill=rgba('panel2'), outline=rgba('line'))
    d.line([x + 1, top + 1, x + w - 1, top + 1], fill=rgba('linehi'))
    for i in range(3):
        dy = top + 5 + i * 17
        d.rectangle([x + 3, dy, x + w - 3, dy + 13], fill=rgba('panel'), outline=rgba('line'))
        d.line([x + 10, dy + 7, x + w - 10, dy + 7], fill=rgba('linehi'))
    d.rectangle([x - 1, floor_row, x + w + 1, floor_row + 2], fill=rgba('void'))   # contact shadow


def plant_pot(img, d, x, floor_row, h=30):
    """Plant stands on the floor -- pot bottom == floor row (this was the 'floating plant' bug)."""
    pot_top = floor_row - h
    d.rectangle([x, pot_top + 10, x + 14, floor_row], fill=rgba('panel2'), outline=rgba('line'))
    d.line([x, pot_top + 10, x + 14, pot_top + 10], fill=rgba('linehi'))
    for (dx, dy) in ((0, 10), (-5, 6), (5, 7), (-2, 14), (3, 15), (-7, 11), (7, 12)):
        d.line([x + 7, pot_top + 10, x + 7 + dx, pot_top + 10 - dy], fill=rgba('signaldim'))
        d.point((x + 7 + dx, pot_top + 10 - dy), fill=rgba('signal'))
    d.rectangle([x - 1, floor_row, x + 15, floor_row + 2], fill=rgba('void'))


def noticeboard(img, d, x, row, w=62, h=38):
    d.rectangle([x, row, x + w, row + h], fill=rgba('panel2'), outline=rgba('linehi'))
    for (px_, py_, pw, ph) in ((6, 5, 19, 14), (30, 4, 23, 11), (10, 22, 20, 12), (36, 18, 17, 15)):
        d.rectangle([x + px_, row + py_, x + px_ + pw, row + py_ + ph], fill=rgba('void'), outline=rgba('line'))
        d.point((x + px_ + pw // 2, row + py_), fill=rgba('danger'))
        for i in range(3, ph - 1, 4):
            d.line([x + px_ + 3, row + py_ + i, x + px_ + pw - 3, row + py_ + i], fill=rgba('panel2'))


def wall_clock(img, d, cx, row, r=8):
    d.ellipse([cx - r, row - r, cx + r, row + r], fill=rgba('panel2'), outline=rgba('linehi'))
    d.line([cx, row, cx, row - 5], fill=rgba('textdim'))
    d.line([cx, row, cx + 3, row + 2], fill=rgba('textdim'))
    d.point((cx, row), fill=rgba('signal'))


def coat_stand(img, d, x, floor_row, h=62):
    top = floor_row - h
    d.line([x, top, x, floor_row], fill=rgba('linehi'))
    d.line([x - 7, floor_row, x + 7, floor_row], fill=rgba('linehi'))
    d.line([x - 6, top + 1, x, top - 2], fill=rgba('linehi'))
    d.line([x, top - 2, x + 6, top + 1], fill=rgba('linehi'))
    d.rectangle([x + 2, top + 3, x + 14, top + 30], fill=rgba('panel2'), outline=rgba('line'))
    d.rectangle([x - 1, floor_row, x + 15, floor_row + 2], fill=rgba('void'))


# --------------------------------------------------------------------------- the desk you sit at
def desk_foreground(img, d, warm_left=True):
    """Full-width desk in the foreground: this is the layer that was missing."""
    d.rectangle([0, DESK_ROW, W - 1, H - 1], fill=rgba('panel2'))          # desk surface
    d.rectangle([0, DESK_EDGE, W - 1, H - 1], fill=rgba('panel'))          # front face in shadow
    d.line([0, DESK_ROW, W - 1, DESK_ROW], fill=rgba('linehi'))            # lit back edge
    d.line([0, DESK_EDGE, W - 1, DESK_EDGE], fill=rgba('line'))            # surface/lip seam
    for x in range(0, W, 40):
        d.line([x, DESK_EDGE + 1, x, H - 1], fill=rgba('void'))            # front panel seams
    # keyboard, seen from the player's seat
    kx, ky, kw, kh = 176, DESK_ROW + 6, 128, 26
    d.rectangle([kx, ky, kx + kw, ky + kh], fill=rgba('panel'), outline=rgba('linehi'))
    d.rectangle([kx + 1, ky + 1, kx + kw - 1, ky + kh - 1], outline=rgba('line'))
    for r_ in range(4):
        for c_ in range(14):
            bx = kx + 5 + c_ * 9
            by = ky + 4 + r_ * 5
            if bx + 6 < kx + kw - 3:
                d.rectangle([bx, by, bx + 6, by + 3], fill=rgba('line'))
    d.rectangle([kx + 36, ky + kh - 6, kx + kw - 36, ky + kh - 3], fill=rgba('line'))   # spacebar
    # mouse + cable
    d.ellipse([kx + kw + 8, ky + 6, kx + kw + 20, ky + 18], fill=rgba('panel'), outline=rgba('line'))
    d.line([kx + kw + 6, ky + 8, kx + kw + 2, ky + 2], fill=rgba('line'))
    # mug with steam, left of the keyboard
    mx, my = 112, DESK_ROW + 10
    d.rectangle([mx, my - 4, mx + 20, my + 16], fill=rgba('panel'), outline=rgba('linehi'))
    d.line([mx + 1, my - 3, mx + 19, my - 3], fill=rgba('ink'))              # coffee
    d.line([mx + 20, my, mx + 25, my + 2], fill=rgba('linehi'))              # handle
    d.line([mx + 25, my + 2, mx + 25, my + 9], fill=rgba('linehi'))
    d.line([mx + 25, my + 9, mx + 20, my + 11], fill=rgba('linehi'))
    for i, sx in enumerate((mx + 5, mx + 11, mx + 16)):                       # steam: two short strokes
        d.line([sx, my - 8, sx + 1, my - 12 - i], fill=rgba('line'))
        d.point((sx + 1, my - 13 - i), fill=rgba('linehi'))
    # paper stack, bottom-right, dim so it never competes with the applicant
    for i in range(3):
        d.rectangle([396 + i, DESK_ROW + 12 - i * 3, 444 + i, DESK_ROW + 26 - i * 3],
                    fill=rgba('paperdim'), outline=rgba('ink'))
    d.line([400, DESK_ROW + 22, 436, DESK_ROW + 22], fill=rgba('ink'))
    if warm_left:                                                          # warm light from off-screen left
        d.line([0, DESK_ROW, 150, DESK_ROW], fill=rgba('paperdim'))
        d.line([0, DESK_ROW + 1, 96, DESK_ROW + 1], fill=rgba('warn'))
        d.line([0, DESK_EDGE, 60, DESK_EDGE], fill=rgba('linehi'))


# --------------------------------------------------------------------------- room
def room_wall():
    img = new(W, H)
    d = ImageDraw.Draw(img)
    d.rectangle([0, 0, W - 1, H - 1], fill=rgba('void'))
    d.rectangle([0, 18, W - 1, FLOOR_ROW - 1], fill=rgba('panel'))
    for x in range(0, W, 48):
        d.line([x, 18, x, FLOOR_ROW - 1], fill=rgba('panel2'))
    d.line([0, 18, W - 1, 18], fill=rgba('panel2'))
    d.rectangle([0, FLOOR_ROW, W - 1, H - 1], fill=rgba('void'))
    d.line([0, FLOOR_ROW, W - 1, FLOOR_ROW], fill=rgba('panel2'))

    # left -> right: lit window (behind the monitor, the hero mood anchor), clock, then a clean
    # dark wall behind the applicant so his silhouette reads and the window frame never crosses
    # his head.
    window_night(img, d, 22, 20, 172, 62)
    wall_clock(img, d, 222, 46)
    filing_cabinet(img, d, 196, FLOOR_ROW, w=34, h=54)
    plant_pot(img, d, 244, FLOOR_ROW)
    # ceiling shadow: two flat steps, no lamp hanging down
    d.rectangle([0, 0, W - 1, 12], fill=rgba('void'))
    d.line([0, 12, W - 1, 12], fill=rgba('panel'))
    return img


def compose(bg, out, mon, doc, app, zoom=3):
    canvas = Image.new('RGBA', (W, H), (11, 14, 19, 255))
    canvas.alpha_composite(bg.convert('RGBA'), (0, 0))

    def put(rel, scale, x, row, crop4=False):
        im = Image.open(os.path.join(KIT, rel)).convert('RGBA')
        if crop4:
            im = im.crop((0, 0, im.width // 4, im.height))
        nw, nh = max(1, int(im.width * scale)), max(1, int(im.height * scale))
        im = im.resize((nw, nh), Image.NEAREST)
        canvas.alpha_composite(im, (x, row))

    bg2 = Image.new('RGBA', (W, H), (0, 0, 0, 0))
    d2 = ImageDraw.Draw(bg2)
    desk_foreground(bg2, d2)
    put(*mon)
    put(*doc)
    put(*app, crop4=True)
    canvas.alpha_composite(bg2)
    canvas.resize((W * zoom, H * zoom), Image.NEAREST).convert('RGB').save(out)
    return out


VARIANTS = {
    # H: window left above the monitor, applicant on a clean wall, desk with keyboard/mug/papers
    'H_window_left': dict(
        mon=('desk/monitor_bezel.png', 0.62, 20, 84),
        doc=('desk/document_sheet.png', 0.66, 44, 112),
        app=('applicants/app_human_solid_a_192x216.png', 0.94, 288, 40)),
    # I: everything a step smaller, more air between monitor and applicant
    'I_more_air': dict(
        mon=('desk/monitor_bezel.png', 0.56, 18, 96),
        doc=('desk/document_sheet.png', 0.60, 40, 122),
        app=('applicants/app_human_solid_a_192x216.png', 0.88, 300, 56)),
}


def main():
    os.makedirs(OUT, exist_ok=True)
    vals = set(PALETTE.values())
    bg = room_wall()
    # desk is drawn on its own layer, so check both
    fg = Image.new('RGBA', (W, H), (0, 0, 0, 0))
    desk_foreground(fg, ImageDraw.Draw(fg))
    bad = 0
    for im in (bg, fg):
        for y in range(H):
            for x in range(W):
                p = im.getpixel((x, y))
                if p[3] == 0:
                    continue
                if p[3] != 255 or p[:3] not in vals:
                    bad += 1
    print(f'off-palette px: {bad}')
    for name, cfg in VARIANTS.items():
        print('wrote', compose(bg, os.path.join(OUT, f'study_{name}.png'),
                               cfg['mon'], cfg['doc'], cfg['app']))
    ims = [Image.open(os.path.join(OUT, f'study_{n}.png')) for n in VARIANTS]
    sheet = Image.new('RGB', (ims[0].width, ims[0].height * len(ims)), (11, 14, 19))
    for i, im in enumerate(ims):
        sheet.paste(im, (0, i * im.height))
    sheet.save(os.path.join(OUT, 'study_contact_sheet_FG.png'))
    print('wrote contact sheet')
    return 0 if bad == 0 else 1


if __name__ == '__main__':
    raise SystemExit(main())
