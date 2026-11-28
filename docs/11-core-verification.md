# Core Verification -- how to run the C# core tests without opening Unity

The gameplay core (`unity/Assets/Scripts/Core/**`) is **pure C#**: no `UnityEngine` references
(`noEngineReferences: true` in its asmdef). That means it can be compiled and tested by a plain
.NET console app -- which is how it was verified before being handed over.

## Run it

Requires the .NET 8 SDK (Dev A: it ships with VS 2022; or `winget install Microsoft.DotNet.SDK.8`).

```powershell
cd C:\users\user\HR-ZeroDay
dotnet run --project tools\CoreTests              # run the 15 unit tests
dotnet run --project tools\CoreTests -- 2000      # soak: 2,000 seeds / 46,000 applicants
```

Expected:

```
PASS  I1_I8_hold_for_200_seeds
PASS  I7_same_seed_same_run
PASS  I7_different_seed_different_run
PASS  I4_exact_synthetic_count_per_day
PASS  I1_synthetic_always_has_reachable_tell
PASS  I5_humans_never_carry_hard_tells
PASS  I3_factual_tells_are_actually_true
PASS  I2_no_duplicate_identities_in_a_run
PASS  Scoring_correct_approve
PASS  Scoring_wrongful_rejection
PASS  Scoring_correct_reject_right_reason
PASS  Scoring_correct_reject_wrong_reason
PASS  Shift_defeat_at_three_synthetics_hired
PASS  Save_roundtrip_restores_same_run
PASS  Checksum_detects_tampering

ALL TESTS PASSED
```

Soak result recorded 2026-11-25:

| Metric | Python reference (`prototype/generator_sim.py`) | C# shipping code |
|---|---|---|
| applicants (2,000 seeds) | 46,000 | 46,000 |
| synthetic / human | 16,000 / 30,000 (35.0%) | 16,000 / 30,000 (34.8%) |
| tells per synthetic | min 1, median 2, max 3 | 1:2754, 2:7988, 3:5258 (median 2) |
| invariant violations | 0 | **0** |
| speed | 0.69 ms/run | **0.08 ms/run** |

The two implementations agree. The Python file stays in the repo as the readable specification; the
C# file is the thing that ships.

## Why the test file has no NUnit attributes

`GeneratorInvariantTests.cs` is written as plain C# with a tiny `Assert` helper so **one file** runs in
two places:

1. **Headless** via `tools/CoreTests` (above) -- what the agent and CI use.
2. **Unity Test Runner** via `Assets/Scripts/Tests/UnityTestAdapter.cs`, which wraps each method in an
   `[Test]` and calls `Assert.Fail` on exception.

When you add a test, add it to `RunAll()` as well so both paths pick it up.

## What is already covered

| Area | Tests |
|---|---|
| Generator invariants I1-I8 | 200 seeds, all five days, per-applicant |
| Determinism (I7) | same seed identical, different seed different |
| Scoring | approve human, reject human (wrongful), correct reject for the right reason, correct reject for the wrong reason |
| Run loop | 3 hired synthetics => `DefeatedPerfectEmployee` |
| Persistence | save -> restore keeps day, index, trust and the same applicant |
| ID checksum | valid, tampered, empty, null |

## Not yet written (Dev A's next tasks)

1. `SaveSystem` (Unity side): atomic write (`tmp` -> `File.Replace`), autosave on transition, corrupt-file recovery.
2. `ContentLoader`: read `StreamingAssets/content/*.json` on Android via `UnityWebRequest`, cache, verify hash.
3. `AnalyticsService`: memory buffer -> JSONL outbox -> `IBackend`; `FakeBackend` for tests; the *"Export my play data"* share-sheet button.
4. `RuleEngine` presentation glue: turns `tellsPresent` into inspectable hotspots on the desk.
5. `Boot` scene + `GameManager` that owns `ShiftController` and survives scene loads.
