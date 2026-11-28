# The after-hours sweep

**Status:** rule **and scene** implemented 2026-11-26 - **Idea from:** the team - **Owner:** FE (scene) + BE2 (copy)
**Rule file:** `unity/Assets/Scripts/Core/Rules/Sweep.cs` - **Tests:** `tools/CoreTests/SweepTests.cs` (7)

---

## 1. The idea, restated as a rule

After the shift ends, the player walks the office once. The walk **does not invent horror**: every trace is
keyed to a synthetic the player personally approved, so the sweep is a receipt for the player's own
mistakes, not a jump-scare. A player who hires nobody walks a silent office.

| Hits | Traces | Where the walk ends | The trace |
|---|---|---|---|
| 0 | 1 (quiet) | desk | "The office is exactly as you left it." |
| 1 | 1 | desk | your mug has moved; it is still warm |
| 2 | 2 | corridor | the interview chair is still warm; the badge is a different face |
| 3 | 3 | **lift** | the lift has been holding on your floor since 00:00, called from inside |

Three is the defeat threshold (`ShiftController.HireLimitForDefeat`), so the last trace always coincides
with the losing condition -- the walk is where the player *feels* the loss before the summary screen says it.

## 2. Why it is honest

- **Cause is attached.** `Sweep.AfterShift` only emits a trace when `CorruptionLevel` says a synthetic got
  through, and names the applicant the player interviewed (`"... a different face. (Maren Voss)"`).
- **The escalation is authored, not random.** Two seeded runs with the same hires produce the same walk.
- **It cannot exceed three.** Severity is 1..3 and the list caps at the defeat threshold.
- **A clean day is not silent by accident.** `AfterShift(0)` returns one explicit quiet trace, so the
  routine still happens -- which is the point of a routine.

## 3. The scene, as built (2026-11-26)

| Piece | File | What it owns |
|---|---|---|
| The rule | `Core/Rules/Sweep.cs` | which traces exist, escalation, `HasSweep`, final floor |
| The pages | `Core/Rules/SweepScript.cs` | floors, captions, wrap, key line, the summary receipt |
| The view | `Game/Interaction/SweepView.cs` | one 480x270 page per floor, band + text + key strip |
| The wiring | `DeskBootstrap.Continue()` | the walk starts after the debrief of a day with `corruption > 0` |
| The preview | `tools/comp/preview_sweep.py` | a twin that renders the walk without Unity |

How a day ends now: **stamp -> debrief -> (walk, if anything got through) -> day summary**. The summary gains
one line the walk produced, e.g. *"THE WALK ENDED AT THE CORRIDOR. 2 THINGS WERE NOT YOURS."* -- and no line
at all on a clean day.

Two things the first draft got wrong, both caught in the twin before Unity:

1. **The words sat straight on the room art** and were unreadable wherever the background had detail.
   Fixed with a caption band: solid `void` fill, 1 px `line` border, sized to the rows actually used.
2. **The key prompt was drawn across the neon caps** at the bottom. The caps stay as decoration; the words
   now sit on a black band just above them.

## 3b. What the scene needs (FE) -- original brief

1. A `SweepRoot` laid over `Desk.unity` with the three floors as simple pan/step backgrounds
   (`desk`, `corridor`, `lift`). One sprite per floor; no new mechanics.
2. `Sweep.WalkSeconds` (45) is a **budget, not a clock** -- no timer on screen, no penalty for standing still.
3. Traces are **read**, not collected: a single line of text (the CRT font) over the spot, plus the second
   line on tap. Reuse `LetterView`'s text path so the type matches the paperwork.
4. The lift is the last frame of day-3-to-5 walks only when `Sweep.FinalFloor` says so -- never open the lift
   on a clean day.

## 4. What BE2 needs to write

The 7 strings above are placeholders with the right length and tone. Keep the rule: **room, not monster.**
No gore, no figures standing in doorways, no sound sting the player did not earn. The scare is that the
office is *familiar and slightly wrong*.

## 5. When it runs -- DECIDED 2026-11-26

**Only on days when something got through** (`Sweep.HasSweep(corruption)` returns `corruption > 0`).
A quiet office is not a scene; that day goes straight to the summary. The walk stays a consequence.

## 6. Art direction -- DECIDED 2026-11-26

The sweep is the **one place** the game changes style, on purpose:

| | Main game | After-hours sweep |
|---|---|---|
| Camera | 3/4 painterly bust across the desk | flat side-on room, tiny characters (~16 px) |
| Technique | dither ramps, rim light, 3-4 px shading bands | 2-tone flat fills, hard 1 px edges |
| UI | diegetic CRT text, no chrome | neon-cyan key caps along the bottom, a caption bar |
| Reference | the painterly applicant samples | the `Lounge -- LEVEL 5` shot |

That contrast is the point: the office at night is a *different kind of thing*, and the player reads it as
"this is not the interview" before reading a single word. The neon key caps are the one non-diegetic UI in
the game, and they exist only here.
