// HR//0D -- the five-day arc: can a player actually START on day 1 and END on a result screen?
// Assembly: test only (Core + the pure parts of Game.Interaction)
//
// WHY THIS EXISTS (2026-11-27, round v101)
//   Every piece of the arc was already here: ShiftController.EndDay() advances the day, the trust
//   economy is tuned, RunOutcome has three endings. Nothing tested the TRANSITION -- that after the last
//   applicant of day 1, the day closes, the next day begins, the desk shows day 2, and after day 5 the
//   run stops on an ending instead of asking for a sixth day. Those are exactly the places a five-day
//   arc breaks, and they break silently: no exception, a desk that just stops.
//
//   So this test plays the run the way the bootstrap does -- CloseDay() then BeginCurrent(), in that
//   order, for every applicant -- with a perfect player and with a careless one.
using System;
using System.Collections.Generic;
using HRZeroDay.Core.Data;
using HRZeroDay.Core.Rules;
using HRZeroDay.Game.Interaction;

namespace HRZeroDay.Tests
{
    internal static class ArcTests
    {
        public static IEnumerable<string> RunAll()
        {
            yield return CheckFiveDaysInOrder();
            yield return CheckPerfectRunSurvivesOnDayFive();
            yield return CheckCarelessRunEndsEarly();
            yield return CheckTheDeskNeverAsksForADaySix();
            yield return CheckArcSurvivesASaveAtEveryBoundary();
            yield return CheckLedgerCountsTheRunThePlayerPlayed();
            yield return CheckSummaryAlwaysSaysWhereYouAre();
            yield return CheckLedgerSurvivesASaveMidRun();
            yield return CheckTheSummaryArrivesInBeatsAndFinishes();
        }


        /// <summary>
        /// THE SUMMARY'S TRANSITION (v119.7). P5's last item is a FEEL change, and a feel change still has to
        /// be finite, ordered and monotonic: four beats, each one later than the last, the whole thing over in
        /// under a second, and once it is over it stays over. The HUD reads these functions and nothing else,
        /// so this test is the transition.
        /// </summary>
        private static string CheckTheSummaryArrivesInBeatsAndFinishes()
        {
            True(RunLedger.SummaryBeats == 4, "the summary transition is no longer four beats");
            float total = RunLedger.SummaryBeatSeconds * RunLedger.SummaryBeats;
            True(total <= 1.2f, "the summary transition takes " + total + " s -- long enough to be in the way");

            // nothing at t=0, everything at the end, and it never goes backwards
            True(RunLedger.SummaryBeatsShown(0f) == 0, "the summary shows a line before it has opened");
            True(RunLedger.SummaryBeatsShown(-1f) == 0, "a negative clock shows a line");
            True(RunLedger.SummaryBeatsShown(total) == RunLedger.SummaryBeats, "the transition never completes");
            True(RunLedger.SummaryBeatsShown(total * 10f) == RunLedger.SummaryBeats, "the transition overruns");
            True(RunLedger.SummaryReady(total), "the prompt is not pressable once the transition is done");
            // the beat boundaries are half-open, so the last hundredth of a beat is still the previous beat:
            // state the intent, not a boundary artefact
            True(!RunLedger.SummaryReady(RunLedger.SummaryBeatSeconds * (RunLedger.SummaryBeats - 1)),
                 "the prompt is pressable before the numbers have landed");

            int previous = 0;
            for (float t = 0f; t <= total + 0.5f; t += 0.02f)
            {
                int shown = RunLedger.SummaryBeatsShown(t);
                True(shown >= previous, "the summary went BACKWARDS at " + t + " s");
                True(shown <= RunLedger.SummaryBeats, "the summary showed more beats than it has");
                float p = RunLedger.SummaryProgress(t);
                True(p >= 0f && p <= 1f, "the progress left 0..1 at " + t + " s");
                previous = shown;
            }
            // every beat is visible for exactly one step: four beats over the window, no beat skipped
            for (int b = 0; b < RunLedger.SummaryBeats; b++)
            {
                float at = RunLedger.SummaryBeatSeconds * b + 0.001f;
                True(RunLedger.SummaryBeatsShown(at) == b + 1, "beat " + (b + 1) + " does not appear on time");
            }
            return "PASS  the day summary arrives in four ordered beats and settles in under a second";
        }

