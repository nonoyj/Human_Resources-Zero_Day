#!/usr/bin/env python3
"""
make_menu_room_extra.py -- the two props the concept needs and the room does not have yet.

    desk/server_rack.png   a tall cabinet on the RIGHT of the window: 8U of vented bays, cold status LEDs,
                           one drive bay pulled out in amber. Palette only, alpha 0 or 255.
    fx/screen_halo.png     the cyan light the monitor throws: a flat ring + a scan-row pool, drawn as its
                           own sprite so the menu can light the room without repainting the background.

The concept (player, 2026-11-28) puts the room behind the monitor: city at night, a server on the right,
the menu inside the bezel. Both new files are ordinary kit art -- they go through the same palette gate,
the same manifest, and the same desk_layout entries as every other sprite.

    python tools/art/make_menu_room_extra.py
    python tools/art/make_menu_room_extra.py --check
"""
import os
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
sys.path.insert(0, os.path.join(ROOT, 'tools', 'art'))

from PIL import Image, ImageDraw  # noqa: E402
from make_art_kit import PALETTE, rgba  # noqa: E402

RACK_W, RACK_H = 34, 92
HALO_W, HALO_H = 176, 116      # glass 142x88 + 17 px ring on three sides, 6 px of desk pool below


def out(rel):
    return os.path.join(ROOT, 'art', 'kit', rel)


