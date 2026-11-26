#!/usr/bin/env python3
"""
Desk re-composition studies (D/E) -- the approved sprites, re-arranged so the room is visible.

Measured fact that drives this: in the frozen desk_layout.json the background is ~80% covered.
Visible slivers: 36 px above the applicant's head, 14 px on the left, 8 px on the right, 18 px at the
bottom. Painting an office there is wasted work, so these studies move the *composition*, not the art:
monitor and applicant are scaled down and separated, the desk edge drops, and the top ~80 px becomes
real room.

Hard rule for every pixel: solid kit-palette colours only (no alpha blending, no new colours) so the
result can be promoted into art/kit and imported with the existing Point/PPU-16 settings unchanged.
"""
import os, sys
from PIL import Image, ImageDraw

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
sys.path.insert(0, os.path.join(ROOT, 'tools', 'art'))
from make_art_kit import PALETTE, rgba, new   # noqa: E402

OUT = os.path.join(ROOT, 'art', 'concepts')
KIT = os.path.join(ROOT, 'unity', 'Assets', 'Art', 'Kit')
W, H = 480, 270
FLOOR_ROW = 200          # row where wall meets desk-front (image rows, top-down)
DESK_ROW = 206           # top edge of the desk you sit at


def U(row):
    """image row (top-down) -> y-up pixel used by desk_layout.json"""
    return H - row


