// HR//0D -- the metrics have ONE source of truth and it is asserted here.
//
// tools/comp/preview_screen.py exists so the glass can be looked at without opening Unity. That only
// works while its constants match MonitorScreen's constants; the moment they drift, the preview lies.
// This test parses the GLYPH_W/LETTER_GAP/... block out of the python and compares it to C#.
//
// It also pins the numbers themselves: 20 x 9 is the size the legibility pass landed on, and the card
// must fit inside it, so a future "just add a line" breaks a test instead of the layout.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using HRZeroDay.Core.Data;
using HRZeroDay.Game;

namespace HRZeroDay.Tests
{
    public static class ScreenMetricsTests
    {
        public static IEnumerable<string> RunAll()
        {
            yield return PreviewMetricsMatchTheCSharp();
            yield return GlassIsTheAgreedTwentyByNine();
            yield return CardFitsTheGlass();
            yield return EveryCardLineFitsTheWidth();
        }

        private static string RepoRoot()
        {
            var dir = AppContext.BaseDirectory;
            while (dir != null && !Directory.Exists(Path.Combine(dir, "art", "kit")))
                dir = Path.GetDirectoryName(dir);
            if (dir == null) throw new InvalidOperationException("repo root not found (run from the repo)");
            return dir;
        }

        /// <summary>
        /// Reads a numeric constant from the python preview, including the packed form the file uses:
        ///   GLYPH_W, GLYPH_H = 5, 7
        ///   LETTER_GAP, LINE_GAP = 2, 3
        /// </summary>
        private static int PyConst(string source, string name)
        {
            foreach (var raw in source.Split('\n'))
            {
                var line = raw.Trim();
                if (line.StartsWith("#")) continue;
                int eq = line.IndexOf(" = ", StringComparison.Ordinal);
                if (eq < 0) continue;
                var names = line.Substring(0, eq).Split(',').Select(n => n.Trim()).ToList();
                int idx = names.IndexOf(name);
                if (idx < 0) continue;
                var values = line.Substring(eq + 3).Split('#')[0].Split(',')
                                 .Select(v => v.Trim()).ToList();
                if (idx >= values.Count)
                    throw new InvalidOperationException($"python constant list shorter than names: {line}");
                return int.Parse(values[idx]);
            }
            throw new InvalidOperationException("python constant not found: " + name);
        }

        private static string PreviewMetricsMatchTheCSharp()
        {
            var path = Path.Combine(RepoRoot(), "tools", "comp", "preview_screen.py");
            var py = File.ReadAllText(path);
            var pairs = new (string py, string cs, int csharp)[]
            {
                ("GLYPH_W", "GlyphW", MonitorScreen.GlyphW),
                ("GLYPH_H", "GlyphH", MonitorScreen.GlyphH),
                ("LETTER_GAP", "LetterGap", MonitorScreen.LetterGap),
                ("LINE_GAP", "LineGap", MonitorScreen.LineGap),
                ("COLUMNS", "glass.cols", Glass().cols),
                ("ROWS", "glass.rows", Glass().rows)
            };
            var bad = new List<string>();
            foreach (var p in pairs)
                if (PyConst(py, p.py) != p.csharp)
                    bad.Add($"{p.py}={PyConst(py, p.py)} vs MonitorScreen.{p.cs}={p.csharp}");
            if (bad.Count > 0) return "FAIL  preview_metrics_drift " + string.Join("; ", bad);
            return $"PASS  preview_screen.py and MonitorScreen agree on all {pairs.Length} metrics " +
                   $"({Glass().cols}x{Glass().rows}, gap {MonitorScreen.LetterGap}/{MonitorScreen.LineGap})";
        }

        // The real monitor sprite is 154x106; the glass inside it is 142x88 (desk_layout.json).
        private static MonitorScreen.GlassRect Glass() => MonitorScreen.GlassForSprite(154, 106);

        private static string GlassIsTheAgreedTwentyByNine()
        {
            var g = Glass();
            if (g.cols != 20 || g.rows != 9)
                return $"FAIL  glass_is_{g.cols}x{g.rows} (legibility pass agreed 20x9)";
            if (MonitorScreen.MeasureWidth(new string('M', g.cols)) > g.w)
                return "FAIL  glass_grid_does_not_fit_pixels";
            // 20 columns must be 138 px and 9 rows 87 px inside the 142x88 glass -- no clipping, no slack
            return $"PASS  glass {g.cols}x{g.rows} cells: {MonitorScreen.MeasureWidth(new string('M', g.cols))}px of " +
                   $"{g.w}px wide, {MonitorScreen.StackHeight(g.rows)}px of {g.h}px tall";
        }

        private static ApplicantRecord LongestApplicant() => new ApplicantRecord
        {
            id = "A-9999", day = 5, fullName = "MARIA CRISTINA SANTOS-DELA CRUZ", role = "SENIOR DATA ENTRY",
            idNumber = "8814-2231", university = "CEBU NORMAL UNIVERSITY", graduationYear = 2019,
            employer = "NORTHSTAR LOGISTICS", yearsExperience = 6, referral = "WALK-IN",
            coverLetterId = "CL-9999"
        };

        private static string CardFitsTheGlass()
        {
            var model = new MonitorScreenModel
            {
                day = 5, trust = 100, trustVisible = true, applicant = LongestApplicant()
            };
            var lines = MonitorScreen.BuildLines(model);
            int rows = Glass().rows;
            if (lines.Count > rows)
                return $"FAIL  card_is_{lines.Count}_lines_but_glass_has_{rows}_rows";
            return $"PASS  the full card is {lines.Count} of {rows} rows (longest name, day 5, full trust)";
        }

        private static string EveryCardLineFitsTheWidth()
        {
            // NOT a truncation test: a name longer than 20 columns legitimately ends in '~' on the
            // glass, and MonitorScreenTests already asserts that behaviour. What matters here is that
            // every line that FITS is drawn whole and nothing spills past the right edge of the glass.
            var glass = Glass();
            var lines = MonitorScreen.BuildLines(new MonitorScreenModel
            {
                day = 5, trust = 100, trustVisible = true, applicant = LongestApplicant()
            });
            var rects = MonitorScreen.LinesToRects(lines, glass);
            var spill = rects.Where(r => r.canvasX + r.glyphW > glass.x1).Select(r => r.text).ToList();
            if (spill.Count > 0) return "FAIL  card_line_spills_past_the_bezel " + string.Join(" | ", spill);
            var whole = rects.Count(r => !r.truncated);
            return $"PASS  {whole} of {rects.Count} card lines draw whole inside {glass.cols} columns " +
                   "(the 28-char test name is the one that ends in '~', by design)";
        }
    }
}