def server_rack():
    img = Image.new('RGBA', (RACK_W, RACK_H), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    w, h = RACK_W, RACK_H
    d.rectangle([0, 0, w - 1, h - 1], fill=rgba('panel2'))
    # top lip: catches the window, so the cabinet reads as an object and not a black hole
    d.rectangle([0, 0, w - 1, 2], fill=rgba('linehi'))
    d.point((3, 1), fill=rgba('textdim'))
    d.line([0, 0, 0, h - 1], fill=rgba('line'))
    d.line([w - 1, 0, w - 1, h - 1], fill=rgba('line'))
    # eight bays
    for i in range(8):
        y = 6 + i * 10
        d.rectangle([3, y, w - 8, y + 5], fill=rgba('void'))
        d.line([4, y + 2, w - 9, y + 2], fill=rgba('panel2'))
        # status LED per bay: three live, two warm, three dark
        led = 'signal' if i in (0, 2, 5) else ('signaldim' if i in (1, 4) else 'line')
        d.point((w - 4, y + 2), fill=rgba(led))
        if i in (0, 5):
            d.point((w - 5, y + 2), fill=rgba('glow_teal'))
    # bay 6 pulled out and lit: the one amber thing in the room
    bay = 6 + 6 * 10
    d.rectangle([w, bay, w + 1, bay + 5], fill=rgba('panel'))
    d.rectangle([6, bay, w - 8, bay + 4], fill=rgba('ink'))
    d.point((w - 4, bay + 2), fill=rgba('warn'))
    # feet
    d.rectangle([2, h - 1, 8, h - 1], fill=rgba('void'))
    d.rectangle([w - 9, h - 1, w - 3, h - 1], fill=rgba('void'))
    return img


def screen_halo():
    """
    Drawn as a ring AROUND a hole: the glass area stays transparent so the menu texture shows through.
    Two flat steps plus a scan-row pool -- the same 'light falls off in steps' rule the corruption
    states use (art bible §7), and no alpha blending anywhere.
    """
    img = Image.new('RGBA', (HALO_W, HALO_H), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    gx0, gy0 = 4, 4
    gl, gt, gw, gh = gx0 + 13, gy0 + 9, 142, 88      # glass sits centred-ish in the halo
    gx1, gy1 = gl + gw - 1, gt + gh - 1

    teal = rgba('glow_teal')
    dim = rgba('signaldim')
    for i in range(1, 4):
        col = dim if i == 1 else teal
        d.rectangle([gl - i, gt - i, gx1 + i, gy1 + i], outline=col)
    # the pool on the desk below the bezel
    for i, tone in enumerate((teal, teal, dim)):
        y = gy1 + 4 + i * 2
        half = gw // 2 + 22 - i * 12
        cx = (gl + gx1) // 2
        d.line([cx - half, y, cx + half, y], fill=tone)
    return img


BIG_W, BIG_H = 260, 170
BIG_GLASS = (12, 12, 236, 144)      # x, y(from top), w, h


def monitor_big():
    """
    The desk monitor is 154x106 with 142x88 of glass -- 17 characters wide, 8 lines. That is fine for a
    shift record and far too small for a menu with a title, five rows and a hint. The concept draws one
    big screen filling the left of the room, so this is that screen: 260x170 with 236x144 of glass,
    which holds 31 characters on 14 lines. Same drawing language as monitor_small (1 px frame, a vent
    strip under the glass, a power LED), just larger, so the two never look like different games.
    """
    img = Image.new('RGBA', (BIG_W, BIG_H), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    gx, gy, gw, gh = BIG_GLASS
    # case
    d.rectangle([0, 0, BIG_W - 1, BIG_H - 1], fill=rgba('panel'))
    d.rectangle([0, 0, BIG_W - 1, 1], fill=rgba('linehi'))          # top edge catches the window
    d.rectangle([0, 0, 1, BIG_H - 1], fill=rgba('line'))
    d.rectangle([BIG_W - 2, 0, BIG_W - 1, BIG_H - 1], fill=rgba('line'))
    d.rectangle([0, BIG_H - 2, BIG_W - 1, BIG_H - 1], fill=rgba('void'))
    # bezel step: one darker inset, so the glass reads as sunk into the case
    d.rectangle([gx - 4, gy - 4, gx + gw + 3, gy + gh + 3], fill=rgba('panel2'))
    # glass
    d.rectangle([gx - 1, gy - 1, gx + gw, gy + gh], fill=rgba('line'))
    d.rectangle([gx, gy, gx + gw - 1, gy + gh - 1], fill=rgba('void'))
    # vent strip + LED under the glass
    vy = gy + gh + 5
    for x in range(gx, gx + gw, 4):
        d.rectangle([x, vy, x + 1, vy + 6], fill=rgba('void'))
    d.rectangle([BIG_W - 26, vy + 2, BIG_W - 22, vy + 4], fill=rgba('signal'))
    d.rectangle([BIG_W - 18, vy + 2, BIG_W - 14, vy + 4], fill=rgba('linehi'))
    # stand: two feet that sit on the desk surface
    d.rectangle([BIG_W // 2 - 26, BIG_H - 1, BIG_W // 2 + 26, BIG_H - 1], fill=rgba('void'))
    return img


def palette_gate(images):
    vals = set(PALETTE.values())
    bad = 0
    for im in images:
        for y in range(im.height):
            for x in range(im.width):
                px = im.getpixel((x, y))
                if px[3] == 0:
                    continue
                if px[3] != 255 or px[:3] not in vals:
                    bad += 1
    return bad


def main():
    rack = server_rack()
    halo = screen_halo()
    big = monitor_big()
    bad = palette_gate((rack, halo, big))
    if '--check' in sys.argv:
        print('%s: %d off-palette pixels' % ('PASS' if bad == 0 else 'FAIL', bad))
        return 0 if bad == 0 else 1

    rack.save(out('desk/server_rack.png'))
    halo.save(out('fx/screen_halo.png'))
    big.save(out('desk/monitor_big.png'))
    print('wrote art/kit/desk/monitor_big.png (%dx%d, glass %dx%d)' % (big.width, big.height, BIG_GLASS[2], BIG_GLASS[3]))
    print('wrote art/kit/desk/server_rack.png (%dx%d)' % rack.size)
    print('wrote art/kit/fx/screen_halo.png (%dx%d)' % halo.size)
    print('off-palette px: %d' % bad)
    return 0 if bad == 0 else 1


if __name__ == '__main__':
    raise SystemExit(main())
