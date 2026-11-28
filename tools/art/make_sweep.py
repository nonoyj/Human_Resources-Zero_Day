#!/usr/bin/env python3
"""HR//0D after-hours sweep art -- the one place the game changes style on purpose.

docs/16-after-hours-sweep.md #6 decides the contrast:

    main game   painterly bust, dither ramps, rim light
    sweep       FLAT side-on room, 2-tone fills, hard 1 px edges, tiny (~16 px) characters,
                neon-cyan key caps along the bottom, caption bar

So this generator is deliberately the opposite of painterly.py: no ramps, no dither, no rim light. Two
tones per mass and a 1 px edge. Palette is still locked -- every pixel is an existing palette entry.

    python tools/art/make_sweep.py
"""
from __future__ import annotations

import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

try:
    from PIL import Image
except ImportError:                                                   # pragma: no cover
    print('Pillow is required to regenerate sweep art (pip install pillow)')
    raise SystemExit(2)

OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..', 'art', 'kit', 'sweep')

PALETTE = {
    'void': (0x0b, 0x0e, 0x13), 'panel': (0x13, 0x19, 0x26), 'panel2': (0x1d, 0x27, 0x35),
    'line': (0x2b, 0x3a, 0x4d), 'linehi': (0x3f, 0x55, 0x70), 'muted': (0x5d, 0x7e, 0xa1),
    'textdim': (0xa9, 0xc7, 0xe4), 'text': (0xe6, 0xf1, 0xff), 'white': (0xff, 0xff, 0xff),
    'signal': (0x18, 0xe0, 0xb0), 'signaldim': (0x0f, 0xa8, 0x8a), 'warn': (0xff, 0xd1, 0x66),
    'orange': (0xff, 0x8a, 0x3d), 'danger': (0xff, 0x47, 0x57), 'dangerdim': (0xa1, 0x1a, 0x2b),
    'synth': (0x7b, 0x61, 0xff), 'synthdim': (0xb3, 0x9d, 0xff), 'ok': (0x6e, 0xe7, 0xc8),
    'paperdim': (0x8a, 0x6d, 0x3b), 'paper': (0xf2, 0xe8, 0xd5), 'ink': (0x2a, 0x21, 0x18),
    'glow_teal': (0x0c, 0x3d, 0x38), 'glow_amber': (0x5c, 0x4a, 0x20), 'glow_red': (0x4a, 0x15, 0x20),
}

W, H = 480, 270


class Flat:
    """Two-tone flat painter: fill, 1 px edge, no gradients. The whole point of the sweep look."""

    def __init__(self, w, h, bg):
        self.img = Image.new('RGBA', (w, h), PALETTE[bg] + (255,))
        self.px = self.img.load()
        self.w, self.h = w, h

    def put(self, x, y, key):
        if 0 <= x < self.w and 0 <= y < self.h:
            self.px[x, y] = PALETTE[key] + (255,)

    def rect(self, x0, y0, x1, y1, key):
        for y in range(y0, y1 + 1):
            for x in range(x0, x1 + 1):
                self.put(x, y, key)

    def frame(self, x0, y0, x1, y1, key):
        """A 1 px edge -- the only "line work" this style allows."""
        for x in range(x0, x1 + 1):
            self.put(x, y0, key)
            self.put(x, y1, key)
        for y in range(y0, y1 + 1):
            self.put(x0, y, key)
            self.put(x1, y, key)

    def dither(self, x0, y0, x1, y1, a, b):
        """A 50% checker of two existing entries -- the only gradient this style allows."""
        for y in range(y0, y1 + 1):
            for x in range(x0, x1 + 1):
                self.put(x, y, a if (x + y) & 1 else b)

    def save(self, name):
        os.makedirs(OUT, exist_ok=True)
        path = os.path.join(OUT, name)
        self.img.save(path)
        return path


