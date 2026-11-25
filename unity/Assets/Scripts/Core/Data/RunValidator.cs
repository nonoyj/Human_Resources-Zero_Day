// HR//0D -- RunValidator: re-checks invariants I1..I8 on generated data.
// Assembly: HRZeroDay.Core  (pure C#, no UnityEngine)
// Use it in EditMode tests, in the content publish gate, and behind a debug key in builds.
using System.Collections.Generic;
using HRZeroDay.Core.Data;

namespace HRZeroDay.Core.Data
{
    public static class RunValidator
    {
        /// <summary>Validates a full run. Empty result means every invariant holds.</summary>
        public static List<Violation> ValidateRun(Dictionary<int, List<ApplicantRecord>> days)
        {
            var violations = new List<Violation>();
            var seenNames = new HashSet<string>();
            var seenIds = new HashSet<string>();

            foreach (var kv in days)
            {
                int day = kv.Key;
                var apps = kv.Value;
                var plan = new GenerationConfig().Days[day];

                int synthetics = 0;
                foreach (var a in apps) if (a.IsSynthetic) synthetics++;

                if (synthetics != plan.Synthetics)                       // I4
                    violations.Add(new Violation("I4", "day " + day + ": " + synthetics +
                        " synthetics, expected " + plan.Synthetics));
                if (apps.Count - synthetics < 4 - plan.Synthetics)
                    violations.Add(new Violation("I4", "day " + day + ": human quota unreachable"));

                foreach (var a in apps)
                {
                    if (!seenNames.Add(a.fullName))                      // I2
                        violations.Add(new Violation("I2", "duplicate name " + a.fullName, a.id));
                    if (!seenIds.Add(a.idNumber))
                        violations.Add(new Violation("I2", "duplicate id " + a.idNumber, a.id));

                    if (a.IsSynthetic)
                    {
                        if (a.tellsPresent.Count == 0)                    // I1
                            violations.Add(new Violation("I1", "synthetic with no tell", a.id));

                        foreach (var t in a.tellsPresent)
                        {
                            if (!TellCatalog.Has(t))
                            {
                                violations.Add(new Violation("I1", "unknown tell id " + t, a.id));
                                continue;
                            }
                            var tell = TellCatalog.Get(t);
                            if (!TellCatalog.IsUnlocked(day, tell.Surface))   // I1b
                                violations.Add(new Violation("I1b", "tell " + t + " needs surface " +
                                    tell.Surface + " which is locked on day " + day, a.id));
                            if (tell.IsFactual && !a.RecomputeFactualTells().Contains(t))  // I3
                                violations.Add(new Violation("I3", "claimed " + t +
                                    " but the record does not contradict", a.id));
                        }
                    }
                    else
                    {
                        var factual = a.RecomputeFactualTells();
                        foreach (var f in factual)
                        {
                            if (TellCatalog.IsHardTell(f) && a.Truth == GroundTruth.Human)   // I5
                                violations.Add(new Violation("I5", "human carries hard tell " + f, a.id));
                        }
                    }
                }
            }
            return violations;
        }

        /// <summary>I7 -- same seed must always produce the same run.</summary>
        public static bool IsDeterministic(int seed, out string firstRun, out string secondRun)
        {
            firstRun = Fingerprint(seed);
            secondRun = Fingerprint(seed);
            return firstRun == secondRun;
        }

        public static string Fingerprint(int seed)
        {
            var gen = new RunGenerator(seed);
            var sb = new System.Text.StringBuilder();
            for (int day = 1; day <= 5; day++)
            {
                foreach (var a in gen.GenerateDay(day))
                {
                    sb.Append(a.id).Append('|').Append(a.fullName).Append('|')
                      .Append(a.idNumber).Append('|').Append(string.Join(",", a.tellsPresent.ToArray()))
                      .Append(';');
                }
            }
            return sb.ToString();
        }
    }
}