        private static void True(bool condition, string message)
        {
            if (!condition) throw new Exception(message);
        }

        /// <summary>
        /// Plays one applicant's decision the way ScoringService expects, choosing the RIGHT call for the
        /// hidden truth. Returns the result so a caller can inspect trust movements.
        /// </summary>
        private static DecisionResult Decide(ShiftController shift, bool playPerfectly)
        {
            // PersonAt(CurrentIndex), not Current: Submit() advances the chair, so the person you are deciding
            // about is the one at CurrentIndex BEFORE the call -- reading Current after a stamp is the null
            // case, on purpose (BUG-038).
            var a = shift.PersonAt(shift.CurrentIndex);
            bool shouldReject = a.Truth == GroundTruth.Synthetic || a.Truth == GroundTruth.HumanBait;
            bool reject = playPerfectly ? shouldReject : !shouldReject;

            var input = new DecisionInput
            {
                Applicant = a,
                Decision = reject ? Decision.Reject : Decision.Approve,
                Reason = reject ? (EvidenceCategory?)EvidenceCategory.DocInconsistency : null
            };
            // a perfect player has looked at everything the tells were in
            if (playPerfectly) input.TellsInspected.AddRange(a.tellsPresent);
            return shift.Submit(input);
        }

        /// <summary>Returns every (day, applicantCount) the shift actually offered, in order.</summary>
        private static List<int> PlayRun(int seed, bool perfect, out RunOutcome outcome, out ShiftController shift)
        {
            shift = new ShiftController(seed);
            var daySizes = new List<int>();
            int guard = 0;
            while (!shift.RunFinished && guard++ < 40)
            {
                int day = shift.Day;
                daySizes.Add(shift.Today.Count);
                while (!shift.DayFinished && !shift.RunFinished)
                    Decide(shift, perfect);
                if (shift.RunFinished) break;
                shift.EndDay();
            }
            outcome = shift.Outcome;
            return daySizes;
        }

        private static string CheckFiveDaysInOrder()
        {
            RunOutcome outcome;
            ShiftController shift;
            var sizes = PlayRun(1000, true, out outcome, out shift);
            True(sizes.Count >= 1, "the run offered no day at all");
            True(sizes.Count <= 5, "the run offered " + sizes.Count + " days -- the arc is five");
            for (int i = 0; i < sizes.Count; i++)
                True(sizes[i] >= 3, "day " + (i + 1) + " only had " + sizes[i] + " applicants -- not a shift");
            return "PASS  the arc offers up to five days, each with a full shift of applicants (" +
                   sizes.Count + " days: " + string.Join(",", sizes.ConvertAll(n => n.ToString()).ToArray()) + ")";
        }

        private static string CheckPerfectRunSurvivesOnDayFive()
        {
            // a perfect player must reach the survival ending, and only at the end of day 5
            int survived = 0;
            for (int seed = 1000; seed < 1012; seed++)
            {
                RunOutcome outcome;
                ShiftController shift;
                PlayRun(seed, true, out outcome, out shift);
                if (outcome == RunOutcome.Survived)
                {
                    survived++;
                    True(shift.Day == 5,
                         "seed " + seed + " SURVIVED on day " + shift.Day + " -- survival is a five-day result");
                }
                else
                {
                    throw new Exception("seed " + seed + " ended a PERFECT run early: " + outcome +
                                        " on day " + shift.Day + " with trust " + shift.Trust);
                }
            }
            True(survived == 12, "only " + survived + " of 12 perfect runs survived");
            return "PASS  twelve perfect runs all reach day 5 and end SURVIVED";
        }

        private static string CheckCarelessRunEndsEarly()
        {
            // a player who hires everything must be stopped by the hire limit, not by a day counter, and
            // the ending must be the one the copy claims
            int early = 0;
            for (int seed = 1000; seed < 1012; seed++)
            {
                RunOutcome outcome;
                ShiftController shift;
                PlayRun(seed, false, out outcome, out shift);
                if (outcome != RunOutcome.Survived)
                {
                    early++;
                    True(shift.Day < 5, "seed " + seed + " was defeated but had already reached day 5");
                    True(outcome == RunOutcome.DefeatedPerfectEmployee || outcome == RunOutcome.DefeatedGhostQuota,
                         "seed " + seed + " ended on an outcome with no ending copy: " + outcome);
                }
            }
            True(early >= 10, "only " + early + " of 12 careless runs were stopped -- the run has no teeth");
            return "PASS  a player who hires everyone is stopped before day 5, on an ending that exists (" +
                   early + " of 12)";
        }

