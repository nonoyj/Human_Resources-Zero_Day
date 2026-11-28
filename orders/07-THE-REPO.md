# 07 -- THE REPO, IN UNDELETABLE MEMORY

The player asked (v130): **"Keep the repo in undeletable memory."** and **"What we talked about are none
deletable memories even if asked unless there is a unnecessary part of it that needs to be updated/removed."**

This file is that memory. It is a *copy* of the facts, never the source: if this file and the repo disagree,
the repo wins and this file is wrong and gets fixed. It exists so that a new session, a new agent or a new
teammate can be useful in ten minutes without re-reading 40 documents.

---

## The repo

| | |
|---|---|
| Name | **Human Resources: Zero Day** (`HR//0D`) |
| Remote | `https://github.com/nonoyj/Human_Resources-Zero_Day.git` (branch `main`) |
| What it is | A 480x270 pixel-art game about being the last human screener at a company staffed by synthetics. |
| Stack | Unity 6 (6000.3.29f1, URP 2D) + pure-C# core + Python art/audio generators + five gate scripts |
| Canvas | **480x270**, PPU 16, landscape, integer scaling only (ADR-005/ADR-007) |
| Palette | 81 colours, 13 authored ramps, `art/kit/palette.json`; alpha is 0 or 255, never partial |
| Chosen at v130 | `52d7440`..`HEAD`; the v129 bundle was `HR-ZeroDay-bundle-v129.zip` |

## The one-paragraph game

You sit at a desk. Applicants arrive. Some are human, some are synthetic, and the only way to know is to read
the evidence: the person's body (a 4-frame idle with tells drawn into it), their paperwork (a cover letter that
can be fake *at* you), and their record. You stamp APPROVE or REJECT and give a reason. Wrong hires put a
trace in the building that you meet again after hours. Five days, one verdict.

## The mechanics, in the order the player meets them

| Mechanic | Where | State |
|---|---|---|
| The desk and the 9 hotspots | `Game/Interaction/HotspotHitTest.cs`, `desk_layout.json` | built, tested |
| The CRT card (day, trust, name, role, ID, grad/exp, ref) | `Game/Desk/MonitorScreen*.cs` | built, 25x11 glass |
| The letter, its five flags and the flag scoring | `Game/Desk/LetterView.cs`, `Core/.../LetterFlags` | built, not yet wired to input |
| Stamps + reasons + trust | `Core/Rules/ScoringService.cs` | built, tested |
| The shift clock (180 s per applicant, reading at 1/4 speed) | `Core/Data/ShiftClock.cs` | built, tested |
| The 5-day arc, 3 endings, save/resume | `Core/Data/ShiftController.cs`, `SaveSystem` | built, tested |
| Security rating + 4 alerts | `Core/Rules/SecurityRating.cs` | built, tested |
| The after-hours walk (one trace per synthetic hired) | `Core/Rules/Sweep.cs`, `SweepScript.cs` | built, tested |
| Day -> dusk -> night | `Core/Rules/Daylight.cs`, `RoomLighting.cs` | built; **three paintings still to make** |
| **Several lights + cast shadows** | `Core/Rules/LightRig.cs` | **new in v130**, tested |
| **The wall clock** | `Core/Rules/RoomClock.cs` + `RoomClockView.cs` | **new in v130**, tested |
| **Applicant variety (15 variants)** | `Core/Data/Appearances.cs` | **new in v130**, tested |

## The backstory (from `docs/06`, `docs/07`, `docs/16`)

The company did not replace its staff, it *replenished* them. HR is a funnel with a human at the end of it,
and that human is a verification device: every hire you approve becomes a face that will be reused, and the
traces you find after hours are the proof. The game never says "they are AIs" out loud; that is the point.
The corruption stages are not a monster, they are the building agreeing with itself.

## The player's instructions -- the standing ones

Kept verbatim in `orders/04-IMPORTANT-MEMORY.md`, summarised here so this file is usable alone:

1. **The menu is the standard.** Copy the menu's art and change only what the game requires. Calling the
   menu's function is a copy; a "same-vibes" redraw is a rejection.
2. **Every delivery includes the PowerShell**, and "What I did" means *the instructions to the player*.
3. **No claim without a render or an executed check.**
4. **A folder of standing instructions, checked before every reply** -- this folder.
5. **The HUD is clickable objects, not text.**
6. **Mobile first:** tap targets, thumb reach, no hover-only information.
7. **The applicant must not look the same** as before, and must sit *beside* the desk.
8. **Keep the workspace clean.**
9. **Name the phase, the phases left, the steps, what remains** -- every round.
10. **Do not forget the pending items** (day/night cycle first among them).

## The decisions that are locked

| Decision | Ruling |
|---|---|
| Landscape 480x270, PPU 16 | locked, ADR-005 |
| No player face customisation | locked, `docs/00` 3.4 |
| No Google Docs handoffs, no LFS, no portrait, no server for v1, no real-time shadows | locked, `docs/16` #6 |
| Monitor RIGHT, applicant LEFT, window behind the applicant | locked, ADR-007 amended at v127 |
| The game room = the menu's room | locked at v129 |
| The menu's dead hardware is menu-only | v129 |
| Coffee swapped with the keyboard/mouse | v129 |
| The glass shows the applicant's own ID photo | confirmed v129, specified `docs/17` #8 |
| **More than one light, on both screens** | v130 |
| **A lamp in the game's room** | v130 |
| **A wall clock showing the shift's time** | v130 |
| **Applicant variety, seeded and stored** | v130 |

## The process (unchanged, and it is the whole method)

1. Read `orders/00-READ-FIRST.md` and `orders/CHECKLIST.md`.
2. Find the existing generator/rule before writing anything.
3. Change it; render it; **measure it**; then gate it.
4. Run all five gates. A gate that fails to parse is not a pass.
5. Commit with the reason, not the diff.
6. Deliver with the four headings and the PowerShell.
7. Log the bug in `docs/90-bug-log.md` -- every rejection becomes a prevention rule.

## The assurances (what the player can rely on)

- Every shipped sprite is byte-identical between `art/kit` and Unity, sha256-verified (`publish --verify`).
- The core is pure C# and tested headlessly: 200+ assertions, `ALL TESTS PASSED` before every delivery.
- Every game rule that a screenshot could miss has a numeric gate (glass 25x11, 480x270, PPU 16, trust bar
  width, hit test padding, light steps, clock monotonicity, applicant variety, cast shadows).
- The bundle extracts, applies, pushes and captures its gate log in one paste -- and that script has been run
  end-to-end on an empty profile five times.
- Nothing is claimed as working that was not executed in this session.

## What is still owed (see `orders/05-phases.md` for the full board)

Day/night paintings (3), the 96x96 portraits with their tells, the consent screen, analytics, the Field Manual,
5 endings (3 built), the flag panel, the stamp animation, the emails screen, the 12 day-events, the Android
build, the QA matrix, store assets -- and the **1 Unity ERROR in the player's status bar** whose text we have
still never seen.
