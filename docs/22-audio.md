# 22 -- AUDIO

**What the game sounds like, why, and how to change it.** Added 2026-11-27 with v105.

---

## 1. The design in one paragraph

The game is a CRT terminal on a desk in an office at night. Every sound in it is a machine, a phone, paper,
or a building that is still running. So the mix is built the way that room sounds: **a bed you stop
hearing, and events you feel.** There is no music. The room is already playing before the player can do
anything, and it changes as the run does -- it flattens and gains hiss as corruption rises -- so the game
gets worse to be in as the story does.

## 2. The nine sounds, all generated from code

`python tools/audio/make_audio.py` regenerates every one of them: 24 kHz mono 16-bit, deterministic,
no samples and no licences.

| sound | what it is | rms | role |
|---|---|---|---|
| `room` | filtered hiss, 50 Hz mains hum, a fan two rooms away (8 s loop) | -32.1 | the bed |
| `crt` | 3,906 Hz + 7,812 Hz supply whine, very quiet (4 s loop) | -34.5 | the monitor is on |
| `stamp` | rubber stamp on a wooden desk: click, thump, wooden ring | -21.5 | the verdict |
| `thud` | a low tone sagging in pitch, then a knock | -21.1 | a wrong call |
| `phone` | two-tone bell, two rings, handset pickup | -23.2 | an email / the hotline |
| `drawer` | metal drawer: pull, grind, four rattles, stop | -25.2 | opening something |
| `page` | a page turned, 400 Hz sweeping to 7 kHz | -25.2 | a file arrives |
| `step` | shoe on carpet, almost no click | -27.9 | the after-hours walk |
| `key` | mechanical key, two impulses 3 ms apart | -26.9 | menus; pitched down for "denied" |

## 3. The mix rules (and they are tests)

`unity/Assets/Scripts/Core/Rules/AudioRules.cs` -- pure C#, no UnityEngine, checked by
`tools/CoreTests/AudioTests.cs` (6 checks, run with the other 152):

- **Event cues clear 6 dB over the bed; texture cues clear 4 dB.** A footstep as loud as a stamp is a
  broken game; a cue below the bed is not quiet, it is absent.
- **Muted means exactly `0.0` gain**, not 0.001. A muted player gets silence, not a rumour.
- **Corruption 0 is bit-exactly neutral**: pitch `1f`, no added hiss. The room flattens monotonically to
  1.5 semitones flat at corruption 3, and clamps there.
- **The same cue inside 40 ms is one sound.** Keyed by *sample + pitch*, so "denied" and "key" (one clip,
  two pitches) suppress each other, and a page turn after a stamp never does.
- **Deny is a fifth down**, not a new file: a refusal must not sound like an acceptance.

## 4. Where it is wired

`Game/Interaction/DeskBootstrap.cs` builds `GameAudio` **before** the intro, so the intro card plays over a
room that is already running. Then:

| moment | cue |
|---|---|
| a new applicant's file arrives | `page` |
| the player inspects a prop | `drawer` |
| a verdict lands | `stamp` (+ `thud` under it when wrong) |
| an action is refused / out of shifts | `deny` (key, pitched down) |
| each floor of the after-hours walk | `step` |
| corruption rises | the bed detunes and gains hiss |

Settings live in the save (`profile.bedVolume`, `profile.sfxVolume`, `profile.muted`) and are applied on
load, so a player who muted the game last night is still muted tonight. `DeskBootstrap.ApplyAudioSettings`
is the single entry point for an options screen.

## 5. Judging it without Unity

- `python tools/audio/preview_audio.py` -> `art/kit/reference/audio_sheet.png` (levels, the bed's loop
  seam, a spectrogram per cue) **and** `%TEMP%\desk_minute.wav` (written by `tools/audio/preview_audio.py`) -- 22 seconds of the bed plus the
  nine cues in the order a player meets them. That file is a reference, not a game asset.
- `python tools/audio/make_audio.py --check` -> fails if a WAV is missing, stale or the wrong format.
- The art gate runs both, plus: every WAV has an audio `.meta` in Resources, and every cue name in the C#
  enum resolves to a published file.

## 6. What is deliberately NOT here

- **The spoken intro.** Decided (ADR-009): DISPATCH reads the five beats, BE2 produces the clips. The card
  works silent until then, so nothing waits on the voice.
- **Music.** Not in this game. If a track is ever wanted it belongs on its own bus with its own duck, and
  it should be one tone, not a soundtrack.
- **3D/spatial audio.** Mono, flat, close. A phone speaker cannot do more and a night office does not want
  more.
