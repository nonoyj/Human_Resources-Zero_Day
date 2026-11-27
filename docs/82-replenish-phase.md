# 82 — THE REPLENISH PHASE

**Answer to the question asked on 2026-11-27: "we will have a replenish phase where we fix everything — the
art, the overall look, the animation — right?"**

Yes. This page is that phase, scheduled and itemised, so that during the build we can park a cosmetic
problem instead of stopping for it — and so that nothing parked here gets forgotten.

---

## 1. The rule we are adopting

> **Freeze the build, not the polish.** Anything that stops the game being *playable and correct* is fixed
> now. Anything that is *how it looks once it plays* gets a ticket number here, a screenshot, and a date.

That means, from today:

- **In-flow (fix immediately):** crashes, exceptions in the Console, a screen that will not respond, wrong
  data, an upside-down or misplaced element that hides content, save/load failure, anything that blocks a
  playtest.
- **Replenish (park it here):** proportions, silhouettes, animation feel, transition timings, font weight,
  colour harmony, the room's overall look, frame-by-frame acting.
- **A parked item may still be *captured* early.** The capture is cheap and it is what makes the phase a
  day of work instead of a week: a reference image, a one-line "what is wrong", the file that owns it.

---

## 2. Where the phase sits

| Phase | What happens | Exit test |
|---|---|---|
| **Now — build** | days 1–5 loop, stamp + reason, letter flags, sweep, save round-trip, audio hooks, endings | one full 5-day run start to finish with no Console output |
| **R-0 Freeze (½ day)** | every screen is reachable, every screen is screenshotted, no new features | a contact sheet of all screens exists |
| **R-1 Art sweep (2 days)** | the list in §3, in priority order, in one pass over the generator | new contact sheet vs old, side by side |
| **R-2 Look & motion (2 days)** | §4: transitions, idle timing, HUD weight, corruption drift | a 60-second capture that reads as finished |
| **R-3 Consistency (1 day)** | §5: every screen against the art bible + palette gate; strings externalised | gates green on the frozen tree |
| **R-4 Playtest gate (½ day)** | three people play it cold; only blockers come back | a written verdict, not opinions |

Two days of art and two of motion is realistic **because the capture already happened** — in this phase we
mostly change numbers in a generator and re-run it, not redraw.

---

## 3. The art list (what "fix the art" concretely means)

| # | Item | Owner | Why it is here instead of now | Capture needed |
|---|---|---|---|---|
| A-1 | **Faces at 3×** — brow/nose/mouth are marks, not features (`ART-DIRECTION-STATE.md` §4). **DECIDED 2026-11-27: option A — a dedicated larger portrait for the letter page; the desk keeps the 192×216 bust.** | Agent | needs the portrait frame (176×198) generated and published | 1 portrait at 3×, marked |
| A-2 | **Skin-tone spread** — the four tones are in, but the cast still reads in three families | Agent | tune after A-1 changes the face | the current cast sheet |
| A-3 | **Hair silhouettes** — simple is right, but 14 heads share too few shapes | Agent | cosmetic, big visual win per pixel | the hair-only strip |
| A-4 | **The room's overall look** — desk lighting, wear overlays, night window balance | Agent | it is the last thing that should move; the furniture is still settling | one 480×270 still |
| A-5 | **Applicant bodies** — shoulders/arms are v9 shapes, suits read flat at 1× | Agent | follows A-1/A-3 | the bust sheet |
| A-6 | **fx set 2** — stamp press, rejection ink, hire flash, card slide (`16-the-later-list.md` 2.2) | Agent | needs the stamp timing from R-2 | — |
| A-7 | **Ending cards** (5) + **menu/intro frames** (LATER-LIST 2.3–2.4) | Agent | needs final copy | — |
| A-8 | **Field Manual pages** (LATER-LIST 2.5) | Agent + C | needs legend text | — |

## 4. The animation list

The spec already exists (`18-today-and-animation-plan.md` §1); this phase is where it gets *tuned*, and one
part of it is still unbuilt.

| # | Item | Owner | State |
|---|---|---|---|
| M-1 | **Idle library**: breathe, blink, fidget, look-away, stare reaction, sweat/tremor | Agent + You | frames exist; the driver does not — this is the biggest missing piece of the game's core tell |
| M-2 | Timing constants per archetype (5–7 s vs exactly 6.0 s etc.) | Agent | spec'd, untuned |
| M-3 | Transitions: stamp press + shake, letter slide, CRT repaint flicker, day wipe, menu fade | You | planned, not built |
| M-4 | Corruption drift cross-fade `fx/wear_a` ↔ `fx/wear_b` (LATER-LIST 1.1) | Agent → You copy | art exists, binder does not |
| M-5 | Reduce-motion path (breathe+fidget off, blinks kept) | You | spec'd |
| M-6 | Sweep: walk step timing, page-turn, the key band's final placement | Agent | **band is broken now** — see D-1, fixed in-flow |

## 5. Consistency pass (the "overall look" catch-all)

- Every screen shot against the palette gate and the art bible's three glow steps.
- One font size scale for the HUD, the letter, and the read-out (the desk HUD has already come down twice).
- String table externalised (LATER-LIST 4.6) so translation is a file swap.
- Contact sheet committed alongside the build, so "it looked better last week" becomes checkable.

---

## 6. The living defect list

Parked items live as **`D-nn`** entries in `docs/90-bug-log.md` under "PARKED FOR REPLENISH", each with a
screenshot path, the owning file, and a one-line acceptance test. Nothing is parked as a chat message only —
if it is not in that section, it is not scheduled.

**Currently parked:** D-1 (walk key band placement — *was* in-flow, demoted only because the content is
correct and the block is cosmetic; still tracked as the top of the phase), D-2 (`still nested:` noise from
`Apply-Bundle`), D-3 (text scale unification across screens), D-4 (cast tone spread, pending A-1).

---

## 6a. Status, 2026-11-27

Phase **not started** (0/8). Two of the five parked defects were pulled forward into the build because
they were placement, not polish: **D-1** (walk key band) and the read-out size. See
`docs/83-phase-tracker.md` for the live plan — that page answers "where are we" in one screen.

## 7. What this buys us

- Nothing stalls the build for a colour argument.
- The player gets one predictable window to judge **look and feel** as a whole, which is the only way to
  judge them — a face judged in isolation always gets over-corrected.
- The generator means an art sweep is mostly **numbers**, so the phase is days, not weeks.
