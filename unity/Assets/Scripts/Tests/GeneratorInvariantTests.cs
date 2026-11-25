// HR//0D -- EditMode tests for the generator and the scoring rules.
// Assembly: HRZeroDay.Tests (Editor only)
//
// These are written as PLAIN C# (no NUnit references) so the identical file can be compiled and run
// both in Unity's Test Runner AND in a plain .NET console harness by the agent/CI. The Unity-side
// wrapper that adapts them to NUnit lives in UnityTestAdapter.cs.
//
// Run locally without Unity:
//     dotnet run --project tools/CoreTests
using System;
using System.Collections.Generic;
using HRZeroDay.Core.Data;
using HRZeroDay.Core.Rules;

namespace HRZeroDay.Tests
{
    public static class GeneratorInvariantTests
    {
        public static IEnumerable<string> RunAll()
        {
            yield return Test("I1_I8_hold_for_200_seeds", InvariantsHoldFor200Seeds);
            yield return Test("I7_same_seed_same_run", SameSeedSameRun);
            yield return Test("I7_different_seed_different_run", DifferentSeedDifferentRun);
            yield return Test("I4_exact_synthetic_count_per_day", ExactSyntheticCounts);
            yield return Test("I1_synthetic_always_has_reachable_tell", SyntheticAlwaysHasTell);
            yield return Test("I5_humans_never_carry_hard_tells", HumansNeverCarryHardTells);
            yield return Test("I3_factual_tells_are_actually_true", FactualTellsAreTrue);
            yield return Test("I2_no_duplicate_identities_in_a_run", NoDuplicateIdentities);
            yield return Test("Scoring_correct_approve", ScoringApproveHuman);
            yield return Test("Scoring_wrongful_rejection", ScoringWrongfulReject);
            yield return Test("Scoring_correct_reject_right_reason", ScoringRejectRightReason);
            yield return Test("Scoring_correct_reject_wrong_reason", ScoringRejectWrongReason);
            yield return Test("Shift_defeat_at_three_synthetics_hired", DefeatAtThreeHires);
            yield return Test("Save_roundtrip_restores_same_run", SaveRoundTrip);
            yield return Test("Checksum_detects_tampering", ChecksumDetectsTampering);
        }

        private static string Test(string name, Action body)
        {
            try { body(); return "PASS  " + name; }
            catch (Exception e) { return "FAIL  " + name + "  ->  " + e.Message; }
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new Exception(message);
        }

        // ---------------------------------------------------------------- I1..I8
        public static void InvariantsHoldFor200Seeds()
        {
            for (int seed = 1; seed <= 200; seed++)
            {
                var c = new ShiftController(seed);
                var violations = c.Validate();
                // NOTE: build the message lazily -- Assert's message is evaluated even on success.
                Assert(violations.Count == 0, violations.Count == 0
                    ? ""
                    : "seed " + seed + " produced " + violations.Count + " violations, first: " + violations[0]);
            }
        }

        public static void SameSeedSameRun()
        {
            string a, b;
            bool det = RunValidator.IsDeterministic(882731, out a, out b);
            Assert(det, "same seed produced different fingerprints");
            var first = RunValidator.Fingerprint(424242);
            var second = RunValidator.Fingerprint(424242);
            Assert(first == second, "fingerprints differ for the same seed");
            Assert(first.Length > 100, "fingerprint suspiciously short");
        }

        public static void DifferentSeedDifferentRun()
        {
            var a = RunValidator.Fingerprint(1001);
            var b = RunValidator.Fingerprint(1002);
            Assert(a != b, "two different seeds produced an identical run");
        }

        public static void ExactSyntheticCounts()
        {
            var plan = new GenerationConfig().Days;
            for (int seed = 1; seed <= 50; seed++)
            {
                var c = new ShiftController(seed);
                foreach (var kv in plan)
                {
                    int synth = 0;
                    foreach (var a in c.AllDays[kv.Key]) if (a.IsSynthetic) synth++;
                    Assert(synth == kv.Value.Synthetics,
                        "seed " + seed + " day " + kv.Key + ": " + synth + " synthetics, expected " + kv.Value.Synthetics);
                }
            }
        }

