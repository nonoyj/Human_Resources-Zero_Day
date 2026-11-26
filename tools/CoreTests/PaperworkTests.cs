// HR//0D -- tests for the two rulings the user made on 2026-11-26:
//   Q1 "slow, do not stop"  -> ShiftClock runs the shift at 25% while the letter is open
//   Q2 "no reopening"       -> Paperwork locks on CloseForStamp() and refuses every later edit
using System;
using System.Collections.Generic;
using System.Linq;
using HRZeroDay.Core.Data;

namespace HRZeroDay.Tests
{
    public static class PaperworkTests
    {
        public static IEnumerable<string> RunAll()
        {
            yield return ReadingRunsTheClockAtAQuarterSpeed();
            yield return PausedStopsAndRunningIsFullSpeed();
            yield return RunningOutClampsAtZero();
            yield return ClockFormatRoundsDown();
            yield return LongReadingCostsALittleNotEverything();
            yield return LetterCannotBeReopenedAfterTheStamp();
            yield return FlagsCannotChangeAfterTheStamp();
            yield return TrustDeltaMatchesTheFlagScoreDirection();
            yield return LetterIsDeterministicPerRunAndApplicant();
            yield return GeneratedLetterAgreesWithItsOwnFlags();
        }

        private static ApplicantRecord Human(bool bait = false, string referral = "WALK-IN") => new ApplicantRecord
        {
            id = "A-0004", day = 2, fullName = "Maria Santos", role = "Data Entry II",
            idNumber = "8814-2231", university = "Cebu Normal University", graduationYear = 2019,
            employer = "Northstar Logistics", yearsExperience = 6, referral = referral,
            coverLetterId = "CL-0004", archetypeRaw = (int)Archetype.HumanSolid,
            groundTruthRaw = bait ? (int)GroundTruth.HumanBait : (int)GroundTruth.Human
        };

        private static ApplicantRecord Synthetic() => new ApplicantRecord
        {
            id = "A-0011", day = 3, fullName = "Lester Cruz", role = "Data Entry I",
            idNumber = "1122-3344", university = "Cebu Normal University", graduationYear = 2020,
            employer = "Northstar Logistics", yearsExperience = 4, referral = "WALK-IN",
            coverLetterId = "CL-0011", archetypeRaw = (int)Archetype.SyntheticPolished,
            groundTruthRaw = (int)GroundTruth.Synthetic
        };

        private static string ReadingRunsTheClockAtAQuarterSpeed()
        {
            double t = ShiftClock.Advance(100, 4, ClockState.Reading);
            if (Math.Abs(t - 99.0) > 1e-9) return $"FAIL  reading_costs_{100 - t}s_for_4s_wall (expected 1s)";
            return "PASS  4 s of wall time costs 1 s of shift time while reading (scale 0.25)";
        }

        private static string PausedStopsAndRunningIsFullSpeed()
        {
            if (ShiftClock.Advance(50, 10, ClockState.Paused) != 50) return "FAIL  pause_let_the_clock_move";
            if (ShiftClock.Advance(50, 10, ClockState.Running) != 40) return "FAIL  running_is_not_realtime";
            return "PASS  pause freezes the clock, running is 1:1 -- the pause button is still the only stop";
        }

        private static string RunningOutClampsAtZero()
        {
            if (ShiftClock.Advance(2, 100, ClockState.Running) != 0) return "FAIL  clock_went_negative";
            if (!ShiftClock.Expired(0)) return "FAIL  zero_is_not_expired";
            return "PASS  the clock clamps at 0:00 and never reports a negative remaining time";
        }

        private static string ClockFormatRoundsDown()
        {
            var cases = new[] { (180.0, "3:00"), (59.9, "0:59"), (61.0, "1:01"), (0.4, "0:00"), (-5.0, "0:00") };
            foreach (var c in cases)
            {
                var got = ShiftClock.Format(c.Item1);
                if (got != c.Item2) return $"FAIL  format({c.Item1})='{got}' expected '{(c.Item2)}'";
            }
            return "PASS  clock reads 3:00 / 0:59 / 1:01 / 0:00 -- it only shows 0:00 when it is actually out";
        }

