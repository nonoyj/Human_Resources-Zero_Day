# INTRO SLOT — the client is ready for your words

**From:** the Unity/client side (FE)
**To:** BE2 / content owner
**Date:** 2026-11-27
**Version:** v102
**Reads with:** `docs/21-content-pack-format.md` §intro, `docs/90-bug-log.md` BUG-063

---

## What changed

The intro now exists on the client side. Not the words — **the slot for them**: a file format, a loader, a
card that draws it, an input rule that stops the desk moving underneath it, and a gate that fails the build
if the words cannot be shown.

`content/intro/intro_01.json` currently holds a **placeholder written by us** (five beats, 15.5 s). It is
there so the path is proven end to end, not because we think we can write it. **The words are yours.**
Replace the `text` of the beats and keep the shape; nothing else needs to change, and no code changes when
you do.

## The file

```
content/intro/intro_01.json          <- you edit this
unity/Assets/Resources/Content/intro/intro_01.json   <- generated, do not edit
```

Run `python tools/art/publish_to_unity.py` after editing, or the art gate will fail with `STALE content
file`. That is deliberate: the game reads the copy under `Resources/`, so the two must not drift.

```json
{
  "id": "intro_01",
  "version": 1,
  "beats": [
    { "speaker": "DISPATCH", "text": "NIGHT SHIFT. FOURTH FLOOR.", "hold": 3.0 }
  ]
}
```

## The rules (the gate enforces every one)

| Rule | Value | What happens if you break it |
|---|---|---|
| Beats | 1–8 | beats past 8 are dropped |
| `text` length | ≤ 90 characters | that beat is **dropped**, not cut in half |
| Characters | `A–Z`, `0–9`, space and `.,!?':;"-/()[]%&+*=<>&_` | a beat containing anything else is dropped |
| Case | write in capitals or not — it is upper-cased | — |
| `hold` | 0.5 – 8 seconds (default 2.4) | clamped |
| Whole intro | ≤ 25 seconds | later beats are cut |

Two reasons for the character list: the game paints text with its own **5×7 monitor atlas**, not a font
file, and the card is 480×270 with a 400×150 body. Curly quotes, em dashes and accents have no glyphs and
would paint as holes. Plain ASCII punctuation reads *better* in this typeface anyway.

Check your file before handing it over — this is the same check the gate runs:

```bash
python tools/comp/preview_intro.py --check     # PASS/FAIL, with a reason per bad beat
python tools/comp/preview_intro.py             # renders every beat exactly as the card will
```

The second command writes `art/kit/reference/intro_beats_2x.png`: five cards, the real atlas, the real
wrapping. If a beat reads badly there, it reads badly in the game.

## What the player can do

- **SPACE / ENTER / click / tap** — next beat
- **X / ESC** — skip the whole thing
- anything else — swallowed (the desk does not move)

The intro is shown **once per profile**, before the first shift, and `introSeen` is saved the moment it
finishes or is skipped. Pressing **R** (new run) shows it again, because a new run is a new first minute.
If the file is missing or broken the game **starts at the desk with no intro** and logs one line to the
Console — that is the intended behaviour, not a bug to report.

## What we are not asking you for

Audio. The card is silent by design in v102; the voice/music pass is v103 and it will want a one-line cue
per beat, not a rewrite. When you hand over the words, tell us whether DISPATCH should speak them (a
dispatch radio voice) or whether they should stay silent text — that decision changes v103 and not this
file.

## Where it sits

```
sorting order 230 (above the HUD at 220, above the letter at 200, above the walk fades at 210)
card          400 x 150 px at (40, 60), 1 px line border, void fill
text          56 columns, 10 px line pitch, header signal-green, body text-white, prompt text-dim
```
