#!/usr/bin/env python3
"""
pngcheck.py -- a tiny, dependency-free PNG reader used by the art checkers.

WHY THIS EXISTS
    `python tools/art/make_art_kit.py --check` used to need Pillow. On a fresh Windows
    machine Pillow is not installed, so the *verification* step failed with
    ModuleNotFoundError even though there was nothing wrong with the art (BUG-016).
    Verifying art should never depend on a pip install.

    Pillow is still required to *regenerate* art (drawing needs it). Checking needs nothing
    but the standard library, so this module decodes PNGs with zlib + the filter algorithms
    and returns plain RGBA tuples.

SCOPE (deliberately small -- it only has to read what Pillow writes for us)
    - colour types 0 (grey), 2 (RGB), 3 (palette), 4 (grey+alpha), 6 (RGBA)
    - bit depth 8 only
    - interlace 0 (none)
    Anything else raises PngError with a clear message instead of guessing.

USAGE
    from pngcheck import read_rgba
    w, h, pixels = read_rgba('sprite.png')      # pixels = [(r, g, b, a), ...] row-major
"""
from __future__ import annotations

import struct
import zlib

PNG_SIG = b'\x89PNG\r\n\x1a\n'


class PngError(Exception):
    pass


def _paeth(a: int, b: int, c: int) -> int:
    p = a + b - c
    pa, pb, pc = abs(p - a), abs(p - b), abs(p - c)
    if pa <= pb and pa <= pc:
        return a
    if pb <= pc:
        return b
    return c


def read_rgba(path: str):
    """Decode a PNG into (width, height, [(r, g, b, a), ...])."""
    with open(path, 'rb') as fh:
        data = fh.read()

    if not data.startswith(PNG_SIG):
        raise PngError(f'{path}: not a PNG')

    pos = len(PNG_SIG)
    width = height = depth = ctype = interlace = 0
    palette = b''
    trns = b''
    idat = bytearray()

    while pos + 8 <= len(data):
        length = struct.unpack('>I', data[pos:pos + 4])[0]
        ctag = data[pos + 4:pos + 8]
        body = data[pos + 8:pos + 8 + length]
        pos += 12 + length          # length + tag + body + crc

        if ctag == b'IHDR':
            width, height, depth, ctype, _comp, _filt, interlace = struct.unpack('>IIBBBBB', body)
        elif ctag == b'PLTE':
            palette = body
        elif ctag == b'tRNS':
            trns = body
        elif ctag == b'IDAT':
            idat += body
        elif ctag == b'IEND':
            break

    if depth != 8:
        raise PngError(f'{path}: only 8-bit PNGs are supported (got bit depth {depth})')
    if interlace != 0:
        raise PngError(f'{path}: interlaced PNGs are not supported')
    if ctype not in (0, 2, 3, 4, 6):
        raise PngError(f'{path}: unsupported colour type {ctype}')

    channels = {0: 1, 2: 3, 3: 1, 4: 2, 6: 4}[ctype]
    stride = width * channels
    raw = zlib.decompress(bytes(idat))
    if len(raw) < height * (stride + 1):
        raise PngError(f'{path}: truncated image data')

    out = bytearray(height * stride)
    prev = bytearray(stride)
    src = 0
    for y in range(height):
        ftype = raw[src]
        src += 1
        line = bytearray(raw[src:src + stride])
        src += stride
        if ftype == 0:
            pass
        elif ftype == 1:                       # Sub
            for i in range(channels, stride):
                line[i] = (line[i] + line[i - channels]) & 0xFF
        elif ftype == 2:                       # Up
            for i in range(stride):
                line[i] = (line[i] + prev[i]) & 0xFF
        elif ftype == 3:                       # Average
            for i in range(stride):
                left = line[i - channels] if i >= channels else 0
                line[i] = (line[i] + ((left + prev[i]) >> 1)) & 0xFF
        elif ftype == 4:                       # Paeth
            for i in range(stride):
                left = line[i - channels] if i >= channels else 0
                upleft = prev[i - channels] if i >= channels else 0
                line[i] = (line[i] + _paeth(left, prev[i], upleft)) & 0xFF
        else:
            raise PngError(f'{path}: bad filter type {ftype} on row {y}')
        out[y * stride:(y + 1) * stride] = line
        prev = line

    pixels = []
    if ctype == 6:
        for i in range(0, len(out), 4):
            pixels.append((out[i], out[i + 1], out[i + 2], out[i + 3]))
    elif ctype == 2:
        for i in range(0, len(out), 3):
            pixels.append((out[i], out[i + 1], out[i + 2], 255))
    elif ctype == 0:
        for i in range(0, len(out), 1):
            v = out[i]
            pixels.append((v, v, v, 255))
    elif ctype == 4:
        for i in range(0, len(out), 2):
            pixels.append((out[i], out[i], out[i], out[i + 1]))
    else:                                       # palette
        if not palette:
            raise PngError(f'{path}: palette image with no PLTE chunk')
        table = [(palette[i], palette[i + 1], palette[i + 2]) for i in range(0, len(palette), 3)]
        alphas = list(trns) if trns else []
        for idx in out:
            r, g, b = table[idx]
            a = alphas[idx] if idx < len(alphas) else 255
            pixels.append((r, g, b, a))

    return width, height, pixels


def violations(path: str, palette_set, allow_partial_alpha: bool = False):
    """Return the first pixel that breaks the palette / alpha rules, or None."""
    _w, _h, pixels = read_rgba(path)
    for px in pixels:
        if px[3] == 0:
            continue
        if px[:3] not in palette_set:
            return f'colour not in palette: {px}'
        if not allow_partial_alpha and 0 < px[3] < 255:
            return f'partial alpha: {px}'
    return None


def write_rgba(path: str, width: int, height: int, pixels) -> None:
    """Write an 8-bit RGBA PNG from [(r, g, b, a), ...] (row-major). No third-party library."""
    import struct as _struct
    import zlib as _zlib

    def chunk(tag, body):
        return (_struct.pack('>I', len(body)) + tag + body +
                _struct.pack('>I', _zlib.crc32(tag + body) & 0xFFFFFFFF))

    rows = bytearray()
    for y in range(height):
        rows.append(0)                                   # filter type 0 (None)
        for x in range(width):
            r, g, b, a = pixels[y * width + x]
            rows += bytes((r, g, b, a))

    ihdr = _struct.pack('>IIBBBBB', width, height, 8, 6, 0, 0, 0)
    blob = (PNG_SIG + chunk(b'IHDR', ihdr) +
            chunk(b'IDAT', _zlib.compress(bytes(rows), 9)) + chunk(b'IEND', b''))
    with open(path, 'wb') as fh:
        fh.write(blob)
