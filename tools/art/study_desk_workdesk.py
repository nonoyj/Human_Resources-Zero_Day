#!/usr/bin/env python3
"""
Composition study J -- study F, revised to the user's notes (2026-11-26):

  1. "adjust the size of the background objects to look bigger"  -> window, cabinet, clock, plant,
                                                                     noticeboard all scaled up
  2. "add a cabinet in the background"                            -> tall 3-drawer filing cabinet in
                                                                     the gap between monitor and
                                                                     applicant
  3. "increase desk work on the table"                            -> notepad + pen, keyboard, mouse,
                                                                     coffee, folder stack, sticky notes,
                                                                     clipboard
  4. "adjust the keyboard and mouse location to the front of the computer"
                                                                     -> keyboard centred on the
                                                                        monitor's centre line, mouse
                                                                        immediately right of it
  5. "put the coffee to the right"                                 -> mug on the right of the desk

Style rules from docs/15-art-bible.md: night room, one warm light from off-screen left, flat hard
shadows, every prop touches the floor line with a 2 px contact shadow, solid kit-palette colours only
(alpha 255, no blending). Reports off-palette px; must be 0.
"""
import os, sys
from PIL import Image, ImageDraw

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
sys.path.insert(0, os.path.join(ROOT, 'tools', 'art'))
from make_art_kit import PALETTE, rgba, new   # noqa: E402

OUT = os.path.join(ROOT, 'art', 'concepts')
KIT = os.path.join(ROOT, 'unity', 'Assets', 'Art', 'Kit')
W, H = 480, 270

FLOOR_ROW = 198      # wall meets floor
DESK_ROW = 206       # desk surface starts
DESK_LIP = 244       # front lip of the desk (below this is the front face)


