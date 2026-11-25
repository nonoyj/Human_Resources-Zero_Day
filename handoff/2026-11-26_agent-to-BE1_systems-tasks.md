# HANDOFF — Agent → **BE1 (Systems dev)** — your task list

**Date:** 2026-11-26 · **Repo:** https://github.com/nonoyj/Human_Resources-Zero_Day
**Read first:** `docs/13-team-plan.md` (team split), `docs/02-tech-stack.md` (engine/folders),
`docs/07-randomization-spec.md` (the rules you are integrating).

You own the **Unity project + all systems**. The gameplay core is already written, tested and waiting:

```
unity/Assets/Scripts/Core/**     10 files, ~1,500 lines, pure C# (noEngineReferences: true)
unity/Assets/Scripts/Tests/**    15 tests
tools/CoreTests/                 headless runner (compiles the same files)
```

Verified state: 15/15 tests pass, 2,000-seed soak = 46,000 applicants, **0 invariant violations**.

---

## Your tasks, in order (do not reorder — each unblocks the next)

### 1. Create the Unity project skeleton — **BLOCKS THE WHOLE TEAM**
```powershell
# Unity Hub > New project > Universal 2D > name HRZeroDayTemp (create OUTSIDE the repo)
# close Unity, then:
$src = "C:\path\to\HRZeroDayTemp"
Copy-Item "$src\ProjectSettings" "C:\users\user\HR-ZeroDay\unity\ProjectSettings" -Recurse
Copy-Item "$src\Packages"        "C:\users\user\HR-ZeroDay\unity\Packages" -Recurse
Remove-Item $src -Recurse -Force
git add unity/ProjectSettings unity/Packages
git commit -m "chore(unity): create project skeleton (URP 2D)"
```
Check: `ProjectSettings/ProjectVersion.txt` says `6000.3.29f1`; `git status` shows **no** `Library/`.
Then: Unity Hub → Add project from disk → `C:\users\user\HR-ZeroDay\unity`.

### 2. Prove the core compiles in Unity
Copy the adapter from `handoff/2026-11-26_agent-to-devA_core-handoff.md` into
`Assets/Scripts/Tests/UnityTestAdapter.cs`. Window → General → Test Runner → **EditMode → Run All → 15 pass**.
If a test fails here but passed headless, the cause is almost always an asmdef reference — check
`HRZeroDay.Tests.asmdef` references `HRZeroDay.Core`.

### 3. Ship the three interfaces as STUBS — **by end of day 2, the FE dev is blocked without them**
From `docs/13-team-plan.md` §5: `IDeskDataSource`, `ISaveService`, `IAudioService`.
Stubs return canned data (one fake applicant, trust 78, day 1). Real implementations land behind them.

### 4. `SaveSystem`
Atomic write (`tmp` → `File.Replace`), autosave on every screen transition, corrupt-file recovery
(if parse fails: keep the bad file as `save.corrupt.bak`, start fresh, tell the player), path =
`Application.persistentDataPath`, version migration via `SaveData.Migrate()`.

### 5. `ContentLoader` + `IBackend`/`FakeBackend`
Read `Assets/StreamingAssets/content/*.json` on Android via `UnityWebRequest` (plain `File.IO` does not
work in an APK), verify `sha256`, cache. `FakeBackend` first so nothing depends on BE2.

### 6. `AnalyticsService`
Memory buffer → `JSONL` outbox in `persistentDataPath` → `IBackend`. Never throws, never blocks, never
touches gameplay. Honour consent opt-out. Add the *“Export my play data”* button (share sheet).

### 7. `GameManager` + `00_Boot` scene
Persistent, owns `ShiftController`, decides intro-vs-menu, hands control to FE scenes.

### 8. Rule-engine presentation glue
Expose `applicant.tellsPresent` as inspectable hotspots and tool results. **Never** expose
`groundTruth` to the UI layer — the FE must be able to cheat only by reading the save file.

### 9. Tests + perf
The 6 analytics/save tests in `docs/10-analytics-spec.md` §5. Profile: applicant layer < 1.5 ms CPU,
60 fps on a mid-range device.

---

## Rules that apply to you

- `Scripts/Core` is **frozen API** — if you need a change, write a handoff; do not edit it casually
  (the 15 tests are the contract).
- Do **not** touch `Assets/Scenes`, `Assets/Prefabs`, `Assets/Scripts/UI` — those are the FE dev's.
- Never commit `Library/`, `Temp/`, `Logs/`, `obj/`, `*.apk`.
- Log every non-trivial bug in `docs/90-bug-log.md` **before** fixing it, with a Prevention rule.

## Report back in this format
```
STATUS / DONE (files) / NEXT / BLOCKED ON + suggested default / RISK / EVIDENCE
```
