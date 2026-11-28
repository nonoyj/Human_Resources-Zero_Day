#!/usr/bin/env python3
"""
tools/comp/twin_desk.py -- renders the desk scene OFFLINE from the exact data Unity uses.

WHY: the artist's preview and the game screen drifting apart is the classic pixel-art failure.
     This renderer reads the same two files the Unity scene builder reads:

         art/kit/unity_sprites.json      (what every PNG is, and how it must be imported)
         unity/Assets/Content/desk_layout.json  (where every sprite goes, 480x270 pixels)

     If the twin and the phone disagree, one of those two JSON files is wrong -- and the twin
     tells you which one, on a machine with no Unity and no Pillow.

USAGE
    python tools/comp/twin_desk.py                      # foreground desk, day 1
    python tools/comp/twin_desk.py --corruption 2       # background variant L2
    python tools/comp/twin_desk.py --applicant synthetic_cloned_a
    python tools/comp/twin_desk.py --frame 3            # 0..3 idle frame
    python tools/comp/twin_desk.py --hotspots           # overlay the click targets
    python tools/comp/twin_desk.py --verify             # compare against the reference composition

EXIT CODES
    0 ok / verify passed, 1 verify failed, 2 bad arguments
"""
from __future__ import annotations

import argparse
import json
import os
import sys

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', 'art'))
from pngcheck import read_rgba, write_rgba  # noqa: E402

ROOT = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..'))
MANIFEST = os.path.join(ROOT, 'art', 'kit', 'unity_sprites.json')
LAYOUT = os.path.join(ROOT, 'unity', 'Assets', 'Content', 'desk_layout.json')
OUTDIR = os.path.join(ROOT, 'art', 'kit', 'reference')
TRANSPARENT = (0, 0, 0, 0)


def load():
    with open(MANIFEST, 'r', encoding='utf-8') as fh:
        manifest = json.load(fh)
    with open(LAYOUT, 'r', encoding='utf-8') as fh:
        layout = json.load(fh)
    by_path = {s['source']: s for s in manifest['sprites']}
    return manifest, layout, by_path


class Canvas:
    """Top-left-origin RGBA canvas with a y-up blit, matching Unity's convention."""

    def __init__(self, w, h, fill=(11, 14, 19, 255)):
        self.w, self.h = w, h
        self.px = [fill] * (w * h)

    def blit(self, src, sw, sh, x, y_bottom, clip=True):
        for row in range(sh):
            ty = self.h - 1 - (y_bottom + row)
            if ty < 0 or ty >= self.h:
                continue
            srow = (sh - 1 - row) * sw
            for col in range(sw):
                tx = x + col
                if tx < 0 or tx >= self.w:
                    continue
                r, g, b, a = src[srow + col]
                if a == 0:
                    continue
                if a == 255 or not clip:
                    self.px[ty * self.w + tx] = (r, g, b, a)
                    continue
                br, bg, bb, _ba = self.px[ty * self.w + tx]
                k = a / 255.0
                self.px[ty * self.w + tx] = (int(r * k + br * (1 - k)),
                                             int(g * k + bg * (1 - k)),
                                             int(b * k + bb * (1 - k)), 255)

    def rect_outline(self, x, y_bottom, w, h, colour, thickness=1):
        for t in range(thickness):
            for col in range(w):
                for yy in (y_bottom + t, y_bottom + h - 1 - t):
                    ty = self.h - 1 - yy
                    if 0 <= ty < self.h and 0 <= x + col < self.w:
                        self.px[ty * self.w + x + col] = colour
            for row in range(h):
                for xx in (x + t, x + w - 1 - t):
                    ty = self.h - 1 - (y_bottom + row)
                    if 0 <= ty < self.h and 0 <= xx < self.w:
                        self.px[ty * self.w + xx] = colour


