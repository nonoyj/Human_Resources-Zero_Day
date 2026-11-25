#!/usr/bin/env python3
"""
HR//0D applicant sprites — 192x216, waist-up, seated, 3/4 view facing the monitor (left).
Palette-locked and deterministic, like the UI kit.

Why 6 archetypes rather than 100 unique faces: the game's tell is *behaviour and evidence*, not
"learn 100 faces". Six visual archetypes (3 human, 3 synthetic) x 4 idle frames give enough variety
while keeping the art reviewable and the kit small.

Synthetic tells baked into the art (deliberately subtle, all documented in docs/08-behaviour-model.md):
  - synthetic_polished : perfectly mirrored face details + a straight-line hair blend seam
  - synthetic_cloned   : the live person does not match the ID photo (photo drawn separately)
  - synthetic_collusion: identical idle silhouette to synthetic_polished (shared mannerisms = the tell)

    python tools/art/make_applicants.py
    python tools/art/make_applicants.py --check
"""
from __future__ import annotations

import argparse
import os
import sys

from PIL import Image

OUT = os.path.join(os.path.dirname(__file__), '..', '..', 'art', 'kit')

PALETTE = {
    'void': (0x0b, 0x0e, 0x13), 'panel': (0x13, 0x19, 0x26), 'panel2': (0x1d, 0x27, 0x35),
    'line': (0x2b, 0x3a, 0x4d), 'linehi': (0x3f, 0x55, 0x70), 'muted': (0x5d, 0x7e, 0xa1),
    'textdim': (0xa9, 0xc7, 0xe4), 'text': (0xe6, 0xf1, 0xff), 'white': (0xff, 0xff, 0xff),
    'signal': (0x18, 0xe0, 0xb0), 'signaldim': (0x0f, 0xa8, 0x8a), 'warn': (0xff, 0xd1, 0x66),
    'orange': (0xff, 0x8a, 0x3d), 'danger': (0xff, 0x47, 0x57), 'dangerdim': (0xa1, 0x1a, 0x2b),
    'synth': (0x7b, 0x61, 0xff), 'synthdim': (0xb3, 0x9d, 0xff), 'ok': (0x6e, 0xe7, 0xc8),
    'paperdim': (0x8a, 0x6d, 0x3b), 'paper': (0xf2, 0xe8, 0xd5), 'ink': (0x2a, 0x21, 0x18),
}
PALETTE_SET = set(PALETTE.values())

# six skin ramps (3 shades each) so applicants read as different people without new colours
SKIN = [
    ('paper',   'paperdim', 'ink'),      # very light
    ('warn',    'orange',   'paperdim'), # light warm
    ('orange',  'paperdim', 'ink'),      # medium warm
    ('paperdim','ink',      'void'),     # medium deep
    ('muted',   'linehi',   'ink'),      # cool deep
    ('linehi',  'line',     'void'),     # very deep / cool
]
# hair must CONTRAST with the dark room -- dark-on-dark reads as bald/balaclava
HAIR = ['paperdim', 'warn', 'muted', 'linehi', 'textdim']
HAIR_HI = {'paperdim': 'warn', 'warn': 'text', 'muted': 'textdim', 'linehi': 'textdim', 'textdim': 'white'}
OUTFIT = ['panel2', 'line', 'panel', 'muted']
ACCENT = ['signal', 'warn', 'synthdim', 'ok', 'orange']


def rgba(name, a=255):
    r, g, b = PALETTE[name]
    return (r, g, b, a)


