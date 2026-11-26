#!/usr/bin/env python3
"""
HR//0D applicant sprites v2 -- 14 archetypes, visible AI artifacts, human variety.

192x216 waist-up, seated, 3/4 to the monitor (left). Palette-locked, deterministic.

DESIGN: humans differ in *identity* (age, skin, hair, glasses, facial hair, headwear, build).
Synthetics differ in *artifacts* -- small things that are wrong in a way a person never is.
Each artifact is drawn, documented, and mirrored in the Field Manual legend.

Artifacts (synthetics only), all listed in docs/08-behaviour-model.md:
    seam            dead-straight hair/background line
    mirror          identical catchlights + beauty mark on BOTH cheeks
    hands           merged fingers (the classic generative-image failure)
    ear             one ear is a featureless rectangle
    glasses_merge   the glasses temple melts into the head with no gap
    shadow          the chair shadow falls on the wrong side
    skin_band       a hard posterised band at the jawline instead of a gradient
    no_blink        the idle loop never blinks (animation tell; see behaviour model)
    straight_badge  the lanyard/badge is perfectly vertical and centred

    python tools/art/make_applicants.py
    python tools/art/make_applicants.py --check
"""
from __future__ import annotations

import argparse
import os
import sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from ragged import chip_corners, scuff   # RAGGED style pass (docs/15-art-bible.md)
from painterly import Painter, ellipse, taper   # v5: ramps, dither, rim light (2026-11-26)

# Pillow draws; it never verifies (BUG-016).
try:
    from PIL import Image
    HAVE_PIL = True
except ImportError:                                  # pragma: no cover
    Image = None
    HAVE_PIL = False

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from pngcheck import read_rgba                   # noqa: E402

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

SKIN = [
    ('paper', 'paperdim', 'ink'), ('warn', 'orange', 'paperdim'),
    ('orange', 'paperdim', 'ink'), ('paperdim', 'ink', 'void'),
    ('muted', 'linehi', 'ink'), ('linehi', 'line', 'void'),
]
HAIR = ['paperdim', 'warn', 'muted', 'linehi', 'textdim', 'orange']
HAIR_HI = {'paperdim': 'warn', 'warn': 'text', 'muted': 'textdim',
           'linehi': 'textdim', 'textdim': 'white', 'orange': 'warn'}
OUTFIT = ['panel2', 'line', 'panel', 'muted', 'linehi']
ACCENT = ['signal', 'warn', 'synthdim', 'ok', 'orange']


def rgba(name, a=255):
    r, g, b = PALETTE[name]
    return (r, g, b, a)


