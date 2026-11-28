#!/usr/bin/env python3
"""
make_menu_room.py -- the front door AS THE ROOM, composed from kit art.

The player's brief (2026-11-28), in their words:
    "menu items are on the pixel computer, then a coffee underneath with applicant files on the table,
     then monitors in the background with an aesthetic view of the city in the night, with a server on the
     side. Overall look has a cyan glow from the night sky and the monitor screen."

So this composes the frame the way the desk already composes itself:

    backgrounds/office_L0.png     the room: night city, moon, board, clock, cabinet, plant, desk band
    desk/monitor_small.png        the monitor, with 142x88 px of glass
    desk/monitor_stand.png        the stand
    desk/desk_foreground.png      coffee, keyboard, file folders, clipboard, notepad, mouse

...then paints the MENU into that glass with the game's own 5x7 atlas, and adds the two things the room
does not have yet: a server rack at the side, and the cyan spill from the screen and the window.

Everything is drawn from PALETTE entries only, alpha 0 or 255 (art bible §3) -- the glow is a flat
palette step, never a blend, so the frame looks the same at every resolution.

    python tools/art/make_menu_room.py            # writes art/kit/reference/menu_room_2x.png
    python tools/art/make_menu_room.py --check    # geometry only: does the menu fit the glass?
"""
import os
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
sys.path.insert(0, os.path.join(ROOT, 'tools', 'art'))
sys.path.insert(0, os.path.join(ROOT, 'tools', 'comp'))

from PIL import Image, ImageDraw  # noqa: E402

from make_art_kit import PALETTE, rgba  # noqa: E402
from preview_intro import load_font, draw_text, GLYPH_W, GLYPH_H, LETTER_GAP, LINE_GAP  # noqa: E402
import json  # noqa: E402

W, H = 480, 270
GLASS_ROWS = 8                       # 88 px of glass / (7 px glyph + 3 px gap), floor 8
GLASS_COLS = 17                      # 142 px / (5 + 2) px per cell

LAYOUT = os.path.join(ROOT, 'unity', 'Assets', 'Resources', 'desk_layout.json')


def layer(layout, ident):
    for l in layout['layers']:
        if l['id'] == ident:
            return l
    raise KeyError('no layer ' + ident)


def compose(layout):
    """The room exactly as the game builds it, in canvas pixels, bottom-left origin flipped to top-left."""
    base = Image.open(os.path.join(ROOT, 'art', 'kit', 'backgrounds', 'office_L0.png')).convert('RGBA')
    img = base.copy()
    canvas_h = layout['canvas']['height']

    mon = layer(layout, 'monitor')
    stand = layer(layout, 'monitor_stand')
    mon_img = Image.open(os.path.join(ROOT, 'art', 'kit', 'desk', 'monitor_small.png')).convert('RGBA')
    stand_img = Image.open(os.path.join(ROOT, 'art', 'kit', 'desk', 'monitor_stand.png')).convert('RGBA')

    def place(sprite, anchor):
        x, y = anchor
        img.alpha_composite(sprite, (int(x), int(canvas_h - y - sprite.height)))

    place(stand_img, stand['anchor'])
    place(mon_img, mon['anchor'])

    desk = Image.open(os.path.join(ROOT, 'art', 'kit', 'desk', 'desk_foreground.png')).convert('RGBA')
    img.alpha_composite(desk, (0, canvas_h - desk.height))

    return img, mon


def glass_rect_in_frame(layout, mon):
    """Where the glass lands in the composed frame, in top-left pixel coords."""
    spec = mon['screen']
    mx, my = mon['anchor']
    canvas_h = layout['canvas']['height']
    # sprite-local y is up from the sprite bottom; frame y is down from the top
    sprite_h = Image.open(os.path.join(ROOT, 'art', 'kit', 'desk', 'monitor_small.png')).height
    frame_bottom = canvas_h - my            # canvas y of the sprite's bottom edge, measured from the top
    left = int(mx + spec['x'])
    top = int(frame_bottom - spec['y'] - spec['h'])
    return left, top, int(spec['w']), int(spec['h'])


def server_rack(img, x, floor_y):
    """The server: a tall cabinet at the side, cold LEDs, one drive bay open. Palette only."""
    d = ImageDraw.Draw(img)
    w, h = 34, 92
    top = floor_y - h
    d.rectangle([x, top, x + w - 1, floor_y - 1], fill=rgba('panel2'))
    d.rectangle([x, top, x + w - 1, top + 3], fill=rgba('linehi'))       # top lip catches the window
    d.rectangle([x + 1, top + 1, x + w - 2, top + 2], fill=rgba('line'))
    d.line([x, top, x, floor_y - 1], fill=rgba('line'))
    d.line([x + w - 1, top, x + w - 1, floor_y - 1], fill=rgba('line'))
    # eight 1U bays: a vent slot and one status LED each
    for i in range(8):
        y = top + 7 + i * 10
        d.rectangle([x + 3, y, x + w - 8, y + 5], fill=rgba('void'))
        d.line([x + 4, y + 2, x + w - 9, y + 2], fill=rgba('panel2'))
        led = 'signal' if i in (0, 2, 5) else ('signaldim' if i in (1, 4) else 'line')
        d.point((x + w - 4, y + 2), fill=rgba(led))
    # one bay pulled out: the only warm thing in the room
    bay = top + 7 + 6 * 10
    d.rectangle([x + w, bay, x + w + 9, bay + 5], fill=rgba('panel'))
    d.rectangle([x + w, bay, x + w + 9, bay + 5], outline=rgba('line'))
    d.point((x + w + 4, bay + 2), fill=rgba('warn'))
    # feet
    d.rectangle([x + 2, floor_y, x + 8, floor_y + 1], fill=rgba('void'))
    d.rectangle([x + w - 9, floor_y, x + w - 3, floor_y + 1], fill=rgba('void'))
    return x + w


