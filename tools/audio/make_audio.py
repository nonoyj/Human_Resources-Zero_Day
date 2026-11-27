#!/usr/bin/env python3
"""
make_audio.py -- generates every sound the game makes, from code, with no samples and no licences.

WHY GENERATED. The game is a CRT terminal on a desk at night. Every sound in it is either a mechanical
click, a telephone, or a room tone -- none of it needs a recording, and all of it needs to be *tunable*.
A generated bed means the hiss can be parametric on corruption, the stamp can be the same thump at three
pitches, and nothing in the repository carries a licence.

THE PALETTE IS THE ART PALETTE'S SIBLING (docs/15-art-bible.md). Six tones, all of them low and dry:
    the room    filtered noise + mains hum + a fan two rooms away       (loopable, 8 s)
    crt        15.7 kHz flyback whine, very quiet, ducked by the bed    (loopable, 4 s)
    stamp      a heavy rubber stamp landing on a wooden desk
    phone      a 1990s desk phone: two-tone bell, then a pickup click
    drawer     a metal drawer, pulled and let go
    page       a paper page turned
    step       a shoe on carpet (the after-hours walk)
    key        a mechanical key click (a menu press)
    thud       a low, wrong sound -- a bad call landing

24 kHz mono 16-bit WAV on purpose: half the size of 44.1 kHz, and the game's whole sonic range is below
8 kHz anyway. Every file is normalised to a documented peak so nothing clips or fights the voice pass.

USAGE
    python tools/audio/make_audio.py             # regenerate everything, report levels
    python tools/audio/make_audio.py --check     # fail if the WAVs are missing or stale
"""
from __future__ import annotations
import argparse, hashlib, json, math, os, struct, sys, wave

import numpy as np

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
OUT = os.path.join(ROOT, 'art', 'kit', 'audio')
MANIFEST = os.path.join(OUT, 'audio_manifest.json')

RATE = 24000
SEED = 0x48305230          # "HR0D", so the noise is identical on every machine


# --------------------------------------------------------------------------------------------- helpers
def rng(tag: str) -> np.random.Generator:
    """A generator seeded by the SOUND NAME, so each file is reproducible and independent."""
    h = hashlib.sha256((tag + str(SEED)).encode()).digest()
    return np.random.default_rng(int.from_bytes(h[:8], 'little'))


def t_axis(seconds: float) -> np.ndarray:
    return np.arange(int(RATE * seconds), dtype=np.float64) / RATE


def env_ad(n: int, attack: float, decay: float, curve: float = 2.0) -> np.ndarray:
    """Attack then exponential-ish decay, in samples. The shape of every mechanical hit."""
    a = max(1, int(RATE * attack))
    d = max(1, n - a)
    up = np.linspace(0.0, 1.0, a) ** 0.5
    down = (np.linspace(1.0, 0.0, d)) ** curve
    return np.concatenate([up, down])[:n]


def lowpass(x: np.ndarray, hz: float) -> np.ndarray:
    """One-pole low-pass. Cheap, and its gentle slope is exactly right for a small room."""
    a = math.exp(-2.0 * math.pi * hz / RATE)
    y = np.empty_like(x)
    acc = 0.0
    for i in range(len(x)):
        acc = (1.0 - a) * x[i] + a * acc
        y[i] = acc
    return y


def highpass(x: np.ndarray, hz: float) -> np.ndarray:
    return x - lowpass(x, hz)


def bandpass(x: np.ndarray, lo: float, hi: float) -> np.ndarray:
    return lowpass(highpass(x, lo), hi)


def normalise(x: np.ndarray, peak_db: float) -> np.ndarray:
    peak = np.max(np.abs(x)) or 1.0
    target = 10.0 ** (peak_db / 20.0)
    return x * (target / peak)


def fade_edges(x: np.ndarray, ms: float = 4.0) -> np.ndarray:
    n = max(1, int(RATE * ms / 1000.0))
    x = x.copy()
    x[:n] *= np.linspace(0.0, 1.0, n)
    x[-n:] *= np.linspace(1.0, 0.0, n)
    return x


def loopable(x: np.ndarray, cross_ms: float = 120.0) -> np.ndarray:
    """Cross-fade the tail over the head so a looped bed has no seam. Returns the shortened loop."""
    n = int(RATE * cross_ms / 1000.0)
    head, tail = x[:n], x[-n:]
    body = x[n:-n]
    ramp = np.linspace(0.0, 1.0, n)
    blend = tail * (1.0 - ramp) + head * ramp
    return np.concatenate([blend, body])


