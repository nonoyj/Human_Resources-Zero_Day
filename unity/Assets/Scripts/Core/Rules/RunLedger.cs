// HR//0D -- RunLedger: the five days as ONE run, not five days in a row.
// Assembly: HRZeroDay.Core  (pure C#, no UnityEngine) -> testable headlessly, and identical on every platform
//
// WHY THIS EXISTS (round v101)
//   ShiftController already knew how to close a day and start the next one. What the game did NOT have was
//   any memory of the arc: the day summary showed one day's numbers, the ending screen showed a total, and
//   nothing in between told the player that day 3 was worse than day 1 because of something they did on
//   day 2. A five-day run with no through-line is just the same day five times.
//
//   The ledger is that through-line, and it is deliberately small: one row per closed day, in order, plus
//   the three questions a player actually asks at the end --
//       1. what did I do?            (the days)
//       2. what did it cost?          (trust, at the start and now)
//       3. was I getting better?      (correct calls, day by day, so a slump is visible)
//
//   It never holds truth. Every field is something the player was already shown on the day summary.
using System;
using System.Collections.Generic;
using HRZeroDay.Core.Data;

namespace HRZeroDay.Core.Rules
{
    /// <summary>One closed day, exactly as the player saw it on that day's summary.</summary>
    public sealed class RunDay
    {
        public int Day;
        public int Approved;
        public int Rejected;
        public int WrongfulRejections;
        public int SyntheticsHired;
        public int SyntheticsCaught;
        public bool QuotaMet;
        public int TrustAtEnd;
        /// <summary>The correct calls this day: right hires plus right rejections. The only "score" the
        /// run keeps, because it is the only one the player can improve by playing better.</summary>
        public int CorrectCalls;
        public int Decisions { get { return Approved + Rejected; } }
    }

    /// <summary>The arc's memory: every closed day, in order, plus the numbers the ending reads.</summary>
    public sealed class RunLedger
    {
        private readonly List<RunDay> _days = new List<RunDay>();

        public const int ArcLength = 5;

        public IList<RunDay> Days { get { return _days; } }
        public int Count { get { return _days.Count; } }

        /// <summary>Trust at the start of day 1. The arc's baseline, so "lost 18" means something.</summary>
        public int TrustAtStart { get; private set; }

        public RunLedger() { TrustAtStart = 100; }

        public RunLedger(int trustAtStart) { TrustAtStart = trustAtStart; }

        /// <summary>Records a closed day. Idempotent per day number: closing the same day twice does not
        /// double-count (the bootstrap can call this from two paths after a save/resume).</summary>
        public void Record(RunDay day)
        {
            if (day == null || day.Day <= 0) return;
            for (int i = 0; i < _days.Count; i++)
                if (_days[i].Day == day.Day) { _days[i] = day; return; }
            _days.Add(day);
        }

        public RunDay For(int dayNumber)
        {
            for (int i = 0; i < _days.Count; i++) if (_days[i].Day == dayNumber) return _days[i];
            return null;
        }

        public int TrustAtEnd { get { return _days.Count == 0 ? TrustAtStart : _days[_days.Count - 1].TrustAtEnd; } }
        public int TrustLost { get { return Math.Max(0, TrustAtStart - TrustAtEnd); } }
        public int Approved { get { int n = 0; foreach (var d in _days) n += d.Approved; return n; } }
        public int Rejected { get { int n = 0; foreach (var d in _days) n += d.Rejected; return n; } }
        public int Wrongful { get { int n = 0; foreach (var d in _days) n += d.WrongfulRejections; return n; } }
        public int Hired { get { int n = 0; foreach (var d in _days) n += d.SyntheticsHired; return n; } }
        public int Caught { get { int n = 0; foreach (var d in _days) n += d.SyntheticsCaught; return n; } }
        public int Correct { get { int n = 0; foreach (var d in _days) n += d.CorrectCalls; return n; } }
        public int Decisions { get { int n = 0; foreach (var d in _days) n += d.Decisions; return n; } }