def glow(img, glass, floor_y):
    """
    The cyan spill. Flat palette steps in rings around the screen and along the desk lip under it --
    scanlines, not a gradient, because a blend alpha would break the palette rule and band differently on
    every display. This is the same trick the room uses for the window light.
    """
    gx, gy, gw, gh = glass
    teal, teal_dim, teal_deep = PALETTE['glow_teal'], PALETTE['signaldim'], PALETTE['synth']
    px = img.load()
    # ring 1: 3 px hard edge around the glass
    for i in range(1, 4):
        edge = (gx - i, gy - i, gx + gw - 1 + i, gy + gh - 1 + i)
        col = teal_dim if i == 1 else teal
        for x in range(edge[0], edge[2] + 1):
            for y in (edge[1], edge[3]):
                if 0 <= x < W and 0 <= y < H and px[x, y][3] > 0:
                    px[x, y] = col
        for y in range(edge[1], edge[3] + 1):
            for x in (edge[0], edge[2]):
                if 0 <= x < W and 0 <= y < H and px[x, y][3] > 0:
                    px[x, y] = col
    # spill on the desk: a pool of teal under the monitor, 2 px scan rows, fading by step not by alpha
    for i, tone in enumerate((teal, teal, teal_dim)):
        y = floor_y - 3 + i * 2
        half = gw // 2 + 18 - i * 10
        cx = gx + gw // 2
        for x in range(cx - half, cx + half):
            if 0 <= x < W and 0 <= y < H and px[x, y][3] > 0:
                px[x, y] = tone


def menu_rows(has_save, in_progress):
    """What fits in 17 columns of glass. Short by necessity, legible by choice."""
    rows = [("HR//0D", 'signal'), ("", 'text')]
    if has_save:
        rows.append(("> CONTINUE RUN" if in_progress else "> CONTINUE", 'text'))
        rows.append(("  NEW RUN", 'textdim'))
        rows.append(("  SETTINGS", 'textdim'))
        rows.append(("  DATA", 'textdim'))
        rows.append(("  CREDITS", 'textdim'))
        rows.append(("[SPACE] GO", 'textdim'))
    else:
        rows.append(("> START", 'text'))
        rows.append(("  SETTINGS", 'textdim'))
        rows.append(("  DATA", 'textdim'))
        rows.append(("  CREDITS", 'textdim'))
        rows.append(("", 'text'))
        rows.append(("[SPACE] GO", 'textdim'))
    return rows


def check():
    problems = []
    for name, rows in (('first run', menu_rows(False, False)), ('with a save', menu_rows(True, True))):
        if len(rows) > GLASS_ROWS:
            problems.append('%s: %d rows in %d rows of glass' % (name, len(rows), GLASS_ROWS))
        for text, _ in rows:
            if len(text) > GLASS_COLS:
                problems.append('%s: "%s" is %d cols, glass holds %d' % (name, text, len(text), GLASS_COLS))
    return problems


def main():
    problems = check()
    if problems:
        for p in problems:
            print('  ' + p)
        print('FAIL: the menu does not fit the monitor glass')
        return 1
    if '--check' in sys.argv:
        print('PASS: the whole menu fits in %dx%d of monitor glass' % (GLASS_COLS, GLASS_ROWS))
        return 0

    layout = json.load(open(LAYOUT))
    img, mon = compose(layout)
    glass = glass_rect_in_frame(layout, mon)
    pal, atlas, rects = load_font()

    # RIGHT SIDE, not under the monitor: the desk's monitor already sits over the left half, and a server
    # behind a monitor is a prop nobody sees. It stands where the room is empty, lit by the window.
    right_edge = server_rack(img, 424, 198)        # floor row 198, as the room's own floor strip
    glow(img, glass, 206)                            # desk row 206

    # paint each menu variant onto its own copy and stack them
    shots = []
    for has_save, in_progress in ((False, False), (True, True)):
        shot = img.copy()
        gx, gy, gw, gh = glass
        d = ImageDraw.Draw(shot)
        d.rectangle([gx, gy, gx + gw - 1, gy + gh - 1], fill=PALETTE['void'])
        # one bright scan row at the top of the glass: the screen is on
        d.line([gx, gy, gx + gw - 1, gy], fill=PALETTE['glow_teal'])
        for i, (text, key) in enumerate(menu_rows(has_save, in_progress)):
            if not text:
                continue
            draw_text(shot, rects, atlas, pal, gx + 4, gy + 5 + i * (GLYPH_H + LINE_GAP), text, key)
        shots.append(shot)

    out = os.path.join(ROOT, 'art', 'kit', 'reference', 'menu_room_2x.png')
    sheet = Image.new('RGBA', (W, H * len(shots) + 6 * (len(shots) - 1)), PALETTE['void'])
    for i, s in enumerate(shots):
        sheet.alpha_composite(s, (0, i * (H + 6)))
    sheet = sheet.resize((sheet.width * 2, sheet.height * 2), Image.NEAREST)
    sheet.save(out)
    print('wrote %s (glass %dx%d at %d,%d; server at x=20..%d)'
          % (os.path.relpath(out, ROOT), glass[2], glass[3], glass[0], glass[1], right_edge))
    return 0


if __name__ == '__main__':
    raise SystemExit(main())