        private static string LongReadingCostsALittleNotEverything()
        {
            // 60 s of reading inside a 180 s applicant: 15 s spent, 165 s left. Free would be 180, stopped
            // would be 180 forever -- this is the number the user chose between.
            double t = 180;
            for (int i = 0; i < 60; i++) t = ShiftClock.Advance(t, 1, ClockState.Reading);
            if (Math.Abs(t - 165) > 1e-6) return $"FAIL  minute_of_reading_left_{t:0.##}s";
            return "PASS  a full minute of reading costs 15 s of a 180 s applicant -- cheap, not free";
        }

        private static string LetterCannotBeReopenedAfterTheStamp()
        {
            var p = Paperwork.For(Human(), 1234);
            if (!p.CanOpen) return "FAIL  letter_locked_before_the_stamp";
            p.CloseForStamp();
            if (p.CanOpen) return "FAIL  letter_reopened_after_the_stamp (user ruled: one read only)";
            return "PASS  the letter opens once and is closed for good at the stamp";
        }

        private static string FlagsCannotChangeAfterTheStamp()
        {
            var p = Paperwork.For(Human(), 1234);
            var id = p.flags[0].id;
            p.Toggle(id);
            int scoreAtStamp = p.Score;
            p.CloseForStamp();
            bool edited = p.Toggle(id);
            if (edited) return "FAIL  toggle_succeeded_after_the_stamp";
            if (p.Score != scoreAtStamp) return "FAIL  score_changed_after_the_stamp";
            return "PASS  flags are frozen at the stamp -- the judgement cannot be revised once committed";
        }

        private static string TrustDeltaMatchesTheFlagScoreDirection()
        {
            var fake = Paperwork.For(Synthetic(), 99);
            var trueFlag = fake.flags.First(f => f.isReal).id;
            fake.Toggle(trueFlag);
            if (fake.Score != 1) return $"FAIL  one_true_flag_scored_{fake.Score}";
            if (fake.TrustDelta <= 0) return "FAIL  catching_a_fake_did_not_pay_trust";

            var clean = Paperwork.For(Human(), 99);
            clean.Toggle(clean.flags.First(f => !f.isReal).id);
            if (clean.Score != -1) return $"FAIL  one_false_flag_scored_{clean.Score}";
            if (clean.TrustDelta >= 0) return "FAIL  a_wrong_flag_did_not_cost_trust";
            return $"PASS  a right flag pays (+{fake.TrustDelta} trust), a wrong one costs ({clean.TrustDelta})";
        }

        private static string LetterIsDeterministicPerRunAndApplicant()
        {
            var a = Paperwork.For(Synthetic(), 77);
            var b = Paperwork.For(Synthetic(), 77);
            if (a.body != b.body) return "FAIL  same_seed_same_applicant_gave_a_different_letter (I7)";
            var c = Paperwork.For(Synthetic(), 78);
            if (a.body == c.body && a.signature == c.signature)
                return "FAIL  a different run produced an identical letter -- the lore seed is not being used";
            return "PASS  the letter is a pure function of (run seed, applicant id): I7 holds";
        }

        private static string GeneratedLetterAgreesWithItsOwnFlags()
        {
            // The flags are derived FROM the body, so a generated fake must always carry at least one
            // true flag and a generated honest letter must carry none. If generation and flagging ever
            // drift apart, the player is punished for reading correctly.
            for (int seed = 1; seed <= 40; seed++)
            {
                var honest = Paperwork.For(Human(), seed);
                if (honest.flags.Any(f => f.isReal))
                    return $"FAIL  honest_letter_seed_{seed}_has_a_true_flag";

                var synth = Paperwork.For(Synthetic(), seed);
                if (!synth.flags.Any(f => f.isReal))
                    return $"FAIL  synthetic_letter_seed_{seed}_has_no_true_flag";

                var bait = Paperwork.For(Human(true), seed);
                if (!bait.flags.Any(f => f.isReal))
                    return $"FAIL  bait_letter_seed_{seed}_has_no_true_flag";
            }
            return "PASS  40 seeds: honest letters raise 0 true flags, synthetic and bait letters always raise at least one";
        }
    }
}
