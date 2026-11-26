#!/usr/bin/env python3
"""
HR//0D pixel-art kit generator.

Deterministic: every sprite is drawn from code, so the kit is reproducible, verifiable and
reviewable in git (a diff means someone changed an intent, not just a pixel).

Base canvas 480x270 (ADR-005), PPU 16, landscape. Output: art/kit/

    python tools/art/make_art_kit.py
    python tools/art/make_art_kit.py --check     # verify every PNG matches the palette, no alpha fringing
"""
from __future__ import annotations

import argparse
import os
import sys

# Pillow is needed to DRAW art, but never to CHECK it (BUG-016: a fresh Windows box has no
# Pillow, so the verification command must not import it). Reading is done by pngcheck.py.

try:
    from PIL import Image, ImageDraw
    HAVE_PIL = True
except ImportError:                                  # pragma: no cover
    Image = ImageDraw = None
    HAVE_PIL = False

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from pngcheck import read_rgba                   # noqa: E402

OUT = os.path.join(os.path.dirname(__file__), '..', '..', 'art', 'kit')

# --------------------------------------------------------------------------- palette (02-tech-stack.md)
PALETTE = {
    'void':      (0x0b, 0x0e, 0x13),
    'panel':     (0x13, 0x19, 0x26),
    'panel2':    (0x1d, 0x27, 0x35),
    'line':      (0x2b, 0x3a, 0x4d),
    'linehi':    (0x3f, 0x55, 0x70),
    'muted':     (0x5d, 0x7e, 0xa1),
    'textdim':   (0xa9, 0xc7, 0xe4),
    'text':      (0xe6, 0xf1, 0xff),
    'white':     (0xff, 0xff, 0xff),
    'signal':    (0x18, 0xe0, 0xb0),
    'signaldim': (0x0f, 0xa8, 0x8a),
    'warn':      (0xff, 0xd1, 0x66),
    'orange':    (0xff, 0x8a, 0x3d),
    'danger':    (0xff, 0x47, 0x57),
    'dangerdim': (0xa1, 0x1a, 0x2b),
    'synth':     (0x7b, 0x61, 0xff),
    'synthdim':  (0xb3, 0x9d, 0xff),
    'ok':        (0x6e, 0xe7, 0xc8),
    'paperdim':  (0x8a, 0x6d, 0x3b),
    'glow_teal': (0x0c, 0x3d, 0x38),   # approved glow step 1/3 (2026-11-26): mint light falling off
    'glow_amber': (0x5c, 0x4a, 0x20),  # approved glow step 2/3: warm lamp falloff
    'glow_red':  (0x4a, 0x15, 0x20),   # approved glow step 3/3: breach/corruption falloff
    'paper':     (0xf2, 0xe8, 0xd5),
    'ink':       (0x2a, 0x21, 0x18),
}
P = PALETTE
PALETTE_SET = {v for v in PALETTE.values()}


def rgba(name: str, a: int = 255):
    r, g, b = P[name]
    return (r, g, b, a)


def new(w: int, h: int) -> Image.Image:
    return Image.new('RGBA', (w, h), (0, 0, 0, 0))


def save(img: Image.Image, rel: str) -> str:
    path = os.path.normpath(os.path.join(OUT, rel))
    os.makedirs(os.path.dirname(path), exist_ok=True)
    img.save(path)
    return path


# --------------------------------------------------------------------------- 9-slice panels
def nine_slice(w, h, fill='panel', border='line', inner=None, corner=3, accent=None):
    """A panel: 1px outer border, optional 1px inner highlight, corner notches for a techy feel."""
    img = new(w, h)
    d = ImageDraw.Draw(img)
    d.rectangle([0, 0, w - 1, h - 1], fill=rgba(fill))
    if inner:
        d.rectangle([1, 1, w - 2, h - 2], outline=rgba(inner))
    d.rectangle([0, 0, w - 1, h - 1], outline=rgba(border))
    if accent:
        # corner brackets -- reads as "targeting reticle" at small sizes
        c = rgba(accent)
        for (x, y, dx, dy) in ((0, 0, 1, 1), (w - 1, 0, -1, 1), (0, h - 1, 1, -1), (w - 1, h - 1, -1, -1)):
            d.line([x, y, x + dx * corner, y], fill=c)
            d.line([x, y, x, y + dy * corner], fill=c)
    return img


