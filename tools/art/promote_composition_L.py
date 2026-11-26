#!/usr/bin/env python3
"""
Promote the approved composition (study L, 2026-11-26) into real kit art.

What this writes into art/kit (and nothing else -- publish_to_unity.py mirrors it afterwards):

    backgrounds/office_L0..L2.png   480x270  the room: night window behind the applicant, board,
                                             clock, filing cabinet, plant, desk front face
    desk/desk_foreground.png        480x112  the desk you sit at, with the work on it
    desk/monitor_small.png          154x106  1-px-crisp redraw at the approved 0.58 scale
    desk/document_small.png         108x63   1-px-crisp redraw at the approved 0.56 scale

Why no downscaling: the approved composition calls for the monitor at 58% and the email sheet at 56%.
Resampling the 264x200 bezel would blur its 1 px frame, which the Point filter then bakes in. These two
sprites are therefore re-authored at their final pixel size with the same 1 px structure and the same
palette entries. Nothing else in the kit is touched.

Palette rule (docs/15-art-bible.md §3): solid PALETTE colours only, alpha 0 or 255, no blending.
Corruption states (bible §7): the same room with lighting changes only -- never a different room.
"""
import os, sys
from PIL import Image, ImageDraw

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
sys.path.insert(0, os.path.join(ROOT, 'tools', 'art'))
from make_art_kit import PALETTE, rgba, new   # noqa: E402
from ragged import ragged_rect, notch_corner, scuff, LCG   # noqa: E402

# Three approved "glow" steps (user, 2026-11-26) -- light falls off without blending. They are added to
# the kit palette by tools/art/make_art_kit.py; referenced here by name only.

W, H = 480, 270
FLOOR_ROW = 198
DESK_ROW = 206
DESK_H = H - DESK_ROW          # 64 px band the desk occupies at the bottom of the screen
MON_W, MON_H = 154, 106
DOC_W, DOC_H = 108, 63


def out(rel):
    return os.path.join(ROOT, 'art', 'kit', rel)