        /// <summary>Days where the quota was met. The arc's most legible number: 5/5 reads as a good week.</summary>
        public int QuotasMet { get { int n = 0; foreach (var d in _days) if (d.QuotaMet) n++; return n; } }

        /// <summary>
        /// The run's one-line verdict for the ending screen. Built from the ledger only, so it cannot
        /// disagree with the days the player just played.
        /// </summary>
        public string Verdict()
        {
            if (_days.Count == 0) return "NO DAYS CLOSED.";
            if (Hired == 0 && Wrongful == 0)
                return "FIVE CLEAN DAYS. NOT ONE OF THEM GOT PAST YOU.";
            if (Hired > 0 && Hired >= Caught)
                return "THEY ARE INSIDE THE BUILDING. YOU SIGNED THEM IN.";
            if (Wrongful >= 3)
                return "YOU HELD THE LINE AND PAID FOR IT IN PEOPLE.";
            if (Hired > 0)
                return "MOST OF THEM WERE PEOPLE. THE ONES THAT WERE NOT ARE WORKING LATE.";
            return "THE FILE CLOSES. THE FLOOR DOES NOT GET ANY QUIETER.";
        }

        /// <summary>
        /// Per-day correct-call counts as a compact string for the ending screen: "4 3 5 3 5".
        /// A drop is visible at a glance, which is the whole point of keeping it.
        /// </summary>
        public string CorrectByDay()
        {
            var parts = new List<string>();
            foreach (var d in _days) parts.Add(d.CorrectCalls.ToString());
            return string.Join("  ", parts.ToArray());
        }

        /// <summary>Writes the ledger into a save file so a resumed run still knows its own history.</summary>
        public void WriteTo(List<DayRecord> into)
        {
            if (into == null) return;
            into.Clear();
            foreach (var d in _days)
                into.Add(new DayRecord
                {
                    day = d.Day,
                    completed = true,
                    approved = d.Approved,
                    rejected = d.Rejected,
                    wrongfulRejections = d.WrongfulRejections,
                    syntheticsHired = d.SyntheticsHired,
                    trustAtEnd = d.TrustAtEnd
                });
        }

        /// <summary>Rebuilds the ledger from a save. Correct-call counts are recomputed from what was
        /// recorded -- they are a function of the day's outcome, never stored separately, so they cannot
        /// drift from the numbers the player saw.</summary>
        public static RunLedger FromSave(List<DayRecord> records, int decisionsPerDayKnown)
        {
            var ledger = new RunLedger();
            if (records == null) return ledger;
            foreach (var r in records)
            {
                if (r == null || !r.completed || r.day <= 0) continue;
                ledger.Record(new RunDay
                {
                    Day = r.day,
                    Approved = r.approved,
                    Rejected = r.rejected,
                    WrongfulRejections = r.wrongfulRejections,
                    SyntheticsHired = r.syntheticsHired,
                    QuotaMet = r.approved >= 3,
                    TrustAtEnd = r.trustAtEnd,
                    CorrectCalls = Math.Max(0, (r.approved + r.rejected) - r.wrongfulRejections - r.syntheticsHired)
                });
            }
            return ledger;
        }

        /// <summary>
        /// The day summary's arc line: where this day sits in the run, and whether the run is heading
        /// somewhere. One line, because the summary already has numbers on it.
        /// </summary>
        public string ArcLine(int justClosedDay)
        {
            int i = justClosedDay <= 0 ? _days.Count : justClosedDay;
            string of = "DAY " + Math.Max(1, Math.Min(ArcLength, i)) + " OF " + ArcLength;
            if (i >= ArcLength) return of + "  THE LAST ONE.";
            if (TrustLost >= 30) return of + "  TRUST IS GOING.";
            if (Hired > 0) return of + "  SOMETHING YOU PASSED THROUGH IS STILL HERE.";
            if (i == 1) return of + "  FOUR SHIFTS TO GO.";
            return of + "  " + (ArcLength - i) + " SHIFTS TO GO.";
        }
    }
}