class Applicant:
    def __init__(self, seed: int, synthetic=False, artifacts=()):
        s = seed
        self.seed = seed
        self.synthetic = synthetic
        self.artifacts = set(artifacts)
        self.skin_light, self.skin_mid, self.skin_dark = SKIN[s % len(SKIN)]
        self.hair = HAIR[(s // 3) % len(HAIR)]
        self.outfit = OUTFIT[(s // 7) % len(OUTFIT)]
        self.accent = ACCENT[(s // 11) % len(ACCENT)]
        self.hair_style = (s // 5) % 6        # crop, side, bun, long, hijab, beanie
        self.glasses = ((s // 13) % 3 == 0)
        self.facial_hair = 0
        if not synthetic and s % 4 == 0:
            self.facial_hair = 1 + (s % 3)          # moustache / beard / both
        self.build = 40 + (s % 5) * 4               # shoulder half-width 40..56
        self.age_lines = (s % 6 == 2)
        # ---- detail traits (v3, 2026-11-26): identity is more than a colour swap.
        self.collar     = s % 4                       # crew / v-neck / turtleneck / shirt+tie
        self.glasses_shape = (s // 13) % 2            # rect / round
        self.accessory  = s % 5                       # none / watch / earrings / pen / ring
        self.desk_prop  = (s // 4) % 4                # mug / pen / note / phone
        self.folds      = 1 + (s % 3)                 # 1..3 fabric fold lines
        self.brow       = 2 + (s % 3)                 # brow thickness 2..4 px
        self.mole       = (s // 3) % 2                # a single mole, off-centre
        self.badge_dx = 0 if 'straight_badge' in self.artifacts else ((s % 5) - 2)
        # ---- v4 (2026-11-26, user note "more human looking"): identity beyond a jacket swap.
        self.wardrobe = (s // 6) % 5          # suit-jacket / knit / hoodie / work-shirt / worn coat
        self.hair_len = (s // 9) % 3          # how far the hair falls past the hairline
        self.outfit2  = 'panel' if self.outfit in ('panel2', 'line') else 'panel2'
        # A person's shoulders are not a rectangle: asymmetrical, one side higher, uneven wear.
        # Synthetics are built, so they are perfectly symmetric -- that is a tell of its own.
        self.shoulder_drop = 0 if self.synthetic else (s % 3)
        # ---- v5 (2026-11-26, "make them look like people"): anatomy variety driven by the seed.
        # head: 0 = oval, 1 = square jaw, 2 = round, 3 = long. neck/build are separate so no two
        # applicants share a silhouette even when they share a face colour.
        self.head_shape = s % 4
        self.neck_w     = 7 + (s % 3)              # 7..9 half-width
        self.build_idx  = (s // 2) % 4             # narrow / average / heavy / very heavy
        self.hand_size  = 4 + (s % 3)              # knuckle span in px
        # three skin families, chosen per seed: light, warm mid, deep. Painted as ramps, not flat fills.
        self.skin_ramp  = ('skin_light', 'skin_mid', 'skin_deep')[(s // 3) % 3]
        self.breathe = 0
        self.blink = False
        self.fidget = 0

    # ------------------------------------------------------------------ drawing
    def draw(self, w=192, h=216) -> Image.Image:
        img = Image.new('RGBA', (w, h), (0, 0, 0, 0))
        px = img.load()
        cx = w // 2
        A = self.artifacts

        def put(x, y, col):
            if 0 <= x < w and 0 <= y < h:
                px[x, y] = rgba(col)

        def rect(x0, y0, x1, y1, col):
            for y in range(y0, y1 + 1):
                for x in range(x0, x1 + 1):
                    put(x, y, col)

        breath = self.breathe
        base = 196
        sh = self.build
        P = Painter(img, PALETTE)      # v5 painterly writer (ramps, dither, rim light)

        # ---------------- chair back
        # REMOVED 2026-11-26 (composition L): the applicant used to carry a lighter slab behind him.
        # It read as a rectangle pasted into the room and forced every background change to fight it.
        # The room now supplies whatever is behind him (see art/kit/backgrounds/office_L*.png).
        # The chair itself lives in desk/table_and_chair.png.

        # (cast shadow is drawn last, on the desk -- see the table block at the end)

        # ---------------- torso (v5: painted masses, not a rectangle)
        # Three masses: a trapezoid chest that widens to the shoulders, ball-shaped deltoids where the
        # arms join, and a belly that sags by build. The silhouette curve is what a shoulder actually is;
        # the old straight-sided slab is why every applicant looked like a cardboard cut-out.
        top = 100 - breath
        build_w = (46, 52, 58, 62)[self.build_idx]
        torso_bot = base + 10
        deltoid_r = (9, 11, 13, 14)[self.build_idx]

        for y in range(top, torso_bot + 1):
            t = (y - top) / float(max(1, torso_bot - top))
            if t < 0.18:                                  # shoulders flare out fast
                half = int(build_w * (0.55 + t / 0.18 * 0.45))
            else:                                         # then the body tapers to the waist
                half = int(build_w * (1.0 - (t - 0.18) * 0.18))
            # belly: a heavy build pushes the waist back out
            if self.build_idx >= 2 and 0.45 < t < 0.85:
                half += int(2 + self.build_idx * 2)
            for x in range(cx - half, cx + half + 1):
                lit = (x - (cx - half)) / float(max(1, 2 * half))
                tone = 3 - int(lit * 2.3)
                if t > 0.92:
                    tone -= 1                              # hem falls into shadow
                P.put(x, y, P.ramp('cloth_mid', max(0, tone)))

        # cloth shading is VERTICAL falloff only. Horizontal dither bands turned the shirt into
        # scan lines, which read as a lit monitor rather than fabric.

        # deltoids: two half-ellipses that overlap the chest edge, so the arm reads as attached
        for side in (-1, 1):
            dxc = cx + side * (build_w - deltoid_r // 2)
            for (ex, ey) in ellipse(P.px and 0 or 0, 0, 0, 0, 0) if False else ellipse(None, dxc, top + 12, deltoid_r, deltoid_r + 2):
                lit = (ex - (dxc - deltoid_r)) / float(max(1, 2 * deltoid_r))
                P.put(ex, ey, P.ramp('cloth_mid', max(0, 2 - int(lit * 2.0))))

        # rim light down the window side of the torso: warm, broken, never a solid outline
        for y in range(top + 6, torso_bot - 2):
            t = (y - top) / float(max(1, torso_bot - top))
            half = int(build_w * ((0.55 + t / 0.18 * 0.45) if t < 0.18 else (1.0 - (t - 0.18) * 0.18)))
            if y % 4:
                P.put(cx + half, y, P.ramp('rim_warm', 1))
            if y % 7 == 0:
                P.put(cx + half - 1, y, P.ramp('cloth_light', 3))

        # one soft crease under the near armpit, human and asymmetric
        for i in range(16):
            P.put(cx - build_w + 8 + i // 3, top + 26 + i, P.ramp('cloth_shadow', 1))

        # ---------------- collar: four shapes, so no two applicants wear the same shirt
        if self.collar == 0:                                  # crew neck
            rect(cx - 15, top, cx + 15, top + 3, 'line')
            rect(cx - 13, top + 3, cx + 13, top + 8, self.skin_dark)
            rect(cx - 13, top + 3, cx + 13, top + 6, self.skin_mid)
            rect(cx - 15, top, cx - 6, top + 2, 'linehi')
        elif self.collar == 1:                                # v-neck
            rect(cx - 15, top, cx + 15, top + 2, 'line')
            for i in range(9):                                # the V: two descending 1 px edges
                rect(cx - 12 + i, top + 2 + i, cx - 12 + i, top + 2 + i, self.skin_mid)
                rect(cx + 12 - i, top + 2 + i, cx + 12 - i, top + 2 + i, self.skin_mid)
                rect(cx - 13 + i, top + 2 + i, cx - 13 + i, top + 2 + i, 'linehi')
                rect(cx + 13 - i, top + 2 + i, cx + 13 - i, top + 2 + i, 'line')
            rect(cx - 4, top + 2, cx + 4, top + 11, self.skin_dark)
        elif self.collar == 2:                                # turtleneck
            rect(cx - 13, top - 4, cx + 13, top + 9, self.outfit)
            rect(cx - 13, top - 4, cx + 13, top - 3, 'linehi')
            rect(cx - 13, top + 8, cx + 13, top + 9, 'line')
            for x in range(cx - 12, cx + 13, 3):              # ribbing
                rect(x, top - 2, x, top + 7, 'line')
            rect(cx - 11, top - 2, cx + 11, top + 2, self.skin_dark)
        else:                                                 # collared shirt + tie
            rect(cx - 16, top, cx + 16, top + 2, 'line')
            rect(cx - 12, top + 2, cx - 2, top + 10, 'linehi')        # left collar leaf
            rect(cx + 2, top + 2, cx + 12, top + 10, 'line')          # right leaf, in shade
            rect(cx - 11, top + 3, cx - 3, top + 8, self.skin_dark)
            rect(cx + 3, top + 3, cx + 11, top + 8, self.skin_mid)
            rect(cx - 2, top + 6, cx + 2, top + 9, self.accent)       # knot
            for i in range(14):                                       # tie taper
                wdt = max(1, 4 - i // 5)
                rect(cx - wdt, top + 9 + i, cx + wdt, top + 9 + i, self.accent)
            rect(cx - 2, top + 9, cx - 2, top + 22, 'linehi')

        # ---------------- fabric folds: 1..3 soft creases, never crossing the badge
        for f in range(self.folds):
            fx = cx - 30 + f * 17 + (self.seed % 3)
            fy = top + 34 + f * 9
            length = 12 + (self.seed + f * 7) % 10
            for k in range(length):
                if fy + k < base:
                    rect(fx, fy + k, fx, fy + k, 'line' if (k // 3) % 2 else 'panel')
            rect(fx, fy + length // 2, fx + 1, fy + length // 2, 'linehi')
        # lanyard + badge
        bx = cx + self.badge_dx
        if 'straight_badge' not in A:
            # humans hang imperfectly: a proper V lanyard off both shoulders, converging on the
            # card, with a 1 px drift so the two straps are never exactly symmetric.
            for i in range(30):
                lx = cx - 12 + ((bx - 4) - (cx - 12)) * i // 30
                rx = cx + 12 + ((bx + 4) - (cx + 12)) * i // 30
                rect(lx, top + 4 + i, lx + 1, top + 4 + i, self.accent)
                rect(rx, top + 5 + i, rx + 1, top + 5 + i, self.accent)
                if i % 7 == 3:
                    put(rx + 1, top + 5 + i, 'line')
            rect(bx - 10, top + 41, bx + 2, top + 57, 'paper')     # left half of the card
            rect(bx + 1, top + 43, bx + 9, top + 59, 'paper')      # right half drops 2 px = tilt
            rect(bx - 10, top + 41, bx + 8, top + 42, 'paperdim')
            rect(bx - 7, top + 45, bx + 1, top + 48, 'line')
            rect(bx - 7, top + 51, bx + 2, top + 52, 'paperdim')
            rect(bx - 9, top + 60, bx + 10, top + 60, 'line')      # card shadow on the shirt
        else:
            rect(bx - 2, top + 14, bx + 2, top + 40, self.accent)  # dead vertical
        rect(bx - 9, top + 40, bx + 9, top + 56, 'paper')
        rect(bx - 9, top + 40, bx + 9, top + 41, 'paperdim')
        rect(bx - 6, top + 44, bx + 6, top + 47, 'line')
        rect(bx - 6, top + 50, bx + 6, top + 51, 'paperdim')

        # ---------------- arms + hands
        # sleeves overlap the torso edge by 6 px and end on the desk; fists rest in front
        shoulder = min(58, sh + 14)
        arm_x = shoulder - 6 + 8            # sleeve centre-line
        for side in (-1, 1):
            ax = cx + side * arm_x
            edge = 'linehi' if side < 0 else 'line'
            # The sleeve GROWS out of the shoulder over 6 rows. Drawing it as a fixed block from
            # top+18 left a gap above, so only its 1 px edge showed and the arm read as a wire.
            for y in range(top + 4, base + 3):
                half = min(7, 2 + (y - (top + 4)))
                rect(ax - half, y, ax + half, y, self.outfit)
                put(ax + side * half, y, edge)
            # seam where the sleeve meets the torso, so the two masses read as separate
            sy0 = top + 16
            rect(ax - side * 7, sy0, ax - side * 7, base + 2, 'line')
            put(ax - side * 7, sy0 - 1, 'linehi')
            rect(ax - 7, top + 9, ax + 7, top + 10, 'linehi')        # shoulder catch-light
        fidget = self.fidget
        for side in (-1, 1):
            hx = cx + side * arm_x
            hy = base - 15 + (fidget if side > 0 else -fidget)
            rect(hx - 8, hy - 5, hx + 8, hy + 1, self.outfit)        # shirt cuff
            rect(hx - 8, hy - 5, hx + 8, hy - 4, 'linehi')
            rect(hx - 8, hy, hx + 8, hy + 1, 'line')                 # cuff shadow on the wrist
            rect(hx - 7, hy + 1, hx + 7, hy + 11, self.skin_mid)     # back of hand
            rect(hx - 9, hy, hx + 9, hy + 2, self.skin_light)        # lit top edge
            rect(hx - 9, hy + 2, hx + 9, hy + 2, self.skin_dark)     # knuckle shadow
            if 'hands' in A:
                # ARTIFACT: one solid mitt -- fingers never separate, no knuckles at all
                rect(hx - 7, hy + 3, hx + 7, hy + 11, self.skin_light)
                rect(hx - 9, hy + 6, hx + 9, hy + 6, self.skin_light)
            else:
                # four fingers + a thumb, each separated by a 1 px shadow gap
                # restored 2026-11-26: the shorter variant read as a grille on the wrist, and this
                # block is what shipped and reads as fists at 1x. Do not shorten the fingers.
                for f in range(4):
                    fx = hx - 7 + f * 4
                    rect(fx, hy + 4, fx + 2, hy + 11, self.skin_light)
                    rect(fx + 3, hy + 4, fx + 3, hy + 11, self.skin_dark)
                rect(hx - 9, hy + 3, hx - 8, hy + 8, self.skin_mid)
                rect(hx - 9, hy + 3, hx - 8, hy + 8, self.skin_mid)  # thumb tucked at the side

        # ---------------- neck (v5: a tapered cylinder with a contact shadow, not a block)
        for (nx, ny) in taper(cx, 84 - breath, 100 - breath, self.neck_w, self.neck_w + 2):
            lit = (nx - (cx - self.neck_w)) / float(max(1, 2 * self.neck_w))
            P.put(nx, ny, P.ramp(self.skin_ramp, 2 - int(lit * 2.0)))
        for (nx, ny) in taper(cx, 84 - breath, 88 - breath, self.neck_w, self.neck_w + 1):
            P.put(nx, ny, P.ramp(self.skin_ramp, 0))          # jaw casts down onto the neck
        for nx in range(cx - self.neck_w, cx + self.neck_w + 1):
            if (nx + 84) & 1:
                P.put(nx, 84 - breath, P.ramp(self.skin_ramp, 1))
        if 'skin_band' in A:
            # ARTIFACT: a hard 4-row posterised step instead of a soft gradient
            rect(cx - 10, 93 - breath, cx + 10, 96 - breath, self.skin_light)

        # ---------------- head (v5 painterly)
        # A head is an egg, not a box: women and men share the same construction, the seed chooses the
        # shape. The old 42x72 rectangle with a 14 px "lit band" was the single biggest reason these read
        # as robots -- a flat rectangle has no cranial curve, so the eye never accepts it as a face.
        hw = 21
        head_top = 16 - breath
        head_bot = 88 - breath
        hcy = (head_top + head_bot) // 2
        hry = (head_bot - head_top) // 2
        shape = self.head_shape

        def head_half(y):
            """Half-width at row y, for the chosen skull shape."""
            t = (y - head_top) / float(max(1, head_bot - head_top))
            if shape == 0:                                    # oval
                return int(hw * (0.72 + 0.28 * (1 - (2 * t - 1) ** 2) ** 0.5))
            if shape == 1:                                    # square jaw
                return hw if t < 0.72 else int(hw * (1 - (t - 0.72) * 1.4))
            if shape == 2:                                    # round
                return int(hw * (0.80 + 0.20 * (1 - (2 * t - 1) ** 2) ** 0.5))
            return int(hw * (0.66 + 0.34 * (1 - (2 * t - 1) ** 2) ** 0.5))   # long

        for y in range(head_top, head_bot + 1):
            hwid = max(2, head_half(y))
            t = (y - head_top) / float(max(1, head_bot - head_top))
            for x in range(cx - hwid, cx + hwid + 1):
                # light from the monitor, screen-left, slightly above
                lit = (x - (cx - hwid)) / float(max(1, 2 * hwid))       # 0 at left edge
                tone = 2 - int(lit * 1.9) - (1 if t > 0.80 else 0)      # jaw falls off
                if tone < 0:
                    tone = 0
                P.put(x, y, P.ramp(self.skin_ramp, tone))

        # dithered transition down the jaw, so the chin does not end on a hard band
        for y in range(int(head_top + hry * 0.55), head_bot):
            hwid = max(2, head_half(y))
            P.dither(cx + hwid - 3, y, cx + hwid, y, P.ramp(self.skin_ramp, 1), P.ramp(self.skin_ramp, 2), seed=y)

        # rim light: the window is behind-right, so the right silhouette catches it
        for y in range(head_top + 2, head_bot - 4):
            hwid = max(2, head_half(y))
            if y % 3 == 0:
                P.put(cx + hwid, y, P.ramp('rim_warm', 1))
            if y % 4 == 0:
                P.put(cx + hwid + 1, y, P.ramp(self.skin_ramp, 2))

        # crown highlight: one short dithered arc, never a solid cap
        P.dither(cx - hw // 2, head_top + 1, cx + hw // 3, head_top + 3,
                 P.ramp(self.skin_ramp, 2), P.ramp(self.skin_ramp, 3), seed=7)

        # ears, sitting on the skull curve rather than bolted to a rectangle
        for side in (-1, 1):
            ex = cx + side * (head_half(int(head_top + hry * 0.9)) )
            for i, (dy, r) in enumerate(((0, 2), (1, 2), (2, 2), (3, 1), (4, 1))):
                for k in range(-r, r + 1):
                    P.put(ex + side * (1 + k // 2), int(head_top + hry * 0.9) + dy, P.ramp(self.skin_ramp, 2 if side < 0 else 1))

        if 'ear' in A:
            # ARTIFACT: one perfectly rectangular slab, uniform colour, spanning hairline to
            # jaw -- no fold, no shadow, no taper, and it grows straight out of the hair.
            hwid = head_half(int(head_top + hry * 0.9))
            P.rect(cx - hwid - 4, head_top + 12, cx - hwid, head_bot - 12, P.ramp('skin_mid', 2))
            P.rect(cx - hwid - 4, head_top + 12, cx - hwid - 4, head_bot - 12, P.ramp('skin_light', 3))
            P.rect(cx - hwid - 4, head_top + 12, cx - hwid, head_top + 12, P.ramp('skin_light', 3))

        # ---------------- facial hair
        if self.facial_hair in (1, 3):
            rect(cx - 7, head_top + 46, cx + 7, head_top + 49, self.hair)      # moustache
        if self.facial_hair in (2, 3):
            rect(cx - hw + 2, head_top + 50, cx + hw - 2, head_bot, self.hair)  # beard
            rect(cx - 8, head_top + 50, cx + 8, head_top + 56, self.skin_mid)   # mouth gap
        # ---------------- face
        eye_y = head_top + 30
        eye_dx = 10
        # ---- v5 painted face: socket shadow, iris, one catchlight, tapered brow, modelled nose and mouth.
        # Flat white rectangles with black squares read as a puppet; the shadow *around* the eye is what
        # makes a face read at 32 px.
        if self.blink:
            for side in (-1, 1):
                ex = cx + side * eye_dx
                P.put(ex - 3, eye_y + 3, P.ramp(self.skin_ramp, 0))
                P.put(ex - 2, eye_y + 3, P.ramp(self.skin_ramp, 0))
                P.put(ex + 2, eye_y + 3, P.ramp(self.skin_ramp, 0))
                for k in range(-3, 4):
                    P.put(ex + k, eye_y + 3, P.ramp(self.skin_ramp, 0) if k % 2 else P.ramp(self.skin_ramp, 1))
        else:
            for side in (-1, 1):
                ex = cx + side * eye_dx
                # socket: a dithered shadow that follows the brow curve
                P.dither(ex - 5, eye_y - 1, ex + 5, eye_y - 1,
                         P.ramp(self.skin_ramp, 1), P.ramp(self.skin_ramp, 2), seed=ex)
                P.rect(ex - 4, eye_y, ex + 3, eye_y + 3, self.skin_light)      # sclera
                if 'mirror' in A:
                    # ARTIFACT: both pupils identical in size AND height, no catchlight anywhere.
                    P.rect(ex - 2, eye_y + 1, ex + 2, eye_y + 3, 'ink')
                else:
                    P.rect(ex - 2, eye_y + 1, ex + 1, eye_y + 3, 'ink')
                    P.put(ex - 1, eye_y + 1, 'white')       # one catchlight, off-centre
                # upper lid: a 1 px darker line, heavier on the outer corner
                for k in range(-4, 5):
                    if k < 3:
                        P.put(ex + k, eye_y, P.ramp(self.skin_ramp, 0))
        # brows: tapered strokes, angled differently per side (a real face is not symmetric)
        for side in (-1, 1):
            bx = cx + side * eye_dx
            lift = 1 if side > 0 else 0
            for k in range(-4, 5):
                h = 2 if abs(k) < 3 else 1
                for d in range(h):
                    P.put(bx + k, eye_y - 4 - lift - d + self.brow // 2, self.hair if d == 0 else P.ramp('hair_mid', 1))
        # nose: one shadowed side plus a dithered tip; the lit side is left as skin
        for i in range(9):
            P.put(cx + 1, eye_y + 6 + i, P.ramp(self.skin_ramp, 1))
            if i > 4:
                P.put(cx + 2, eye_y + 6 + i, P.ramp(self.skin_ramp, 0))
        P.dither(cx - 2, eye_y + 14, cx + 3, eye_y + 15,
                 P.ramp(self.skin_ramp, 1), P.ramp(self.skin_ramp, 2), seed=cx)
        # mouth: dark line, lit lower lip, and a soft shadow beneath
        mouth_y = eye_y + 22
        for k in range(-6, 7):
            if abs(k) < 5:
                P.put(cx + k, mouth_y, P.ramp(self.skin_ramp, 0))
        for k in range(-4, 5):
            P.put(cx + k, mouth_y + 1, P.ramp(self.skin_ramp, 2))
        P.dither(cx - 4, mouth_y + 3, cx + 4, mouth_y + 3,
                 P.ramp(self.skin_ramp, 1), P.ramp(self.skin_ramp, 2), seed=mouth_y)
        if 'mirror' in A:
            put(cx - 15, eye_y + 13, 'ink'); put(cx + 15, eye_y + 13, 'ink')
        elif self.seed % 2 == 0:
            put(cx - 15, eye_y + 13, 'ink')
        if self.age_lines:
            P.dither(cx - eye_dx - 4, eye_y + 7, cx - eye_dx + 3, eye_y + 7,
                     P.ramp(self.skin_ramp, 1), P.ramp(self.skin_ramp, 2), seed=3)
            P.dither(cx + eye_dx - 4, eye_y + 8, cx + eye_dx + 3, eye_y + 8,
                     P.ramp(self.skin_ramp, 1), P.ramp(self.skin_ramp, 2), seed=5)

        # ---------------- glasses
        if self.glasses and self.glasses_shape == 1:
            # round lenses: a filled disc with a dark rim, and a glint that sells the glass
            for (ex, sign) in ((cx - eye_dx, -1), (cx + eye_dx, 1)):
                # pixel-built round rim: 1 px ring drawn as a row-span table, no antialiasing
                RING = ((2, 6), (1, 7), (0, 8), (0, 8), (0, 8), (0, 8), (0, 8), (0, 8),
                        (0, 8), (1, 7), (2, 6))
                for ry, (rx0, rx1) in enumerate(RING):
                    put(ex - 6 + rx0, eye_y - 3 + ry, 'linehi')
                    put(ex - 6 + rx1, eye_y - 3 + ry, 'linehi')
                rect(ex - 6, eye_y - 3, ex + 6, eye_y - 3, 'linehi')
                rect(ex - 6, eye_y + 7, ex + 6, eye_y + 7, 'linehi')
                put(ex - 4 * sign, eye_y, 'white')
                put(ex + 3, eye_y + 1, 'textdim')
            rect(cx - 3, eye_y + 2, cx + 3, eye_y + 2, 'linehi')
        elif self.glasses:
            rect(cx - eye_dx - 6, eye_y - 3, cx - eye_dx + 6, eye_y + 8, 'linehi')
            rect(cx + eye_dx - 6, eye_y - 3, cx + eye_dx + 6, eye_y + 8, 'linehi')
            rect(cx - 4, eye_y + 1, cx + 4, eye_y + 2, 'linehi')
            if 'glasses_merge' in A:
                # ARTIFACT: the arm is a bright bar fused to the head -- no gap, no contact
                # shadow, and it overruns the silhouette by 2 px on both sides.
                rect(cx - hw - 2, eye_y + 3, cx - eye_dx - 6, eye_y + 4, 'white')
                rect(cx + eye_dx + 6, eye_y + 3, cx + hw + 2, eye_y + 4, 'white')
                rect(cx - eye_dx - 4, eye_y + 2, cx - eye_dx + 2, eye_y + 4, 'white')
                rect(cx + eye_dx - 2, eye_y + 2, cx + eye_dx + 4, eye_y + 4, 'white')
            else:
                # human: arm sits lower (toward the ear) with a dark contact shadow under it
                rect(cx - hw, eye_y + 5, cx - eye_dx - 6, eye_y + 6, 'linehi')
                rect(cx - hw, eye_y + 7, cx - eye_dx - 6, eye_y + 7, self.skin_dark)
                rect(cx + eye_dx + 6, eye_y + 5, cx + hw, eye_y + 6, 'linehi')
                rect(cx + eye_dx + 6, eye_y + 7, cx + hw, eye_y + 7, self.skin_dark)
                rect(cx - eye_dx - 4, eye_y + 2, cx - eye_dx + 1, eye_y + 3, 'white')
                rect(cx + eye_dx + 1, eye_y + 2, cx + eye_dx + 4, eye_y + 3, 'textdim')

        # ---------------- accessory (watch / earrings / pen / ring)
        if self.accessory == 1:
            wx = cx - arm_x                              # left wrist, resting on the desk
            rect(wx - 8, base - 1, wx + 8, base, 'linehi')
            rect(wx - 9, base - 2, wx - 8, base - 1, 'textdim')
        elif self.accessory == 2:
            rect(cx + hw + 1, head_bot - 12, cx + hw + 2, head_bot - 10, 'warn')
            rect(cx - hw - 2, head_bot - 12, cx - hw - 1, head_bot - 10, 'warn')
        elif self.accessory == 3:
            px0 = cx + 16                                 # pen clipped in the breast
            rect(px0, top + 22, px0 + 1, top + 31, self.accent)
            rect(px0 - 1, top + 20, px0 + 2, top + 21, 'linehi')
        elif self.accessory == 4:
            rect(cx + arm_x - 3, base - 8, cx + arm_x - 1, base - 7, 'warn')
        if self.mole:
            put(cx + 12, eye_y + 11, self.skin_dark)

        # ---------------- table edge
        rect(0, base, w - 1, h - 1, 'panel2')
        rect(0, base, w - 1, base + 3, 'linehi')
        rect(0, base + 3, w - 1, base + 4, 'line')
        for x in range(0, w, 16):
            rect(x, base + 5, x, h - 1, 'panel')

        # ---------------- cast shadow on the desk (key light = the monitor, screen-left)
        if 'shadow' in A:
            # ARTIFACT: one hard slab thrown TOWARD the monitor light, ending on a
            # perfectly straight edge -- no falloff, no contact darkening.
            rect(cx - 58, base + 5, cx - 10, base + 11, 'line')
            rect(cx - 54, base + 6, cx - 10, base + 10, 'void')
        else:
            # human: stepped shadow thrown away from the monitor, fading as it goes
            for i in range(4):
                rect(cx + 14 + i * 4, base + 5 + i * 3, cx + 62, base + 7 + i * 3, 'line')
            rect(cx + 18, base + 17, cx + 58, base + 17, 'panel2')

        # ---------------- personal item on the desk (he brought something with him)
        dx = cx - 66
        if self.desk_prop == 0:                       # mug
            rect(dx, base + 6, dx + 9, base + 16, 'panel2')
            rect(dx, base + 6, dx + 9, base + 7, 'linehi')
            rect(dx + 10, base + 9, dx + 12, base + 13, 'panel2')
            rect(dx + 2, base + 9, dx + 7, base + 9, self.accent)
        elif self.desk_prop == 1:                     # pen, lying flat with a clip
            rect(dx, base + 13, dx + 13, base + 14, self.accent)
            rect(dx, base + 13, dx + 13, base + 13, 'linehi')
            rect(dx + 2, base + 12, dx + 6, base + 12, 'line')
        elif self.desk_prop == 2:                     # folded note
            rect(dx, base + 10, dx + 15, base + 16, 'paper')
            rect(dx, base + 10, dx + 15, base + 10, 'paperdim')
            for i in range(3):
                rect(dx + 2, base + 12 + i * 2, dx + 12, base + 12 + i * 2, 'paperdim')
        else:                                         # phone, face down, screen edge lit
            rect(dx, base + 7, dx + 7, base + 16, 'void')
            rect(dx, base + 7, dx + 7, base + 7, 'linehi')
            put(dx + 3, base + 15, self.accent)
        return img


# name -> (seed, synthetic, artifacts)
ARCHETYPES = {
    # --- humans: identity variety, no artifacts
    'human_solid_a':     (3,   False, ()),
    'human_solid_b':     (17,  False, ()),
    'human_solid_c':     (58,  False, ()),      # older, glasses, age lines
    'human_nervous_a':   (29,  False, ()),
    'human_nervous_b':   (71,  False, ()),      # headscarf
    'human_bait_a':      (41,  False, ()),
    'human_bait_b':      (86,  False, ()),      # bearded, beanie
    # --- synthetics: each carries a specific artifact set
    'synthetic_polished_a':  (101, True, ('seam', 'mirror')),
    'synthetic_polished_b':  (131, True, ('hands', 'skin_band')),
    'synthetic_polished_c':  (163, True, ('glasses_merge', 'mirror')),
    'synthetic_cloned_a':    (113, True, ('ear', 'shadow')),
    'synthetic_cloned_b':    (149, True, ('hands', 'seam')),
    'synthetic_collusion_a': (127, True, ('straight_badge', 'mirror')),
    'synthetic_collusion_b': (173, True, ('shadow', 'no_blink')),
}


def build_frame(state: Applicant, frame: int) -> Image.Image:
    a = Applicant(state.seed, state.synthetic, state.artifacts)
    a.breathe = 1 if frame in (1, 2) else 0
    # ARTIFACT no_blink: the loop never blinks. Humans always do on frame 3.
    a.blink = (frame == 3) and ('no_blink' not in a.artifacts)
    a.fidget = 1 if frame == 2 else 0
    return a.draw()


def make_sheet(state: Applicant) -> Image.Image:
    sheet = Image.new('RGBA', (192 * 4, 216), (0, 0, 0, 0))
    for i in range(4):
        frame = build_frame(state, i)
        # RAGGED (2026-11-26): the same wear rule as the rest of the game -- chipped silhouette at the
        # corners, sparse scuffs on clothing. Faces and tells are never touched.
        # 2026-11-26: halved. At 2 chips/corner + 10 scuffs the silhouette of a PERSON started to
        # read as damaged sprite data rather than clothing.
        chip_corners(frame, 900 + i, per_corner=1)
        scuff(frame, 910 + i, n=5, tones=('panel', 'void'), box=(40, 120, 152, 210))
        sheet.paste(frame, (192 * i, 0))
    return sheet


def make_portrait(state: Applicant) -> Image.Image:
    """96x96 document photo (head + shoulders)."""
    return state.draw().crop((48, 30, 144, 126))


def check() -> int:
    """Palette + alpha rules, read with the stdlib decoder -- runs without Pillow."""
    root = os.path.normpath(OUT)
    problems, count = [], 0
    for sub in ('applicants', 'portraits'):
        d = os.path.join(root, sub)
        if not os.path.isdir(d):
            continue
        for f in sorted(os.listdir(d)):
            if not f.endswith('.png'):
                continue
            count += 1
            path = os.path.join(d, f)
            try:
                _w, _h, pixels = read_rgba(path)
            except Exception as exc:                 # noqa: BLE001
                problems.append((f, f'unreadable ({exc})'))
                continue
            for px in pixels:
                if px[3] == 0:
                    continue
                if px[:3] not in PALETTE_SET or 0 < px[3] < 255:
                    problems.append((f, px))
                    break
    print(f"checked {count} applicant sprites")
    if problems:
        for f, px in problems[:8]:
            print("   ", f, px)
        print("FAIL: palette violations above")
        return 1
    print("PASS: palette-conformant, no partial alpha")
    return 0


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('--check', action='store_true')
    args = ap.parse_args()
    if args.check:
        return check()
    if not HAVE_PIL:
        print("Pillow is required to REGENERATE sprites, but not to check them.")
        print("  pip install pillow")
        print("Or verify what is committed:  python tools/art/make_applicants.py --check")
        return 2
    root = os.path.normpath(OUT)
    made = []
    for name, (seed, syn, arts) in ARCHETYPES.items():
        st = Applicant(seed, syn, arts)
        p = os.path.join(root, 'applicants', f'app_{name}_192x216.png')
        os.makedirs(os.path.dirname(p), exist_ok=True)
        make_sheet(st).save(p)
        made.append(p)
        pseed = seed + 500 if name == 'synthetic_cloned_a' else seed
        p2 = os.path.join(root, 'portraits', f'portrait_{name}_96x96.png')
        os.makedirs(os.path.dirname(p2), exist_ok=True)
        make_portrait(Applicant(pseed, syn, arts)).save(p2)
        made.append(p2)
    print(f"wrote {len(made)} files for {len(ARCHETYPES)} archetypes")
    return 0


if __name__ == '__main__':
    sys.exit(main())