def _wear():
    """ragged.py imports PALETTE from this module, so wear helpers must be imported lazily."""
    import os as _o, sys as _s
    _s.path.insert(0, _o.path.dirname(_o.path.abspath(__file__)))
    import ragged
    return ragged


def _chip(img, seed):
    """One-pixel corner knock-off: the most wear a 9-slice may carry without tiling badly."""
    nc = _wear().notch_corner
    nc(img, 0, 0, 1, 1, depth=1)
    nc(img, img.width - 1, 0, -1, 1, depth=1)
    nc(img, 0, img.height - 1, 1, -1, depth=1)
    nc(img, img.width - 1, img.height - 1, -1, -1, depth=1)
    return img


def kit_nine_slices():
    made = []
    made.append(save(nine_slice(24, 24, 'panel', 'line', corner=3), 'ui/panel_9slice.png'))
    made.append(save(nine_slice(24, 24, 'panel2', 'linehi', corner=3, accent='signal'), 'ui/panel_active_9slice.png'))
    made.append(save(nine_slice(24, 24, 'panel', 'dangerdim', corner=3, accent='danger'), 'ui/panel_danger_9slice.png'))
    made.append(save(nine_slice(16, 16, 'void', 'line', corner=2), 'ui/inset_9slice.png'))
    # tooltip: paper stock with ink border
    made.append(save(nine_slice(20, 16, 'paper', 'ink', corner=2), 'ui/tooltip_9slice.png'))

    # 9-slices: the border must stay intact for tiling, so only the four corner pixels may be
    # knocked off (bible 6b: a 9-slice is never warped).
    made.append(save(_chip(nine_slice(24, 24, 'panel', 'line', corner=3), 720), 'ui/panel_9slice.png'))
    made.append(save(_chip(nine_slice(24, 24, 'panel2', 'linehi', corner=3, accent='signal'), 721),
                     'ui/panel_active_9slice.png'))
    made.append(save(_chip(nine_slice(24, 24, 'panel', 'dangerdim', corner=3, accent='danger'), 722),
                     'ui/panel_danger_9slice.png'))

    for state, fill, border, label in (
        ('idle',    'panel2', 'line',    None),
        ('hover',   'panel2', 'muted',   None),
        ('pressed', 'panel',  'signal',  None),
        ('disabled','panel',  'line',    None),
        ('primary', 'signaldim', 'signal', None),
        ('danger',  'dangerdim', 'danger', None),
    ):
        w, h = 40, 20
        img = new(w, h)
        d = ImageDraw.Draw(img)
        d.rectangle([0, 0, w - 1, h - 1], fill=rgba(fill))
        d.rectangle([0, 0, w - 1, h - 1], outline=rgba(border))
        if state == 'primary':
            d.rectangle([2, 2, w - 3, h - 3], outline=rgba('signal'))
        if state == 'disabled':
            for x in range(2, w - 2, 3):
                d.point((x, h - 3), fill=rgba('line'), )
        made.append(save(img, f'ui/button_{state}_9slice.png'))
    return made


def kit_bars():
    made = []
    for name, col, frac in (('trust_high', 'signal', 0.72), ('trust_mid', 'warn', 0.45), ('trust_low', 'danger', 0.18)):
        w, h = 64, 6
        img = new(w, h)
        d = ImageDraw.Draw(img)
        d.rectangle([0, 0, w - 1, h - 1], fill=rgba('void'), outline=rgba('line'))
        d.rectangle([1, 1, int((w - 2) * frac), h - 2], fill=rgba(col))
        made.append(save(img, f'ui/bar_{name}.png'))
    # use pips for tool charges: filled / empty, 3 states each
    for state, col in (('full', 'signal'), ('used', 'line')):
        img = new(6, 6)
        d = ImageDraw.Draw(img)
        d.rectangle([0, 0, 5, 5], fill=rgba(col))
        d.rectangle([0, 0, 5, 5], outline=rgba('void'))
        made.append(save(img, f'ui/pip_{state}.png'))
    # slider track + knob
    img = new(64, 4); d = ImageDraw.Draw(img)
    d.rectangle([0, 1, 63, 2], fill=rgba('line'))
    d.rectangle([0, 1, 40, 2], fill=rgba('signal'))
    made.append(save(img, 'ui/slider_track.png'))
    img = new(7, 9); d = ImageDraw.Draw(img)
    d.rectangle([0, 0, 6, 8], fill=rgba('text'), outline=rgba('void'))
    made.append(save(img, 'ui/slider_knob.png'))
    return made


