#!/usr/bin/env python3
"""HR//0D -- write the Unity import settings for the RUNTIME art mirror.

WHY THIS FILE EXISTS (BUG-045, BUG-046)

Unity's import settings for an asset live in `<asset>.meta`. When the file is missing, Unity generates it
with its DEFAULTS on import, and the defaults are wrong for this game in two separate ways:

  * A PNG's default textureType is **Default**, not **Sprite**. `Resources.Load<Sprite>(...)` then returns
    null and every sprite in the game is invisible -- with no error at all.
  * Read/Write Enabled is **off** by default, so any `GetPixels32()` throws.

Committing the .meta files is the only way a fresh clone imports the art correctly, because Unity never runs
python and the import settings are not in the commit otherwise. That is the same lesson as BUG-044
(`Resources/Art` was gitignored) and BUG-045 (no atlas .meta): **a build input that lives in an uncommitted
file is not a build input.**

    python tools/art/write_unity_metas.py          # write any missing .meta under Resources/Art
    python tools/art/write_unity_metas.py --check  # report only, exit 1 if anything is missing

The guid is derived from the asset's repo path, so re-running this never churns the repository and never
breaks a reference that Unity has already resolved.
"""
from __future__ import annotations

import argparse
import os
import sys
import uuid

ROOT = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..'))
MIRROR = os.path.join(ROOT, 'unity', 'Assets', 'Resources', 'Art', 'Kit')

# Font atlases are read pixel-by-pixel by the runtime, so they are NOT sprites and must stay readable.
# Assets the game reads PIXELS from, not just draws. SweepView composites a floor's room art into the
# walk's page texture, so those three have to be Read/Write Enabled or the walk shows text over a flat
# fill (2026-11-27). The glyph atlas has always needed it.
READABLE = (
    'fonts/monitor_font_5x7.png',
    'sweep/sweep_desk_480x270.png',
    'sweep/sweep_corridor_480x270.png',
    'sweep/sweep_lift_480x270.png',
)


def unity_guid(asset_rel_path: str) -> str:
    """Stable, deterministic: the same asset always gets the same guid."""
    return uuid.uuid5(uuid.NAMESPACE_URL, 'hr0d/' + asset_rel_path.replace('\\', '/')).hex


def sprite_meta(guid: str, ppu: int = 16, readable: bool = False) -> str:
    return f"""fileFormatVersion: 2
guid: {guid}
TextureImporter:
  internalIDToNameTable: []
  externalObjects: {{}}
  serializedVersion: 12
  mipmaps:
    mipMapMode: 0
    enableMipMap: 0
    sRGBTexture: 1
    linearTexture: 0
  isReadable: {1 if readable else 0}
  streamingMipmaps: 0
  wrapMode: 1
  filterMode: 0
  aniso: 1
  maxTextureSize: 2048
  textureFormat: 1
  compressionQuality: 50
  compressionLevel: 0
  textureSettings:
    serializedVersion: 2
    filterMode: 0
    aniso: 1
    mipBias: 0
  spriteMode: 1
  spriteExtrude: 0
  spriteMeshType: 0
  alignment: 0
  spritePivot: {{x: 0.5, y: 0.5}}
  spriteBorder: {{x: 0, y: 0, z: 0, w: 0}}
  spritePixelsToUnits: {ppu}
  spriteGenerateFallbackPhysicsShape: 0
  alphaUsage: 1
  alphaIsTransparency: 1
  textureType: 8
  textureShape: 1
  spriteSheet:
    serializedVersion: 2
    sprites: []
    outline: []
    physicsShape: []
  userData:
  assetBundleName:
  assetBundleVariant:
"""


def texture_meta(guid: str) -> str:
    """Plain Texture2D, readable -- the glyph atlases."""
    return f"""fileFormatVersion: 2
guid: {guid}
TextureImporter:
  internalIDToNameTable: []
  externalObjects: {{}}
  serializedVersion: 12
  mipmaps:
    mipMapMode: 0
    enableMipMap: 0
    sRGBTexture: 1
  isReadable: 1
  streamingMipmaps: 0
  wrapMode: 1
  filterMode: 0
  aniso: 1
  maxTextureSize: 2048
  textureFormat: 1
  textureSettings:
    serializedVersion: 2
    filterMode: 0
    aniso: 1
    mipBias: 0
  spriteMode: 0
  spritePixelsToUnits: 16
  alphaUsage: 1
  alphaIsTransparency: 1
  textureType: 0
  textureShape: 1
  userData:
  assetBundleName:
  assetBundleVariant:
"""


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument('--check', action='store_true')
    args = ap.parse_args()

    if not os.path.isdir(MIRROR):
        print('FAIL: %s does not exist -- run tools/art/publish_to_unity.py first' % MIRROR)
        return 1

    written, missing, already, fixed_readable = 0, [], 0, 0
    for dirpath, _dirs, files in os.walk(MIRROR):
        for name in sorted(files):
            if not name.endswith('.png'):
                continue
            path = os.path.join(dirpath, name)
            rel = os.path.relpath(path, os.path.join(ROOT, 'unity')).replace('\\', '/')
            meta_path = path + '.meta'
            is_readable = rel.endswith(READABLE) or any(rel.endswith(r) for r in READABLE)
            if os.path.isfile(meta_path):
                already += 1
                text = open(meta_path, encoding='utf-8').read()
                if is_readable and 'isReadable: 1' not in text:
                    if args.check:
                        missing.append(rel + ' (not readable -- the game needs its pixels)')
                        continue
                    # sprite meta -> readable sprite meta, keeping the guid so nothing re-imports
                    guid = [l.split(':', 1)[1].strip() for l in text.splitlines() if l.startswith('guid:')][0]
                    open(meta_path, 'w', encoding='utf-8').write(sprite_meta(guid, readable=True))
                    fixed_readable += 1
                continue
            missing.append(rel)
            if args.check:
                continue
            with open(meta_path, 'w', encoding='utf-8') as fh:
                fh.write(texture_meta(unity_guid(rel)) if is_readable else sprite_meta(unity_guid(rel)))
            written += 1

    if fixed_readable:
        print('made %d existing sprite(s) readable for in-game compositing' % fixed_readable)

    if args.check:
        if missing:
            print('FAIL: %d asset(s) have no usable .meta -- Unity would import them with the wrong '
                  'settings (no sprite, not readable):' % len(missing))
            for m in missing[:10]:
                print('   ', m)
            print('  run: python tools/art/write_unity_metas.py')
            return 1
        print('PASS: all %d runtime images have import settings' % already)
        return 0

    print('written %d, already present %d, problems %d' % (written, already, len(missing)))
    if missing:
        for m in missing[:10]:
            print('   ', m)
        return 1
    return 0


if __name__ == '__main__':
    raise SystemExit(main())