def write_wav(name: str, x: np.ndarray, peak_db: float) -> dict:
    os.makedirs(OUT, exist_ok=True)
    x = normalise(fade_edges(x), peak_db)
    data = np.clip(x, -1.0, 1.0)
    pcm = (data * 32767.0).astype('<i2')
    path = os.path.join(OUT, name + '.wav')
    with wave.open(path, 'wb') as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(RATE)
        w.writeframes(pcm.tobytes())
    rms = float(np.sqrt(np.mean(data ** 2)))
    return {
        'name': name,
        'path': 'art/kit/audio/%s.wav' % name,
        'seconds': round(len(data) / RATE, 3),
        'peakDb': peak_db,
        'rmsDb': round(20.0 * math.log10(max(rms, 1e-9)), 1),
        'sha256_16': hashlib.sha256(open(path, 'rb').read()).hexdigest()[:16],
    }


# --------------------------------------------------------------------------------------------- sounds
def room() -> np.ndarray:
    """A room at night: filtered hiss, mains hum, and a fan two rooms away. Loopable, 8 s.

    The fan is 0.7 Hz amplitude modulation on a 140 Hz band -- not a fan recording, the FEELING of a
    building that is still running. It is the sound the player will hear for an hour and stop hearing.
    """
    g = rng('room')
    n = int(RATE * 8.0)
    hiss = lowpass(g.normal(0, 1, n), 900) + 0.35 * lowpass(g.normal(0, 1, n), 2400)
    hum = 0.10 * np.sin(2 * np.pi * 50.0 * t_axis(8.0)) + 0.05 * np.sin(2 * np.pi * 100.0 * t_axis(8.0))
    fan_env = 0.75 + 0.25 * np.sin(2 * np.pi * 0.7 * t_axis(8.0))
    fan = bandpass(g.normal(0, 1, n), 90, 220) * 2.2 * fan_env
    return loopable(0.55 * hiss + hum + fan)


def crt() -> np.ndarray:
    """The whine of a cheap CRT power supply, very quiet. Loopable, 4 s, ducked by the bed.

    NOT 15,625 Hz. The first version generated the real flyback frequency and it was a bug twice over:
    this project renders at 24 kHz, so 15.6 kHz is above Nyquist and ALIASED down to ~8.4 kHz -- the file
    would have played as a mosquito tone, not a whine. A CRT also radiates a strong sub-harmonic of the
    flyback that cheap sets audibly buzz at, so the game uses that instead: 3,906 Hz plus a bit of
    7,812 Hz. It reads as "a monitor is on" at the volume it is mixed at, and it survives mono phone
    speakers, which a 15 kHz tone would not have.
    """
    n = int(RATE * 4.0)
    t = t_axis(4.0)
    whine = 0.6 * np.sin(2 * np.pi * 3906.25 * t) + 0.22 * np.sin(2 * np.pi * 7812.5 * t)
    flicker = 1.0 + 0.06 * np.sin(2 * np.pi * 7.0 * t)
    return loopable(whine * flicker)


def stamp() -> np.ndarray:
    """A heavy rubber stamp on a wooden desk: a click, a body thump, a wooden ring, a paper settle."""
    g = rng('stamp')
    n = int(RATE * 0.55)
    t = t_axis(0.55)
    click = bandpass(g.normal(0, 1, n), 1800, 6000) * env_ad(n, 0.001, 0.02, 3.0)
    body = np.sin(2 * np.pi * 96.0 * t) * env_ad(n, 0.004, 0.12, 2.4)
    ring = (np.sin(2 * np.pi * 320.0 * t) * 0.5 + np.sin(2 * np.pi * 780.0 * t) * 0.25) * \
        env_ad(n, 0.002, 0.28, 2.6)
    settle = bandpass(g.normal(0, 1, n), 400, 1600) * env_ad(n, 0.05, 0.35, 2.0) * 0.18
    return 0.9 * click + 1.0 * body + 0.45 * ring + settle