# --------------------------------------------------------------------------- icons
def _icon(pattern, color='textdim'):
    """pattern: list of strings, '#' = pixel."""
    h = len(pattern); w = max(len(r) for r in pattern)
    img = new(w, h); d = ImageDraw.Draw(img)
    for y, row in enumerate(pattern):
        for x, ch in enumerate(row):
            if ch == '#':
                d.point((x, y), fill=rgba(color))
    return img


ICONS = {
    'id_registry': [
        '###.###',
        '#.....#',
        '#.###.#',
        '#.....#',
        '#.###.#',
        '#.....#',
        '###.###',
    ],
    'metadata_scan': [
        '..###..',
        '.#...#.',
        '#..#..#',
        '#..#..#',
        '#..#..#',
        '.#...#.',
        '..###..',
    ],
    'reference_call': [
        '..###..',
        '.#...#.',
        '#..#..#',
        '#..#..#',
        '.#...#.',
        '..###..',
        '...#...',
    ],
    'inbox': [
        '#.....#',
        '##...##',
        '#.#.#.#',
        '#..#..#',
        '#.....#',
        '#######',
    ],
    'manual': [
        '#######',
        '#.....#',
        '#.###.#',
        '#.....#',
        '#.###.#',
        '#######',
    ],
    'pause': [
        '##..##',
        '##..##',
        '##..##',
        '##..##',
        '##..##',
    ],
    'gear': [
        '.#.#.#.',
        '#######',
        '.#...#.',
        '#.....#',
        '.#...#.',
        '#######',
        '.#.#.#.',
    ],
    'check': [
        '.....##',
        '....##.',
        '##.##..',
        '.###...',
        '..#....',
    ],
    'cross': [
        '#...#',
        '.#.#.',
        '..#..',
        '.#.#.',
        '#...#',
    ],
}


def kit_icons():
    made = []
    for i, (name, pat) in enumerate(ICONS.items()):
        img = _icon(pat)
        # icons are stencil-cut cards: wear the CORNERS only, never the glyph (1-2 px chips)
        nc = _wear().notch_corner
        nc(img, 0, 0, 1, 1, depth=1)
        nc(img, img.width - 1, img.height - 1, -1, -1, depth=1)
        made.append(save(img, f'icons/{name}.png'))
        img2 = _icon(pat, 'signal')
        made.append(save(img2, f'icons/{name}_active.png'))
    return made


# --------------------------------------------------------------------------- stamps
def kit_stamps():
    made = []
    # APPROVE: green, chunky, slightly worn edge
    w, h = 56, 20
    for name, col, dimcol in (('approve', 'signal', 'signaldim'), ('reject', 'danger', 'dangerdim')):
        img = new(w, h); d = ImageDraw.Draw(img)
        d.rectangle([0, 0, w - 1, h - 1], fill=rgba(dimcol))
        d.rectangle([2, 2, w - 3, h - 3], outline=rgba(col))
        d.rectangle([4, 4, w - 5, h - 5], outline=rgba(col))
        # lettering blocks (real text is TMP; these are the stamp body)
        cx = 8
        for i in range(6 if name == 'approve' else 5):
            d.rectangle([cx, 7, cx + 4, h - 8], fill=rgba(col))
            cx += 7
        _r = _wear()
        _r.chip_corners(img, 700 + (0 if name == 'approve' else 1), per_corner=2)
        _r.scuff(img, 710 + (0 if name == 'approve' else 1), n=3,
              tones=('void', 'panel'), box=(2, 2, w - 3, h - 3))
        made.append(save(img, f'stamps/stamp_{name}.png'))
    return made