        public static void SyntheticAlwaysHasTell()
        {
            for (int seed = 1; seed <= 100; seed++)
            {
                var c = new ShiftController(seed);
                foreach (var day in c.AllDays.Keys)
                    foreach (var a in c.AllDays[day])
                        if (a.IsSynthetic)
                            Assert(a.tellsPresent.Count > 0, "seed " + seed + ": synthetic " + a.id + " has no tell");
            }
        }

        public static void HumansNeverCarryHardTells()
        {
            var hardTells = new[] { TellCatalog.PortraitAsymmetry, TellCatalog.PortraitBlendSeam,
                                    TellCatalog.IdentityClone, TellCatalog.IdChecksum };
            for (int seed = 1; seed <= 100; seed++)
            {
                var c = new ShiftController(seed);
                foreach (var day in c.AllDays.Keys)
                    foreach (var a in c.AllDays[day])
                    {
                        if (a.Truth != GroundTruth.Human) continue;
                        foreach (var t in hardTells)
                            Assert(!a.tellsPresent.Contains(t),
                                "seed " + seed + ": human " + a.id + " carries hard tell " + t);
                    }
            }
        }

        public static void FactualTellsAreTrue()
        {
            int checkedCount = 0;
            for (int seed = 1; seed <= 100; seed++)
            {
                var c = new ShiftController(seed);
                foreach (var day in c.AllDays.Keys)
                    foreach (var a in c.AllDays[day])
                    {
                        var factual = a.RecomputeFactualTells();
                        foreach (var t in a.tellsPresent)
                        {
                            if (!TellCatalog.Has(t) || !TellCatalog.Get(t).IsFactual) continue;
                            checkedCount++;
                            Assert(factual.Contains(t),
                                "seed " + seed + ": " + a.id + " claims " + t + " but the record does not contradict");
                        }
                    }
            }
            Assert(checkedCount > 20, "expected to check many factual tells, checked " + checkedCount);
        }

        public static void NoDuplicateIdentities()
        {
            for (int seed = 1; seed <= 100; seed++)
            {
                var names = new HashSet<string>();
                var ids = new HashSet<string>();
                var c = new ShiftController(seed);
                foreach (var day in c.AllDays.Keys)
                    foreach (var a in c.AllDays[day])
                    {
                        Assert(names.Add(a.fullName), "seed " + seed + ": duplicate name " + a.fullName);
                        Assert(ids.Add(a.idNumber), "seed " + seed + ": duplicate id " + a.idNumber);
                    }
            }
        }

        // ---------------------------------------------------------------- scoring
        private static ApplicantRecord Human()
        {
            return new ApplicantRecord
            {
                id = "t_h1", fullName = "Honest Human", age = 30, idNumber = "PH-1111-2208",
                graduationYear = 2016, employerFounded = 1990, employmentStartYear = 2018,
                yearsExperience = 6, archetypeRaw = (int)Archetype.HumanSolid,
                groundTruthRaw = (int)GroundTruth.Human
            };
        }

        private static ApplicantRecord Synthetic()
        {
            var a = new ApplicantRecord
            {
                id = "t_s1", fullName = "Synthetic One", age = 30, idNumber = "PH-1234-5601",
                graduationYear = 2016, employerFounded = 1990, employmentStartYear = 2018,
                yearsExperience = 6, archetypeRaw = (int)Archetype.SyntheticPolished,
                groundTruthRaw = (int)GroundTruth.Synthetic
            };
            a.yearsExperience = a.CareerSpanYears + 3;      // real contradiction
            a.tellsPresent.Add(TellCatalog.ExpVsGrad);
            return a;
        }

        public static void ScoringApproveHuman()
        {
            var input = new DecisionInput { Applicant = Human(), Decision = Decision.Approve, SecondsOnCard = 30 };
            var r = ScoringService.Evaluate(input);
            Assert(r.IsCorrect, "approving an honest human must be correct");
            Assert(r.TrustDelta > 0, "approving an honest human should raise trust");
            Assert(r.SyntheticHired == 0, "no synthetic was hired");
        }

