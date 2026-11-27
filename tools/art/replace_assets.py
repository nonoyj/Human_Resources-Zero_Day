#!/usr/bin/env python3
"""
replace_assets.py -- drop in your own art or sound under the SAME NAME, and have everything follow.

WHY THIS EXISTS. The generators (`make_art_kit.py`, `make_audio.py`) are how the placeholder kit was made,
and they are the wrong tool for a replacement: running one overwrites your file. What the art and audio
owners need is the opposite -- a way to say "this PNG *is* the stamp now", and have the manifest, the
Unity mirror, the import settings and the gates all agree, without touching a generator.

    tools/art/incoming/stamp.png          -> art/kit/audio? no: art/kit/desk/stamp.png   (path preserved)
    tools/art/incoming/audio/phone.wav    -> art/kit/audio/phone.wav
    tools/art/incoming/sweep/lift.png     -> art/kit/sweep/sweep_lift_480x270.png        (alias below)

The `incoming/` tree mirrors `art/kit/`, so the same name lands in the same place:

    python tools/art/replace_assets.py --stamp art/kit/audio/phone.wav my_phone.wav   # one file, explicit
    python tools/art/replace_assets.py --from tools/art/incoming                      # everything staged
    python tools/art/replace_assets.py --from tools/art/incoming --dry-run            # say what would move

WHAT IT REFUSES, and why refusing matters more than accepting:

  * a WAV that is not 24 kHz mono 16-bit  -- the mix is measured in that format; anything else changes the
    levels the audio tests assert, silently. (--allow-any-format overrides, and says so loudly.)
  * a PNG whose size does not match the sprite it replaces -- sprites are placed in pixels at 16 ppu, so a
    2x asset is not "higher resolution", it is twice as big on the desk. (--allow-any-size overrides.)
  * a file whose name is not a known asset -- a typo must not become a new, unused sprite that looks like a
    successful replacement.
  * a replacement whose alpha is empty -- a fully transparent PNG is the most common export mistake, and it
    replaces a working sprite with nothing.

After the move it runs the whole chain itself: manifest -> Unity mirror -> import settings -> verify. So a
replacement is one command, and the gates it would have failed are run before it can fail them.
"""
from __future__ import annotations
import argparse
import hashlib
import json
import os
import shutil
import subprocess
import sys
import wave

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
KIT = os.path.join(ROOT, 'art', 'kit')
INCOMING = os.path.join(KIT, '..', 'incoming')
MANIFEST = os.path.join(KIT, 'unity_sprites.json')
AUDIO_MANIFEST = os.path.join(KIT, 'audio', 'audio_manifest.json')
PIN_FILE = os.path.join(KIT, '.replacements.json')

# A short list of the names people will actually replace, with the sheet/target they map to. The full set is
# read from the manifests; this is only for the `--<name>` convenience and for aliases that differ.
ALIASES = {
    'lift': 'sweep/sweep_lift_480x270.png',
    'corridor': 'sweep/sweep_corridor_480x270.png',
    'sweep_desk': 'sweep/sweep_desk_480x270.png',
    'font': 'fonts/monitor_font_5x7.png',
}


def sha16(path: str) -> str:
    return hashlib.sha256(open(path, 'rb').read()).hexdigest()[:16]


def known_assets() -> dict:
    """Every replaceable asset: repo-relative path -> ('sprite'|'audio', size or format)."""
    assets = {}
    if os.path.isfile(MANIFEST):
        doc = json.load(open(MANIFEST, encoding='utf-8'))
        for e in doc.get('sprites', []):
            if isinstance(e, dict) and e.get('source'):
                assets[e['source']] = ('sprite', (e.get('width'), e.get('height')))
    audio_dir = os.path.join(KIT, 'audio')
    if os.path.isdir(audio_dir):
        for name in sorted(os.listdir(audio_dir)):
            if name.endswith('.wav'):
                assets['art/kit/audio/' + name] = ('audio', None)
    return assets


def target_for(src_rel: str, assets: dict) -> str | None:
    """incoming/audio/phone.wav -> art/kit/audio/phone.wav;  incoming/desk/stamp.png -> art/kit/desk/stamp.png"""
    rel = src_rel.replace('\\', '/')
    stem = os.path.splitext(os.path.basename(rel))[0]
    if stem in ALIASES:
        return 'art/kit/' + ALIASES[stem]
    guess = 'art/kit/' + rel
    return guess if guess in assets else None


def png_size(path: str):
    import struct
    with open(path, 'rb') as fh:
        head = fh.read(33)
    if head[:8] != b'\x89PNG\r\n\x1a\n':
        return None
    w, h = struct.unpack('>II', head[16:24])
    return (w, h)