# ---------------------------------------------------------------- the three floors
def desk():
    """The interview room at night, side-on and flat. Nobody in it. The monitor is still on."""
    f = Flat(W, H, 'void')
    # back wall: two flat masses, split by a hard horizontal line at picture-rail height
    f.rect(0, 0, W - 1, 196, 'panel')
    f.rect(0, 196, W - 1, 214, 'panel2')                    # skirting
    f.put(0, 196, 'line')
    for x in range(0, W, 1):
        f.put(x, 195, 'line')
    # floor, flat, with a single seam to sell depth
    f.rect(0, 215, W - 1, H - 1, 'panel2')
    for x in range(0, W, 40):
        f.rect(x, 216, x, H - 1, 'panel')
    # window: night city, dithered glow (the only soft edge in the frame)
    f.rect(56, 44, 190, 132, 'void')
    f.frame(54, 42, 192, 134, 'line')
    f.dither(58, 60, 188, 96, 'void', 'panel')
    for (bx, bw, bh) in ((70, 14, 44), (96, 18, 58), (124, 12, 36), (150, 20, 50), (176, 10, 30)):
        f.rect(bx, 130 - bh, bx + bw, 132, 'panel')
    for (bx, by) in ((74, 96), (100, 88), (128, 106), (156, 92), (180, 110)):
        f.put(bx, by, 'glow_amber')
        f.put(bx + 1, by, 'glow_amber')
    f.rect(56, 44, 190, 46, 'linehi')                        # the top of the frame catches moonlight
    # the desk, seen side-on: a slab with two legs
    f.rect(196, 148, 432, 160, 'panel2')
    f.rect(196, 148, 432, 150, 'linehi')
    f.rect(196, 151, 432, 151, 'line')
    f.rect(196, 161, 432, 163, 'line')
    f.rect(208, 164, 216, 214, 'panel')
    f.rect(412, 164, 420, 214, 'panel')
    # the monitor, switched on, showing nothing
    f.rect(236, 92, 306, 148, 'panel2')
    f.rect(242, 98, 300, 142, 'glow_teal')
    f.frame(240, 96, 302, 144, 'line')
    f.rect(268, 149, 274, 156, 'panel')                     # stand
    f.rect(258, 156, 284, 158, 'panel2')
    for x in range(246, 298, 2):                            # one dead scanline, dithered
        f.put(x, 120, 'signaldim')
    f.rect(246, 104, 262, 106, 'signaldim')                  # a cursor block, left on
    # the interview chair, empty, facing the desk
    f.rect(346, 128, 406, 138, 'panel')                     # back
    f.rect(346, 128, 406, 130, 'linehi')
    f.frame(346, 128, 406, 138, 'line')
    f.rect(352, 139, 400, 150, 'panel2')                    # seat
    f.rect(356, 151, 362, 200, 'panel')
    f.rect(392, 151, 398, 200, 'panel')
    f.rect(350, 200, 404, 204, 'line')                      # base
    # the mug, on the desk, moved from where you left it
    f.rect(214, 138, 226, 150, 'panel2')
    f.rect(214, 138, 226, 139, 'linehi')
    f.rect(227, 141, 230, 147, 'panel2')
    # ceiling light, off, still hanging
    f.rect(200, 8, 340, 14, 'line')
    f.rect(206, 15, 334, 17, 'panel')
    for x in range(210, 332, 24):
        f.rect(x, 0, x + 1, 8, 'line')
    return f.save('sweep_desk_480x270.png')


def corridor():
    """A corridor is the same wall repeating. Flat bands, one doorway with the light on."""
    f = Flat(W, H, 'void')
    f.rect(0, 0, W - 1, 206, 'panel')
    f.rect(0, 207, W - 1, 216, 'line')                      # skirting
    f.rect(0, 217, W - 1, H - 1, 'panel2')                  # floor
    # ceiling tiles receding: wider gaps as they approach
    for i, x in enumerate(range(0, W, 30)):
        f.rect(x, 0, x + 1, 26, 'panel2')
    f.rect(0, 26, W - 1, 27, 'line')
    # doors: every third one is a staff door (taller)
    for i, x in enumerate(range(24, W, 112)):
        tall = i % 3 == 1
        top = 62 if tall else 84
        f.rect(x, top, x + 46, 206, 'panel2')
        f.frame(x, top, x + 46, 206, 'line')
        f.rect(x + 6, top + 8, x + 40, top + 54, 'panel')    # the door's inset panel
        f.frame(x + 6, top + 8, x + 40, top + 54, 'line')
        f.rect(x + 38, top + 92, x + 41, top + 96, 'linehi') # handle
    # one doorway is open and lit: the only warm shape on the floor
    f.rect(248, 60, 292, 206, 'void')
    f.rect(250, 62, 290, 206, 'glow_amber')
    f.dither(250, 62, 290, 96, 'glow_amber', 'panel2')       # light falling out of the top of the door
    f.frame(246, 58, 294, 206, 'line')
    # a chair, pulled out into the corridor, facing the wrong way
    f.rect(96, 168, 140, 174, 'panel')
    f.rect(100, 175, 136, 186, 'panel2')
    f.rect(104, 187, 110, 216, 'panel')
    f.rect(128, 187, 134, 216, 'panel')
    # a ceiling strip light, on, one panel out
    f.rect(150, 32, 330, 42, 'panel2')
    f.frame(150, 32, 330, 42, 'line')
    f.rect(158, 35, 238, 39, 'glow_teal')
    f.rect(268, 20, 300, 34, 'panel2')                      # EXIT sign
    f.frame(268, 20, 300, 34, 'line')
    f.rect(274, 24, 294, 30, 'glow_teal')
    f.rect(276, 26, 292, 28, 'signaldim')
    f.put(300, 37, 'line')
    return f.save('sweep_corridor_480x270.png')


