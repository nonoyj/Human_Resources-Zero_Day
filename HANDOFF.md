# HR//0D — working state

**One page. If you read only one file, read this one.** Updated 2026-11-27 (bundle **v88**, commit `cc29be9`).

> **Two files now outrank this one, and they exist because the player asked for them:**
> `docs/80-COMMAND-BOOK.md` — **every command, verbatim** (bundle, push, Unity, gates, art pipeline).
> `docs/82-replenish-phase.md` — the scheduled polish phase (art, overall look, animation) and the rule
> that cosmetic problems are **parked and scheduled**, never allowed to stall the build.
> `.\scripts\Where-Am-I.ps1` prints both, plus the live state of your copy.

---

## What this is

*Human Resources: Zero Day* — you are an HR screener on a deadline, interview applicants, and decide who
is a person and who is a synthetic. 480×270, Unity 6, painterly pixel art. Windows.

- Repo root: `C:\users\user\HR-ZeroDay` · Unity project: `unity\` · Remote: pushed, `main` at `b86bbde`
- **From here on: `git pull` / `git push`. Bundles are only for a machine with no clone.**

---

## Current state

**Playable.** Press Play in an empty scene and `GameBoot` builds the desk. Interview → read the letter on
the CRT → inspect artifacts → stamp → day summary → after-hours sweep.

| Gate | Command | Status |
|---|---|---|
| Headless tests (96) | `dotnet run --project tools\CoreTests` | pass |
| Art + stub + layout gate | `.\scripts\Test-ArtPipeline.ps1` | OK |
| Script health | `.\scripts\Test-Scripts.ps1` | healthy |
| **Unity will compile** | `dotnet build tools\UnityStubs\CompileCheck.csproj` | succeeded |
| **The COMMIT works** | `python tools\verify_fresh_clone.py` | PASS |

**Pushing.** `Apply-Bundle.ps1` rebuilds `.git` from `history.bundle` (fresh `git init`, refs moved by hand —
that is what stopped BUG-021), so a bundle-installed copy has **no upstream**. `git pull`/`git push` then
stop with *"The current branch main has no upstream branch"*. Fix it once per copy, and let the script do
the ordering:

```powershell
.\scripts\Push.ps1                       # fetches, checks it is a fast-forward, pushes, sets upstream
.\scripts\Push.ps1 -Message "fix: ..."   # commit everything first, then the same
```

**Order matters: push first, update from a bundle second.** `Apply-Bundle ... -Clean` replaces `main`
with the bundle's history, so a push after an update needs a force — and the script refuses to ever force.
It also refuses for real divergence and tells you to `git pull --no-rebase`.

**Unity does not have to be closed.** Replay after an update = **Stop, then Play**. Editing in Play mode and
expecting the change to stay is the mistake; Unity throws Play-mode edits away, which is what makes the
relaunch feel necessary. `HR//0D > Open Game (play)` opens/boots the scene without a click, and
`HR//0D > Reload Art Kit (open editor)` re-publishes art under a running editor.

The last one is the one that matters most: it exports `HEAD` with `git archive` into a temp folder and runs
every other check **inside it**. Six boot failures in one day were all the same shape — a check that read my
working directory while you ran the commit. It must be green **after** committing, and before any bundle.

---

## The five things that have cost the most time (all fixed, all guarded)

1. **A stub that lied.** `sortingOrder` was added to the `TextMesh` stub to silence an error. The gate went
   green; the editor went to Safe Mode. Rule: `tools/UnityStubs/ASSEMBLY-RULES.md`. **Never add a member to
   a stub to make the gate pass.** A guard now fails the gate if `sortingOrder` reappears on `TextMesh`.
2. **Package namespaces in engine stubs.** `UnityEngine.EventSystems` in `UnityStubs.cs` → CS0234 → Safe
   Mode. Package stubs live in `InputSystemStubs.cs`, compiled only by `tools/`, never by Unity.
3. **The menu ritual.** You do not need `HR//0D` to play. Press Play. The menu is for when art PNGs changed.
4. **Closing Unity to apply a bundle.** No longer needed; the `.git` swap retries instead of failing.
5. **My commands being wrong.** The zip is in `Downloads`; `Open-Game.ps1` ships *inside* the bundle so it
   cannot be run before the apply. `docs/BUNDLE-WORKFLOW.md` now has the two commands, in order, plus a
   symptom → cause → fix table.
6. **Shipping a bundle without the art the game reads.** `.gitignore` excluded
   `unity/Assets/Resources/Art/` as "derived", but `Resources.Load` cannot see outside a `Resources`
   folder — so every bundle was missing the 85 files the game actually loads. It is committed now and the
   gate enforces it. **Before any "just press Play", ask what the runtime reads and whether that file is
   in the thing being shipped.**

---

## Art direction, in force

Painterly busts for the whole game; the flat neon look is **only** the after-hours sweep. Palette locked —
no new colours. Full state, including **everything already tried and failed**, is
`docs/ART-DIRECTION-STATE.md`. Read it before changing the face.

Cast: 7 humans + 7 synthetics, three women per side. Frame 192×216, 4 frames, never resize.

---

## Layout of the things you will actually open

| Path | What it is |
|---|---|
| `unity/Assets/Scripts/Core/` | pure C# rules: shift, scoring, sweep, paperwork, tell catalog |
| `unity/Assets/Scripts/Game/` | Unity layer: desk scene, input, HUD, interaction |
| `tools/art/make_applicants.py` | the applicant generator (v10) |
| `tools/art/painterly.py` | ramps + painter used by everything |
| `tools/art/make_sweep.py` | the flat sweep floors |
| `tools/art/check_proportions.py` | the structural gate that catches floating heads |
| `tools/CoreTests/` | 96 headless tests |
| `tools/UnityStubs/ASSEMBLY-RULES.md` | **read before touching a stub** |
| `docs/15-art-bible.md` | palette + style law |
| `docs/90-bug-log.md` | every failure and its cause, BUG-001…043 |
| `docs/BUNDLE-WORKFLOW.md` | how to take an update |

---

## What is next, in order

1. **The sweep scene.** Art exists (`art/kit/sweep/`), the rule exists (`Core/Rules/Sweep.cs`, tested),
   nothing spawns them. This is the next playable chunk.
2. **Faces at 3×.** Waiting on the player's verdict after playing v10. The likely fix is a larger dedicated
   portrait for the letter page; the desk keeps the bust.
3. **Sweep copy.** The 7 trace strings are BE2 placeholders.