        public static void ScoringWrongfulReject()
        {
            var input = new DecisionInput
            {
                Applicant = Human(), Decision = Decision.Reject, Reason = EvidenceCategory.DocInconsistency
            };
            var r = ScoringService.Evaluate(input);
            Assert(!r.IsCorrect, "rejecting an honest human must be incorrect");
            Assert(r.WrongfulRejection == 1, "wrongful rejection must be counted");
            Assert(r.TrustDelta < 0, "wrongful rejection must cost trust");
            Assert(!string.IsNullOrEmpty(r.DebriefLine), "debrief line must exist");
        }

        public static void ScoringRejectRightReason()
        {
            var a = Synthetic();
            var input = new DecisionInput
            {
                Applicant = a, Decision = Decision.Reject, Reason = EvidenceCategory.DocInconsistency
            };
            input.TellsInspected.Add(TellCatalog.ExpVsGrad);
            var r = ScoringService.Evaluate(input);
            Assert(r.IsCorrect, "rejecting a synthetic is correct");
            Assert(r.Quality == DecisionQuality.CorrectRightReason, "reason matched the evidence");
            Assert(r.MissedEvidence.Count == 0, "the player inspected the only tell");
        }

        public static void ScoringRejectWrongReason()
        {
            var a = Synthetic();
            var input = new DecisionInput
            {
                Applicant = a, Decision = Decision.Reject, Reason = EvidenceCategory.SyntheticMedia
            };
            var r = ScoringService.Evaluate(input);
            Assert(r.IsCorrect, "the call was still right");
            Assert(r.Quality == DecisionQuality.CorrectWrongReason, "but the reason was unsupported");
            Assert(r.MissedEvidence.Contains(TellCatalog.ExpVsGrad), "missed evidence must be reported");
        }

        // ---------------------------------------------------------------- loop + save
        public static void DefeatAtThreeHires()
        {
            var c = new ShiftController(777);
            int hired = 0;
            for (int guard = 0; guard < 20 && !c.RunFinished; guard++)
            {
                if (c.DayFinished) { c.EndDay(); continue; }
                var a = c.Current;
                var input = new DecisionInput
                {
                    Applicant = a,
                    Decision = a.IsSynthetic ? Decision.Approve : Decision.Approve
                };
                if (a.IsSynthetic) hired++;
                c.Submit(input);
                if (hired >= 3) break;
            }
            Assert(c.Outcome == RunOutcome.DefeatedPerfectEmployee,
                "hiring 3 synthetics must end the run, outcome was " + c.Outcome);
        }

        public static void SaveRoundTrip()
        {
            var c = new ShiftController(31337);
            var input = new DecisionInput { Applicant = c.Current, Decision = Decision.Approve };
            c.Submit(input);

            var save = new SaveData();
            c.WriteTo(save);
            var errors = RunValidator.ValidateRun(c.AllDays);
            Assert(errors.Count == 0, "generated run invalid");

            var restored = ShiftController.FromSave(save);
            Assert(restored.Day == c.Day, "day must survive the round trip");
            Assert(restored.CurrentIndex == c.CurrentIndex, "applicant index must survive the round trip");
            Assert(restored.Trust == c.Trust, "trust must survive the round trip");
            Assert(restored.Current.fullName == c.Current.fullName,
                "the same seed must restore the same applicant: " + restored.Current.fullName + " vs " + c.Current.fullName);
        }

        public static void ChecksumDetectsTampering()
        {
            Assert(ApplicantRecord.IdChecksumValid("PH-1111-2208"), "valid checksum rejected");
            Assert(!ApplicantRecord.IdChecksumValid("PH-1111-2209"), "tampered checksum accepted");
            Assert(!ApplicantRecord.IdChecksumValid(""), "empty id accepted");
            Assert(!ApplicantRecord.IdChecksumValid(null), "null id accepted");
        }
    }
}
