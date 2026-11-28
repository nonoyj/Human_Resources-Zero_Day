#!/usr/bin/env python3
"""HR//0D -- painterly rendering vocabulary for the applicant busts.

WHY THIS MODULE EXISTS (2026-11-26)
    The applicant sheets were flat: one fill per plane, a 1 px lit edge, a 1 px shadow. That reads as a
    vector mascot, not as a person, which is exactly what the player said: "they all look like robots".

    The painterly reference the player supplied does four things flat art cannot:
      1. SHADING RAMPS -- every form is 3-4 tones of the same hue, not one fill plus an outline.
      2. DITHER AT THE RAMPS -- a 50% checker between two ramp steps, so a transition reads as a
         gradient at 1x instead of a hard band. This is the single biggest "hand-painted" cue at 480x270.
      3. RIM LIGHT -- the window/CRT throws a warm-cool edge along one side of every form. It is what
         separates a figure from the room without an outline.
      4. FORM, NOT SILHOUETTE -- a head is an egg (wider at the crown, tapered at the jaw), a neck is a
         cylinder, a shoulder is a ball. Blocks with corners read as puppets.

    Palette rule (art bible #4, unchanged): every pixel written here is an existing palette entry or a
    50% dither of two existing entries. No new colours are introduced, so the palette gate still passes.
"""
from __future__ import annotations


# --------------------------------------------------------------------------- ramps
# Ordered dark -> light. Each is 3-4 existing palette entries of one hue family.
# v130: the ramp names on the LEFT are historical. Rendering moved to the single material palette
# (`menu_palette`, imported by `art_palette`) in v120, and nothing should read these any more -- the applicant
# generator used `P.ramp('skin_mid', 2)` for years and every call silently fell back to step 0, because the
# Ramps table it consulted was this one while the palette it wrote into was the other one. The table is kept
# so no old code path crashes; `art_palette.ramp_keys()` is the only legitimate source of ramp names.
RAMPS = {
    # ------------------------------------------------------------------ SKIN (v9, rewritten)
    # THE BUG THIS FIXES: the old ramps mixed a warm tone with 'panel2'/'line' (navy UI colours) as their
    # shadow steps, so every face was lit on one side and NAVY on the other -- at 5x it reads as a mask,
    # and in the game it read as "robotic", which is what the player kept telling us.
    #
    # Skin is one four-step warm ramp -- ink (a brown-black, not a neutral black) -> paperdim -> orange ->
    # warn -- and the three "families" are just where a face sits on it. Nothing cool is ever written into
    # a face, so the shadow side of a head is brown, as it is in life.
    'skin_light':   ('ink', 'paperdim', 'paperdim', 'orange'),
    'skin_mid':     ('ink', 'paperdim', 'paperdim', 'orange'),
    'skin_deep':    ('void', 'ink', 'ink', 'paperdim'),
    'skin_shadow':  ('void', 'ink', 'ink', 'paperdim'),
    # ------------------------------------------------------------------ HAIR (v9, natural only)
    # 'warn' as a hair colour is a UI accent (255,209,102): at hair-area size it read as bleached plastic,
    # which is the "hair too bright" note. Natural hair is a dark warm ramp, with one grey family for older
    # applicants -- greys use the cool ramp, because grey hair really is cool.
    'hair_black':   ('void', 'void', 'ink', 'paperdim'),
    'hair_brown':   ('void', 'ink', 'ink', 'paperdim'),
    'hair_grey':    ('void', 'panel2', 'line', 'muted'),   # grey hair is not white hair
    # ------------------------------------------------------------------ cloth + light
    'cloth_shadow': ('void', 'panel', 'panel2', 'line'),
    'cloth_mid':    ('panel', 'panel2', 'line', 'linehi'),
    'cloth_light':  ('panel2', 'line', 'linehi', 'muted'),
    'hair_shadow':  ('void', 'ink', 'paperdim', 'paperdim'),   # kept for the old call sites
    'hair_mid':     ('void', 'void', 'ink', 'paperdim'),
    'metal':        ('line', 'linehi', 'muted', 'textdim'),
    'rim_cool':     ('muted', 'textdim', 'text', 'white'),
    'rim_warm':     ('orange', 'warn', 'text', 'white'),
}


