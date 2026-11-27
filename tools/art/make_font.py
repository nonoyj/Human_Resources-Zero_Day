#!/usr/bin/env python3
"""
make_font.py -- the 5x7 glyph atlas the CRT screen renders with.

WHY A GENERATOR AND NOT A TTF
    TMP font assets are built by an editor GUI and cannot be produced headlessly, and a hinted TTF
    at 480x270 will never land on a 5x7 grid. So the monitor font is *art*: one atlas PNG in the
    project palette, plus a metrics json. Same pipeline, same palette gate, same determinism as
    every other pixel asset.

    Unity does NOT slice this atlas. `MonitorScreenRaster` composites glyph pixels into a glass-sized
    texture at runtime, so the screen is a single sprite = one draw call, and nothing needs a custom
    importer.

OUTPUT
    art/kit/fonts/monitor_font_5x7.png   atlas: 16 glyphs per row, 1 px padding
    art/kit/fonts/monitor_font_5x7.json  { cellW, cellH, pad, cols, chars, glyphs:[{ch,x,y}] }

    python tools/art/make_font.py
    python tools/art/make_font.py --check
"""
from __future__ import annotations

import argparse
import json
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from make_art_kit import PALETTE, rgba                              # noqa: E402
from pngcheck import read_rgba                                      # noqa: E402

try:
    from PIL import Image
    HAVE_PIL = True
except ImportError:                                                 # pragma: no cover
    Image = None
    HAVE_PIL = False

OUT = os.path.join(os.path.dirname(__file__), '..', '..', 'art', 'kit', 'fonts')
ATLAS = 'monitor_font_5x7.png'
METRICS = 'monitor_font_5x7.json'
GLYPH_W, GLYPH_H, PAD, COLS = 5, 7, 2, 16   # pad 2 so the atlas is inspectable by eye
INK = 'text'          # glyphs are drawn in the palette's brightest tone; the raster tints them

