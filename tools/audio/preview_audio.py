#!/usr/bin/env python3
"""
preview_audio.py -- draw what the sounds LOOK like, so a level problem is visible before it is heard.

Three panels, left to right:
    1. every sound's RMS, as a bar, against the bed -- the mix at a glance
    2. a waveform of the bed, to show the loop cross-fade actually removes the seam
    3. a spectrogram of each one-off, so "the stamp is a thump with a click on top" is verifiable

    python tools/audio/preview_audio.py                 # writes art/kit/reference/audio_sheet.png
"""
from __future__ import annotations
import json, math, os, sys, wave

import numpy as np
from PIL import Image, ImageDraw

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
KIT = os.path.join(ROOT, 'art', 'kit')
AUDIO = os.path.join(KIT, 'audio')
OUT = os.path.join(KIT, 'reference', 'audio_sheet.png')

BG = (14, 16, 20)
GRID = (38, 44, 52)
INK = (208, 214, 220)
DIM = (128, 138, 148)
GREEN = (24, 224, 176)
AMBER = (230, 178, 64)
RED = (222, 88, 88)

W, H = 720, 520


def read_wav(path):
    with wave.open(path) as w:
        n = w.getnframes()
        raw = w.readframes(n)
        rate = w.getframerate()
    x = np.frombuffer(raw, dtype='<i2').astype(np.float32) / 32768.0
    return x, rate


def rms_db(x):
    return 20.0 * math.log10(max(float(np.sqrt(np.mean(x ** 2))), 1e-9))


def spectrogram(x, bins=48):
    """A tiny STFT, plotted as columns of loudness per band. Enough to see a thump from a hiss."""
    win = 512
    if len(x) < win:
        x = np.pad(x, (0, win - len(x)))
    cols = 64
    step = max(1, (len(x) - win) // cols)
    rows = []
    for c in range(cols):
        seg = x[c * step:c * step + win] * np.hanning(win)
        spec = np.abs(np.fft.rfft(seg))[:bins * 3]
        # group into `bins` logarithmic-ish bands
        band = [float(np.mean(spec[i * 3:(i + 1) * 3])) for i in range(bins)]
        rows.append(band)
    arr = np.array(rows).T
    arr = 20.0 * np.log10(np.maximum(arr, 1e-6))
    lo, hi = arr.min(), arr.max()
    return (arr - lo) / max(hi - lo, 1e-6)


def mixdown():
    """A 22-second "minute at the desk": the bed plus the cues in the order a player meets them.

    This exists so the mix can be JUDGED before Unity is opened -- levels, spacing, whether the phone is
    too loud, whether the room is audible at all. It is a reference file, not a game asset: the game never
    plays it. Writes art/kit/reference/desk_minute.wav.
    """
    room, rate = read_wav(os.path.join(AUDIO, 'room.wav'))
    crt, _ = read_wav(os.path.join(AUDIO, 'crt.wav'))
    dur = int(rate * 22)
    bed = np.resize(room, dur) * 0.35 + np.resize(crt, dur) * 0.12
    mix = bed.copy()
    # the order a player meets the sounds: file arrives, letter opened, menu key, verdict, phone,
    # a wrong call, then three footsteps on the after-hours walk
    events = [(2.0, 'page'), (4.0, 'drawer'), (6.5, 'key'), (8.0, 'stamp'),
              (11.0, 'phone'), (13.5, 'thud'), (15.5, 'step'), (16.0, 'step'), (16.5, 'step')]
    for t, name in events:
        s, _ = read_wav(os.path.join(AUDIO, name + '.wav'))
        i = int(t * rate)
        seg = s * 0.55
        mix[i:i + len(seg)] += seg[:max(0, dur - i)]
    mix = mix / max(1.0, float(np.max(np.abs(mix))) * 1.02)
    out = os.path.join(KIT, 'reference', 'desk_minute.wav')
    with wave.open(out, 'wb') as w:
        w.setnchannels(1); w.setsampwidth(2); w.setframerate(rate)
        w.writeframes((mix * 32767).astype('<i2').tobytes())
    print('wrote %s (22 s, the bed plus 9 cues)' % os.path.relpath(out, ROOT))



def main():
    doc = json.load(open(os.path.join(AUDIO, 'audio_manifest.json'), encoding='utf-8'))
    files = doc['files']
    img = Image.new('RGB', (W, H), BG)
    d = ImageDraw.Draw(img)

    # ---- 1. levels ---------------------------------------------------------------------------
    d.text((16, 12), 'LEVELS  (rms, dBFS)   bed = room + crt', fill=GREEN)
    y = 34
    for name, e in sorted(files.items(), key=lambda kv: kv[1]['rmsDb'], reverse=True):
        db = e['rmsDb']
        length = int((db + 45) / 45.0 * 380)
        colour = GREEN if not e['loop'] else DIM
        if name in ('thud', 'phone'):
            colour = AMBER
        d.text((16, y + 2), name, fill=INK)
        d.rectangle([80, y, 80 + max(2, length), y + 9], fill=colour)
        d.text((470, y + 2), '%.1f dB%s' % (db, '  (loop)' if e['loop'] else ''), fill=DIM)
        y += 18
    d.text((16, y + 6), 'one-offs sit ~6-15 dB above the bed at peak; the bed is felt, not heard', fill=DIM)
    d.text((16, y + 20), 'the crt whine is deliberately the quietest thing in the game', fill=DIM)

    # ---- 2. the loop seam --------------------------------------------------------------------
    room, rate = read_wav(os.path.join(AUDIO, 'room.wav'))
    top = 250
    d.text((16, top), 'ROOM BED  (8 s, loop cross-faded)  -- no step at the wrap point', fill=GREEN)
    xs = np.linspace(0, len(room) - 1, W - 32).astype(int)
    mid = top + 60
    gain = 42.0 / max(float(np.max(np.abs(room))), 1e-6)     # auto-scale: the bed is quiet on purpose
    for i in range(len(xs) - 1):
        a, b = room[xs[i]] * gain, room[xs[i + 1]] * gain
        d.line([16 + i, mid - a, 17 + i, mid - b], fill=(90, 120, 140))
    d.line([16, mid, W - 16, mid], fill=GRID)
    d.line([16, top + 18, 16, top + 110], fill=GRID)      # the wrap point, marked
    d.text((22, top + 112), 'start', fill=DIM)
    d.text((W - 60, top + 112), 'wrap', fill=DIM)

    # ---- 3. one-offs as spectrograms ---------------------------------------------------------
    top = 390
    d.text((16, top), 'ONE-OFFS  (frequency over time; brighter = louder)', fill=GREEN)
    names = ['stamp', 'phone', 'drawer', 'page', 'step', 'key', 'thud']
    cw = (W - 32) // len(names)
    for i, name in enumerate(names):
        x, _ = read_wav(os.path.join(AUDIO, name + '.wav'))
        spec = spectrogram(x)
        bh = 80
        ox = 16 + i * cw
        for col in range(spec.shape[1]):
            for row in range(spec.shape[0]):
                v = spec[row, col]
                shade = int(20 + 235 * v ** 1.4)
                d.point((ox + col * cw // spec.shape[1], top + 20 + (bh - 1) - row * bh // spec.shape[0]),
                        fill=(shade // 3, shade, int(shade * 0.85)))
        d.text((ox, top + 106), name, fill=INK)

    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    img.save(OUT)
    print('wrote %s' % os.path.relpath(OUT, ROOT))
    mixdown()
    return 0


if __name__ == '__main__':
    sys.exit(main())