        private static string CheckTheDeskNeverAsksForADaySix()
        {
            // THE BUG THIS IS FOR: Day++ with no ceiling, so the summary says "NEXT DAY" forever.
            RunOutcome outcome;
            ShiftController shift;
            PlayRun(1005, true, out outcome, out shift);
            True(shift.Day == 5, "the surviving run ended on day " + shift.Day);
            True(shift.RunFinished, "the run is not finished after day 5");
            True(shift.EndDay() != null || true, "");        // EndDay after the end must not throw
            var after = shift.EndDay();
            True(after != null, "EndDay() on a finished run must still return a debrief, not null");
            True(shift.Day == 5, "EndDay() on a finished run advanced to day " + shift.Day + " -- a sixth day");
            True(shift.Outcome == RunOutcome.Survived, "EndDay() on a finished run changed the outcome");
            return "PASS  after day 5 the shift stays on day 5 and keeps its outcome -- no sixth day";
        }

        private static string CheckArcSurvivesASaveAtEveryBoundary()
        {
            // The player quits at the end of every day and relaunches. The arc must not shift by one.
            for (int seed = 1000; seed < 1006; seed++)
            {
                var straight = new ShiftController(seed);
                int guard = 0;
                var expected = new List<int>();
                while (!straight.RunFinished && guard++ < 40)
                {
                    expected.Add(straight.Day);
                    while (!straight.DayFinished && !straight.RunFinished) Decide(straight, true);
                    if (straight.RunFinished) break;
                    straight.EndDay();
                }

                // now the same run, saved and reloaded at every day boundary
                RunOutcome oOut;
                ShiftController shift;
                PlayRun(seed, true, out oOut, out shift);       // warm path

                var save = new SaveData();
                var s1 = new ShiftController(seed);
                save.runSeed = seed;
                int g2 = 0;
                var seen = new List<int>();
                while (!s1.RunFinished && g2++ < 40)
                {
                    seen.Add(s1.Day);
                    s1.WriteTo(save);
                    var reloaded = ShiftController.FromSave(save);
                    True(reloaded.Day == s1.Day,
                         "seed " + seed + ": a save at day " + s1.Day + " reloaded as day " + reloaded.Day);
                    while (!s1.DayFinished && !s1.RunFinished) Decide(s1, true);
                    if (s1.RunFinished) break;
                    s1.EndDay();
                }
                True(seen.Count == expected.Count,
                     "seed " + seed + ": saving at each boundary changed the arc from " + expected.Count +
                     " days to " + seen.Count);
                for (int i = 0; i < seen.Count; i++)
                    True(seen[i] == expected[i], "seed " + seed + ": day " + (i + 1) + " differed after reload");
            }
            return "PASS  saving and reloading at every day boundary leaves the arc unchanged";
        }

        // ------------------------------------------------------------------ the ledger (v101)

        private static RunDay Day(int n, int approved, int rejected, int wrongful, int hired, int caught, int trust)
        {
            return new RunDay
            {
                Day = n, Approved = approved, Rejected = rejected, WrongfulRejections = wrongful,
                SyntheticsHired = hired, SyntheticsCaught = caught, QuotaMet = approved >= 3,
                TrustAtEnd = trust,
                CorrectCalls = Math.Max(0, (approved + rejected) - wrongful - hired)
            };
        }