# 5x7 glyphs, one string per row. '#' = ink, '.' = transparent. Only what the screen actually prints.
GLYPHS = {
    ' ': '...../...../...../...../...../...../.....',
    'A': '.###./#...#/#...#/#####/#...#/#...#/#...#',
    'B': '####./#...#/####./#...#/#...#/#...#/####.',
    'C': '.###./#...#/#..../#..../#..../#...#/.###.',
    'D': '####./#...#/#...#/#...#/#...#/#...#/####.',
    'E': '#####/#..../###../#..../#..../#..../#####',
    'F': '#####/#..../###../#..../#..../#..../#....',
    'G': '.###./#...#/#..../#.###/#...#/#...#/.####',
    'H': '#...#/#...#/#...#/#####/#...#/#...#/#...#',
    'I': '.###./..#../..#../..#../..#../..#../.###.',
    'J': '..###/...#./...#./...#./...#./#..#./.##..',
    'K': '#...#/#..#./#.#../##.../#.#../#..#./#...#',
    'L': '#..../#..../#..../#..../#..../#..../#####',
    'M': '#...#/##.##/#.#.#/#...#/#...#/#...#/#...#',
    'N': '#...#/##..#/#.#.#/#..##/#...#/#...#/#...#',
    'O': '.###./#...#/#...#/#...#/#...#/#...#/.###.',
    'P': '####./#...#/#...#/####./#..../#..../#....',
    'Q': '.###./#...#/#...#/#...#/#.#.#/#..#./.##.#',
    'R': '####./#...#/#...#/####./#.#../#..#./#...#',
    'S': '.####/#..../#..../.###./....#/....#/####.',
    'T': '#####/..#../..#../..#../..#../..#../..#..',
    'U': '#...#/#...#/#...#/#...#/#...#/#...#/.###.',
    'V': '#...#/#...#/#...#/#...#/#...#/.#.#./..#..',
    'W': '#...#/#...#/#...#/#.#.#/#.#.#/##.##/#...#',
    'X': '#...#/#...#/.#.#./..#../.#.#./#...#/#...#',
    'Y': '#...#/#...#/.#.#./..#../..#../..#../..#..',
    'Z': '#####/....#/...#./..#../.#.../#..../#####',
    '0': '.###./#...#/#..##/#.#.#/##..#/#...#/.###.',
    '1': '..#../.##../..#../..#../..#../..#../.###.',
    '2': '.###./#...#/....#/...#./..#../.#.../#####',
    '3': '####./....#/....#/.###./....#/....#/####.',
    '4': '...#./..##./.#.#./#..#./#####/...#./...#.',
    '5': '#####/#..../####./....#/....#/#...#/.###.',
    '6': '..##./.#.../#..../####./#...#/#...#/.###.',
    '7': '#####/....#/...#./..#../.#.../.#.../.#...',
    '8': '.###./#...#/#...#/.###./#...#/#...#/.###.',
    '9': '.###./#...#/#...#/.####/....#/...#./.##..',
    '.': '...../...../...../...../...../..##./..##.',
    ',': '...../...../...../...../..##./..##./.#...',
    ':': '...../..##./..##./...../..##./..##./.....',
    ';': '...../..##./..##./...../..##./..#../.#...',
    '-': '...../...../...../#####/...../...../.....',
    '+': '...../..#../..#../#####/..#../..#../.....',
    '/': '....#/....#/...#./..#../.#.../#..../#....',
    '#': '.#.#./#####/.#.#./#####/.#.#./...../.....',
    # REAL SQUARE BRACKETS. A previous pass drew both brackets as '.#.#.' on all seven rows -- two
    # parallel bars, no arms -- to avoid an arm reading as C or J at 5x7. The cure was worse than the
    # disease: on the walk page every "[SPACE] WALK ON" rendered as "I SPACE I I WALK ON", which the player
    # read (correctly) as machine noise. Now '[' is a left arm with top and bottom hooks: unmistakably a
    # bracket at 5 px, and it cannot be mistaken for a letter because it has no middle bar.
    '[': '.###./.#.../.#.../.#.../.#.../.#.../.###.',
    ']': '.###./...#./...#./...#./...#./...#./.###.',
    '(': '..##./.#.../.#.../.#.../.#.../.#.../..##.',
    ')': '.##../...#./...#./...#./...#./...#./.##..',
    "'": '..#../..#../...../...../...../...../.....',
    '~': '...../...../.##.#/#..#./...../...../.....',
    '?': '.###./#...#/....#/...#./..#../...../..#..',
    '!': '..#../..#../..#../..#../..#../...../..#..',
    '%': '##..#/##..#/...#./..#../.#.../#..##/#..##',
    '=': '...../...../#####/...../#####/...../.....',
    '*': '...../#.#.#/.###./#####/.###./#.#.#/.....',
    '&': '.##../#..#./#.#../.#.../#.#.#/#..#./.##.#',
    '<': '...#./..#../.#.../#..../.#.../..#../...#.',
    '>': '.#.../..#../...#./....#/...#./..#../.#...',
    '"': '#.#../#.#../...../...../...../...../.....',
    '_': '...../...../...../...../...../...../#####',
}

# Characters the raster can fall back on when a record contains something exotic.
FALLBACK = '?'


def glyph_rows(ch):
    """Returns the 7 rows of '#'/'.' for ch, falling back so text can never render as a gap."""
    pat = GLYPHS.get(ch) or GLYPHS.get(ch.upper()) or GLYPHS[FALLBACK]
    return pat.split('/')