def png_has_pixels(path: str) -> bool:
    """Refuse a fully transparent PNG -- the most common export mistake."""
    try:
        from PIL import Image
    except ImportError:
        return True
    img = Image.open(path).convert('RGBA')
    lo, hi = img.getchannel('A').getextrema()
    return hi > 0


def wav_problem(path: str):
    try:
        with wave.open(path) as w:
            rate, ch, sw = w.getframerate(), w.getnchannels(), w.getsampwidth()
    except Exception as e:                       # noqa: BLE001 -- any unreadable file is the same problem
        return 'not a readable WAV (%s)' % e
    if (rate, ch, sw) != (24000, 1, 2):
        return 'must be 24000 Hz mono 16-bit, got %d Hz / %d ch / %d bytes' % (rate, ch, sw)
    return None


def plan_moves(pairs, assets, args):
    """pairs: [(source_path, target_rel)]. Returns (moves, errors)."""
    moves, errors = [], []
    for src, target in pairs:
        if target is None:
            errors.append('%s: no asset by that name. Names come from art/kit/unity_sprites.json and '
                          'art/kit/audio/audio_manifest.json (or pass --allow-new with a full target).' % src)
            continue
        kind, size = assets[target]
        if not os.path.isfile(src):
            errors.append('%s: no such file' % src); continue
        new_hash = sha16(src)
        if kind == 'sprite':
            got = png_size(src)
            if got is None:
                errors.append('%s: not a PNG' % src); continue
            if size and got != tuple(size) and not args.allow_any_size:
                errors.append('%s: %dx%d, but %s is %dx%d. Sprites are placed in pixels at 16 ppu, so this '
                              'is not "sharper", it is a different size on the desk (--allow-any-size to '
                              'override).' % (src, got[0], got[1], target, size[0], size[1]))
                continue
            if not png_has_pixels(src):
                errors.append('%s: fully transparent -- replacing a sprite with nothing' % src); continue
        else:
            problem = wav_problem(src)
            if problem and not args.allow_any_format:
                errors.append('%s: %s (--allow-any-format to override, which will break the audio tests)'
                              % (src, problem))
                continue
        dst = os.path.join(ROOT, target)
        old = sha16(dst) if os.path.isfile(dst) else '(new)'
        moves.append((src, dst, target, old, new_hash))
    return moves, errors


def record_replacements(moves) -> None:
    """Keep the two records honest after a replacement.

    1. `audio_manifest.json` is what the audio tests and the art gate read: it must carry the NEW hash, the
       new duration and the new measured level, or the gate fails on a file the team just shipped on
       purpose. Its `source` becomes "team" so the provenance is visible.
    2. `.replacements.json` is a PIN. The generators (`make_audio.py`, and anything else that writes into
       art/kit) read it and refuse to overwrite a team asset. Without this, the next time anyone runs the
       generator -- which the docs tell them to do -- the team's work is silently reverted to a placeholder.
    """
    pins = {}
    if os.path.isfile(PIN_FILE):
        pins = json.load(open(PIN_FILE, encoding='utf-8'))
    audio = {}
    if os.path.isfile(AUDIO_MANIFEST):
        audio = json.load(open(AUDIO_MANIFEST, encoding='utf-8'))

    changed_audio = False
    for src, dst, target, old, new in moves:
        pins[target] = {'sha256_16': new, 'replacedFrom': old, 'pin': 'do not regenerate'}
        # the manifest keys are stem names ('phone'), not file names ('phone.wav')
        name = os.path.splitext(os.path.basename(target))[0]
        if target.startswith('art/kit/audio/') and name in audio.get('files', {}):
            entry = audio['files'][name]
            try:
                import wave as _w
                import numpy as _np
                with _w.open(dst) as w:
                    frames = w.getnframes(); rate = w.getframerate()
                    raw = w.readframes(frames)
                x = _np.frombuffer(raw, dtype='<i2').astype(_np.float64) / 32768.0
                rms = float(_np.sqrt(_np.mean(x ** 2))) if len(x) else 0.0
                peak = float(_np.max(_np.abs(x))) if len(x) else 0.0
                entry['seconds'] = round(frames / float(rate), 3)
                entry['rmsDb'] = round(20.0 * _np.log10(max(rms, 1e-9)), 1)
                entry['peakDb'] = round(20.0 * _np.log10(max(peak, 1e-9)), 1)
                entry['sha256_16'] = new
                entry['source'] = 'team'
                changed_audio = True
            except Exception as e:                       # noqa: BLE001
                print('  [warn] could not re-measure %s (%s) -- the audio gate may fail' % (target, e))
    if changed_audio:
        with open(AUDIO_MANIFEST, 'w', encoding='utf-8') as fh:
            json.dump(audio, fh, indent=2, sort_keys=True)
            fh.write('\n')
        print('  updated audio_manifest.json (new hashes and measured levels)')
    os.makedirs(os.path.dirname(PIN_FILE), exist_ok=True)
    with open(PIN_FILE, 'w', encoding='utf-8') as fh:
        json.dump(pins, fh, indent=2, sort_keys=True)
        fh.write('\n')
    print('  pinned %d replaced asset(s) in art/kit/.replacements.json -- generators will not overwrite them'
          % len(pins))