# --------------------------------------------------------------------------- background props, bigger
def window_night(img, d, x, row, w, h, moon=True):
    d.rectangle([x, row, x + w, row + h], fill=rgba('panel2'), outline=rgba('linehi'))
    d.rectangle([x + 2, row + 2, x + w - 2, row + h - 2], fill=rgba('void'))
    base = row + h - 3
    depths = (26, 42, 18, 36, 48, 22, 34, 44, 20, 30, 40, 24, 38)
    for i, bx in enumerate(range(x + 6, x + w - 14, 14)):
        depth = depths[i % len(depths)]
        d.rectangle([bx, base - depth, bx + 11, base], fill=rgba('panel'))
        for wy in range(base - depth + 5, base - 2, 8):
            d.point((bx + 3, wy), fill=rgba('warn'))
            d.point((bx + 7, wy), fill=rgba('warn') if (bx + wy) % 4 else rgba('orange'))
    if moon:
        cx, cy, r = x + w - 30, row + 20, 9
        d.ellipse([cx - r, cy - r, cx + r, cy + r], fill=rgba('textdim'))
        d.ellipse([cx - r + 4, cy - r - 1, cx + r + 4, cy + r - 1], fill=rgba('void'))
    for by in range(row + 3, row + h // 3, 6):
        d.line([x + 2, by, x + w - 2, by], fill=rgba('line'))
    d.line([x + w // 2, row + 2, x + w // 2, row + h - 2], fill=rgba('line'))
    d.line([x - 3, row + h, x + w + 3, row + h], fill=rgba('linehi'))      # sill


def filing_cabinet(img, d, x, floor_row, w=44, h=76):
    """Big 3-drawer cabinet standing on the floor."""
    top = floor_row - h
    d.rectangle([x, top, x + w, floor_row], fill=rgba('panel2'), outline=rgba('line'))
    d.rectangle([x + 2, top + 2, x + w - 2, floor_row - 2], outline=rgba('line'))
    d.line([x + 2, top + 2, x + w - 2, top + 2], fill=rgba('linehi'))
    for i in range(3):
        dy = top + 6 + i * 23
        d.rectangle([x + 5, dy, x + w - 5, dy + 18], fill=rgba('panel'), outline=rgba('line'))
        d.line([x + 13, dy + 9, x + w - 13, dy + 9], fill=rgba('linehi'))
        d.point((x + w // 2, dy + 9), fill=rgba('textdim'))
    d.rectangle([x - 2, floor_row, x + w + 2, floor_row + 2], fill=rgba('void'))


def noticeboard(img, d, x, row, w=84, h=48):
    d.rectangle([x, row, x + w, row + h], fill=rgba('panel2'), outline=rgba('linehi'))
    for (px_, py_, pw, ph) in ((8, 6, 24, 18), (40, 5, 30, 14), (13, 29, 26, 15), (48, 24, 22, 19)):
        d.rectangle([x + px_, row + py_, x + px_ + pw, row + py_ + ph], fill=rgba('void'), outline=rgba('line'))
        d.point((x + px_ + pw // 2, row + py_), fill=rgba('danger'))
        for i in range(4, ph - 2, 5):
            d.line([x + px_ + 4, row + py_ + i, x + px_ + pw - 4, row + py_ + i], fill=rgba('panel2'))


def wall_clock(img, d, cx, row, r=11):
    d.ellipse([cx - r, row - r, cx + r, row + r], fill=rgba('panel2'), outline=rgba('linehi'))
    d.ellipse([cx - r + 2, row - r + 2, cx + r - 2, row + r - 2], outline=rgba('line'))
    d.line([cx, row, cx, row - 7], fill=rgba('textdim'))
    d.line([cx, row, cx + 4, row + 3], fill=rgba('textdim'))
    d.point((cx, row), fill=rgba('signal'))
    for (tx, ty) in ((cx, row - 8), (cx, row + 8), (cx - 8, row), (cx + 8, row)):
        d.point((tx, ty), fill=rgba('muted'))


def plant_pot(img, d, x, floor_row, h=42):
    pot_top = floor_row - h
    d.rectangle([x, pot_top + 14, x + 18, floor_row], fill=rgba('panel2'), outline=rgba('line'))
    d.line([x, pot_top + 14, x + 18, pot_top + 14], fill=rgba('linehi'))
    for (dx, dy) in ((0, 14), (-7, 9), (7, 10), (-3, 20), (4, 21), (-9, 16), (9, 17), (-5, 25), (6, 24)):
        d.line([x + 9, pot_top + 14, x + 9 + dx, pot_top + 14 - dy], fill=rgba('signaldim'))
        d.point((x + 9 + dx, pot_top + 14 - dy), fill=rgba('signal'))
    d.rectangle([x - 2, floor_row, x + 20, floor_row + 2], fill=rgba('void'))


# --------------------------------------------------------------------------- desk work
def desk_surface(img, d):
    d.rectangle([0, DESK_ROW, W - 1, H - 1], fill=rgba('panel2'))
    d.rectangle([0, DESK_LIP, W - 1, H - 1], fill=rgba('panel'))
    d.line([0, DESK_ROW, W - 1, DESK_ROW], fill=rgba('linehi'))
    d.line([0, DESK_ROW + 1, W - 1, DESK_ROW + 1], fill=rgba('panel'))
    d.line([0, DESK_LIP, W - 1, DESK_LIP], fill=rgba('line'))
    for x in range(0, W, 44):
        d.line([x, DESK_LIP + 1, x, H - 1], fill=rgba('void'))
    # warm rim from the off-screen lamp, left only
    d.line([0, DESK_ROW, 132, DESK_ROW], fill=rgba('paperdim'))
    d.line([0, DESK_LIP, 54, DESK_LIP], fill=rgba('linehi'))


def keyboard(img, d, cx, row, w=92, h=24):
    """Centred on the monitor, i.e. in front of the computer."""
    x = cx - w // 2
    d.rectangle([x, row, x + w, row + h], fill=rgba('panel'), outline=rgba('linehi'))
    d.rectangle([x + 1, row + 1, x + w - 1, row + h - 1], outline=rgba('line'))
    for r_ in range(3):
        for c_ in range(13):
            bx = x + 5 + c_ * 6
            by = row + 4 + r_ * 4
            if bx + 4 < x + w - 4:
                d.rectangle([bx, by, bx + 4, by + 2], fill=rgba('line'))
    d.rectangle([x + 24, row + h - 5, x + w - 24, row + h - 2], fill=rgba('line'))
    d.line([x, row + h, x + w, row + h], fill=rgba('void'))


def mouse(img, d, x, row):
    d.ellipse([x, row, x + 13, row + 18], fill=rgba('panel'), outline=rgba('linehi'))
    d.line([x + 6, row + 2, x + 6, row + 7], fill=rgba('line'))
    d.line([x - 4, row + 1, x + 1, row + 4], fill=rgba('line'))              # cable back to keyboard


def coffee(img, d, x, row):
    d.rectangle([x, row, x + 22, row + 24], fill=rgba('panel'), outline=rgba('linehi'))
    d.line([x + 1, row + 1, x + 21, row + 1], fill=rgba('ink'))              # coffee surface
    d.line([x + 22, row + 4, x + 28, row + 7], fill=rgba('linehi'))          # handle
    d.line([x + 28, row + 7, x + 28, row + 15], fill=rgba('linehi'))
    d.line([x + 28, row + 15, x + 22, row + 18], fill=rgba('linehi'))
    for i, sx in enumerate((x + 5, x + 11, x + 17)):                         # steam
        d.line([sx, row - 4, sx + 1, row - 9 - i], fill=rgba('line'))
        d.point((sx + 1, row - 10 - i), fill=rgba('linehi'))


def notepad_and_pen(img, d, x, row):
    d.rectangle([x, row, x + 40, row + 30], fill=rgba('paper'), outline=rgba('ink'))
    d.line([x + 1, row + 4, x + 39, row + 4], fill=rgba('ink'))
    for i in range(4):
        y = row + 9 + i * 5
        d.line([x + 5, y, x + 33 - (i % 2) * 9, y], fill=rgba('paperdim'))
    d.line([x + 44, row + 26, x + 58, row + 18], fill=rgba('warn'))          # pen
    d.line([x + 58, row + 18, x + 61, row + 16], fill=rgba('linehi'))


def folder_stack(img, d, x, row):
    for i in range(3):
        col = 'paper' if i == 2 else 'paperdim'
        d.rectangle([x + i, row + 22 - i * 4, x + 62 + i, row + 38 - i * 4], fill=rgba(col), outline=rgba('ink'))
    d.line([x + 6, row + 34, x + 54, row + 34], fill=rgba('ink'))
    d.line([x + 6, row + 30, x + 40, row + 30], fill=rgba('paperdim'))
    d.rectangle([x + 2, row + 12, x + 66, row + 24], fill=rgba('paper'), outline=rgba('ink'))   # sheet on top
    for i in range(3):
        d.line([x + 8, row + 16 + i * 4, x + 58 - i * 10, row + 16 + i * 4], fill=rgba('paperdim'))


def sticky_notes(img, d, x, row):
    for i, col in enumerate(('warn', 'paperdim')):
        d.rectangle([x, row + i * 12, x + 15, row + 10 + i * 12], fill=rgba(col), outline=rgba('ink'))
        d.line([x + 3, row + i * 12 + 4, x + 11, row + i * 12 + 4], fill=rgba('ink'))


def clipboard(img, d, x, row):
    d.rectangle([x, row, x + 34, row + 40], fill=rgba('panel'), outline=rgba('linehi'))
    d.rectangle([x + 3, row + 6, x + 31, row + 36], fill=rgba('paper'), outline=rgba('ink'))
    d.rectangle([x + 11, row - 2, x + 23, row + 3], fill=rgba('linehi'), outline=rgba('ink'))
    for i in range(5):
        d.line([x + 7, row + 11 + i * 5, x + 27 - (i % 3) * 6, row + 11 + i * 5], fill=rgba('paperdim'))


# --------------------------------------------------------------------------- room
def build_room():
    img = new(W, H)
    d = ImageDraw.Draw(img)
    d.rectangle([0, 0, W - 1, H - 1], fill=rgba('void'))
    # ceiling: two flat steps, no fixture
    d.rectangle([0, 0, W - 1, 10], fill=rgba('void'))
    d.line([0, 10, W - 1, 10], fill=rgba('panel'))
    # far wall
    d.rectangle([0, 16, W - 1, FLOOR_ROW - 1], fill=rgba('panel'))
    for x in range(0, W, 56):
        d.line([x, 16, x, FLOOR_ROW - 1], fill=rgba('panel2'))
    d.line([0, 16, W - 1, 16], fill=rgba('panel2'))
    # floor strip
    d.rectangle([0, FLOOR_ROW, W - 1, H - 1], fill=rgba('void'))
    d.line([0, FLOOR_ROW, W - 1, FLOOR_ROW], fill=rgba('panel2'))

    # Window on the RIGHT behind the applicant -- user, 2026-11-26: it highlights the interviewee.
    # It sits ABOVE him (his hair tops out at row ~105) so the skyline never crosses his face, which is
    # what kept the silhouette readable in the earlier left-hand version.
    # rows 96..170: the frame's top edge sits BELOW his hair (row ~39), so his head reads against
    # clean wall and the city lights act as a rim light behind his shoulders. The moon is dropped --
    # at this size it landed beside his head and read as a second focal point.
    window_night(img, d, 286, 96, 182, 74, moon=False)
    noticeboard(img, d, 20, 22, 84, 48)
    wall_clock(img, d, 126, 44)          # beside the board, never on top of it
    filing_cabinet(img, d, 206, FLOOR_ROW, w=44, h=76)
    plant_pot(img, d, 258, FLOOR_ROW, h=42)
    return img


def build_desk():
    """Desk work, as a separate layer so it sorts in front of the applicant."""
    img = Image.new('RGBA', (W, H), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    desk_surface(img, d)
    notepad_and_pen(img, d, 14, DESK_ROW + 8)
    keyboard(img, d, cx=100, row=DESK_ROW + 10)      # directly in front of the monitor
    mouse(img, d, 152, DESK_ROW + 14)
    sticky_notes(img, d, 180, DESK_ROW + 10)
    folder_stack(img, d, 200, DESK_ROW + 6)
    clipboard(img, d, 330, DESK_ROW + 2)
    coffee(img, d, 408, DESK_ROW + 10)               # coffee on the right hand side
    return img


def compose(out, mon, doc, app, zoom=3):
    canvas = new(W, H).convert('RGBA')
    canvas.alpha_composite(build_room().convert('RGBA'), (0, 0))

    def put(rel, scale, x, row, crop4=False):
        im = Image.open(os.path.join(KIT, rel)).convert('RGBA')
        if crop4:
            im = im.crop((0, 0, im.width // 4, im.height))
        nw, nh = max(1, int(im.width * scale)), max(1, int(im.height * scale))
        canvas.alpha_composite(im.resize((nw, nh), Image.NEAREST), (x, row))

    put(*mon)
    put(*doc)
    put(*app, crop4=True)
    canvas.alpha_composite(build_desk())             # desk in front of everything
    canvas.resize((W * zoom, H * zoom), Image.NEAREST).convert('RGB').save(out)
    return out


VARIANTS = {
    # K -- APPROVED by the user 2026-11-26. Numbers here are the spec for desk_layout.json.
    'K_workdesk_bold': dict(
        mon=('desk/monitor_bezel.png', 0.58, 14, 88),
        doc=('desk/document_sheet.png', 0.56, 34, 114),
        app=('applicants/app_human_solid_a_192x216.png', 0.96, 288, 34)),
    # L: identical composition, but the window is seated behind his shoulders (see room())
    'L_window_behind_shoulders': dict(
        mon=('desk/monitor_bezel.png', 0.58, 14, 88),
        doc=('desk/document_sheet.png', 0.56, 34, 114),
        app=('applicants/app_human_solid_a_192x216.png', 0.96, 288, 34)),
}


def main():
    os.makedirs(OUT, exist_ok=True)
    vals = set(PALETTE.values())
    bad = 0
    for im in (build_room(), build_desk()):
        for y in range(H):
            for x in range(W):
                p = im.getpixel((x, y))
                if p[3] == 0:
                    continue
                if p[3] != 255 or p[:3] not in vals:
                    bad += 1
    print(f'off-palette px: {bad}')
    for name, cfg in VARIANTS.items():
        print('wrote', compose(os.path.join(OUT, f'study_{name}.png'), cfg['mon'], cfg['doc'], cfg['app']))
    ims = [Image.open(os.path.join(OUT, f'study_{n}.png')) for n in VARIANTS]
    sheet = Image.new('RGB', (ims[0].width, ims[0].height * len(ims)), (11, 14, 19))
    for i, im in enumerate(ims):
        sheet.paste(im, (0, i * im.height))
    sheet.save(os.path.join(OUT, 'study_contact_sheet_JK.png'))
    print('wrote contact sheet')
    return 0 if bad == 0 else 1


if __name__ == '__main__':
    raise SystemExit(main())