def lift():
    """The lift, holding on your floor. Doors open. The button is lit from the inside."""
    f = Flat(W, H, 'void')
    f.rect(0, 0, W - 1, 200, 'panel')
    f.rect(0, 201, W - 1, 216, 'line')
    f.rect(0, 217, W - 1, H - 1, 'panel2')
    # the frame around the doors
    f.rect(112, 30, 368, 216, 'panel2')
    f.frame(110, 28, 370, 216, 'line')
    # the car itself: open, empty, lit from nowhere
    f.rect(124, 42, 356, 216, 'void')
    f.rect(126, 44, 354, 216, 'glow_amber')
    f.dither(126, 44, 354, 120, 'glow_amber', 'panel2')      # light coming down the back wall
    f.frame(122, 40, 358, 216, 'line')
    # the back wall of the car, and a mirror that shows the corridor behind you
    f.rect(190, 60, 290, 130, 'panel2')
    f.frame(188, 58, 292, 132, 'line')
    f.dither(192, 62, 288, 128, 'panel2', 'panel')
    f.rect(236, 132, 244, 216, 'panel')                      # handrail post
    # the two doors, slid apart
    f.rect(112, 36, 130, 216, 'panel2')
    f.frame(112, 36, 130, 216, 'line')
    f.rect(350, 36, 368, 216, 'panel2')
    f.frame(350, 36, 368, 216, 'line')
    # floor indicator: 5, and an arrow pointing at the floor you are on
    f.rect(206, 8, 274, 26, 'void')
    f.frame(204, 6, 276, 28, 'line')
    f.rect(224, 12, 234, 14, 'warn')                        # the "5", blocky
    f.rect(232, 12, 234, 19, 'warn')
    f.rect(224, 17, 234, 19, 'warn')
    f.rect(224, 17, 226, 19, 'warn')
    f.rect(238, 12, 244, 18, 'glow_amber')                   # a lit arrow, down
    f.rect(240, 18, 242, 22, 'glow_amber')
    # the call button, outside, lit from the inside of the car
    f.rect(64, 96, 84, 116, 'panel2')
    f.frame(62, 94, 86, 118, 'line')
    f.rect(70, 102, 78, 110, 'glow_amber')
    f.put(74, 106, 'warn')
    return f.save('sweep_lift_480x270.png')


def walker():
    """You, at 16 px, back view, walking. Tiny on purpose: the room is the subject."""
    f = Flat(16, 24, 'void')
    for y in range(24):
        for x in range(16):
            f.put(x, y, 'void')
    f.rect(5, 2, 10, 6, 'ink')                               # head, hair from behind
    f.put(7, 4, 'line')
    f.rect(4, 7, 11, 15, 'panel2')                           # coat
    f.frame(4, 7, 11, 15, 'line')
    f.rect(4, 9, 11, 10, 'line')                             # shoulder line
    f.rect(4, 16, 6, 21, 'panel')                            # legs, one forward
    f.rect(9, 16, 11, 20, 'panel')
    f.rect(4, 22, 6, 22, 'line')
    f.rect(9, 21, 11, 21, 'line')
    # the image is composited, so the background has to be transparent
    for y in range(24):
        for x in range(16):
            if f.px[x, y][:3] == PALETTE['void']:
                f.px[x, y] = (0, 0, 0, 0)
    return f.save('sweep_walker_16x24.png')


def keys():
    """The neon key caps. The one non-diegetic UI in the game, and it exists only here."""
    f = Flat(480, 22, 'void')
    labels = ['WALK', 'LOOK', 'BACK', 'END']
    x = 8
    for i, lab in enumerate(labels):
        w = 74
        f.rect(x, 2, x + w, 19, 'glow_teal')
        f.frame(x, 2, x + w, 19, 'signal')
        # blocky glyphs: 3 px strokes, no font needed at this size
        for k in range(3):
            f.rect(x + 8 + k, 7, x + 8 + k, 14, 'signal')
            f.rect(x + 8, 7 + k * 3, x + 8 + 4, 7 + k * 3, 'signal')
        f.rect(x + 18, 7, x + 18 + len(lab) * 4, 14, 'glow_teal')
        for k, ch in enumerate(lab):
            f.rect(x + 20 + k * 4, 9, x + 21 + k * 4, 12, 'signal')
        x += w + 6
    return f.save('sweep_keys_480x22.png')


def main():
    made = [desk(), corridor(), lift(), walker(), keys()]
    for p in made:
        print('wrote', os.path.relpath(p, os.path.join(OUT, '..', '..', '..')))
    return 0


if __name__ == '__main__':
    raise SystemExit(main())
