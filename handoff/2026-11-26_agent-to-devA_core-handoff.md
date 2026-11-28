# HANDOFF -- Agent -> Dev A (systems) -- Core game logic, verified

**Date:** 2026-11-26 - **Branch to create:** `feat/core-integration` - **Status:** ready to integrate

## What is done (and how it was verified)
Pure-C# gameplay core, 10 files, ~1,490 lines, **compiled and tested with the .NET 8 SDK**:

```
PASS x15  (generator invariants I1-I8, determinism, scoring, run loop, save round-trip, checksum)
Soak: 2,000 seeds / 46,000 applicants -> 0 invariant violations, 0.08 ms/run
```

Files (all under `unity/Assets/Scripts/`):
| File | Responsibility |
|---|---|
| `Core/HRZeroDay.Core.asmdef` | `noEngineReferences: true` -- keeps the core pure and testable |
| `Core/Data/Enums.cs` | EvidenceCategory, RevealSurface, Decision, GroundTruth, DecisionQuality, RunOutcome, Violation |
| `Core/Data/TellCatalog.cs` | 12 tell definitions + surface-unlock table (days 1-5) |
| `Core/Data/ApplicantRecord.cs` | record shown on the desk + `RecomputeFactualTells()` (source of truth for I3) |
| `Core/Data/RunGenerator.cs` | seeded generator, port of `prototype/generator_sim.py` |
| `Core/Data/RunValidator.cs` | checks I1-I8 on any generated run; `Fingerprint(seed)` for I7 |
| `Core/Rules/ScoringService.cs` | decision scoring, trust economy, debrief lines, missed-evidence reporting |
| `Core/Data/ShiftController.cs` | day loop: submit -> advance -> end day -> defeat/survive, save/restore |
| `Core/Data/SaveData.cs` | save schema v1 + `Migrate()` |
| `Core/Analytics/AnalyticsEvents.cs` | event models from `docs/10-analytics-spec.md` |
| `Tests/GeneratorInvariantTests.cs` | 15 tests, plain C#, runs headless and in Unity |

## What you must do
- [ ] Run `dotnet run --project tools\CoreTests` -> expect `ALL TESTS PASSED` (do this before and after any change)
- [ ] Add `Assets/Scripts/Tests/UnityTestAdapter.cs` (below) so the same tests show in Unity's Test Runner
- [ ] Write `SaveSystem` (Unity side): atomic write, autosave, corrupt-file recovery
- [ ] Write `ContentLoader` (StreamingAssets JSON on Android)
- [ ] Write `AnalyticsService` + `FakeBackend` + the "Export my play data" share-sheet button
- [ ] Write `GameManager` (persistent, owns `ShiftController`) + `00_Boot` scene

## Unity test adapter (copy this file in)
```csharp
using NUnit.Framework;
using HRZeroDay.Tests;

namespace HRZeroDay.Tests.Unity
{
    public class GeneratorInvariantTests
    {
        [Test] public void I1_I8_hold_for_200_seeds()          => Wrap(HRZeroDay.Tests.GeneratorInvariantTests.InvariantsHoldFor200Seeds);
        [Test] public void I7_same_seed_same_run()             => Wrap(HRZeroDay.Tests.GeneratorInvariantTests.SameSeedSameRun);
        [Test] public void I7_different_seed_different_run()   => Wrap(HRZeroDay.Tests.GeneratorInvariantTests.DifferentSeedDifferentRun);
        [Test] public void I4_exact_synthetic_count_per_day()  => Wrap(HRZeroDay.Tests.GeneratorInvariantTests.ExactSyntheticCounts);
        [Test] public void I1_synthetic_always_has_tell()      => Wrap(HRZeroDay.Tests.GeneratorInvariantTests.SyntheticAlwaysHasTell);
        [Test] public void I5_humans_never_carry_hard_tells()  => Wrap(HRZeroDay.Tests.GeneratorInvariantTests.HumansNeverCarryHardTells);
        [Test] public void I3_factual_tells_are_true()         => Wrap(HRZeroDay.Tests.GeneratorInvariantTests.FactualTellsAreTrue);
        [Test] public void I2_no_duplicate_identities()        => Wrap(HRZeroDay.Tests.GeneratorInvariantTests.NoDuplicateIdentities);
        [Test] public void Scoring_correct_approve()           => Wrap(HRZeroDay.Tests.GeneratorInvariantTests.ScoringApproveHuman);
        [Test] public void Scoring_wrongful_rejection()        => Wrap(HRZeroDay.Tests.GeneratorInvariantTests.ScoringWrongfulReject);
        [Test] public void Scoring_reject_right_reason()       => Wrap(HRZeroDay.Tests.GeneratorInvariantTests.ScoringRejectRightReason);
        [Test] public void Scoring_reject_wrong_reason()       => Wrap(HRZeroDay.Tests.GeneratorInvariantTests.ScoringRejectWrongReason);
        [Test] public void Shift_defeat_at_three_hires()       => Wrap(HRZeroDay.Tests.GeneratorInvariantTests.DefeatAtThreeHires);
        [Test] public void Save_roundtrip()                    => Wrap(HRZeroDay.Tests.GeneratorInvariantTests.SaveRoundTrip);
        [Test] public void Checksum_detects_tampering()        => Wrap(HRZeroDay.Tests.GeneratorInvariantTests.ChecksumDetectsTampering);

        private static void Wrap(System.Action test)
        {
            try { test(); }
            catch (System.Exception e) { Assert.Fail(e.Message); }
        }
    }
}
```

## Contract changes
None -- all new files. `SaveData` is v1; if you add fields, bump `CurrentVersion` and extend `Migrate()`.

## Known issues / do not touch
- `GenerationConfig.Days` and the tell weights are **tuned constants**; changing them requires re-running
  the soak test and logging the numbers in `docs/07-randomization-spec.md` section 8.
- `RunGenerator.CurrentYear` is hardcoded to 2026 on purpose (determinism). Do not use `DateTime.Now`
  inside the generator.

## How to verify end to end
1. `dotnet run --project tools\CoreTests` -> `ALL TESTS PASSED`
2. `dotnet run --project tools\CoreTests -- 2000` -> `invariant violations: 0`
3. In Unity: Test Runner -> EditMode -> 15 tests green
4. Call `new ShiftController(882731).Validate().Count` from any MonoBehaviour -> `0`