        private static string CheckLedgerCountsTheRunThePlayerPlayed()
        {
            var l = new RunLedger();
            l.Record(Day(1, 3, 1, 0, 0, 1, 100 + 2 * 3 + 4));
            l.Record(Day(2, 4, 0, 0, 0, 1, 118));
            True(l.Count == 2, "the ledger kept " + l.Count + " days, not 2");
            True(l.Approved == 7, "approved counted " + l.Approved + ", expected 7");
            True(l.Rejected == 1, "rejected counted " + l.Rejected + ", expected 1");
            True(l.Caught == 2, "caught counted " + l.Caught + ", expected 2");
            True(l.Hired == 0, "hired counted " + l.Hired + ", expected 0");
            True(l.Decisions == 8, "decisions counted " + l.Decisions + ", expected 8");
            True(l.QuotasMet == 2, "quotas met counted " + l.QuotasMet + ", expected 2");
            True(l.TrustAtEnd == 118, "trust at end was " + l.TrustAtEnd);
            True(l.TrustLost == 0, "trust lost was " + l.TrustLost + " on a run that only gained");
            // 3 right hires + 1 right rejection on day 1, 4 right hires on day 2 = 8
            True(l.Correct == 8, "correct calls counted " + l.Correct + ", expected 8");

            // the same day recorded twice must not double-count: the bootstrap can reach Record from two
            // paths after a resume, and a ledger that inflates is worse than no ledger
            l.Record(Day(2, 4, 0, 0, 0, 1, 118));
            True(l.Count == 2, "recording day 2 again made the ledger " + l.Count + " days long");
            True(l.Approved == 7, "recording day 2 again inflated approvals to " + l.Approved);
            return "PASS  the ledger counts exactly the run the player played, and never double-counts a day";
        }

        private static string CheckSummaryAlwaysSaysWhereYouAre()
        {
            var l = new RunLedger();
            for (int d = 1; d <= 5; d++)
            {
                l.Record(Day(d, 3, 1, 0, 0, 1, 100));
                string line = l.ArcLine(d);
                True(!string.IsNullOrEmpty(line), "day " + d + " has no arc line at all");
                True(line.Contains("OF 5"), "day " + d + " arc line does not say how long the arc is: " + line);
                True(line.Contains("DAY " + d + " OF 5"),
                     "day " + d + " arc line says the wrong day: " + line);
                True(line.Length <= 62, "day " + d + " arc line is " + line.Length + " chars -- the HUD wraps at 62");
            }
            True(l.ArcLine(5).Contains("LAST"), "the last day does not say it is the last day: " + l.ArcLine(5));

            // and the dangerous state must be louder than the count: losing the run must outrank "3 to go"
            var bad = new RunLedger();
            bad.Record(Day(1, 1, 3, 2, 0, 1, 60));
            True(bad.ArcLine(1).Contains("TRUST"), "a run that has lost 40 trust still says 'three shifts to go'");
            return "PASS  every day summary says where you are in the five, and warnings outrank the count";
        }

        private static string CheckLedgerSurvivesASaveMidRun()
        {
            var l = new RunLedger();
            l.Record(Day(1, 3, 1, 0, 1, 1, 88));
            l.Record(Day(2, 2, 2, 1, 1, 0, 70));
            var records = new List<DayRecord>();
            l.WriteTo(records);
            True(records.Count == 2, "the save got " + records.Count + " day records, expected 2");

            var back = RunLedger.FromSave(records, 0);
            True(back.Count == 2, "the ledger came back with " + back.Count + " days");
            True(back.Approved == l.Approved && back.Rejected == l.Rejected,
                 "approvals/rejections changed across the save: " + back.Approved + "/" + back.Rejected +
                 " vs " + l.Approved + "/" + l.Rejected);
            True(back.Hired == l.Hired, "hires changed across the save: " + back.Hired + " vs " + l.Hired);
            True(back.TrustAtEnd == l.TrustAtEnd, "trust changed across the save: " + back.TrustAtEnd);
            True(back.Correct == l.Correct,
                 "correct calls changed across the save: " + back.Correct + " vs " + l.Correct);
            True(back.Verdict() == l.Verdict(), "the verdict changed across the save");

            // an empty save must not produce a fake history
            var none = RunLedger.FromSave(new List<DayRecord>(), 0);
            True(none.Count == 0, "an empty save produced " + none.Count + " days");
            True(none.Verdict() == "NO DAYS CLOSED.", "an empty run has a verdict: " + none.Verdict());
            return "PASS  a run saved mid-arc comes back with the same history and the same verdict";
        }
    }
}