# --------------------------------------------------------------------------- props, all y-up aware
def window_night(img, d, x, row, w, h):
    d.rectangle([x, row, x + w, row + h], fill=rgba('panel2'), outline=rgba('linehi'))
    d.rectangle([x + 2, row + 2, x + w - 2, row + h - 2], fill=rgba('void'))
    # skyline silhouette along the bottom of the glass
    base = row + h - 3
    for i, bx in enumerate(range(x + 6, x + w - 12, 11)):
        depth = (18, 30, 12, 26, 34, 16, 24, 30, 14, 22)[i % 10]
        d.rectangle([bx, base - depth, bx + 9, base], fill=rgba('panel'))
        for wy in range(base - depth + 4, base - 1, 7):
            d.point((bx + 3, wy), fill=rgba('warn'))
            if (bx + wy) % 3 == 0:
                d.point((bx + 6, wy), fill=rgba('orange'))
    # moon
    cx, cy, r = x + w - 24, row + 16, 6
    d.ellipse([cx - r, cy - r, cx + r, cy + r], fill=rgba('textdim'))
    d.ellipse([cx - r + 3, cy - r - 1, cx + r + 3, cy + r - 1], fill=rgba('void'))
    # blinds: slats across the top third
    for by in range(row + 3, row + h // 3, 5):
        d.line([x + 2, by, x + w - 2, by], fill=rgba('line'))
    d.line([x + w // 2, row + 2, x + w // 2, row + h - 2], fill=rgba('line'))


def filing_cabinet(img, d, x, row, w=34, h=54):
    d.rectangle([x, row, x + w, row + h], fill=rgba('panel2'), outline=rgba('line'))
    d.line([x + 1, row + 1, x + w - 1, row + 1], fill=rgba('linehi'))
    for i in range(3):
        dy = row + 5 + i * 17
        d.rectangle([x + 3, dy, x + w - 3, dy + 13], fill=rgba('panel'), outline=rgba('line'))
        d.line([x + 10, dy + 7, x + w - 10, dy + 7], fill=rgba('linehi'))


def noticeboard(img, d, x, row, w=64, h=40):
    d.rectangle([x, row, x + w, row + h], fill=rgba('panel2'), outline=rgba('linehi'))
    for (px_, py_, pw, ph) in ((6, 5, 20, 15), (32, 4, 24, 11), (10, 23, 21, 12), (38, 19, 18, 15)):
        d.rectangle([x + px_, row + py_, x + px_ + pw, row + py_ + ph], fill=rgba('void'), outline=rgba('line'))
        d.point((x + px_ + pw // 2, row + py_), fill=rgba('danger'))
        for i in range(3, ph - 1, 4):
            d.line([x + px_ + 3, row + py_ + i, x + px_ + pw - 3, row + py_ + i], fill=rgba('panel2'))


def wall_clock(img, d, cx, row, r=8):
    d.ellipse([cx - r, row - r, cx + r, row + r], fill=rgba('panel2'), outline=rgba('linehi'))
    d.line([cx, row, cx, row - 5], fill=rgba('textdim'))
    d.line([cx, row, cx + 3, row + 2], fill=rgba('textdim'))
    d.point((cx, row), fill=rgba('signal'))


def plant(img, d, x, row):
    d.rectangle([x + 4, row + 14, x + 14, row + 24], fill=rgba('panel2'), outline=rgba('line'))
    for (dx, dy) in ((0, 6), (-6, 3), (6, 2), (-3, 8), (4, 9), (-7, 6), (7, 7)):
        d.line([x + 9, row + 14, x + 9 + dx, row + 14 - dy], fill=rgba('signaldim'))
        d.point((x + 9 + dx, row + 14 - dy), fill=rgba('signal'))


def water_cooler(img, d, x, row):
    d.rectangle([x + 2, row + 10, x + 14, row + 30], fill=rgba('panel2'), outline=rgba('line'))
    d.rectangle([x + 5, row, x + 11, row + 10], fill=rgba('muted'))
    d.point((x + 6, row + 2), fill=rgba('textdim'))
    d.point((x + 6, row + 12), fill=rgba('signal'))
    d.point((x + 10, row + 12), fill=rgba('danger'))


def coat_stand(img, d, x, row):
    d.line([x, row, x, row + 44], fill=rgba('linehi'))
    d.line([x - 6, row + 1, x, row - 2], fill=rgba('linehi'))
    d.line([x, row - 2, x + 6, row + 1], fill=rgba('linehi'))
    d.line([x - 2, row + 45, x + 2, row + 45], fill=rgba('linehi'))
    d.rectangle([x + 2, row + 3, x + 13, row + 24], fill=rgba('panel2'), outline=rgba('line'))


def floor_lamp(img, d, x, row):
    """Small warm floor lamp -- the one warm light source in an otherwise cold room."""
    d.line([x + 6, row, x + 6, row + 34], fill=rgba('linehi'))          # pole
    d.line([x + 1, row, x + 11, row], fill=rgba('linehi'))              # base
    d.polygon([(x, row - 16), (x + 13, row - 16), (x + 9, row - 27), (x + 3, row - 27)],
              fill=rgba('panel2'))                                       # shade
    d.line([x, row - 16, x + 13, row - 16], fill=rgba('warn'))           # lit rim
    d.line([x + 2, row - 18, x + 11, row - 18], fill=rgba('paperdim'))   # glow inside shade
    # pool on the floor: flat posterised rings, no alpha
    d.ellipse([x - 16, row - 2, x + 30, row + 7], fill=rgba('panel2'))
    d.ellipse([x - 7, row - 1, x + 21, row + 5], fill=rgba('line'))


def desk_front(img, d, lamp_x=None):
    """The desk you sit at. Dark enough that UI panels read on top of it."""
    d.rectangle([0, DESK_ROW, W - 1, H - 1], fill=rgba('panel'))
    d.line([0, DESK_ROW, W - 1, DESK_ROW], fill=rgba('linehi'))
    d.line([0, DESK_ROW + 1, W - 1, DESK_ROW + 1], fill=rgba('panel2'))
    for x in range(0, W, 32):
        d.line([x, DESK_ROW + 2, x, H - 1], fill=rgba('panel2'))
    if lamp_x is not None:                       # warm rim where the lamp reaches the desk edge
        d.line([lamp_x - 24, DESK_ROW, lamp_x + 30, DESK_ROW], fill=rgba('paperdim'))
        d.line([lamp_x - 12, DESK_ROW + 2, lamp_x + 18, DESK_ROW + 2], fill=rgba('panel2'))


# --------------------------------------------------------------------------- room + composition
def room(lamp=True):
    img = new(W, H)
    d = ImageDraw.Draw(img)
    d.rectangle([0, 0, W - 1, H - 1], fill=rgba('void'))
    # ceiling shadow, far wall, floor
    d.rectangle([0, 22, W - 1, FLOOR_ROW - 1], fill=rgba('panel'))
    for x in range(0, W, 48):
        d.line([x, 22, x, FLOOR_ROW - 1], fill=rgba('panel2'))
    d.line([0, 22, W - 1, 22], fill=rgba('panel2'))
    d.rectangle([0, FLOOR_ROW, W - 1, H - 1], fill=rgba('void'))
    d.line([0, FLOOR_ROW, W - 1, FLOOR_ROW], fill=rgba('panel2'))

    window_night(img, d, 286, 26, 168, 82)
    noticeboard(img, d, 56, 30, 66, 44)
    wall_clock(img, d, 196, 44)
    plant(img, d, 258, 158)
    desk_front(img, d, lamp_x=232 if lamp else None)
    if lamp:
        floor_lamp(img, d, 224, FLOOR_ROW - 26)      # draws on top of the wall/desk seam
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

    put(*mon)
    put(*doc)
    put(*app, crop4=True)
    canvas.resize((W * zoom, H * zoom), Image.NEAREST).convert('RGB').save(out)
    return out


VARIANTS = {
    # D: balanced -- monitor 3/4 size on the left, applicant slightly smaller on the right
    'D_balanced': dict(
        mon=('desk/monitor_bezel.png', 0.74, 14, 68),
        doc=('desk/document_sheet.png', 0.78, 40, 128),
        app=('applicants/app_human_solid_a_192x216.png', 0.70, 314, 96)),
    # E: wider room -- everything smaller, more office, applicant reads as "across the desk"
    'E_wide_room': dict(
        mon=('desk/monitor_bezel.png', 0.68, 12, 84),
        doc=('desk/document_sheet.png', 0.72, 36, 140),
        app=('applicants/app_human_solid_a_192x216.png', 0.62, 330, 112)),
}


def main():
    os.makedirs(OUT, exist_ok=True)
    vals = set(PALETTE.values())
    bg = room()
    bad = sum(1 for y in range(H) for x in range(W)
              if bg.getpixel((x, y))[3] != 255 or bg.getpixel((x, y))[:3] not in vals)
    print(f'room off-palette px: {bad}')
    for name, cfg in VARIANTS.items():
        p = compose(bg, os.path.join(OUT, f'study_{name}.png'), cfg['mon'], cfg['doc'], cfg['app'])
        print('wrote', p)
    # contact sheet
    ims = [Image.open(os.path.join(OUT, f'study_{n}.png')) for n in VARIANTS]
    sheet = Image.new('RGB', (ims[0].width, ims[0].height * len(ims)), (11, 14, 19))
    for i, im in enumerate(ims):
        sheet.paste(im, (0, i * im.height))
    sheet.save(os.path.join(OUT, 'study_contact_sheet_DE.png'))
    print('wrote contact sheet')
    return 0 if bad == 0 else 1


if __name__ == '__main__':
    raise SystemExit(main())