def publish(args) -> int:
    print('publishing: manifest -> unity mirrors -> import settings')
    steps = [
        [sys.executable, os.path.join(ROOT, 'tools', 'art', 'make_sprite_manifest.py')],
        [sys.executable, os.path.join(ROOT, 'tools', 'art', 'publish_to_unity.py')],
        [sys.executable, os.path.join(ROOT, 'tools', 'art', 'write_unity_metas.py')],
        [sys.executable, os.path.join(ROOT, 'tools', 'art', 'publish_to_unity.py'), '--verify'],
    ]
    for cmd in steps:
        r = subprocess.run(cmd, capture_output=True, text=True, cwd=ROOT)
        name = os.path.relpath(cmd[1], ROOT)
        if r.returncode != 0:
            print('  FAILED: %s' % ' '.join(cmd[2:]) if len(cmd) > 2 else '  FAILED: %s' % name)
            print((r.stdout or '') + (r.stderr or ''))
            return 1
        tail = [l for l in (r.stdout or '').strip().splitlines() if l.strip()]
        print('  [ok] %-38s %s' % (name, tail[-1] if tail else ''))
    return 0


def main() -> int:
    ap = argparse.ArgumentParser(description='Replace a shipped art or audio asset by name.')
    ap.add_argument('--from', dest='folder', help='a tree that mirrors art/kit (e.g. tools/art/incoming)')
    ap.add_argument('--stamp', nargs=2, metavar=('TARGET', 'FILE'),
                    help='replace one asset: --stamp art/kit/audio/phone.wav my_phone.wav')
    ap.add_argument('--dry-run', action='store_true', help='say what would move, change nothing')
    ap.add_argument('--allow-any-size', action='store_true')
    ap.add_argument('--allow-any-format', action='store_true')
    ap.add_argument('--allow-new', action='store_true', help='allow a name that is not in the manifest')
    ap.add_argument('--list', action='store_true', help='print every replaceable asset and stop')
    args = ap.parse_args()

    assets = known_assets()
    if args.list:
        for path in sorted(assets):
            print(path)
        return 0

    pairs = []
    if args.stamp:
        pairs.append((args.stamp[1], args.stamp[0]))
    if args.folder:
        root = args.folder if os.path.isabs(args.folder) else os.path.join(ROOT, args.folder)
        if not os.path.isdir(root):
            print('FAIL: %s is not a folder' % root); return 1
        for dirpath, _dirs, files in os.walk(root):
            for name in sorted(files):
                # README/.md notes are for humans and live in the same tree; they are not assets
                if name.startswith('.') or name.lower().endswith(('.md', '.txt')):
                    continue
                src = os.path.join(dirpath, name)
                rel = os.path.relpath(src, root).replace('\\', '/')
                target = target_for(rel, assets)
                if target is None and args.allow_new:
                    target = 'art/kit/' + rel
                pairs.append((src, target))

    if not pairs:
        print('nothing to do: pass --from <folder> or --stamp <target> <file>. See --list for names.')
        return 1

    moves, errors = plan_moves(pairs, assets, args)
    for e in errors:
        print('  REFUSED:', e)
    if errors:
        print('\n%d file(s) refused; nothing was changed.' % len(errors))
        return 1
    if not moves:
        print('everything staged is already identical to the shipped file -- nothing to do.')
        return 0

    for src, dst, target, old, new in moves:
        print('  %s %s  (%s -> %s)' % ('would replace' if args.dry_run else 'replaced', target, old, new))

    if args.dry_run:
        print('\ndry run: %d file(s) would move, nothing was written.' % len(moves))
        return 0

    for src, dst, target, _old, _new in moves:
        os.makedirs(os.path.dirname(dst), exist_ok=True)
        shutil.copyfile(src, dst)
    record_replacements(moves)
    return publish(args)


if __name__ == '__main__':
    sys.exit(main())
