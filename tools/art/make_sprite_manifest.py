#!/usr/bin/env python3
"""
Builds art/kit/unity_sprites.json -- the one list of truth for getting the real sprites into Unity.

Every PNG under art/kit is described here with exactly the import settings it needs, so the Unity
Editor menu (HR//0D > Import Art Kit) and the offline twin renderer read the SAME data. If the two
ever disagree, this file is wrong -- not the art.

    python tools/art/make_sprite_manifest.py           # write the manifest
    python tools/art/make_sprite_manifest.py --verify   # check the manifest still matches disk
"""
from __future__ import annotations

import argparse
import hashlib
import json
import os
import re
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from pngcheck import read_rgba  # noqa: E402

ROOT = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..'))
KIT = os.path.join(ROOT, 'art', 'kit')
OUT = os.path.join(KIT, 'unity_sprites.json')

# ---------------------------------------------------------------- rules
# Folder-specific import behaviour. `slice` turns a strip into named sub-sprites.
RULES = {
    'applicants': dict(ppu=16, svc='Multiple', cell=(192, 216), frames=4, kind='applicant'),
    'portraits':  dict(ppu=16, svc='Single', kind='portrait'),
    'ui':         dict(ppu=16, svc='Single', kind='ui', nine_slice=8),
    'icons':      dict(ppu=16, svc='Single', kind='icon'),
    'stamps':     dict(ppu=16, svc='Single', kind='stamp'),
    'desk':       dict(ppu=16, svc='Single', kind='desk'),
    'backgrounds': dict(ppu=16, svc='Single', kind='background'),
    # wear overlays: full-screen, drawn ON TOP of the room at the same pixels-per-unit as the
    # background, so the two align exactly when both are spawned at 0,0.
    'fx':         dict(ppu=16, svc='Single', kind='fx'),
}

# Screens use these names in code. Keep them stable -- the twin and the scene builder both read them.
IDLE_FRAME_ORDER = ['neutral', 'breathe', 'breathe_fidget', 'blink']


def digest(path: str) -> str:
    with open(path, 'rb') as fh:
        return hashlib.sha256(fh.read()).hexdigest()[:16]


def sprite_entry(path: str, folder: str) -> dict:
    rel = os.path.relpath(path, ROOT).replace('\\', '/')
    rule = RULES[folder]
    w, h, _pixels = read_rgba(path)
    entry = {
        'source': rel,                                   # repo path: generators, twin renderer
        'assetPath': 'Assets/Art/Kit/' + rel.split('art/kit/', 1)[1],   # Unity path: importer

        'width': w,
        'height': h,
        'ppu': rule['ppu'],
        'spriteMode': rule['svc'],
        'kind': rule['kind'],
        'sha256_16': digest(path),
        'import': {
            'textureType': 'Sprite',
            'filterMode': 'Point',
            'compression': 'None',
            'mipmaps': False,
            'alphaIsTransparency': True,
            'meshType': 'FullRect',
            'wrapMode': 'Clamp',
        },
    }
    if rule.get('nine_slice'):
        entry['import']['border'] = [rule['nine_slice']] * 4
    if rule['svc'] == 'Multiple':
        cw, ch = rule['cell']
        if w % cw or h % ch:
            raise SystemExit(f'{rel}: size {w}x{h} does not divide into {cw}x{ch} cells')
        cells = (w // cw) * (h // ch)
        if cells != rule['frames']:
            raise SystemExit(f'{rel}: expected {rule["frames"]} cells, found {cells}')
        base = os.path.basename(path).split('_19')[0]
        if base.startswith('app_'):
            base = base[4:]                       # app_human_solid_a -> human_solid_a
        names = IDLE_FRAME_ORDER if rule['frames'] == 4 else [f'f{i}' for i in range(cells)]
        entry['sprites'] = [
            {'name': f'{base}_{names[i]}', 'rect': [i * cw, 0, cw, ch]}
            for i in range(cells)
        ]
    return entry


def collect() -> dict:
    sprites = []
    for folder, rule in RULES.items():
        d = os.path.join(KIT, folder)
        if not os.path.isdir(d):
            continue
        for f in sorted(os.listdir(d)):
            if f.lower().endswith('.png'):
                sprites.append(sprite_entry(os.path.join(d, f), folder))

    reference = []
    refdir = os.path.join(KIT, 'reference')
    if os.path.isdir(refdir):
        reference = [os.path.relpath(os.path.join(refdir, f), ROOT).replace('\\', '/')
                     for f in sorted(os.listdir(refdir)) if f.lower().endswith('.png')]

    return {
        'manifestVersion': 1,
        'generatedBy': 'tools/art/make_sprite_manifest.py',
        'note': 'Import settings for every shipped sprite. Reference sheets are docs, not game art.',
        'unity': {
            'referenceResolution': [480, 270],
            'pixelsPerUnit': 16,
            'filterMode': 'Point',
            'colorSpace': 'Linear',
            'spriteAtlas': None,
            'targetFrameRate': 60,
        },
        'counts': {folder: sum(1 for s in sprites if s['source'].startswith(f'art/kit/{folder}/'))
                   for folder in RULES},
        'sprites': sprites,
        'referenceSheets': reference,
    }


def verify() -> int:
    if not os.path.isfile(OUT):
        print('FAIL: manifest missing -- run: python tools/art/make_sprite_manifest.py')
        return 1
    with open(OUT, 'r', encoding='utf-8') as fh:
        old = json.load(fh)
    new = collect()
    if old == new:
        print(f"PASS: manifest matches disk ({len(new['sprites'])} sprites, "
              f"{len(new['referenceSheets'])} reference sheets)")
        return 0
    oldbyname = {s['source']: s for s in old['sprites']}
    newbyname = {s['source']: s for s in new['sprites']}
    for p in sorted(set(newbyname) - set(oldbyname)):
        print('  MISSING from manifest:', p)
    for p in sorted(set(oldbyname) - set(newbyname)):
        print('  STALE in manifest:', p)
    for p in sorted(set(oldbyname) & set(newbyname)):
        if oldbyname[p] != newbyname[p]:
            print('  CHANGED:', p)
    print('FAIL: manifest is out of date -- re-run tools/art/make_sprite_manifest.py')
    return 1


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument('--verify', action='store_true')
    args = ap.parse_args()
    if args.verify:
        return verify()

    data = collect()
    with open(OUT, 'w', encoding='utf-8') as fh:
        json.dump(data, fh, indent=2)
        fh.write('\n')
    total = len(data['sprites'])
    print(f'wrote {os.path.relpath(OUT, ROOT)} -- {total} sprites')
    for folder, n in data['counts'].items():
        print(f'   {folder:<12} {n:>3}')
    multi = [s for s in data['sprites'] if s['spriteMode'] == 'Multiple']
    print(f'   sliced strips: {len(multi)} x {multi[0]["sprites"].__len__() if multi else 0} frames')
    return 0


if __name__ == '__main__':
    sys.exit(main())
