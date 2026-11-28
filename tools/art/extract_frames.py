#!/usr/bin/env python3
"""HR//0D -- cut the 4 animation frames out of every applicant strip, as files, in python.

WHY THIS EXISTS (BUG-046)

The 4 frames per applicant (neutral / breathe / breathe_fidget / blink) used to be cut by the EDITOR
(`ArtKitImporter.ExtractFrames`) because Unity 6 removed `TextureImporter.spritesheet` and the replacement
package was not worth a dependency. Nothing was wrong with that code -- but it is an editor step, so a
fresh clone has no frames, and the first thing the desk does is load them:

    [HR//0D] could not start the game: frame not found at
    Resources/Art/Kit/applicants/frames/app_human_solid_a_192x216__human_solid_a_neutral

Same class as BUG-044 (`Resources/Art` was gitignored) and BUG-045 (no atlas .meta): the runtime needs a
file, and the file was produced by something that only runs on a machine where a human clicked a menu.

    python tools/art/extract_frames.py          # write the frames for every Multiple-mode sprite
    python tools/art/extract_frames.py --check  # report only, exit 1 if any frame is missing

Frame names and rects come from unity_sprites.json, so the manifest stays the single source of truth --
this script and the editor importer cannot disagree about what the frames are called.
"""
from __future__ import annotations

import argparse
import json
import os
import sys

ROOT = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..'))
MANIFEST = os.path.join(ROOT, 'art', 'kit', 'unity_sprites.json')
# Written into BOTH mirrors: Assets/Art/Kit is the authoring copy, Assets/Resources/Art/Kit is what the game
# loads at runtime (Resources.Load cannot see outside a Resources folder).
MIRRORS = [
    os.path.join(ROOT, 'unity', 'Assets', 'Art', 'Kit'),
    os.path.join(ROOT, 'unity', 'Assets', 'Resources', 'Art', 'Kit'),
]

# Pillow CUTS frames; it never has to CHECK them. The import used to be at the top and unconditional, so
# `--check` -- the mode every gate and every fresh clone runs -- died with "Pillow is required to cut frames"
# on a machine that only wanted to know whether the frames were there. That is the difference between
# "your art is broken" and "you have not installed a drawing library" (BUG-016, again, in the check path).
def _pillow():
    try:
        from PIL import Image
        return Image
    except ImportError:                                                # pragma: no cover
        print('Pillow is required to CUT frames (pip install pillow). Nothing is known to be wrong with the art.')
        raise SystemExit(3)


def frames_for(entry: dict):
    """-> [(relative_dir, filename)] for every frame of a Multiple-mode sprite."""
    asset_path = entry['assetPath']                       # Assets/Art/Kit/applicants/app_x_192x216.png
    rel = asset_path[len('Assets/Art/Kit/'):]             # applicants/app_x_192x216.png
    folder, name = os.path.split(rel)
    stem = os.path.splitext(name)[0]
    out = []
    for frame in entry.get('sprites') or []:
        out.append((os.path.join(folder, 'frames'), stem + '__' + frame['name'] + '.png'))
    return out


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument('--check', action='store_true')
    args = ap.parse_args()

    with open(MANIFEST, encoding='utf-8') as fh:
        manifest = json.load(fh)

    wanted, written, missing = 0, 0, []
    for entry in manifest['sprites']:
        if entry.get('spriteMode') != 'Multiple':
            continue
        asset_path = entry['assetPath']
        stem = os.path.splitext(os.path.basename(asset_path))[0]
        name = os.path.basename(asset_path)

        for sub, filename in frames_for(entry):
            wanted += 1
            absent = [m for m in MIRRORS if not os.path.isfile(os.path.join(m, sub, filename))]
            if not absent:
                continue
            missing.append(os.path.join(sub, filename))
            if args.check:
                continue

            with _pillow().open(os.path.join(ROOT, 'art', 'kit', sub.split('frames')[0].rstrip('/'), name)) as sheet:
                sheet = sheet.convert('RGBA')
                index = [f['name'] for f in entry['sprites']].index(
                    os.path.splitext(filename)[0].split('__', 1)[1])
                x, y, w, h = entry['sprites'][index]['rect']
                frame = sheet.crop((x, y, x + w, y + h))    # json rects are top-left based, like PIL
                for mirror in MIRRORS:
                    dst = os.path.join(mirror, sub, filename)
                    os.makedirs(os.path.dirname(dst), exist_ok=True)
                    frame.save(dst)
            written += 1

    if args.check:
        if missing:
            print('FAIL: %d animation frame(s) missing -- a clone would not boot:' % len(missing))
            for m in missing[:6]:
                print('   ', m)
            print('  run: python tools/art/extract_frames.py')
            return 1
        print('PASS: all %d animation frames exist in both mirrors' % wanted)
        return 0

    print('%d frames across %d strips: %d written, %d already present'
          % (wanted, sum(1 for e in manifest['sprites'] if e.get('spriteMode') == 'Multiple'),
             written, wanted - written))
    return 0 if not missing else 1


if __name__ == '__main__':
    raise SystemExit(main())