# --------------------------------------------------------------------------- furniture / desk
def kit_desk():
    made = []
    # monitor bezel: the frame the UI sits inside (left half of the screen)
    w, h = 264, 200
    img = new(w, h); d = ImageDraw.Draw(img)
    d.rectangle([0, 0, w - 1, h - 1], fill=rgba('panel'))
    d.rectangle([0, 0, w - 1, h - 1], outline=rgba('line'))
    d.rectangle([2, 2, w - 3, h - 3], outline=rgba('linehi'))
    d.rectangle([6, 6, w - 7, h - 7], fill=rgba('void'), outline=rgba('line'))
    # camera dot + power LED
    d.rectangle([w // 2 - 1, 3, w // 2 + 1, 4], fill=rgba('linehi'))
    d.point((w - 8, 4), fill=rgba('signal'))
    made.append(save(img, 'desk/monitor_bezel.png'))

    # document sheet: paper with header rule and text lines
    w, h = 192, 108
    img = new(w, h); d = ImageDraw.Draw(img)
    d.rectangle([0, 0, w - 1, h - 1], fill=rgba('paper'))
    d.rectangle([0, 0, w - 1, h - 1], outline=rgba('ink'))
    d.line([10, 14, w - 11, 14], fill=rgba('ink'))
    y = 22
    for i in range(9):
        ln = (w - 40) if i % 4 else (w - 90)
        d.line([10, y, 10 + ln, y], fill=rgba('paperdim'))
        y += 8
    # paper clip
    d.rectangle([w - 30, 6, w - 22, 12], outline=rgba('muted'))
    made.append(save(img, 'desk/document_sheet.png'))

    # ID card
    w, h = 84, 52
    img = new(w, h); d = ImageDraw.Draw(img)
    d.rectangle([0, 0, w - 1, h - 1], fill=rgba('panel2'), outline=rgba('linehi'))
    d.rectangle([6, 8, 30, 36], fill=rgba('line'), outline=rgba('muted'))     # photo box
    for i in range(4):
        d.line([36, 12 + i * 7, 62, 12 + i * 7], fill=rgba('textdim'))
    d.rectangle([6, 40, w - 7, 44], fill=rgba('signal'))                      # stripe
    made.append(save(img, 'desk/id_card.png'))

    # table edge + the empty chair (the applicant sits here)
    w, h = 264, 54
    img = new(w, h); d = ImageDraw.Draw(img)
    d.rectangle([0, 18, w - 1, h - 1], fill=rgba('panel2'))
    d.line([0, 18, w - 1, 18], fill=rgba('linehi'))
    d.rectangle([24, 6, 90, 26], fill=rgba('void'), outline=rgba('line'))     # table in front
    d.rectangle([24, 6, 90, 10], fill=rgba('line'))
    made.append(save(img, 'desk/table_and_chair.png'))
    return made


# --------------------------------------------------------------------------- backgrounds (3 corruption levels)
def kit_backgrounds():
    """RETIRED 2026-11-26.

    This used to paint backgrounds/office_L0..L2.png. Composition L now owns those files
    (tools/art/promote_composition_L.py), and having two generators write the same path meant that
    re-running this script silently reverted the approved room. One path, one owner (BUG-024).
    """
    print('  backgrounds/ delegated to promote_composition_L.py (composition L owns them)')
    return []


def _old_kit_backgrounds():
    made = []
    W, H = 480, 270
    for level, accent in ((0, 'signaldim'), (1, 'warn'), (2, 'danger')):
        img = new(W, H); d = ImageDraw.Draw(img)
        # wall
        d.rectangle([0, 0, W - 1, H - 1], fill=rgba('void'))
        # far wall panels
        for x in range(0, W, 48):
            d.rectangle([x + 1, 40, x + 46, 150], fill=rgba('panel'), outline=rgba('panel2'))
        # window with city skyline
        d.rectangle([300, 52, 452, 150], fill=rgba('panel2'), outline=rgba('line'))
        skyline = [(306, 120), (318, 104), (330, 128), (344, 96), (358, 118),
                   (372, 88), (386, 124), (400, 108), (414, 132), (428, 100), (442, 122)]
        for x, top in skyline:
            col = 'line' if level < 2 else 'linehi'
            d.rectangle([x, top, x + 9, 148], fill=rgba(col))
            for wy in range(top + 4, 144, 8):
                if (x + wy) % 3 != 0:
                    d.point((x + 2, wy), fill=rgba('warn' if level else 'muted'))
        # server rack on the left of the far wall
        d.rectangle([16, 60, 84, 150], fill=rgba('panel2'), outline=rgba('line'))
        for i in range(6):
            y = 66 + i * 14
            d.rectangle([20, y, 80, y + 10], fill=rgba('panel'), outline=rgba('line'))
            for k in range(3):
                d.point((24 + k * 4, y + 5), fill=rgba(accent))
        # desk surface (bottom third) -- the UI panels sit on this
        d.rectangle([0, 150, W - 1, H - 1], fill=rgba('panel'))
        d.line([0, 150, W - 1, 150], fill=rgba('linehi'))
        for x in range(0, W, 24):
            d.line([x, 152, x, H - 1], fill=rgba('panel2'))
        # corruption overlays
        if level >= 1:
            for i in range(0, W, 37):
                d.line([i, 152, i + 18, H - 1], fill=rgba('panel2'))
        if level >= 2:
            for i in range(0, H, 9):
                d.line([0, i, W - 1, i], fill=(0xff, 0x47, 0x57, 26))   # scan/tint
            d.rectangle([0, 0, W - 1, 4], fill=rgba('danger'))
        made.append(save(img, f'backgrounds/office_L{level}.png'))
    return made


# --------------------------------------------------------------------------- palette / reference sheet
def kit_reference():
    W, H = 240, 44
    img = new(W, H)
    d = ImageDraw.Draw(img)
    d.rectangle([0, 0, W - 1, H - 1], fill=rgba('void'))
    x = 4
    for name, col in PALETTE.items():
        d.rectangle([x, 4, x + 18, 22], fill=rgba(name), outline=rgba('line'))
        x += 21
        if x > W - 24:
            break
    return [save(img, 'reference/palette_swatch.png')]


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('--check', action='store_true', help='verify existing PNGs instead of writing')
    args = ap.parse_args()

    if args.check:
        return check()

    if not HAVE_PIL:
        print("Pillow is required to REGENERATE art, but not to check it.")
        print("  pip install pillow")
        print("Or verify what is committed:  python tools/art/make_art_kit.py --check")
        return 2

    made = []
    made += kit_nine_slices()
    made += kit_bars()
    made += kit_icons()
    made += kit_stamps()
    made += kit_desk()
    made += kit_backgrounds()
    made += kit_reference()

    print(f"wrote {len(made)} files to {os.path.normpath(OUT)}")
    for m in made:
        rel = os.path.relpath(m, os.path.normpath(OUT))
        print(f"  {rel}")
    return 0


def check() -> int:
    """Every sprite must use only palette colours; no stray alpha.

    Reads PNGs with tools/art/pngcheck.py, so this runs on a machine with no Pillow (BUG-016).
    """
    problems = []
    count = 0
    root_dir = os.path.normpath(OUT)
    for root, _dirs, files in os.walk(root_dir):
        # reference/ holds documentation images (contact sheet, swatch) that use UI label colours
        if 'reference' in os.path.relpath(root, root_dir).split(os.sep):
            continue
        for f in sorted(files):
            if not f.endswith('.png'):
                continue
            count += 1
            path = os.path.join(root, f)
            bad = set()
            try:
                _w, _h, pixels = read_rgba(path)
            except Exception as exc:                 # noqa: BLE001
                problems.append(f"{os.path.relpath(path, root_dir)}: unreadable ({exc})")
                continue
            for px in pixels:
                if px[3] == 0:
                    continue
                # allow the translucent scanline tint on L2 backgrounds
                is_alarm_tint = px[:3] == (0xff, 0x47, 0x57) and px[3] < 60
                if px[:3] not in PALETTE_SET and not is_alarm_tint:
                    bad.add(px)
                if 0 < px[3] < 255 and px[:3] != (0xff, 0x47, 0x57):
                    bad.add(px)
            if bad:
                problems.append(f"{os.path.relpath(path, root_dir)}: {sorted(bad)[:4]}")
    print(f"checked {count} sprites")
    if problems:
        for line in problems[:8]:
            print("   ", line)
        print("FAIL: palette violations above")
        return 1
    print("PASS: every pixel is palette-conformant, no partial alpha")
    return 0


if __name__ == '__main__':
    sys.exit(main())