def build():
    n = len(GLYPHS)
    rows = (n + COLS - 1) // COLS
    w = COLS * (GLYPH_W + PAD) - PAD
    h = rows * (GLYPH_H + PAD) - PAD
    img = Image.new('RGBA', (w, h), (0, 0, 0, 0))
    px = img.load()
    glyphs = []
    for i, ch in enumerate(sorted(GLYPHS)):
        cx = (i % COLS) * (GLYPH_W + PAD)
        cy = (i // COLS) * (GLYPH_H + PAD)
        for gy, row in enumerate(glyph_rows(ch)):
            for gx, c in enumerate(row):
                if c == '#':
                    px[cx + gx, cy + gy] = rgba(INK)
        # 'rows' carries the same ink as the atlas, so tests (and anything without an image
        # decoder) can verify glyph coverage from data alone. The PNG is what Unity renders.
        glyphs.append({'ch': ch, 'x': cx, 'y': cy, 'rows': glyph_rows(ch)})
    return img, {
        'cellW': GLYPH_W, 'cellH': GLYPH_H, 'pad': PAD, 'cols': COLS,
        'atlasW': w, 'atlasH': h, 'ink': INK,
        'note': 'generated by tools/art/make_font.py -- do not edit',
        'glyphs': glyphs,
    }


def check() -> int:
    path = os.path.join(os.path.normpath(OUT), ATLAS)
    mpath = os.path.join(os.path.normpath(OUT), METRICS)
    problems = []
    if not os.path.isfile(path):
        problems.append(ATLAS + ' missing')
    if not os.path.isfile(mpath):
        problems.append(METRICS + ' missing')
    if problems:
        print('checked 0 font assets')
        for p in problems:
            print('   ', p)
        print('FAIL: font assets missing')
        return 1

    w, h, pixels = read_rgba(path)
    vals = set(PALETTE.values())
    with open(mpath, 'r', encoding='utf-8') as fh:
        meta = json.load(fh)
    for px in pixels:
        if px[3] == 0:
            continue
        if px[:3] not in vals or px[3] != 255:
            problems.append(('palette', px))
            break
    if (w, h) != (meta['atlasW'], meta['atlasH']):
        problems.append(('size', f'{w}x{h} != {meta["atlasW"]}x{meta["atlasH"]}'))
    if len(meta['glyphs']) != len(GLYPHS):
        problems.append(('glyphcount', f'{len(meta["glyphs"])} != {len(GLYPHS)}'))
    # every glyph rect must be inside the atlas
    for g in meta['glyphs']:
        if g['x'] < 0 or g['y'] < 0 or g['x'] + GLYPH_W > w or g['y'] + GLYPH_H > h:
            problems.append(('rect', g))
            break
    print(f"checked font atlas {w}x{h}, {len(meta['glyphs'])} glyphs")
    if problems:
        for row in problems[:8]:
            print('   ', row)
        print('FAIL: font problems above')
        return 1
    print('PASS: font atlas is palette-conformant, alpha 0/255, metrics match')
    return 0


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument('--check', action='store_true')
    args = ap.parse_args()
    if args.check:
        return check()
    if not HAVE_PIL:
        print('Pillow is required to regenerate the font; --check needs only the stdlib.')
        return 2
    os.makedirs(os.path.normpath(OUT), exist_ok=True)
    img, meta = build()
    img.save(os.path.join(os.path.normpath(OUT), ATLAS))
    with open(os.path.join(os.path.normpath(OUT), METRICS), 'w', encoding='utf-8') as fh:
        json.dump(meta, fh, indent=1, sort_keys=False)
        fh.write('\n')
    print(f'wrote {ATLAS} and {METRICS} ({len(GLYPHS)} glyphs, {meta["atlasW"]}x{meta["atlasH"]})')
    write_import_settings(os.path.join(os.path.normpath(OUT), ATLAS))
    return 0


# ------------------------------------------------------------------ Unity import settings
# A texture's import flags live ONLY in its .meta file, and Unity generates that file with its defaults the
# first time it sees the asset -- Read/Write DISABLED. The runtime reads this atlas pixel by pixel
# (Texture2D.GetPixels32), so on a fresh clone the CRT threw "texture data is either not readable" on the
# first Play. Committing a .meta with isReadable: 1 makes the import correct before anyone presses Play.
# The guid is derived from the path so re-running this never churns the repository.
def write_import_settings(atlas_path):
    import uuid
    meta_path = atlas_path + '.meta'
    guid = uuid.uuid5(uuid.NAMESPACE_URL, 'hr0d/' + atlas_path.replace(os.sep, '/')).hex
    with open(meta_path, 'w', encoding='utf-8') as fh:
        fh.write(
            'fileFormatVersion: 2\n'
            'guid: %s\n'
            'TextureImporter:\n'
            '  internalIDToNameTable: []\n'
            '  externalObjects: {}\n'
            '  serializedVersion: 12\n'
            '  mipmaps:\n'
            '    mipMapMode: 0\n'
            '    enableMipMap: 0\n'
            '    sRGBTexture: 1\n'
            '  isReadable: 1\n'          # <- the whole point
            '  streamingMipmaps: 0\n'
            '  wrapMode: 1\n'
            '  filterMode: 0\n'          # point, never bilinear: a 5x7 glyph must stay crisp
            '  aniso: 1\n'
            '  maxTextureSize: 2048\n'
            '  textureFormat: 1\n'
            '  spriteMode: 0\n'
            '  spritePixelsPerUnit: 16\n'
            '  alphaIsTransparency: 1\n'
            '  textureType: 0\n'
            '  textureShape: 1\n' % guid)
    print('wrote %s (isReadable: 1)' % os.path.basename(meta_path))


if __name__ == '__main__':
    sys.exit(main())