class Painter:
    """Pixel writer with ramps, dither and rim light. One instance per frame."""

    def __init__(self, img, palette):
        self.img = img
        self.px = img.load()
        self.w = img.width
        self.h = img.height
        self.palette = palette
        self._tone = {}

    # -- low level ---------------------------------------------------------
    def put(self, x, y, key, alpha=255):
        if 0 <= x < self.w and 0 <= y < self.h:
            r, g, b = self.palette[key]
            self.px[x, y] = (r, g, b, alpha)

    def clear(self, x, y):
        if 0 <= x < self.w and 0 <= y < self.h:
            self.px[x, y] = (0, 0, 0, 0)

    def alpha_at(self, x, y):
        if 0 <= x < self.w and 0 <= y < self.h:
            return self.px[x, y][3]
        return 0

    def rect(self, x0, y0, x1, y1, key):
        for y in range(y0, y1 + 1):
            for x in range(x0, x1 + 1):
                self.put(x, y, key)

    # -- painterly ops -----------------------------------------------------
    def dither(self, x0, y0, x1, y1, key_a, key_b, seed=0, density=0.5):
        """50% checker of two ramp steps. The hand-painted gradient cue at 1:1."""
        for y in range(y0, y1 + 1):
            for x in range(x0, x1 + 1):
                if ((x + y + seed) & 1) == 0 if density >= 0.5 else (((x * 3 + y * 5 + seed) & 3) == 0):
                    self.put(x, y, key_a)
                else:
                    self.put(x, y, key_b)

    def ramp(self, key_ramp, step):
        """'skin' + step -> a palette key, clamped to the ramp.

        v130: resolution order is the MATERIAL palette first (`skin_0..3`, `hair_0..3`, ... via art_palette),
        then the legacy table above. The painter writes into the material palette, so the legacy table can
        only ever produce a wrong answer; it is the fallback, not the path.
        """
        try:
            import art_palette
            keys = art_palette.ramp_keys(key_ramp)
            if keys:
                return keys[max(0, min(len(keys) - 1, step))]
        except Exception:                               # noqa: BLE001  (no Pillow / no palette -> legacy)
            pass
        if key_ramp in RAMPS:
            pass
        r = RAMPS.get(key_ramp) or RAMPS['skin_light']
        i = max(0, min(len(r) - 1, step))
        if r[i] not in self.palette:
            return r[0]
        return r[i]

    def fill_shaded(self, x0, y0, x1, y1, ramp, lit='left', steps=(1, 2, 3)):
        """
        Fills a box with a 3-band ramp: dark away from the light, light toward it, and a dithered
        transition between each band. `lit` is the side the room light comes from.
        """
        if x1 < x0 or y1 < y0:
            return
        third = max(1, (y1 - y0 + 1) // len(steps))
        bands = []
        y = y0
        for s in steps:
            bands.append((y, min(y1, y + third - 1), s))
            y += third
        if bands:
            bands[-1] = (bands[-1][0], y1, bands[-1][2])

        for (by0, by1, s) in bands:
            for yy in range(by0, by1 + 1):
                # horizontal falloff: the lit side is one step brighter
                for xx in range(x0, x1 + 1):
                    near_light = (xx < (x0 + x1) / 2) if lit == 'left' else (xx > (x0 + x1) / 2)
                    step = s + (0 if near_light else -1)
                    self.put(xx, yy, self.ramp(ramp, step))
            # dithered seam to the next band
            if by1 + 1 <= y1 and s < steps[-1]:
                self.dither(x0, by1, x1, by1 + 1, self.ramp(ramp, s), self.ramp(ramp, s + 1),
                            seed=by0)

    def rim(self, points, key_a, key_b):
        """A 1-2 px light edge. Draws key_a where the pixel is solid, key_b just outside it."""
        for (x, y) in points:
            if self.alpha_at(x, y) > 0:
                self.put(x, y, key_a)
            elif self.alpha_at(x - 1, y) > 0 or self.alpha_at(x, y - 1) > 0:
                if (x + y) & 1:
                    self.put(x, y, key_b, alpha=200)

    def rim_column(self, x, y0, y1, key_a, key_b, side=-1):
        """Vertical rim along a silhouette edge, alternating the brighter tone in a short pattern so it
        reads as a highlight rather than a drawn line."""
        for y in range(y0, y1 + 1):
            xx = x
            while 0 <= xx < self.w and self.alpha_at(xx, y) == 0 and abs(xx - x) < 3:
                xx += side
            if self.alpha_at(xx, y) == 0:
                continue
            self.put(xx, y, key_a if (y % 3) else key_b)

    def tatter(self, x0, y0, x1, y1, seed=0, step=3):
        """Break a straight edge. Uses a cheap deterministic walk, no RNG object."""
        v = seed * 2654435761 & 0xFFFFFFFF
        for x in range(x0, x1 + 1):
            v = (v * 1103515245 + 12345) & 0x7FFFFFFF
            if (v >> 16) % step == 0:
                self.clear(x, y0 if (v >> 8) % 2 else y1)


def ellipse(px, cx, cy, rx, ry, top=None, bottom=None):
    """Yields the filled pixels of an ellipse (integer, no antialiasing)."""
    y0 = cy - ry if top is None else top
    y1 = cy + ry if bottom is None else bottom
    for y in range(y0, y1 + 1):
        dy = (y - cy) / float(ry) if ry else 0
        if abs(dy) > 1.0:
            continue
        half = int(round(rx * (1.0 - dy * dy) ** 0.5))
        for x in range(cx - half, cx + half + 1):
            yield (x, y)


def taper(cx, y0, y1, r0, r1):
    """A tapered column -- neck, upper arm. Yields (x, y) row by row."""
    span = max(1, y1 - y0)
    for i in range(span + 1):
        r = r0 + (r1 - r0) * (i / float(span))
        half = int(round(r))
        for x in range(cx - half, cx + half + 1):
            yield (x, y0 + i)