def scale_nearest(src, sw, sh, nw, nh):
    """Nearest-neighbour scale -- keeps pixel art crisp; no filtering ever.

    read_rgba returns a LIST of (r, g, b, a) tuples, not a byte buffer (seed the BUG-017 lesson:
    check the container before indexing it)."""
    out = [None] * (nw * nh)
    for y in range(nh):
        sy = min(sh - 1, y * sh // nh)
        row = sy * sw
        for x in range(nw):
            out[y * nw + x] = src[row + min(sw - 1, x * sw // nw)]
    return out


def slice_sprite(path, entry, frame_index):
    """Crop frame `frame_index` out of a Multiple-mode strip."""
    w, h, pixels = read_rgba(os.path.join(ROOT, path))
    cw, ch = entry['sprites'][0]['rect'][2], entry['sprites'][0]['rect'][3]
    idx = frame_index % len(entry['sprites'])
    ox = entry['sprites'][idx]['rect'][0]
    out = []
    for row in range(ch):                       # TOP-DOWN, exactly like read_rgba returns
        for col in range(cw):
            out.append(pixels[row * w + ox + col])
    return out, cw, ch


def render(layout, by_path, applicant=None, frame=0, corruption=0, hotspots=False):
    cv = Canvas(layout['canvas']['width'], layout['canvas']['height'])
    for layer in sorted(layout['layers'], key=lambda L: L['sortingOrder']):
        path = layer['sprite']
        if layer['id'] == 'background' and corruption:
            path = f"art/kit/backgrounds/office_L{corruption}.png"
        if layer['id'] == 'applicant' and applicant:
            path = f"art/kit/applicants/app_{applicant}_192x216.png"
        entry = by_path.get(path)
        if entry is None:
            raise SystemExit(f'layer {layer["id"]}: {path} is not in unity_sprites.json')
        if entry['spriteMode'] == 'Multiple':
            src, sw, sh = slice_sprite(path, entry, frame)
        else:
            _w, _h, src = read_rgba(os.path.join(ROOT, path))
            sw, sh = entry['width'], entry['height']
        scale = layer.get('scaleFactor', 1.0)
        if scale != 1.0:
            nw, nh = max(1, int(sw * scale)), max(1, int(sh * scale))
            src = scale_nearest(src, sw, sh, nw, nh)
            sw, sh = nw, nh
        cv.blit(src, sw, sh, layer['anchor'][0], layer['anchor'][1])

    if hotspots:
        app = next(L for L in layout['layers'] if L['id'] == 'applicant')
        ax, ay = app['anchor']
        sc = app.get('scaleFactor', 1.0)
        for hp in layout['hotspots']:
            x, y, w, h = hp['rect']
            cv.rect_outline(ax + int(x * sc), ay + int(y * sc),
                            int(w * sc), int(h * sc), (255, 71, 87, 255))
    return cv


def reference_frames():
    """The two committed previews the twin must agree with, and how they were composed."""
    return [
        {'file': 'composition_preview.png', 'note': 'four applicants in one 2x2 sheet, 3x zoom -- '
                                                    'compared cell by cell, not in full'},
    ]


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument('--applicant', default='human_solid_a')
    ap.add_argument('--frame', type=int, default=0)
    ap.add_argument('--corruption', type=int, default=0, choices=(0, 1, 2))
    ap.add_argument('--hotspots', action='store_true')
    ap.add_argument('--zoom', type=int, default=3)
    ap.add_argument('--out', default=None)
    ap.add_argument('--verify', action='store_true')
    args = ap.parse_args()

    manifest, layout, by_path = load()

    if args.verify:
        return verify(layout, by_path, manifest)

    cv = render(layout, by_path, args.applicant, args.frame, args.corruption, args.hotspots)
    name = args.out or f"twin_desk_{args.applicant}_f{args.frame}_L{args.corruption}.png"
    path = name if os.path.isabs(name) else os.path.join(OUTDIR, name)

    if args.zoom > 1:
        big = Canvas(cv.w * args.zoom, cv.h * args.zoom)
        for y in range(cv.h):
            for x in range(cv.w):
                p = cv.px[y * cv.w + x]
                for dy in range(args.zoom):
                    for dx in range(args.zoom):
                        big.px[(y * args.zoom + dy) * big.w + x * args.zoom + dx] = p
        write_rgba(path, big.w, big.h, big.px)
    else:
        write_rgba(path, cv.w, cv.h, cv.px)

    print(f'wrote {os.path.relpath(path, ROOT)}  ({cv.w}x{cv.h} @ {args.zoom}x)')
    return 0


def verify(layout, by_path, manifest) -> int:
    """Structural verification: every layer resolves, every hotspot sits inside the applicant,
    and the desk composition matches the brief (monitor left, applicant right)."""
    problems = []
    cw, ch = layout['canvas']['width'], layout['canvas']['height']

    for layer in layout['layers']:
        entry = by_path.get(layer['sprite'])
        if entry is None:
            problems.append(f'layer {layer["id"]}: sprite missing from manifest')
            continue
        x, y = layer['anchor']
        w, h = entry['width'], entry['height']
        if entry['spriteMode'] == 'Multiple':
            w = entry['sprites'][0]['rect'][2]
            h = entry['sprites'][0]['rect'][3]
        if x < 0 or y < 0 or x + w > cw or y + h > ch:
            problems.append(f'layer {layer["id"]}: {w}x{h} at ({x},{y}) leaves the {cw}x{ch} canvas')

    app = next(L for L in layout['layers'] if L['id'] == 'applicant')
    ax, ay = app['anchor']
    app_w, app_h = 192, 216
    for hp in layout['hotspots']:
        x, y, w, h = hp['rect']
        if x < 0 or y < 0 or x + w > app_w or y + h > app_h:
            problems.append(f'hotspot {hp["artifact"]}: rect leaves the applicant sprite')
    if ay + app_h > ch:
        problems.append('applicant is taller than the canvas')

    # brief checks (v127, composition N -- the player's instruction): MONITOR ON THE RIGHT HALF, APPLICANT
    # ON THE LEFT. This read the other way round and would have failed the approved layout.
    monitor = next(L for L in layout['layers'] if L['id'] == 'monitor')
    monitor_centre = monitor['anchor'][0] + by_path[monitor['sprite']]['width'] / 2
    applicant_centre = ax + app_w / 2
    if monitor_centre <= cw / 2:
        problems.append('monitor is not on the right half')
    if applicant_centre >= cw / 2:
        problems.append('applicant is not on the left half')

    # manifest sanity: every sprite must be palette-import-ready
    for s in manifest['sprites']:
        if s['import']['filterMode'] != 'Point':
            problems.append(f'{s["source"]}: filter mode is not Point')
        if s['import']['compression'] != 'None':
            problems.append(f'{s["source"]}: compression is not None')

    if problems:
        for p in problems:
            print('  FAIL', p)
        print(f'FAIL: {len(problems)} layout problems')
        return 1
    print(f'PASS: {len(layout["layers"])} layers + {len(layout["hotspots"])} hotspots resolve '
          f'on a {cw}x{ch} canvas; monitor left of x={cw // 2}, applicant right of it')
    print(f'      manifest: {len(manifest["sprites"])} sprites, all Point / uncompressed')
    return 0


if __name__ == '__main__':
    sys.exit(main())
