# Art direction — where it actually stands

**Status:** active (v10, 2026-11-25) · **Supersedes nothing; read with `docs/15-art-bible.md` (palette) and
`tools/art/painterly.py` (the technique).**

This page exists so the next session does not re-derive six rounds of trial and error. It records **what
the player asked for**, **what is settled**, and — most usefully — **what has already been tried and
failed**.

---

## 1. The player's direction, in their words

| # | Instruction | State |
|---|---|---|
| 1 | Applicant busts must read as **painterly pixel humans**, not robots or mannequins | done |
| 2 | **Heads round** and human — "stick with the human touch" | done (v10) |
| 3 | Then: *"maybe the head is too round, give it a human like shape"* | done (v10) — egg skull, not a circle |
| 4 | **Shoulders and arms** per the reference samples | done (v9) |
| 5 | **Neck too long** | done (v9) — torso starts 4 px under the chin |
| 6 | **Hair too bright** — use natural colours | done (v9) |
| 7 | **Female applicants** | done (v9) — 3 of 7 humans, 3 of 7 synthetics |
| 8 | **Remove the spikes, keep the hair simple** | done (v10) |
| 9 | The flat `Lounge — LEVEL 5` look is **minigame-only** | held: sweep only |

**Palette is locked.** No new colours, ever: every pixel is an existing palette entry or a 50% dither of
two. `make_applicants.py --check` enforces it.

---

## 2. What is settled, and where it lives

- **Frame:** 192×216, 4-frame strip (neutral / breathe / fidget / blink). Never change the frame size —
  the desk layout, the manifest and the Unity importer all key off it.
- **Generator:** `tools/art/make_applicants.py`. Technique library: `tools/art/painterly.py`.
- **Structure gate:** `tools/art/check_proportions.py` — no empty rows inside the figure (a floating head
  is a hole), head aspect ≤ 1.6:1, neck ≥ 14 px average, neck must reach the shoulders. Wired into
  `Test-ArtPipeline.ps1`.
- **Cast:** 7 humans, 7 synthetics; each synthetic carries a specific artifact set. The `female` flag is
  explicit per archetype, not inferred — a designer can rebalance the cast without reading draw code.
- **Tells survive every art pass:** mirror pupils, dead-straight seam, rectangular ear, merged glasses
  temple, merged fingers, wrong-side shadow, posterised skin band, no blink, perfectly straight badge.

---

## 3. What has been tried and FAILED (do not repeat)

These are all recorded because each one looked correct in code and wrong on screen.

| Attempt | Why it failed |
|---|---|
| Dither (Bayer) over the whole face | reads as **stubble** / noise at this size |
| Shading by horizontal position (`x`) | puts the terminator on a **straight vertical line** — a seam down the face |
| Shading by a per-row surface normal | darkens **every silhouette edge**, eats the face |
| Flat `warn`/`orange` as a large plane | reads as **sunburn**, not skin |
| `panel2` / `line` (navy UI colours) as skin shadow steps | half of every face was **blue-grey** — the root cause of "robotic" |
| Hair `warn` (255,209,102) as a hair colour | **bleached plastic** — this was the "hair too bright" note |
| Brown hair with an `orange` highlight | **gold strands** in the hair |
| Grey hair highlighting to `textdim`/near-white | a **white cap** |
| Hair lighter than skin | reads as a **swim cap** |
| Hair rim drawn at `head_half(ear_y)` for every row | **two black wedges** off the crown — the "spike" in every screenshot |
| Strands escaping the silhouette / sheen bands | every extra strand was another chance to draw something that is not there |
| Cronenberg of the jaw: 79 px jaw on a 27 px skull | a **cone** |
| Deltoid discs drawn separately from the torso | read as **floating plates** |
| Warm rim light down a sleeve's inner edge | a line of **gold dots** |

**What works:** three concentric tone planes around one light point (an ellipse) for skin; a flat hair
mass on the skull curve with one darker band at the roots; a hard 1 px edge and two tones per mass in the
sweep style.

---

## 4. Known weak spot, stated honestly

The faces are **acceptable at 1× and crude at 3×**. Brow, nose and mouth are modelled but read as marks
rather than features. The cure is more pixels on the face, which means either a tighter crop or a
dedicated larger portrait for the letter page while the desk keeps the bust. **Not started** — waiting on
the player's verdict after playing v10.
