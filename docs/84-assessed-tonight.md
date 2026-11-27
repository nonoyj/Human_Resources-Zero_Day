# Tonight, assessed — 2026-11-27, late

**One page. What is built, what is not, what it is worth, and the next three moves.**
Written for the player, not for the repo. Source of truth for numbers: `docs/83-phase-tracker.md`,
`docs/90-bug-log.md`, and a live run of the five gates.

---

## 1. The headline

The game is **playable start to finish on its intended shape** — five days, three endings, an intro, an
after-hours walk — and it has been played on screen by you twice tonight. It is **not finished and not
pretty**: audio is zero, the art is functional rather than good, and the ship phase has not started.

**Rough completion: 55–58 %.** P1–P3 are effectively done, P4 is two-thirds, P5/P6 are untouched.

## 2. What exists, by the numbers

| | |
|---|---|
| Commits | 194 (all on `origin/main`) |
| Game code | 54 C# files, 9,133 lines |
| Tests | 146 checks in 3,701 lines — **all passing** |
| Art | 101 PNGs, 85 registered sprites, generated + hashed |
| Content | 28 JSON files (days, emails, letters, intro) |
| Docs | 34 files, 5,425 lines |
| Bugs found and killed | 45 logged; 64 numbered findings; 5 parked |
| Gates | 5, all green at `baa8cf3` |

Every one of those numbers is verifiable in one command (`.\scripts\Where-Am-I.ps1`), which is the point:
the project does not ask you to believe a status report.

## 3. Phase by phase

| Phase | Bar | Honest read |
|---|---|---|
| **P1 Vertical slice** | `[########..]` 8/10 | Desk, applicant, CRT, letter, stamp, day summary all work. Missing: the two items that need P5 art. |
| **P2 The loops** | `[#########.]` 9/10 | Five-day arc, three endings, trust, corruption, scoring — all tested end to end. The **reason list is still short**: a player can be right and not be told why in enough detail. |
| **P3 After-hours sweep** | `[##########]` 10/10 | Done in v97 after three attempts. It is the most finished system in the game. |
| **P4 Save/audio/endings/intro** | `[######....]` 6/10 | Save, resume, endings, **intro slot + spoken-intro decision** done. **Audio: zero.** |
| **P5 Replenish** | `[..........]` 0/8 | Not started, deliberately. Faces, cast spread, animation feel, one text scale. |
| **P6 Ship** | `[..........]` 0/8 | Not started. Android build, QA matrix, store assets, performance on a real phone. |

## 4. What tonight actually produced

Three rounds, all of them real:

1. **v102 — the intro slot.** The file format, the loader, the card, the input rule (nothing behind the card
   moves), the gate, and a placeholder script so the path is proven. You saw it: five cards, `BEAT n / 5`,
   DISPATCH in green. **Confirmed working.**
2. **v103 — an installer that can deliver its own fixes.** Your `105 entries` warning was not a bad install:
   the apply was running last round's launcher from Downloads. 143 strays swept on the next run, one apply,
   clean. **Confirmed working** — and the fix no longer costs two applies.
3. **v104 — commands a human can read.** My install instruction was a .NET one-liner. You were right to
   reject it. It is now `Expand-Archive`, `Copy-Item`, `.\Apply-Bundle.ps1`, in that order, tested from a
   stale launcher against the real zip. Also recorded: **DISPATCH speaks** (ADR-009) and **the game starts
   itself; R is replay** (ADR-010) — behaviour that was true but had never been written down.

The pattern in all three: **the game was fine; the tooling and the written contract around it were not.**
That is worth saying plainly, because it is where your rounds have been lost.

## 5. What is left in this phase (P4, the last 4 of 10)

1. **The spoken intro** (v105). DISPATCH reads the five beats. Needs an `audio` field per beat, a clip
   player that waits for the clip with a cap, and a graceful path when a clip is missing — the card must
   still work silent, which it already does.
2. **The room bed** (v106). A night-office loop under the whole game, duckable.
3. **Action cues.** Stamp, phone, drawer, the walk's footstep ticks. Small, but this is most of the
   difference between "a prototype" and "a game".
4. **The save-quit `IOException`** (D-5, open since v87). Not a crash, but it prints red on quit and it is
   the last known rough edge in the shipping path.

## 6. The risk I would name

**P6 has not been touched, and it contains the only unknowns that can still change the design.** Android
build, a real phone, a QA matrix. Everything built so far assumes a 480×270 canvas scaling cleanly; on a
phone at 20:9 with a notch, the letter page and the sweep key band are the two things I would expect to
break first. I would rather find that in the next two rounds than at the end.

## 7. The next three moves I propose

1. **v105 — DISPATCH speaks.** Closes P4's headline item and gives you something to *hear*, which is the
   first time the game will not be silent.
2. **v106 — the room + cues.** Then P4 is done and audio is no longer a zero.
3. **v107 — a phone-shaped smoke test on the current build**, no new features: run it at 1080×2400 and
   screenshot the letter, the sweep band and the HUD. Cheap now, expensive later.

If you would rather I go straight at **P5 (faces and look)**, say so — it is the difference the player
*feels* most, and it is the one thing that makes screenshots look like a finished game. But I would not
skip the phone smoke test twice.

---

## What to tell me

- **"Audio."** → v105 and v106 as above.
- **"Make it pretty."** → P5 first: faces, cast spread, one text scale.
- **"Is it shippable?"** → I will run the phone smoke test and answer with a screenshot, not an opinion.