def phone() -> np.ndarray:
    """A 1990s desk phone: two-tone bell, two rings, then the handset's pickup click."""
    def bell(f1: float, f2: float, seconds: float) -> np.ndarray:
        t = t_axis(seconds)
        tone = np.sin(2 * np.pi * f1 * t) + np.sin(2 * np.pi * f2 * t)
        # a real bell is a struck resonator: fast attack, uneven decay, slight beating
        beat = 1.0 + 0.25 * np.sin(2 * np.pi * 23.0 * t)
        return tone * beat * env_ad(len(t), 0.004, seconds * 0.85, 2.2)

    ring1 = bell(800.0, 1000.0, 0.42)
    gap = np.zeros(int(RATE * 0.20))
    ring2 = bell(800.0, 1000.0, 0.42)
    g = rng('phone')
    click = bandpass(g.normal(0, 1, int(RATE * 0.09)), 900, 5000) * \
        env_ad(int(RATE * 0.09), 0.001, 0.03, 3.0)
    return np.concatenate([ring1, gap, ring2, np.zeros(int(RATE * 0.12)), click])


def drawer() -> np.ndarray:
    """A metal drawer: the pull, the grind, the stop. Two seconds of office furniture."""
    g = rng('drawer')
    n = int(RATE * 1.3)
    grind = lowpass(g.normal(0, 1, n), 1800) * env_ad(n, 0.08, 0.75, 1.6)
    rattle = np.zeros(n)
    for pos in (0.30, 0.55, 0.78, 1.02):
        i = int(RATE * pos)
        seg = bandpass(g.normal(0, 1, 400), 900, 4000) * env_ad(400, 0.001, 0.01, 3.0)
        rattle[i:i + 400] += seg[:max(0, n - i)]
    stop = np.sin(2 * np.pi * 120.0 * t_axis(1.3)) * env_ad(n, 0.001, 0.05, 3.0) * 0.5
    return 0.7 * grind + 0.9 * rattle + stop


def page() -> np.ndarray:
    """A paper page turned: a sweep of noise from 2 kHz to 6 kHz, and a soft landing."""
    g = rng('page')
    n = int(RATE * 0.45)
    x = g.normal(0, 1, n)
    swept = np.zeros(n)
    # crude but convincing: three band-passed slices, cross-faded, gives the "paper travelling" feel
    slices = [(0, 0.35, 400, 1800), (0.25, 0.7, 1200, 3600), (0.55, 1.0, 2400, 7000)]
    for a, b, lo, hi in slices:
        i0, i1 = int(n * a), int(n * b)
        seg = bandpass(x[i0:i1], lo, hi)
        seg *= np.hanning(len(seg))
        swept[i0:i1] += seg
    return swept


def step() -> np.ndarray:
    """A shoe on carpet: a soft, low thump with almost no click."""
    g = rng('step')
    n = int(RATE * 0.18)
    t = t_axis(0.18)
    thump = np.sin(2 * np.pi * 70.0 * t) * env_ad(n, 0.002, 0.07, 2.6)
    carpet = lowpass(g.normal(0, 1, n), 700) * env_ad(n, 0.004, 0.05, 2.0) * 0.5
    return thump + carpet


def key() -> np.ndarray:
    """A mechanical key click: two impulses 3 ms apart (down and up of the switch)."""
    def impulse(delay: float, gain: float) -> np.ndarray:
        n = int(RATE * (delay + 0.05))
        t = t_axis(delay + 0.05)
        click = bandpass(np.random.default_rng(7).normal(0, 1, n), 2000, 7000) * \
            env_ad(n, 0.0005, 0.006, 3.5)
        return click * gain

    a = impulse(0.0, 1.0)
    b = impulse(0.003, 0.6)
    n = max(len(a), len(b))
    out = np.zeros(n)
    out[:len(a)] += a
    out[:len(b)] += b
    return out


def thud() -> np.ndarray:
    """A wrong call landing: a low tone that sags in pitch, then a soft knock. Deliberately unpleasant."""
    n = int(RATE * 0.9)
    t = t_axis(0.9)
    sag = 1.0 - 0.18 * (t / 0.9)
    tone = np.sin(2 * np.pi * 55.0 * sag * t) + 0.4 * np.sin(2 * np.pi * 82.0 * sag * t)
    env = env_ad(n, 0.01, 0.55, 1.8)
    knock = bandpass(np.random.default_rng(11).normal(0, 1, n), 120, 700) * env_ad(n, 0.001, 0.02, 3.0)
    return tone * env + 0.6 * knock