class Applicant:
    """Procedural but deterministic: identity comes from an int seed, not randomness at runtime."""

    def __init__(self, seed: int, synthetic: bool = False, mirrored_face: bool = False):
        s = seed
        self.seed = seed
        self.synthetic = synthetic
        self.skin_light, self.skin_mid, self.skin_dark = SKIN[s % len(SKIN)]
        self.hair = HAIR[(s // 3) % len(HAIR)]
        self.outfit = OUTFIT[(s // 7) % len(OUTFIT)]
        self.accent = ACCENT[(s // 11) % len(ACCENT)]
        self.head_w = 34 + (s % 3) * 2          # 34..38
        self.shoulders = 78 + (s % 4) * 4       # 78..90
        self.hair_style = s % 4                 # 0..3
        self.mirrored_face = mirrored_face      # synthetic tell: perfect symmetry
        self.blend_seam = synthetic             # synthetic tell: straight hair/background line
        # idle variation
        self.breathe = 0                        # 0..1 px shoulder rise
        self.blink = False
        self.fidget = 0                         # 0..1 hand offset

    # ------------------------------------------------------------------ drawing
    def draw(self, w=192, h=216) -> Image.Image:
        """Waist-up, seated, 3/4 to the monitor. Fills the frame: head top ~14, table edge ~196."""
        img = Image.new('RGBA', (w, h), (0, 0, 0, 0))
        px = img.load()
        cx = w // 2

        def put(x, y, col):
            if 0 <= x < w and 0 <= y < h:
                px[x, y] = rgba(col)

        def rect(x0, y0, x1, y1, col):
            for y in range(y0, y1 + 1):
                for x in range(x0, x1 + 1):
                    put(x, y, col)

        def ellipse_ish(cx0, cy0, rx, ry, col):
            for y in range(-ry, ry + 1):
                half = int(rx * (1 - (y * y) / float(ry * ry + 1)) ** 0.5)
                for x in range(-half, half + 1):
                    put(cx0 + x, cy0 + y, col)

        breath = self.breathe
        base = 196                      # table edge line
        # ---------------- chair back (behind everything)
        rect(cx - 52, 120, cx + 52, base + 6, 'panel')
        rect(cx - 52, 120, cx + 52, 124, 'panel2')

        # ---------------- torso: sloped shoulders widening downward, fills to the table
        sh = 46                          # shoulder half-width at the top
        top = 100 - breath
        for i in range(base + 10 - top):
            y = top + i
            # shoulders widen quickly for 18px then stay roughly flat to the table
            half = sh + min(18, int(i * 1.6)) if i < 18 else 46 + 18
            half = min(half, 74)
            rect(cx - half, y, cx + half, y, self.outfit)
            rect(cx - half, y, cx - half + 1, y, 'linehi')     # rim light on the silhouette
            rect(cx - half + 1, y, cx - half + 3, y, 'line')
            rect(cx + half - 3, y, cx + half - 1, y, 'line')
            rect(cx + half, y, cx + half, y, 'linehi')
        # shirt opening / collar
        for i in range(16):
            rect(cx - 10 + i // 2, top + i, cx + 10 - i // 2, top + i, self.skin_mid)
        # lanyard + ID badge
        rect(cx - 2, top + 14, cx + 2, top + 40, self.accent)
        rect(cx - 9, top + 40, cx + 9, top + 56, 'paper')
        rect(cx - 9, top + 40, cx + 9, top + 56, 'paper')
        rect(cx - 9, top + 40, cx + 9, top + 41, 'paperdim')
        rect(cx - 6, top + 44, cx + 6, top + 47, 'line')
        rect(cx - 6, top + 50, cx + 6, top + 51, 'paperdim')

        # ---------------- arms resting toward the table, hands just above the edge
        arm_x = 52
        rect(cx - arm_x - 6, top + 16, cx - arm_x + 4, base + 4, self.outfit)
        rect(cx + arm_x - 4, top + 16, cx + arm_x + 6, base + 4, self.outfit)
        rect(cx - arm_x - 6, top + 16, cx - arm_x - 4, base + 4, 'line')
        rect(cx + arm_x + 4, top + 16, cx + arm_x + 6, base + 4, 'line')
        fidget = self.fidget
        rect(cx - arm_x - 7, base - 6 - fidget, cx - arm_x + 6, base + 2 - fidget, self.skin_mid)
        rect(cx + arm_x - 6, base - 6 + fidget, cx + arm_x + 7, base + 2 + fidget, self.skin_mid)

        # ---------------- neck
        rect(cx - 10, 88 - breath, cx + 10, 102 - breath, self.skin_mid)
        rect(cx - 10, 88 - breath, cx + 10, 91 - breath, self.skin_dark)

        # ---------------- head
        hw = 21                                  # head half-width -> 42px wide
        head_top = 16 - breath
        head_bot = 88 - breath
        rect(cx - hw, head_top + 8, cx + hw, head_bot, self.skin_mid)
        rect(cx - hw, head_top + 8, cx + hw, head_top + 14, self.skin_light)
        rect(cx - hw, head_bot - 8, cx + hw, head_bot, self.skin_dark)
        rect(cx - hw - 2, head_top + 20, cx - hw, head_top + 34, self.skin_mid)   # ears
        rect(cx + hw, head_top + 20, cx + hw + 2, head_top + 34, self.skin_mid)
        # jaw taper
        for i in range(8):
            rect(cx - hw + i, head_bot + i, cx + hw - i, head_bot + i, self.skin_mid)

        # ---------------- hair (4 styles)
        if self.hair_style == 0:          # short crop
            rect(cx - hw, head_top, cx + hw, head_top + 12, self.hair)
            rect(cx - hw, head_top + 12, cx - hw + 4, head_top + 26, self.hair)
            rect(cx + hw - 4, head_top + 12, cx + hw, head_top + 26, self.hair)
        elif self.hair_style == 1:        # side part
            rect(cx - hw - 1, head_top, cx + hw, head_top + 14, self.hair)
            rect(cx - hw - 1, head_top + 14, cx - hw + 7, head_top + 4, self.hair)
            rect(cx - hw - 1, head_top + 14, cx - hw + 6, head_top + 40, self.hair)
        elif self.hair_style == 2:        # bun
            rect(cx - hw, head_top + 2, cx + hw, head_top + 12, self.hair)
            ellipse_ish(cx, head_top - 2, 9, 8, self.hair)
        else:                             # long, pulled back
            rect(cx - hw - 3, head_top, cx + hw + 3, head_top + 14, self.hair)
            rect(cx - hw - 3, head_top + 14, cx - hw + 2, head_bot - 2, self.hair)
            rect(cx + hw - 2, head_top + 14, cx + hw + 3, head_bot - 2, self.hair)

        # hair top highlight so the silhouette reads against a dark room
        hi = HAIR_HI.get(self.hair, 'textdim')
        rect(cx - hw + 2, head_top + 1, cx + hw - 2, head_top + 2, hi)
        rect(cx - hw + 4, head_top + 3, cx - hw + 6, head_top + 5, hi)
        if self.blend_seam:
            # SYNTHETIC TELL: hair meets the background on a perfectly straight line, both sides equal
            for x in range(cx - hw - 3, cx + hw + 4):
                put(x, head_top - 1, 'synth')

        # ---------------- face (readable at 44px wide)
        eye_y = head_top + 30
        eye_dx = 10
        if self.blink:
            rect(cx - eye_dx - 4, eye_y + 2, cx - eye_dx + 4, eye_y + 2, 'ink')
            rect(cx + eye_dx - 4, eye_y + 2, cx + eye_dx + 4, eye_y + 2, 'ink')
        else:
            rect(cx - eye_dx - 3, eye_y, cx - eye_dx + 4, eye_y + 5, 'white')
            rect(cx + eye_dx - 4, eye_y, cx + eye_dx + 3, eye_y + 5, 'white')
            if self.mirrored_face:
                rect(cx - eye_dx - 1, eye_y + 1, cx - eye_dx + 2, eye_y + 4, 'ink')
                rect(cx + eye_dx - 2, eye_y + 1, cx + eye_dx + 1, eye_y + 4, 'ink')
            else:
                rect(cx - eye_dx - 1, eye_y + 1, cx - eye_dx + 2, eye_y + 4, 'ink')
                rect(cx + eye_dx - 1, eye_y + 2, cx + eye_dx + 2, eye_y + 5, 'ink')
        # brows
        rect(cx - eye_dx - 4, eye_y - 6, cx - eye_dx + 4, eye_y - 4, self.hair)
        rect(cx + eye_dx - 4, eye_y - 6, cx + eye_dx + 4, eye_y - 4, self.hair)
        # nose
        rect(cx - 1, eye_y + 8, cx + 1, eye_y + 15, self.skin_dark)
        rect(cx - 3, eye_y + 15, cx + 3, eye_y + 16, self.skin_dark)
        # mouth
        mouth_y = eye_y + 22
        rect(cx - 7, mouth_y, cx + 7, mouth_y + 2, self.skin_dark)
        rect(cx - 4, mouth_y + 3, cx + 4, mouth_y + 3, self.skin_light)
        # beauty mark: one cheek for humans, BOTH cheeks for mirrored synthetics
        if self.mirrored_face:
            put(cx - 15, eye_y + 13, 'ink')
            put(cx + 15, eye_y + 13, 'ink')
        elif self.seed % 2 == 0:
            put(cx - 15, eye_y + 13, 'ink')
        # eye bags / age line for the bait applicant (weariness, not a tell)
        if self.seed % 5 == 1:
            rect(cx - eye_dx - 3, eye_y + 7, cx - eye_dx + 3, eye_y + 7, self.skin_dark)
            rect(cx + eye_dx - 3, eye_y + 7, cx + eye_dx + 3, eye_y + 7, self.skin_dark)

        # ---------------- table edge in front (occludes the lower body)
        rect(0, base, w - 1, h - 1, 'panel2')
        rect(0, base, w - 1, base + 3, 'linehi')
        rect(0, base + 3, w - 1, base + 4, 'line')
        for x in range(0, w, 16):
            rect(x, base + 5, x, h - 1, 'panel')
        return img


ARCHETYPES = {
    # name                seed  synthetic  mirrored
    'human_solid_a':    (3,   False, False),
    'human_solid_b':    (17,  False, False),
    'human_nervous_a':  (29,  False, False),
    'human_bait_a':     (41,  False, False),
    'synthetic_polished_a': (101, True, True),
    'synthetic_cloned_a':   (113, True, True),
    'synthetic_collusion_a':(127, True, True),
}


def build_frame(state: Applicant, frame: int) -> Image.Image:
    """4 idle frames: breathe 0/1, blink on frame 3, fidget on frame 2."""
    a = Applicant(state.seed, state.synthetic, state.mirrored_face)
    a.breathe = 1 if frame in (1, 2) else 0
    a.blink = (frame == 3)
    a.fidget = 1 if frame == 2 else 0
    return a.draw()


def make_sheet(state: Applicant) -> Image.Image:
    frames = [build_frame(state, i) for i in range(4)]
    sheet = Image.new('RGBA', (192 * 4, 216), (0, 0, 0, 0))
    for i, f in enumerate(frames):
        sheet.paste(f, (192 * i, 0))
    return sheet


def make_portrait(state: Applicant) -> Image.Image:
    """96x96 document photo crop of the head+shoulders."""
    full = state.draw()
    head = full.crop((48, 34, 144, 130))
    return head


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('--check', action='store_true')
    args = ap.parse_args()
    root = os.path.normpath(OUT)

    if args.check:
        problems, count = [], 0
        for sub in ('applicants', 'portraits'):
            d = os.path.join(root, sub)
            if not os.path.isdir(d):
                continue
            for f in sorted(os.listdir(d)):
                if not f.endswith('.png'):
                    continue
                count += 1
                img = Image.open(os.path.join(d, f)).convert('RGBA')
                for px in list(img.getdata()):
                    if px[3] == 0:
                        continue
                    if px[:3] not in PALETTE_SET:
                        problems.append((f, px))
                        break
                    if 0 < px[3] < 255:
                        problems.append((f, px))
                        break
        print(f"checked {count} applicant sprites")
        if problems:
            print("violations:")
            for f, px in problems[:8]:
                print("   ", f, px)
            return 1
        print("PASS: palette-conformant, no partial alpha")
        return 0

    made = []
    for name, (seed, synthetic, mirrored) in ARCHETYPES.items():
        st = Applicant(seed, synthetic, mirrored)
        sheet = make_sheet(st)
        p = os.path.join(root, 'applicants', f'app_{name}_192x216.png')
        os.makedirs(os.path.dirname(p), exist_ok=True)
        sheet.save(p)
        made.append(p)
        # document portrait (clone archetype: portrait belongs to a DIFFERENT person -> the tell)
        pseed = seed + 500 if name == 'synthetic_cloned_a' else seed
        port = make_portrait(Applicant(pseed, synthetic, mirrored))
        p2 = os.path.join(root, 'portraits', f'portrait_{name}_96x96.png')
        os.makedirs(os.path.dirname(p2), exist_ok=True)
        port.save(p2)
        made.append(p2)

    print(f"wrote {len(made)} files")
    for m in made:
        print("  " + os.path.relpath(m, root))
    return 0


if __name__ == '__main__':
    sys.exit(main())
