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

    python tools/art/write_unity_metas.py          # write missing .meta, and REPAIR stale slices
    python tools/art/write_unity_metas.py --check  # report only, exit 1 if anything needs writing

BUG-075 (2026-11-28): THE STALE SLICE. v118 resized menu/monitor_stack.png (112x96 -> 76x66) and
menu/window.png (104x132 -> 88x160). The PNGs were regenerated, the .meta files were NOT, and Unity then
tried to build the OLD sub-sprite rects on the NEW texture:

    "monitor_stack_0 not generated because the rect lies (partially) outside of texture"

Two separate causes, and both had to be fixed here:

  * the MIRROR metas (Assets/Resources/Art/Kit) are now rewritten whenever the PNG's size no longer matches
    the size the meta implies -- the meta does not store the size, so the size is recorded in this script's
    own table (art/kit/unity_sprites.json is the manifest, and Unity's slicing is per-file);
  * the SOURCE folder (Assets/Art/Kit) had NO committed .meta files at all, so on any machine that had
    imported the art once, Unity kept .meta files on disk that matched the OLD sizes and the old slicing.
    Every published PNG now gets a .meta in the source folder as well, and one that disagrees is replaced.

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
SOURCE = os.path.join(ROOT, 'unity', 'Assets', 'Art', 'Kit')       # where publish_to_unity.py copies the kit

# Font atlases are read pixel-by-pixel by the runtime, so they are NOT sprites and must stay readable.
# Assets the game reads PIXELS from, not just draws. SweepView composites a floor's room art into the
# walk's page texture, so those three have to be Read/Write Enabled or the walk shows text over a flat
# fill (2026-11-27). The glyph atlas has always needed it.
READABLE = (
    'fonts/monitor_font_5x7.png',
    'sweep/sweep_desk_480x270.png',
    'sweep/sweep_corridor_480x270.png',
    'sweep/sweep_lift_480x270.png',
    # THE FRONT DOOR'S ROOM (v119.4, BUG-082). MenuView does not DRAW these eight sprites -- it BLITS them
    # into one 480x270 buffer with GetPixels32, lights that buffer, and draws the result. Read/Write off
    # means GetPixels32 throws, MenuView swallows the throw, and the room comes up empty: the menu's text
    # floating in the middle of the desk scene, which is exactly the screenshot the player sent.
    # Keep this list and ArtKitImporter.MenuRoomSprites() and MenuLayout's blit order in step; a test
    # (ManifestBridgeTests.CheckTheMenuRoomIsReadable) fails if they drift apart.
    'Art/Kit/menu/shell.png',
    'Art/Kit/menu/window.png',
    'Art/Kit/menu/monitor_stack.png',
    'Art/Kit/menu/crt.png',
    'Art/Kit/menu/server.png',
    'Art/Kit/menu/desk.png',
    'Art/Kit/menu/lamp.png',
    'Art/Kit/menu/glow.png',
    'Art/Kit/menu/glow_lamp.png',
    'Art/Kit/menu/glow_tube.png',
    'Art/Kit/menu/glow_window.png',
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


def audio_meta(guid: str) -> str:
    """Import settings for a generated sound (v105).

    Unity's defaults for a WAV are actually close to right, but three of them are wrong for sounds that are
    played dozens of times a session on a phone:
        * forceToMono     -- leave OFF: the files are already mono; forcing adds nothing and can re-encode
        * loadType        -- DecompressOnLoad for short cues (a stamp must not decompress at the moment the
                             player presses A), Streaming for the two long beds
        * preloadAudioData-- ON: the cue has to be in memory before the first press, not after it
    Compression is PCM, so the file plays as generated: no Vorbis smearing on a 50 ms transient.
    """
    return f"""fileFormatVersion: 2
guid: {guid}
AudioImporter:
  externalObjects: {{}}
  serializedVersion: 7
  defaultSettings:
    serializedVersion: 2
    loadType: 0
    sampleRateSetting: 0
    sampleRateOverride: 24000
    compressionFormat: 1
    quality: 1
    conversionMode: 0
    preloadAudioData: 1
  platformSettingOverrides: {{}}
  forceToMono: 0
  normalize: 0
  loadInBackground: 0
  ambisonic: 0
  3D: 0
  userData:
  assetBundleName:
  assetBundleVariant:
"""


def expected_sizes() -> dict:
    """
    path (relative to art/kit) -> (width, height), read from the PNGs themselves. The meta does not record a
    size, so the ONLY durable way to know a meta is stale is to compare the sprite rects it declares against
    the texture it is attached to. Everything this project publishes is a Single sprite, so a meta with
    `spriteMode: 1` and an empty `sprites: []` is correct for ANY size -- and a meta with slices is stale by
    definition, because nothing in this repository generates slices for a menu sprite.
    """
    from pngcheck import read_rgba                    # no PIL needed, same as the art gate
    sizes = {}
    kit = os.path.join(ROOT, 'art', 'kit')
    for dirpath, _dirs, files in os.walk(kit):
        for name in files:
            if not name.endswith('.png'):
                continue
            full = os.path.join(dirpath, name)
            try:
                w, h, _px = read_rgba(full)
            except Exception:
                continue
            sizes[os.path.relpath(full, kit).replace('\\', '/')] = (w, h)
    return sizes


def stale_reason(meta_text: str) -> str:
    """Why a .meta cannot be trusted, or '' when it is fine."""
    if 'spriteMode: 1' not in meta_text:
        return 'spriteMode is not Single'
    if 'sprites: []' not in meta_text and 'sprites:\n    - ' in meta_text:
        return 'it declares sub-sprite slices'
    if 'textureType: 8' not in meta_text:
        return 'textureType is not Sprite'
    return ''


def is_atlas(rel: str) -> bool:
    """The glyph atlas is the one readable asset that is NOT a sprite -- the runtime reads it as a Texture2D."""
    return 'fonts/monitor_font_5x7' in rel


def readable_meta(guid: str, rel: str) -> str:
    """
    A readable asset that is still a SPRITE (v119.4, BUG-082). The game blits these with GetPixels32, but it
    may also draw them as sprites -- so the readable flag is ADDED to the sprite import settings rather than
    replacing them with a plain Texture import. Only the glyph atlas is a non-sprite.
    """
    return texture_meta(guid) if is_atlas(rel) else sprite_meta(guid, readable=True)


def repair(rel_under_assets: str, meta_path: str, readable: bool, dry: bool) -> str:
    """Rewrite one .meta if it is stale. Returns a short label, or '' when nothing was needed."""
    text = open(meta_path, encoding='utf-8').read()
    if readable:
        # Readability is the requirement, and a readable sprite is still a sprite: the only thing that can be
        # wrong with a readable asset is that it is not readable (or that it was flattened to a plain texture
        # by an earlier run of this script -- which is a real bug, so it is repaired too).
        if 'isReadable: 1' in text and (is_atlas(rel_under_assets) or 'spriteMode: 1' in text):
            return ''
        why = 'not readable, but the game reads its pixels' if 'isReadable: 1' not in text \
            else 'readable, but no longer a sprite'
        if dry:
            return why
        guid = [l.split(':', 1)[1].strip() for l in text.splitlines() if l.startswith('guid:')]
        guid = guid[0] if guid else unity_guid(rel_under_assets)
        with open(meta_path, 'w', encoding='utf-8') as fh:
            fh.write(readable_meta(guid, rel_under_assets))
        return why
    why = stale_reason(text)
    if not why:
        return ''
    if dry:
        return why
    guid = [l.split(':', 1)[1].strip() for l in text.splitlines() if l.startswith('guid:')]
    guid = guid[0] if guid else unity_guid(rel_under_assets)
    with open(meta_path, 'w', encoding='utf-8') as fh:
        fh.write(sprite_meta(guid))
    return why


def source_metas(dry: bool) -> list:
    """
    The SOURCE folder (Assets/Art/Kit) is what the player's Unity has imported since round 1. It had no
    committed metas, which is why the stale slices survived an install: Unity kept the ones it made itself.
    Every published PNG now gets a meta here too, and a stale one is replaced.
    """
    notes = []
    if not os.path.isdir(SOURCE):
        return notes
    for dirpath, _dirs, files in os.walk(SOURCE):
        for name in sorted(files):
            if not name.endswith('.png'):
                continue
            path = os.path.join(dirpath, name)
            meta_path = path + '.meta'
            rel = os.path.relpath(path, os.path.join(ROOT, 'unity')).replace('\\', '/')
            if not os.path.isfile(meta_path):
                notes.append(('missing  ' if dry else 'wrote    ') + rel)
                if not dry:
                    with open(meta_path, 'w', encoding='utf-8') as fh:
                        fh.write(readable_meta(unity_guid(rel), rel))
                continue
            # BUG-082: the authoring copy gets the SAME Read/Write setting as the mirror. The game only ever
            # loads Assets/Resources, but two copies of one asset that disagree about a hard requirement is
            # how BUG-075 happened -- and ArtKitImporter now sets both, so the committed metas must match.
            why = repair(rel, meta_path, any(rel.endswith(r) for r in READABLE), dry)
            if why:
                notes.append(('stale    ' if dry else 'repaired ') + rel + '  (' + why + ')')
    return notes


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument('--check', action='store_true')
    args = ap.parse_args()

    if not os.path.isdir(MIRROR):
        print('FAIL: %s does not exist -- run tools/art/publish_to_unity.py first' % MIRROR)
        return 1

    repair_notes = source_metas(args.check)
    for n in repair_notes[:12]:
        print('   ', n)
    if repair_notes and not args.check:
        print('    repaired/wrote %d source .meta file(s)' % len(repair_notes))

    written, missing, already, fixed_readable = 0, [], 0, 0
    for dirpath, _dirs, files in os.walk(MIRROR):
        for name in sorted(files):
            if name.endswith('.wav'):
                path = os.path.join(dirpath, name)
                rel = os.path.relpath(path, os.path.join(ROOT, 'unity')).replace('\\', '/')
                meta_path = path + '.meta'
                if os.path.isfile(meta_path):
                    already += 1
                    continue
                missing.append(rel)
                if args.check:
                    continue
                with open(meta_path, 'w', encoding='utf-8') as fh:
                    fh.write(audio_meta(unity_guid(rel)))
                written += 1
                continue
            if not name.endswith('.png'):
                continue
            path = os.path.join(dirpath, name)
            rel = os.path.relpath(path, os.path.join(ROOT, 'unity')).replace('\\', '/')
            meta_path = path + '.meta'
            is_readable = rel.endswith(READABLE) or any(rel.endswith(r) for r in READABLE)
            if os.path.isfile(meta_path):
                already += 1
                text = open(meta_path, encoding='utf-8').read()
                if is_readable and 'isReadable: 1' in text and (is_atlas(rel) or 'spriteMode: 1' in text):
                    continue                                  # readable sprite: nothing to do
                if stale_reason(text) and not (is_readable and 'isReadable: 1' in text):
                    why = stale_reason(text)
                    if args.check:
                        missing.append(rel + ' (' + why + ')')
                        continue
                    guid = [l.split(':', 1)[1].strip() for l in text.splitlines() if l.startswith('guid:')][0]
                    with open(meta_path, 'w', encoding='utf-8') as fh:
                        fh.write(readable_meta(guid, rel))
                    fixed_readable += 1
                    continue
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
        if repair_notes:
            missing.extend(n for n in repair_notes)
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
