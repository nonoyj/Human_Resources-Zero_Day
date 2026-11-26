# The after-hours sweep

**Status:** rule implemented + tested 2026-11-26 · **Idea from:** the team · **Owner:** FE (scene) + BE2 (copy)
**Rule file:** `unity/Assets/Scripts/Core/Rules/Sweep.cs` · **Tests:** `tools/CoreTests/SweepTests.cs` (7)

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
with the losing condition — the walk is where the player *feels* the loss before the summary screen says it.

## 2. Why it is honest

- **Cause is attached.** `Sweep.AfterShift` only emits a trace when `CorruptionLevel` says a synthetic got
  through, and names the applicant the player interviewed (`"… a different face. (Maren Voss)"`).
- **The escalation is authored, not random.** Two seeded runs with the same hires produce the same walk.
- **It cannot exceed three.** Severity is 1..3 and the list caps at the defeat threshold.
- **A clean day is not silent by accident.** `AfterShift(0)` returns one explicit quiet trace, so the
  routine still happens — which is the point of a routine.

## 3. What the scene needs (FE)

1. A `SweepRoot` laid over `Desk.unity` with the three floors as simple pan/step backgrounds
   (`desk`, `corridor`, `lift`). One sprite per floor; no new mechanics.
2. `Sweep.WalkSeconds` (45) is a **budget, not a clock** — no timer on screen, no penalty for standing still.
3. Traces are **read**, not collected: a single line of text (the CRT font) over the spot, plus the second
   line on tap. Reuse `LetterView`'s text path so the type matches the paperwork.
4. The lift is the last frame of day-3-to-5 walks only when `Sweep.FinalFloor` says so — never open the lift
   on a clean day.

## 4. What BE2 needs to write

The 7 strings above are placeholders with the right length and tone. Keep the rule: **room, not monster.**
No gore, no figures standing in doorways, no sound sting the player did not earn. The scare is that the
office is *familiar and slightly wrong*.

## 5. Open question for the user

Should the sweep happen **every** day (routine, per the team's wording) or only on days where something got
through? The rule supports both — `Sweep.HasSweep(day)` is the single switch. Currently: every day.
