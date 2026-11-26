// HR//0D -- Headless test runner for the pure-C# core.
// Compiles the SAME source files Unity uses (../unity/Assets/Scripts/Core/**) and runs the same tests
// Unity's Test Runner runs. This is how the core is verified without opening Unity.
//
//   dotnet run --project tools/CoreTests            (all tests)
//   dotnet run --project tools/CoreTests -- 2000    (generate 2000 seeds and report violations)
using System;
using System.Collections.Generic;
using HRZeroDay.Core.Data;
using HRZeroDay.Core.Rules;
using HRZeroDay.Tests;

internal static class Program
{
    private static int Main(string[] args)
    {
        if (args.Length > 0 && int.TryParse(args[0], out int seeds))
            return Soak(seeds);

        Console.WriteLine("HR//0D core test run");
        Console.WriteLine("====================");

        int failed = 0;
        var lines = new List<string>();
        lines.AddRange(GeneratorInvariantTests.RunAll());
        lines.AddRange(GameDataTests.RunAll());        // art -> Unity data bridge
        lines.AddRange(MonitorScreenTests.RunAll());   // the CRT glass is a real character grid
        lines.AddRange(MonitorScreenRasterTests.RunAll()); // glyph atlas -> pixels, clipped and tinted
        lines.AddRange(LetterViewTests.RunAll());      // paperwork: wrap, paginate, flag scoring
        lines.AddRange(ScreenMetricsTests.RunAll());   // one source of truth for the glass grid
        lines.AddRange(PaperworkTests.RunAll());       // slow-clock ruling, one-read ruling, letter generation
        lines.AddRange(ManifestBridgeTests.RunAll());  // json -> Unity: serializable, frame names, rects
        lines.AddRange(DaylightTests.RunAll());        // day -> dusk -> night, from the shift clock
        lines.AddRange(InteractionTests.RunAll());     // hotspots, evidence ledger, tool budget
        lines.AddRange(SweepTests.RunAll());           // after-hours sweep: one trace per wrong hire
        foreach (var line in lines)
        {
            Console.WriteLine(line);
            if (line.StartsWith("FAIL")) failed++;
        }

        Console.WriteLine();
        Console.WriteLine(failed == 0 ? "ALL TESTS PASSED" : failed + " TEST(S) FAILED");
        return failed == 0 ? 0 : 1;
    }

    /// <summary>Soak test: mirrors prototype/generator_sim.py but against the shipping C# code.</summary>
    private static int Soak(int seedCount)
    {
        Console.WriteLine("Soak: " + seedCount + " seeds (5 days each = " + (seedCount * 23) + " applicants)");
        var sw = System.Diagnostics.Stopwatch.StartNew();
        int violations = 0, synthetics = 0, humans = 0;
        var syntheticTells = new Dictionary<int, int>();
        string firstViolation = null;

        for (int seed = 1; seed <= seedCount; seed++)
        {
            var c = new ShiftController(seed);
            var v = c.Validate();
            if (v.Count > 0)
            {
                violations += v.Count;
                if (firstViolation == null) firstViolation = "seed " + seed + ": " + v[0];
            }
            foreach (var day in c.AllDays.Keys)
                foreach (var a in c.AllDays[day])
                {
                    if (a.IsSynthetic)
                    {
                        synthetics++;
                        int n = a.tellsPresent.Count;
                        syntheticTells[n] = syntheticTells.TryGetValue(n, out int cur) ? cur + 1 : 1;
                    }
                    else humans++;
                }
        }
        sw.Stop();

        Console.WriteLine("applicants        : " + (synthetics + humans));
        Console.WriteLine("synthetic / human : " + synthetics + " / " + humans +
                          "  (" + Math.Round(100.0 * synthetics / (synthetics + humans), 1) + "% synthetic)");
        Console.Write("tells per synthetic: ");
        foreach (var kv in syntheticTells) Console.Write("[" + kv.Key + "]->" + kv.Value + " ");
        Console.WriteLine();
        Console.WriteLine("invariant violations: " + violations + (firstViolation == null ? "" : "  first: " + firstViolation));
        Console.WriteLine("ms per run          : " + Math.Round(sw.Elapsed.TotalMilliseconds / seedCount, 2));
        return violations == 0 ? 0 : 1;
    }
}
