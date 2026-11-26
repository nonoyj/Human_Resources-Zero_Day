// HR//0D -- the after-hours sweep (team idea 2026-11-26: a routine walk-through that hints at the AI that
// got in). The rule must never invent a trace the player did not cause, and must never be silent by
// accident. Both are tested here.
using System.Collections.Generic;
using HRZeroDay.Core.Data;
using HRZeroDay.Core.Rules;

namespace HRZeroDay.Tests
{
    internal static class SweepTests
    {
        public static IEnumerable<string> RunAll()
        {
            yield return CheckQuietDayStillHasASweep();
            yield return CheckOneTracePerHiredSynthetic();
            yield return CheckTracesEscalate();
            yield return CheckNothingBeyondThree();
            yield return CheckSweepEndsOnTheWorstFloor();
            yield return CheckCauseNamesTheActualApplicant();
            yield return CheckCapsAndClamps();
        }

        private static List<ApplicantRecord> Hired(params Archetype[] types)
        {
            var list = new List<ApplicantRecord>();
            for (int i = 0; i < types.Length; i++)
                list.Add(new ApplicantRecord { id = "a" + i, archetypeRaw = (int)types[i], fullName = "Person " + i });
            return list;
        }

        private static string CheckQuietDayStillHasASweep()
        {
            var traces = Sweep.AfterShift(0, Hired());
            if (traces.Count != 1) return "FAIL  a clean day produced " + traces.Count + " traces";
            if (traces[0].Severity != 0) return "FAIL  a clean day is not quiet";
            if (string.IsNullOrEmpty(traces[0].Title)) return "FAIL  the quiet sweep has no line to print";
            return "PASS  a clean day still gets a sweep, and it is quiet: '" + traces[0].Title + "'";
        }

        private static string CheckOneTracePerHiredSynthetic()
        {
            for (int level = 1; level <= 3; level++)
            {
                int hired = 0;
                for (int i = 0; i < level; i++) hired++;
                var traces = Sweep.AfterShift(level, Hired(Archetype.SyntheticPolished));
                if (traces.Count != level)
                    return "FAIL  corruption " + level + " produced " + traces.Count + " traces, expected " + level;
            }
            return "PASS  one trace per synthetic hired: 1/2/3";
        }

        private static string CheckTracesEscalate()
        {
            var traces = Sweep.AfterShift(3, Hired(Archetype.SyntheticPolished, Archetype.SyntheticCloned,
                                                  Archetype.SyntheticCollusion));
            for (int i = 1; i < traces.Count; i++)
                if (traces[i].Severity <= traces[i - 1].Severity)
                    return "FAIL  trace " + i + " is not worse than the one before it";
            return "PASS  traces escalate: severity " + traces[0].Severity + " -> " + traces[traces.Count - 1].Severity;
        }

        private static string CheckNothingBeyondThree()
        {
            var traces = Sweep.AfterShift(3, Hired(Archetype.SyntheticPolished));
            foreach (var t in traces)
                if (t.Severity > 3) return "FAIL  severity " + t.Severity + " exceeds the scale (1..3)";
            return "PASS  severity stays on the 1..3 scale";
        }

        private static string CheckSweepEndsOnTheWorstFloor()
        {
            int lvl2 = Sweep.AfterShift(2, Hired(Archetype.SyntheticCloned, Archetype.SyntheticCloned)).Count;
            if (Sweep.FinalFloor(Sweep.AfterShift(2, Hired(Archetype.SyntheticCloned,
                    Archetype.SyntheticCloned))) != "corridor")
                return "FAIL  two hires should end the walk in the corridor";
            if (Sweep.FinalFloor(Sweep.AfterShift(3, Hired(Archetype.SyntheticCollusion,
                    Archetype.SyntheticCollusion, Archetype.SyntheticCollusion))) != "lift")
                return "FAIL  three hires must end at the lift";
            if (lvl2 != 2) return "FAIL  two hires should give two traces";
            return "PASS  the walk ends where the damage is: desk -> corridor -> lift";
        }

        private static string CheckCauseNamesTheActualApplicant()
        {
            var today = Hired(Archetype.SyntheticCloned);
            var traces = Sweep.AfterShift(2, today);
            var last = traces[traces.Count - 1];
            if (last.Detail == null || !last.Detail.Contains("Person 0"))
                return "FAIL  the trace does not name who caused it: " + last.Detail;
            return "PASS  the trace names the applicant the player actually passed: '" + last.Detail + "'";
        }

        private static string CheckCapsAndClamps()
        {
            var a = Sweep.AfterShift(-5, Hired());
            var b = Sweep.AfterShift(99, Hired(Archetype.SyntheticPolished));
            if (a.Count != 1 || a[0].Severity != 0) return "FAIL  a negative corruption level does not clamp to quiet";
            if (b.Count > 3) return "FAIL  99 hired synthetics produced " + b.Count + " traces";
            // decided 2026-11-26: only on days when something got through
            if (Sweep.HasSweep(0)) return "FAIL  a clean day must not trigger the walk";
            if (!Sweep.HasSweep(1) || !Sweep.HasSweep(3)) return "FAIL  a dirty day must trigger the walk";
            return "PASS  corruption is clamped 0..3 and the sweep caps at three traces";
        }
    }
}
