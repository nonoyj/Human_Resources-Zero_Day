// HR//0D -- ShiftController: the day loop as a pure state machine (no UnityEngine).
// Assembly: HRZeroDay.Core  (pure C#, no UnityEngine)
// The Unity layer subscribes to events and renders; it holds NO game rules.
using System;
using System.Collections.Generic;
using HRZeroDay.Core.Rules;

namespace HRZeroDay.Core.Data
{
    public sealed class DayResult
    {
        public int Day;
        public int Approved;
        public int Rejected;
        public int WrongfulRejections;
        public int SyntheticsHired;
        public int SyntheticsCaught;
        public bool QuotaMet;
        public int TrustAfter;
        public string HintLine;                 // diegetic hint, never a confession
        public string TaughtConcept;            // Field Manual entry to highlight
    }

    public sealed class ShiftController
    {
        public const int QuotaPerDay = 3;
        public const int HireLimitForDefeat = 3;

        private readonly Dictionary<int, List<ApplicantRecord>> _days;
        private readonly RunGenerator _generator;

        public ShiftController(int seed)
        {
            Seed = seed;
            _generator = new RunGenerator(seed, new GenerationConfig());
            _days = new Dictionary<int, List<ApplicantRecord>>();
            for (int d = 1; d <= 5; d++) _days[d] = _generator.GenerateDay(d);
            Day = 1;
            Trust = 100;
            CurrentIndex = 0;
        }

        public int Seed { get; private set; }
        public int Day { get; private set; }
        public int Trust { get; private set; }
        public int CurrentIndex { get; private set; }
        public int SyntheticsHired { get; private set; }
        public int SyntheticsCaught { get; private set; }
        public int WrongfulRejections { get; private set; }
        public int CorruptionLevel
        {
            get
            {
                if (SyntheticsHired >= 3) return 3;
                return SyntheticsHired;      // 0,1,2 -> visible degradation (ADR-003)
            }
        }
        public RunOutcome Outcome { get; private set; }
        public int DayApproved { get; private set; }
        public int DayRejected { get; private set; }
        public int DayWrongful { get; private set; }
        public int DaySyntheticHired { get; private set; }
        public int DaySyntheticCaught { get; private set; }

        public IList<ApplicantRecord> Today { get { return _days[Day]; } }
        public ApplicantRecord Current { get { return _days[Day][CurrentIndex]; } }
        public bool DayFinished { get { return CurrentIndex >= _days[Day].Count; } }
        public bool RunFinished { get { return Outcome != RunOutcome.InProgress; } }
        public Dictionary<int, List<ApplicantRecord>> AllDays { get { return _days; } }

        /// <summary>The whole run's invariants -- call once at start-up in debug, and in tests.</summary>
        public List<Violation> Validate()
        {
            return RunValidator.ValidateRun(_days);
        }

        public DecisionResult Submit(DecisionInput input)
        {
            if (RunFinished) throw new InvalidOperationException("run already finished");
            if (DayFinished) throw new InvalidOperationException("day already finished");

            var result = ScoringService.Evaluate(input);
            Trust = Math.Max(0, Math.Min(100, Trust + result.TrustDelta));

            if (input.Decision == Decision.Approve)
            {
                DayApproved++;
                if (input.Applicant.IsSynthetic) { DaySyntheticHired++; SyntheticsHired++; }
            }
            else
            {
                DayRejected++;
                if (result.WrongfulRejection > 0) { DayWrongful++; WrongfulRejections++; }
                if (input.Applicant.IsSynthetic || input.Applicant.Truth == GroundTruth.HumanBait)
                    DaySyntheticCaught++;
                if (input.Applicant.IsSynthetic) SyntheticsCaught++;
            }

            CurrentIndex++;
            CheckDefeat();
            return result;
        }

        private void CheckDefeat()
        {
            if (SyntheticsHired >= HireLimitForDefeat) { Outcome = RunOutcome.DefeatedPerfectEmployee; return; }
            if (Trust <= 0) { Outcome = RunOutcome.DefeatedGhostQuota; return; }
        }

        /// <summary>Closes the day and returns its debrief. Advances to the next day when possible.</summary>
        public DayResult EndDay()
        {
            var r = new DayResult();
            r.Day = Day;
            r.Approved = DayApproved;
            r.Rejected = DayRejected;
            r.WrongfulRejections = DayWrongful;
            r.SyntheticsHired = DaySyntheticHired;
            r.SyntheticsCaught = DaySyntheticCaught;
            r.QuotaMet = DayApproved >= QuotaPerDay;
            if (!r.QuotaMet) Trust = Math.Max(0, Trust - 6);
            r.TrustAfter = Trust;
            r.HintLine = BuildHint(r);
            r.TaughtConcept = "manual_day_" + Day;

            CheckDefeat();
            if (Outcome == RunOutcome.InProgress)
            {
                if (Day >= 5) Outcome = RunOutcome.Survived;
                else
                {
                    Day++;
                    CurrentIndex = 0;
                    DayApproved = DayRejected = DayWrongful = DaySyntheticHired = DaySyntheticCaught = 0;
                }
            }
            return r;
        }

        private string BuildHint(DayResult r)
        {
            if (r.SyntheticsHired > 0)
                return "Overtime access logs show a new badge on the archive floor at 03:14. No one was rostered.";
            if (!r.QuotaMet)
                return "The floor is short-staffed. Two desks sat empty through the entire shift.";
            if (r.WrongfulRejections > 0)
                return "A rejected applicant has filed a complaint. Their referee answered on the first ring.";
            return "Nothing unusual in the logs today.";
        }

        /// <summary>Restores a run from a save file (same seed => identical applicants).</summary>
        public static ShiftController FromSave(SaveData save)
        {
            var c = new ShiftController(save.runSeed);
            c.Day = Math.Max(1, Math.Min(5, save.day));
            c.CurrentIndex = Math.Max(0, Math.Min(c._days[c.Day].Count, save.applicantIndex));
            c.Trust = save.trust;
            c.SyntheticsHired = save.syntheticsHired;
            c.SyntheticsCaught = save.syntheticsCaught;
            c.WrongfulRejections = save.wrongfulRejections;
            c.DayApproved = save.dayApproved;
            c.DayRejected = save.dayRejected;
            c.DayWrongful = save.dayWrongful;
            c.DaySyntheticHired = save.daySyntheticHired;
            c.DaySyntheticCaught = save.daySyntheticCaught;
            c.Outcome = save.Outcome;
            return c;
        }

        public void WriteTo(SaveData save)
        {
            save.runSeed = Seed;
            save.day = Day;
            save.applicantIndex = CurrentIndex;
            save.trust = Trust;
            save.syntheticsHired = SyntheticsHired;
            save.syntheticsCaught = SyntheticsCaught;
            save.wrongfulRejections = WrongfulRejections;
            save.corruptionLevel = CorruptionLevel;
            save.dayApproved = DayApproved;
            save.dayRejected = DayRejected;
            save.dayWrongful = DayWrongful;
            save.daySyntheticHired = DaySyntheticHired;
            save.daySyntheticCaught = DaySyntheticCaught;
            save.Outcome = Outcome;
        }
    }
}
