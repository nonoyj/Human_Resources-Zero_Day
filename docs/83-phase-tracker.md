# 83 — PHASE TRACKER

**One page that answers "where are we, what is left, how close is the end".** Updated 2026-11-27.
`.\scripts\Where-Am-I.ps1` prints the live state; this page is the plan that state sits in.

Read the bar like this: `[##........]` — done / total, so a glance says how much of the phase is behind us.

---

## The whole project, in six phases

| # | Phase | Bar | State |
|---|---|---|---|
| P1 | **Vertical slice** — desk art, applicant, CRT, letter, stamp, day summary | `[########..]` 8/10 | playable end to end |
| P2 | **The loops** — 5-day run, scoring, reasons, trust, corruption | `[#########.]` 9/10 | **five-day arc + three endings, tested end to end (v101)**; the reason list is still short |
| P3 | **The after-hours sweep** — walk, traces, pages, key strip | `[##########]` 10/10 | **done in v97** (`docs/90` BUG-057) |
| P4 | **Save, resume, audio, endings, intro** | `[######....]` 6/10 | save + resume + endings + **intro slot done** (placeholder words, owned by BE2); audio 0 |
| P5 | **Replenish** — art sweep, look, animation feel (`docs/82`) | `[..........]` 0/8 | **scheduled, not started** |
| P6 | **Ship** — Android build, QA matrix, store assets | `[..........]` 0/8 | not started |

**The honest headline: the game is playable and correct; it is not yet finished and not yet pretty.**
Roughly 55 % of the work is behind us, and the remaining 45 % is mostly *breadth* (audio, intro,
ship) rather than new mechanics.

---

## What phase are we in *this week*

**P1 → P2, with P3 CLOSED.** Concretely, in order:

1. ✅ the desk boots and plays in an empty scene
2. ✅ 9 hotspots that look clickable and respond where they are drawn
3. ✅ the read-out, the letter, the stamp, the day summary
4. ✅ the after-hours walk with copy on its own band
5. ✅ **the walk's page placement is correct** — closed in **v97** (BUG-057): the composed page is what
   draws, its copy is laid out by Core's own rows, and the walk is no longer tinted by the daylight
6. ⬜ the stamp + reason loop is the teaching mechanism (it stamps, but the reason list is short)
7. ✅ **a 5-day run with an ending** — v101
8. ⬜ audio hooks
9. ⬜ Android build
10. → then P5 replenish, one scheduled pass, everything cosmetic at once

**Next three rounds, by subject (not by date):**

| Round | Subject | Ends with |
|---|---|---|
| v99 | **the tools hold**: Where-Am-I survives every repo state it reports on | seven states, seven passes, in the gate |
| v98 | **the walk reads**: the strip is painted once, in one space, and a fresh install stays clean | the walk's key strip is the right way up, and `Where-Am-I` says *clean* |
| v97 | **the walk draws** — one page texture, Core's rows, no tint, a bracket instead of a panel | you press SPACE and the page changes |
| v101 | **the five-day arc**: the run has a through-line, and three endings read the whole run | you can start on day 1 and finish on a verdict |
| v102 | **the intro slot**, and an install that cleans up after Unity | **confirmed on screen by the player, 2026-11-27** |
| v103 (this one) | **an installer that can actually deliver its own fixes** | one apply is enough: no more "the next apply uses this version" |
| v104 | **audio** | it sounds like a game |

---

## What is NOT in any phase (so it stops being asked)

- Google Docs handoffs, Git LFS, portrait orientation, a server backend for v1, real-time shadows on the
  desk, player face customisation — all recorded as deliberately absent in `docs/16-the-later-list.md` §6.

## What waits for you, and blocking nobody

- **A-1 faces**: bigger portrait for the letter page (your answer: **A**). This is a P5 item; nothing else
  depends on it, so it does not hold up v91/v92.
- Ending copy (5 endings) and the 12 day-events belong to the content owner; the code for both exists.

---

## How to tell me to slow down or speed up

- **"Where are we?"** → I open this page and say the bar, the phase, and the next three rounds.
- **"Park it."** → the item goes to `docs/90-bug-log.md` PARKED FOR REPLENISH with an id, and P5 is where
  it gets fixed. Nothing parked is forgotten; nothing parked blocks a playtest.
- **"Fix it now."** → it jumps the queue and the bar for the current phase moves.