# --------------------------------------------------------------------------- room props
def window_night(img, d, x, row, w, h, accent_pips=('warn', 'orange'), sky=None):
    d.rectangle([x, row, x + w, row + h], fill=rgba(sky or 'panel2'), outline=rgba('linehi'))
    d.rectangle([x + 2, row + 2, x + w - 2, row + h - 2], fill=rgba('void'))
    base = row + h - 3
    depths = (26, 42, 18, 36, 48, 22, 34, 44, 20, 30, 40, 24, 38)
    for i, bx in enumerate(range(x + 6, x + w - 14, 14)):
        depth = depths[i % len(depths)]
        d.rectangle([bx, base - depth, bx + 11, base], fill=rgba('panel'))
        for wy in range(base - depth + 5, base - 2, 8):
            d.point((bx + 3, wy), fill=rgba(accent_pips[0]))
            d.point((bx + 7, wy), fill=rgba(accent_pips[1] if (bx + wy) % 4 else accent_pips[0]))
    for by in range(row + 3, row + h // 3, 6):
        d.line([x + 2, by, x + w - 2, by], fill=rgba('line'))
    d.line([x + w // 2, row + 2, x + w // 2, row + h - 2], fill=rgba('line'))
    d.line([x - 3, row + h, x + w + 3, row + h], fill=rgba('linehi'))


def noticeboard(img, d, x, row, w=84, h=48):
    d.rectangle([x, row, x + w, row + h], fill=rgba('panel2'), outline=rgba('linehi'))
    for (px_, py_, pw, ph) in ((8, 6, 24, 18), (40, 5, 30, 14), (13, 29, 26, 15), (48, 24, 22, 19)):
        d.rectangle([x + px_, row + py_, x + px_ + pw, row + py_ + ph], fill=rgba('void'), outline=rgba('line'))
        d.point((x + px_ + pw // 2, row + py_), fill=rgba('danger'))
        for i in range(4, ph - 2, 5):
            d.line([x + px_ + 4, row + py_ + i, x + px_ + pw - 4, row + py_ + i], fill=rgba('panel2'))


def wall_clock(img, d, cx, row, r=11, hand='textdim', tick='muted', dot='signal'):
    d.ellipse([cx - r, row - r, cx + r, row + r], fill=rgba('panel2'), outline=rgba('linehi'))
    d.ellipse([cx - r + 2, row - r + 2, cx + r - 2, row + r - 2], outline=rgba('line'))
    d.line([cx, row, cx, row - 7], fill=rgba(hand))
    d.line([cx, row, cx + 4, row + 3], fill=rgba(hand))
    d.point((cx, row), fill=rgba(dot))
    for (tx, ty) in ((cx, row - 8), (cx, row + 8), (cx - 8, row), (cx + 8, row)):
        d.point((tx, ty), fill=rgba(tick))


def filing_cabinet(img, d, x, floor_row, w=44, h=76, body='panel2', face='panel', edge='line'):
    top = floor_row - h
    d.rectangle([x, top, x + w, floor_row], fill=rgba(body), outline=rgba(edge))
    d.rectangle([x + 2, top + 2, x + w - 2, floor_row - 2], outline=rgba(edge))
    d.line([x + 2, top + 2, x + w - 2, top + 2], fill=rgba('linehi'))
    for i in range(3):
        dy = top + 6 + i * 23
        d.rectangle([x + 5, dy, x + w - 5, dy + 18], fill=rgba(face), outline=rgba(edge))
        d.line([x + 13, dy + 9, x + w - 13, dy + 9], fill=rgba('linehi'))
        d.point((x + w // 2, dy + 9), fill=rgba('textdim'))
    d.rectangle([x - 2, floor_row, x + w + 2, floor_row + 2], fill=rgba('void'))


def plant_pot(img, d, x, floor_row, h=42, leaf='signaldim', tip='signal'):
    pot_top = floor_row - h
    d.rectangle([x, pot_top + 14, x + 18, floor_row], fill=rgba('panel2'), outline=rgba('line'))
    d.line([x, pot_top + 14, x + 18, pot_top + 14], fill=rgba('linehi'))
    for (dx, dy) in ((0, 14), (-7, 9), (7, 10), (-3, 20), (4, 21), (-9, 16), (9, 17), (-5, 25), (6, 24)):
        d.line([x + 9, pot_top + 14, x + 9 + dx, pot_top + 14 - dy], fill=rgba(leaf))
        d.point((x + 9 + dx, pot_top + 14 - dy), fill=rgba(tip))
    d.rectangle([x - 2, floor_row, x + 20, floor_row + 2], fill=rgba('void'))


def desk_front(img, d, level=0):
    """The desk surface + front face at the bottom of the screen. Same band in every corruption level."""
    d.rectangle([0, DESK_ROW, W - 1, H - 1], fill=rgba('panel2'))
    d.rectangle([0, DESK_ROW + 38, W - 1, H - 1], fill=rgba('panel'))
    seam = 'line' if level < 2 else 'dangerdim'
    d.line([0, DESK_ROW, W - 1, DESK_ROW], fill=rgba(seam))
    d.line([0, DESK_ROW + 1, W - 1, DESK_ROW + 1], fill=rgba('panel'))
    d.line([0, DESK_ROW + 38, W - 1, DESK_ROW + 38], fill=rgba('line'))
    for x in range(0, W, 44):
        d.line([x, DESK_ROW + 39, x, H - 1], fill=rgba('void'))
    # warm rim only from the off-screen lamp on the left (bible §2)
    if level == 0:
        d.line([0, DESK_ROW, 148, DESK_ROW], fill=rgba('glow_amber'))   # lamp falloff on the desk edge
        d.line([0, DESK_ROW + 1, 96, DESK_ROW + 1], fill=rgba('paperdim'))
        d.line([0, DESK_ROW + 2, 54, DESK_ROW + 2], fill=rgba('warn'))  # warmest right under the lamp
    elif level == 2:
        d.line([0, DESK_ROW, 132, DESK_ROW], fill=rgba('dangerdim'))


# --------------------------------------------------------------------------- the three levels
def room(level=0):
    img = new(W, H)
    d = ImageDraw.Draw(img)
    d.rectangle([0, 0, W - 1, H - 1], fill=rgba('void'))
    # ceiling: two flat steps, no hanging fixture (bible §2)
    d.rectangle([0, 0, W - 1, 10], fill=rgba('void'))
    d.line([0, 10, W - 1, 10], fill=rgba('panel'))
    # far wall
    wall = 'panel' if level < 2 else 'panel'
    d.rectangle([0, 16, W - 1, FLOOR_ROW - 1], fill=rgba(wall))
    seam = 'panel2' if level < 2 else 'dangerdim'
    for x in range(0, W, 56):
        d.line([x, 16, x, FLOOR_ROW - 1], fill=rgba(seam))
    d.line([0, 16, W - 1, 16], fill=rgba(seam))

    pips = ('warn', 'orange')
    if level == 1:
        pips = ('orange', 'orange')
    elif level == 2:
        pips = ('danger', 'dangerdim')
    # Bigger city, per user 2026-11-26: frame runs from just below the shoulder line (row 145) all the
    # way above the head (row 8) and reads as a proper skyline behind him.
    window_night(img, d, 244, 8, 224, 137, accent_pips=pips,
                 sky='panel2' if level < 2 else 'dangerdim')
    noticeboard(img, d, 20, 22, 84, 48)
    wall_clock(img, d, 126, 44,
               hand='textdim' if level == 0 else ('danger' if level == 1 else 'dangerdim'),
               dot='signal' if level == 0 else 'danger')
    filing_cabinet(img, d, 206, FLOOR_ROW)
    plant_pot(img, d, 258, FLOOR_ROW,
              leaf='signaldim' if level == 0 else 'line', tip='signal' if level == 0 else 'linehi')

    # floor strip
    d.rectangle([0, FLOOR_ROW, W - 1, H - 1], fill=rgba('void'))
    d.line([0, FLOOR_ROW, W - 1, FLOOR_ROW], fill=rgba('panel2'))
    desk_front(img, d, level)

    if level >= 1:                                    # scan band, flat
        for y in range(20, DESK_ROW, 22):
            d.rectangle([0, y, W - 1, y + 1], fill=rgba('panel'))
    if level >= 2:                                    # breach: red frame + every 3rd row darkened
        d.rectangle([0, 0, W - 1, 3], fill=rgba('danger'))
        for y in range(0, DESK_ROW, 3):
            d.line([0, y, W - 1, y], fill=rgba('dangerdim'))
    return img


# --------------------------------------------------------------------------- desk work
def keyboard(img, d, cx, row, w=92, h=24):
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
    d.line([x - 4, row + 1, x + 1, row + 4], fill=rgba('line'))


def coffee(img, d, x, row):
    d.rectangle([x, row, x + 22, row + 24], fill=rgba('panel'), outline=rgba('linehi'))
    d.line([x + 1, row + 1, x + 21, row + 1], fill=rgba('ink'))
    d.line([x + 22, row + 4, x + 28, row + 7], fill=rgba('linehi'))
    d.line([x + 28, row + 7, x + 28, row + 15], fill=rgba('linehi'))
    d.line([x + 28, row + 15, x + 22, row + 18], fill=rgba('linehi'))
    for i, sx in enumerate((x + 5, x + 11, x + 17)):
        d.line([sx, row - 4, sx + 1, row - 9 - i], fill=rgba('line'))
        d.point((sx + 1, row - 10 - i), fill=rgba('linehi'))


def notepad_and_pen(img, d, x, row):
    d.rectangle([x, row, x + 40, row + 30], fill=rgba('paper'), outline=rgba('ink'))
    d.line([x + 1, row + 4, x + 39, row + 4], fill=rgba('ink'))
    for i in range(4):
        y = row + 9 + i * 5
        d.line([x + 5, y, x + 33 - (i % 2) * 9, y], fill=rgba('paperdim'))
    d.line([x + 44, row + 26, x + 58, row + 18], fill=rgba('warn'))
    d.line([x + 58, row + 18, x + 61, row + 16], fill=rgba('linehi'))


def folder_stack(img, d, x, row):
    for i in range(3):
        col = 'paper' if i == 2 else 'paperdim'
        d.rectangle([x + i, row + 22 - i * 4, x + 62 + i, row + 38 - i * 4], fill=rgba(col), outline=rgba('ink'))
    d.rectangle([x + 2, row + 12, x + 66, row + 24], fill=rgba('paper'), outline=rgba('ink'))
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


def desk_work():
    """Everything lying on the desk. Local rows: 0 = DESK_ROW."""
    img = Image.new('RGBA', (W, DESK_H), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    base = 8
    notepad_and_pen(img, d, 14, base)
    keyboard(img, d, cx=100, row=base + 2)
    mouse(img, d, 152, base + 6)
    sticky_notes(img, d, 180, base + 2)
    folder_stack(img, d, 200, base - 2)
    clipboard(img, d, 330, base - 6)
    coffee(img, d, 408, base + 2)
    # RAGGED: a desk that has been worked at for years -- sparse scuffs, no noise
    scuff(img, 301, n=26, tones=('panel', 'void'))
    return img


# --------------------------------------------------------------------------- small sprites, 1 px crisp
def monitor_small():
    w, h = MON_W, MON_H
    img = Image.new('RGBA', (w, h), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    # RAGGED: three hand-drawn boxes instead of CAD rectangles -- the bezel bulges, the glass edge
    # steps by a pixel, one corner is chipped. Same structure, same palette, no extra colours.
    ragged_rect(img, 0, 0, w - 1, h - 1, 'panel', 'line', seed=101, amp=1)   # outer case: worn
    d.rectangle([1, 2, w - 2, h - 3], outline=rgba('linehi'))                 # lit inner frame: straight
    d.rectangle([4, 5, w - 5, h - 6], fill=rgba('void'), outline=rgba('line'))# glass: straight
    notch_corner(img, w - 1, h - 1, -1, -1, depth=2)
    notch_corner(img, 0, 0, 1, 1, depth=1)
    scuff(img, 104, n=14, tones=('panel', 'void'))
    # camera dot + power LED, same as the full-size bezel
    d.rectangle([w // 2 - 1, 2, w // 2 + 1, 3], fill=rgba('linehi'))
    d.point((w - 6, 2), fill=rgba('signal'))
    # stand
    d.rectangle([w // 2 - 12, h - 4, w // 2 + 12, h - 1], fill=rgba('panel2'), outline=rgba('line'))
    return img


def document_small():
    w, h = DOC_W, DOC_H
    img = Image.new('RGBA', (w, h), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    d.rectangle([0, 0, w - 1, h - 1], fill=rgba('paper'), outline=rgba('ink'))
    # PAPER RULE (bible §4b): sheets get torn EDGES, never a stepped fill -- a stepped fill makes the
    # page look chewed. Tear the bottom and both sides by 1 px, occasionally 2.
    rnd = LCG(202)
    for x in range(w):
        if rnd.below(0.5):
            for k in range(rnd.irange(1, 2)):
                yy = h - 1 - k
                if 0 <= yy < h:
                    img.putpixel((x, yy), (0, 0, 0, 0))
    for y in range(h):
        if rnd.below(0.28):
            img.putpixel((0, y), (0, 0, 0, 0))
        if rnd.below(0.28):
            img.putpixel((w - 1, y), (0, 0, 0, 0))
    # the header rule that never printed straight
    for x in range(7, w - 8):
        y = 8 + (1 if (x // 11) % 4 == 0 else 0)
        img.putpixel((x, y), rgba('ink'))
    y = 14
    for i in range(7):
        ln = (w - 26) if i % 4 else (w - 58)
        d.line([7, y, 7 + ln, y], fill=rgba('paperdim'))
        y += 6
    d.rectangle([w - 18, 3, w - 12, 7], outline=rgba('muted'))    # paper clip
    return img


# --------------------------------------------------------------------------- main
def main():
    made = []
    for level in (0, 1, 2):
        p = out(f'backgrounds/office_L{level}.png')
        room(level).save(p)
        made.append(p)
    p = out('desk/desk_foreground.png')
    desk_work().save(p); made.append(p)
    p = out('desk/monitor_small.png')
    monitor_small().save(p); made.append(p)
    p = out('desk/document_small.png')
    document_small().save(p); made.append(p)

    # palette gate over everything we just wrote
    vals = set(PALETTE.values())
    bad = 0
    for p in made:
        im = Image.open(p).convert('RGBA')
        for y in range(im.height):
            for x in range(im.width):
                px = im.getpixel((x, y))
                if px[3] == 0:
                    continue
                if px[3] != 255 or px[:3] not in vals:
                    bad += 1
    for p in made:
        print('wrote', os.path.relpath(p, ROOT))
    print(f'off-palette px: {bad}')
    return 0 if bad == 0 else 1


if __name__ == '__main__':
    raise SystemExit(main())
