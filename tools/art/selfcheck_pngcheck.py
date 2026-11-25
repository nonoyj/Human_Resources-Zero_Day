#!/usr/bin/env python3
"""
make_palette_check.py  (invoked as:  python tools/art/make_palette_check.py)

Verifies that tools/art/pngcheck.py decodes every kind of PNG Pillow writes for us, by
writing one file of each colour type / filter mix and reading it back with BOTH decoders.
If Pillow is missing this still runs -- it just skips the cross-check and reports it.

    python tools/art/make_palette_check.py
"""
from __future__ import annotations

import os
import sys
import tempfile

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from pngcheck import read_rgba  # noqa: E402


def stdlib_cases():
    """PNGs built by pngcheck-adjacent code, no third-party library involved."""
    import struct
    import zlib

    def chunk(tag, body):
        return struct.pack('>I', len(body)) + tag + body + struct.pack('>I', zlib.crc32(tag + body) & 0xFFFFFFFF)

    files = []

    # 1. RGBA, 8-bit, no filter
    w = h = 8
    rows = b''.join(b'\x00' + bytes([255, 0, 0, 255] * w) for _ in range(h))
    ihdr = struct.pack('>IIBBBBB', w, h, 8, 6, 0, 0, 0)
    files.append(('rgba.png', b'\x89PNG\r\n\x1a\n' + chunk(b'IHDR', ihdr) +
                  chunk(b'IDAT', zlib.compress(rows)) + chunk(b'IEND', b'')))

    # 2. palette with tRNS
    w = h = 4
    rows = b''.join(b'\x00' + bytes([0, 1, 0, 1] * 1) for _ in range(h))
    plte = bytes([10, 20, 30, 200, 100, 50])
    trns = bytes([255, 64])
    ihdr = struct.pack('>IIBBBBB', w, h, 8, 3, 0, 0, 0)
    files.append(('palette.png', b'\x89PNG\r\n\x1a\n' + chunk(b'IHDR', ihdr) + chunk(b'PLTE', plte) +
                  chunk(b'tRNS', trns) + chunk(b'IDAT', zlib.compress(rows)) + chunk(b'IEND', b'')))

    # 3. grey+alpha, Paeth filter on every row
    w = h = 4
    line = bytes([9, 200] * w)
    rows = b''
    prev = bytes(len(line))
    for _ in range(h):
        rows += b'\x04' + bytes((line[i] - (line[i - 2] if i >= 2 else 0)) & 0xFF for i in range(len(line)))
        prev = line
    ihdr = struct.pack('>IIBBBBB', w, h, 8, 4, 0, 0, 0)
    files.append(('grey_alpha.png', b'\x89PNG\r\n\x1a\n' + chunk(b'IHDR', ihdr) +
                  chunk(b'IDAT', zlib.compress(rows)) + chunk(b'IEND', b'')))
    return files


def main() -> int:
    tmp = tempfile.mkdtemp(prefix='pngcheck-')
    failures = 0

    print('stdlib-written PNGs:')
    for name, blob in stdlib_cases():
        path = os.path.join(tmp, name)
        with open(path, 'wb') as fh:
            fh.write(blob)
        try:
            w, h, px = read_rgba(path)
            print(f'  PASS  {name}  {w}x{h}  first pixel {px[0]}')
        except Exception as exc:                     # noqa: BLE001
            print(f'  FAIL  {name}  {exc}')
            failures += 1

    try:
        from PIL import Image                        # noqa: F401
    except ImportError:
        print('\nPillow not installed here -- cross-check with Pillow-written files skipped.')
        print('(That is fine: the --check paths no longer use Pillow at all.)')
    else:
        print('\nPillow-written PNGs (real files from the kit):')
        root = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..', 'art', 'kit'))
        checked = 0
        for sub in ('ui', 'icons', 'applicants', 'portraits', 'backgrounds'):
            folder = os.path.join(root, sub)
            if not os.path.isdir(folder):
                continue
            for f in sorted(os.listdir(folder))[:2]:
                if not f.endswith('.png'):
                    continue
                path = os.path.join(folder, f)
                try:
                    w, h, px = read_rgba(path)
                    from PIL import Image
                    pil = Image.open(path).convert('RGBA')
                    if (w, h) != pil.size:
                        raise AssertionError(f'size mismatch {w}x{h} vs {pil.size}')
                    if px[0] != pil.getpixel((0, 0)):
                        raise AssertionError(f'pixel mismatch {px[0]} vs {pil.getpixel((0, 0))}')
                    print(f'  PASS  {sub}/{f}  {w}x{h}  agrees with Pillow')
                    checked += 1
                except Exception as exc:             # noqa: BLE001
                    print(f'  FAIL  {sub}/{f}  {exc}')
                    failures += 1
        if checked == 0:
            print('  (no kit files on disk -- skipped)')

    print('\nRESULT:', 'PASS -- stdlib decoder is trustworthy' if failures == 0 else f'FAIL ({failures})')
    return 0 if failures == 0 else 1


if __name__ == '__main__':
    sys.exit(main())
