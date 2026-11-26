// HR//0D -- tests for MonitorScreen: the CRT glass is a character grid, and text must never leave it.
//
// These exist because the painted document used to hang 6 px BELOW the glass (BUG-028). A test that
// only checks "it compiles" would not have caught that; the tests below assert the geometry, not the
// intent: every line fits, the stack fits, the y-flip is real, and the json glass rect agrees with the
// sprite the art pipeline actually ships.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using HRZeroDay.Core.Data;
using HRZeroDay.Game;

namespace HRZeroDay.Tests
{
    public static class MonitorScreenTests
    {
        public static IEnumerable<string> RunAll()
        {
            yield return GlassMatchesJsonAndSprite();
            yield return GlassFitsTheStandardFontGrid();
            yield return LinesNeverExceedGlassWidth();
            yield return StackFitsVerticallyAndStopsAtTheBottom();
            yield return YFlipIsReal();
            yield return SilentApplicantFieldsVanish();
            yield return TrustBarIsFixedWidthAndClamped();
        }

        // ------------------------------------------------------------------ helpers
        private static string RepoRoot()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null && !(Directory.Exists(Path.Combine(dir.FullName, "art", "kit")) &&
                                    Directory.Exists(Path.Combine(dir.FullName, "unity"))))
                dir = dir.Parent;
            if (dir == null) throw new DirectoryNotFoundException("repo root not found");
            return dir.FullName;
        }

        /// <summary>Sprite size straight out of the published manifest, so the test cannot guess.</summary>
        private static (int w, int h) MonitorSpriteSize()
        {
            var path = Path.Combine(RepoRoot(), "art/kit/unity_sprites.json");
            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            foreach (var s in doc.RootElement.GetProperty("sprites").EnumerateArray())
            {
                if (s.GetProperty("source").GetString().EndsWith("monitor_small.png"))
                    return (s.GetProperty("width").GetInt32(), s.GetProperty("height").GetInt32());
            }
            throw new InvalidOperationException("monitor_small.png missing from the manifest");
        }

        private static (int x, int y, int w, int h) JsonGlass()
        {
            var path = Path.Combine(RepoRoot(), "unity/Assets/Content/desk_layout.json");
            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            foreach (var l in doc.RootElement.GetProperty("layers").EnumerateArray())
            {
                if (l.GetProperty("id").GetString() != "monitor") continue;
                var sc = l.GetProperty("screen");
                return (sc.GetProperty("x").GetInt32(), sc.GetProperty("y").GetInt32(),
                        sc.GetProperty("w").GetInt32(), sc.GetProperty("h").GetInt32());
            }
            throw new InvalidOperationException("monitor layer has no 'screen' rect");
        }

        private static ApplicantRecord Sample(string name = "MARIA SANTOS",
                                             string role = "DATA ENTRY II",
                                             string uni = "CEBU NORMAL UNIVERSITY",
                                             string emp = "NORTHSTAR LOGISTICS",
                                             string refer = "WALK-IN")
        {
            return new ApplicantRecord
            {
                id = "A-0001", day = 1,
                fullName = name, age = 27, idNumber = "8814-2231",
                university = uni, graduationYear = 2019,
                employer = emp, employerFounded = 2011, employmentStartYear = 2019,
                yearsExperience = 6, role = role, referral = refer,
                coverLetterId = "CL-0001", portraitId = "portrait_human_solid_a_96x96"
            };
        }

        // ------------------------------------------------------------------ tests
        private static string GlassMatchesJsonAndSprite()
        {
            var (sw, sh) = MonitorSpriteSize();
            var derived = MonitorScreen.GlassForSprite(sw, sh);
            var (jx, jy, jw, jh) = JsonGlass();
            if (derived.x != jx || derived.y != jy || derived.w != jw || derived.h != jh)
                return $"FAIL  glass_rect_drift json=({jx},{jy},{jw},{jh}) sprite-derived=" +
                       $"({derived.x},{derived.y},{derived.w},{derived.h}) -- the monitor was redrawn " +
                       "without updating desk_layout.json 'screen'";
            return "PASS  monitor glass rect agrees with the sprite and the json";
        }

        private static string GlassFitsTheStandardFontGrid()
        {
            var (sw, sh) = MonitorSpriteSize();
            var g = MonitorScreen.GlassForSprite(sw, sh);
            // 142 / 6 = 23 columns, 88 / 9 = 9 rows with the shipping 5x7 font + 1/2 px gaps
            if (g.cols < 20) return $"FAIL  glass_too_narrow cols={g.cols}";
            if (g.rows < 8) return $"FAIL  glass_too_short rows={g.rows}";
            return $"PASS  glass grid is {g.cols}x{g.rows} characters ({g.w}x{g.h} px)";
        }

        private static string LinesNeverExceedGlassWidth()
        {
            var (sw, sh) = MonitorSpriteSize();
            var g = MonitorScreen.GlassForSprite(sw, sh);
            var model = new MonitorScreenModel { day = 5, trust = 40, applicant = Sample() };
            var lines = MonitorScreen.BuildLines(model);
            var rects = MonitorScreen.LinesToRects(
                lines.Select(l => new MonitorScreen.LineSpec(l.text, l.colour)).ToList(), g);

            foreach (var r in rects)
            {
                if (r.glyphW > g.w)
                    return $"FAIL  line_overflows_glass '{r.text}' {r.glyphW}px > {g.w}px";
                if (r.canvasX < g.x || r.canvasX + r.glyphW > g.x1)
                    return $"FAIL  line_outside_glass_x '{r.text}'";
            }
            // and a deliberately absurd name must be CUT, not allowed to run off the bezel
            var longModel = new MonitorScreenModel
            {
                applicant = Sample(name: "BARTOLOME VILLANUEVA-DELA CRUZ THE THIRD OF CEBU CITY")
            };
            var longRects = MonitorScreen.LinesToRects(
                MonitorScreen.BuildLines(longModel)
                    .Select(l => new MonitorScreen.LineSpec(l.text, l.colour)).ToList(), g);
            var nameLine = longRects.FirstOrDefault(r => r.text.StartsWith("BARTOLOME"));
            if (nameLine.text == null) return "FAIL  long_name_line_vanished";
            if (!nameLine.truncated) return "FAIL  long_name_was_not_truncated";
            if (nameLine.glyphW > g.w) return "FAIL  long_name_still_overflows";
            return $"PASS  every line fits {g.w}px; a 54-char name is truncated to {nameLine.text.Length} chars";
        }

        private static string StackFitsVerticallyAndStopsAtTheBottom()
        {
            var (sw, sh) = MonitorSpriteSize();
            var g = MonitorScreen.GlassForSprite(sw, sh);
            var model = new MonitorScreenModel { day = 3, trust = 100, applicant = Sample() };
            var rects = MonitorScreen.LinesToRects(
                MonitorScreen.BuildLines(model)
                    .Select(l => new MonitorScreen.LineSpec(l.text, l.colour)).ToList(), g);
            foreach (var r in rects)
                if (r.canvasY < g.y || r.canvasY + r.lineH > g.y1)
                    return $"FAIL  line_outside_glass_y '{r.text}' at {r.canvasY}";

            // a model with far too many lines must be CLIPPED, not overflow: 30 lines into 9 rows
            var flood = new List<MonitorScreen.LineSpec>();
            for (int i = 0; i < 30; i++) flood.Add(new MonitorScreen.LineSpec("LINE " + i));
            var clipped = MonitorScreen.LinesToRects(flood, g);
            if (clipped.Count > g.rows)
                return $"FAIL  vertical_clip_failed {clipped.Count} lines into {g.rows} rows";
            return $"PASS  {rects.Count} lines sit inside the glass; a 30-line flood clips to {clipped.Count}";
        }

        private static string YFlipIsReal()
        {
            var g = new MonitorScreen.GlassRect { x = 0, y = 10, w = 60, h = 40 };
            var two = new List<MonitorScreen.LineSpec>
            {
                new MonitorScreen.LineSpec("FIRST"),
                new MonitorScreen.LineSpec("SECOND")
            };
            var rects = MonitorScreen.LinesToRects(two, g);
            if (rects.Count != 2) return "FAIL  yflip_line_count";
            // "FIRST" is drawn at the TOP of the glass, which in y-up canvas space is the HIGHER y
            if (!(rects[0].canvasY > rects[1].canvasY))
                return $"FAIL  yflip_wrong first={rects[0].canvasY} second={rects[1].canvasY}";
            if (rects[0].canvasY + MonitorScreen.GlyphH > g.y1)
                return "FAIL  yflip_first_line_above_glass";
            return "PASS  first line is at the top of the glass in y-up canvas space";
        }

        private static string SilentApplicantFieldsVanish()
        {
            var bare = new ApplicantRecord { fullName = "KIM", role = null, university = null,
                                             employer = null, referral = null };
            var lines = MonitorScreen.BuildLines(new MonitorScreenModel { applicant = bare });
            if (lines.Any(l => l.text != null && l.text.Contains("UNI")))
                return "FAIL  empty_university_was_rendered";
            if (lines.Any(l => l.text != null && l.text.Contains("EMP")))
                return "FAIL  empty_employer_was_rendered";
            return "PASS  fields the applicant did not provide produce no line at all";
        }

        private static string TrustBarIsFixedWidthAndClamped()
        {
            var a = MonitorScreen.TrustBar(100);
            var b = MonitorScreen.TrustBar(0);
            var c = MonitorScreen.TrustBar(150);
            var d = MonitorScreen.TrustBar(-40);
            if (a.Length != b.Length) return $"FAIL  trust_bar_width_drift '{a}' vs '{b}'";
            if (c != a) return $"FAIL  trust_bar_not_clamped_high '{c}'";
            if (d != b) return $"FAIL  trust_bar_not_clamped_low '{d}'";
            if (!a.Contains("##########")) return $"FAIL  trust_bar_full_not_full '{a}'";
            return $"PASS  trust bar is {a.Length} chars at any input ('{a}' / '{b}')";
        }
    }
}
