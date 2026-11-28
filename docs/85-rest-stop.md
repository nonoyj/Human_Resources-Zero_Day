# 85 -- WHERE TO STOP, AND HOW TO RESTART

**Written 2026-11-27, at the end of a long session.** This page exists so stopping is a decision and not an
accident, and so the next session starts in ten minutes instead of an hour.

---

## 1. The answer: stop here

**Stop after v106.** That is the point where:

- every phase that is *finished* is genuinely finished and tested -- P1, P2, P3, and P4's systems;
- the only things left are **look** (P5) and **shipping** (P6), and both are better started fresh than
  started tired, because both are about judgement rather than mechanics;
- the repo is in a state a stranger can pick up: `main` pushed, five gates green, `AGENTS.md` for the
  agents, the restart order in #3 below.

**Do not stop mid-phase.** The two places stopping costs the most are (a) halfway through a P5 art pass,
where the old and new looks are both half-present, and (b) anywhere in P6, where a half-built Android
profile is worse than none.

## 2. Where we are, in one screen

| | |
|---|---|
| Commit | `46d9206`, pushed to `origin/main` |
| Bundle | `HR-ZeroDay-bundle-v106.zip` |
| Gates | 152 headless checks - Unity compile - art+audio pipeline - scripts - fresh-clone verify -- **all green** |
| Backend | 22 contract tests green in the same tree, `AGENTS.md` + `backend/README.md` written |
| Phase bars | P1 8/10 - P2 9/10 - **P3 10/10** - P4 8/10 - P5 0/8 - P6 0/8 (**~57 %**) |
| Playable | five days, three endings, intro, walk, sound -- start to finish |

**What "done" would still need:** faces and look (P5, 8 items in `docs/82-replenish-phase.md`), the Android
build and a QA matrix (P6, 8 items), DISPATCH's spoken intro (v107, waits on BE2), and the backend's seven
owed items.

## 3. The restart order (do these in this order, nothing else)

```powershell
cd $env:USERPROFILE\HR-ZeroDay
git pull --no-rebase
.\scripts\Where-Am-I.ps1                      # live state: dirty? which bundle? next command?
```

Then prove the tree is healthy **before** changing anything -- if a gate is red on a clean pull, that is the
first bug of the session, not something you caused:

```powershell
dotnet run --project tools\CoreTests
dotnet build tools\UnityStubs\CompileCheck.csproj
.\scripts\Test-ArtPipeline.ps1
.\scripts\Test-Scripts.ps1
python tools\verify_fresh_clone.py
cd backend ; python -m pytest -q ; cd ..
```

Then open the game once and press Play. **Look at it before touching it.** A session that starts by reading
the code tends to fix things nobody complained about.

## 4. The first three moves next session

1. **v107 -- the phone smoke test** (no new features): run at 1080x2400, screenshot the letter, the sweep key
   band, the desk HUD. This is the only thing that can still change the *design*, and it has been deferred
   twice. Do it first, not last.
2. **P5 pass 1 -- faces.** The one difference players *feel*. Bigger portrait for the letter page (player's
   answer A-1: **A**), a continuous spread across the cast sheet, one documented text scale.
3. **v108 -- the spoken intro**, when BE2's clips exist. The layout is already decided (ADR-009): `audio` per
   beat, optional, silent-safe.

## 4a. The full list of what is owed

`docs/86-whats-owed.md` -- every feature the player has asked about, checked one by one against the repo
(BUILT / PARTLY / PLANNED / NEEDS A DECISION), plus the things mentioned earlier that nobody has built yet
(the consent screen, analytics emitting, the two missing endings, the Field Manual, Android). Read it with
this page: **85 is when to stop, 86 is what comes after.**

## 5. What is parked, and stays parked until replenish

From `docs/82-replenish-phase.md` and `docs/90-bug-log.md`:

- **D-5** -- save `IOException` printed when quitting Play mode (known since v87; not a crash, not a data
  loss, red text on quit). The last rough edge in the shipping path.
- **D-4** -- cast reads in three tone families; faces crude at 3x.
- **D-3** -- text scale differs between HUD, letter and sweep page (reduced by eye twice).
- The reason list is still short: a player can be right and not be told why clearly enough. That is P2's
  last point and it is **writing**, not code.

Nothing above blocks a playtest, which is why none of it blocked the build.

## 6. The rule for stopping, so it does not need a decision next time

Stop when **all of these** are true, and not before:

1. the five gates are green *and* the backend suite is green in the same tree;
2. `main` is pushed and `git status` is clean (Unity's `.meta` noise is swept by the installer, so "clean"
   means what the apply says, not what Explorer shows);
3. `HANDOFF.md` names the commit and the build that was last *played*;
4. the next session's first three moves are written down (this page, #4).

If any of those is false, the stopping point is the next hour, not now -- a bad handover costs more than an
extra hour.