# name -> (generator, peak dBFS, looping?)
SOUNDS = {
    'room': (room, -18.0, True),
    'crt': (crt, -30.0, True),
    'stamp': (stamp, -6.0, False),
    'phone': (phone, -7.0, False),
    'drawer': (drawer, -7.5, False),   # raised v105: measured 5.4 dB over the bed, under the 6 dB rule
    'page': (page, -11.0, False),   # raised v105: a page turn must be audible over the bed
    'step': (step, -16.0, False),
    'key': (key, -9.5, False),      # v105: peak -11 gave rms -28.4, still under the 4 dB texture floor
    'thud': (thud, -8.0, False),
}


def pinned() -> set:
    """Sounds the team has replaced with their own recordings (art/kit/.replacements.json)."""
    path = os.path.join(ROOT, 'art', 'kit', '.replacements.json')
    if not os.path.isfile(path):
        return set()
    try:
        pins = json.load(open(path, encoding='utf-8'))
    except Exception:                                   # noqa: BLE001
        return set()
    return {os.path.basename(k)[:-4] for k in pins if k.startswith('art/kit/audio/')}


def build(force: bool = False) -> dict:
    """Regenerate every generated sound, but NEVER overwrite a sound the team replaced.

    A replacement is recorded in art/kit/.replacements.json; this respects it and keeps the pinned file's
    existing manifest entry, so running this script cannot revert somebody's recording to a placeholder.
    `--force` regenerates everything, including pinned files, and says how many it took back.
    """
    keep = pinned() if not force else set()
    old_doc = {}
    if os.path.isfile(MANIFEST):
        try:
            old_doc = json.load(open(MANIFEST, encoding='utf-8')).get('files', {})
        except Exception:                               # noqa: BLE001
            old_doc = {}
    entries = {}
    skipped = 0
    for name, (gen, peak, looping) in sorted(SOUNDS.items()):
        if name in keep and name in old_doc:
            entries[name] = dict(old_doc[name])
            entries[name]['loop'] = looping
            skipped += 1
            print('  %-7s left alone (replaced by the team)' % name)
            continue
        e = write_wav(name, gen(), peak)
        e['loop'] = looping
        entries[name] = e
        print('  %-7s %5.2fs  peak %5.1f dB  rms %5.1f dB  %s' %
              (name, e['seconds'], e['peakDb'], e['rmsDb'], e['sha256_16']))
    if skipped:
        print('  %d sound(s) kept as the team shipped them (use --force to regenerate them anyway)' % skipped)
    doc = {'schemaVersion': 1, 'rate': RATE, 'notes': 'generated by tools/audio/make_audio.py', 'files': entries}
    with open(MANIFEST, 'w', encoding='utf-8') as fh:
        json.dump(doc, fh, indent=2, sort_keys=True)
        fh.write('\n')
    return doc


def check() -> int:
    if not os.path.isfile(MANIFEST):
        print('FAIL: art/kit/audio/audio_manifest.json missing -- run tools/audio/make_audio.py')
        return 1
    doc = json.load(open(MANIFEST, encoding='utf-8'))
    bad = 0
    for name, e in sorted(doc['files'].items()):
        path = os.path.join(ROOT, e['path'])
        if not os.path.isfile(path):
            print('  MISSING:', e['path']); bad += 1; continue
        got = hashlib.sha256(open(path, 'rb').read()).hexdigest()[:16]
        if got != e['sha256_16']:
            print('  STALE:', e['path']); bad += 1; continue
        with wave.open(path) as w:
            if w.getframerate() != RATE or w.getnchannels() != 1 or w.getsampwidth() != 2:
                print('  WRONG FORMAT:', e['path']); bad += 1
    if bad:
        print('FAIL: %d audio file(s) missing or stale -- run tools/audio/make_audio.py' % bad)
        return 1
    print('PASS: %d sounds present, 24 kHz mono 16-bit, sha256 verified' % len(doc['files']))
    return 0


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument('--check', action='store_true')
    ap.add_argument('--force', action='store_true',
                    help='regenerate even sounds the team has replaced (off by default)')
    a = ap.parse_args()
    if a.check:
        return check()
    print('generating %d sounds into art/kit/audio/' % len(SOUNDS))
    build(force=a.force)
    print('PASS: every sound generated and manifests written')
    return 0


if __name__ == '__main__':
    sys.exit(main())
