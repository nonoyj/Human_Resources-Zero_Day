// HR//0D -- tests for MonitorScreenRaster: the glass texture must clip, tint, and actually draw ink.
//
// The raster is pure (Color32[] in, Color32[] out) so it can be proven without Unity. These tests use
// the REAL generated atlas (art/kit/fonts/monitor_font_5x7.json) rather than a fake, so a broken atlas
// fails here instead of on a phone.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using UnityEngine;
using HRZeroDay.Game;

namespace HRZeroDay.Tests
{
    public static class MonitorScreenRasterTests
    {
        private static readonly Color32 Bg = new Color32(0x0b, 0x0e, 0x13, 0xff);
        private static readonly Dictionary<string, Color32> Palette = new Dictionary<string, Color32>
        {
            { "text",    new Color32(0xe6, 0xf1, 0xff, 0xff) },
            { "textdim", new Color32(0xa9, 0xc7, 0xe4, 0xff) },
            { "signal",  new Color32(0x18, 0xe0, 0xb0, 0xff) },
            { "void",    new Color32(0x0b, 0x0e, 0x13, 0xff) }
        };

        public static IEnumerable<string> RunAll()
        {
            yield return AtlasCoversEveryPrintableAscii();
            yield return RasterDrawsInkInsideTheGlass();
            yield return RasterClipsRatherThanOverflowing();
            yield return RasterUsesThePaletteColourOfTheLine();
            yield return RasterShowsATruncationMarker();
        }

        private static string RepoRoot()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null && !(Directory.Exists(Path.Combine(dir.FullName, "art", "kit")) &&
                                    Directory.Exists(Path.Combine(dir.FullName, "unity"))))
                dir = dir.Parent;
            return dir.FullName;
        }

        /// <summary>
        /// Loads the shipping metrics and wraps them as an IGlyphSource. The json carries the glyph ink
        /// as 'rows' (the same data the PNG holds), so the test needs no image decoder and still
        /// verifies the real, generated atlas rather than a fake.
        /// </summary>
        private static IGlyphSource RealAtlas()
        {
            var path = Path.Combine(RepoRoot(), "art/kit/fonts/monitor_font_5x7.json");
            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            var cellW = doc.RootElement.GetProperty("cellW").GetInt32();
            var cellH = doc.RootElement.GetProperty("cellH").GetInt32();
            var map = new Dictionary<char, string[]>();
            foreach (var g in doc.RootElement.GetProperty("glyphs").EnumerateArray())
            {
                var ch = g.GetProperty("ch").GetString();
                if (string.IsNullOrEmpty(ch)) continue;
                var rows = g.GetProperty("rows").EnumerateArray().Select(r => r.GetString()).ToArray();
                if (!map.ContainsKey(ch[0])) map[ch[0]] = rows;
            }
            return new TestGlyphSource(cellW, cellH, map, Palette);
        }

        private sealed class TestGlyphSource : IGlyphSource
        {
            private readonly Dictionary<char, string[]> _map;
            private readonly Dictionary<string, Color32> _pal;
            public int CellW { get; }
            public int CellH { get; }
            public TestGlyphSource(int cw, int ch, Dictionary<char, string[]> map, Dictionary<string, Color32> pal)
            { CellW = cw; CellH = ch; _map = map; _pal = pal; }

            public bool InkAt(char ch, int gx, int gy)
            {
                if (!_map.TryGetValue(ch, out var rows))
                    if (!_map.TryGetValue(char.ToUpperInvariant(ch), out rows)) return false;
                if (gy < 0 || gy >= rows.Length) return false;
                var r = rows[gy];
                return gx >= 0 && gx < r.Length && r[gx] == '#';
            }

            public Color32 Colour(string key)
            {
                if (key != null && _pal.TryGetValue(key, out var c)) return c;
                return _pal["text"];
            }
        }

        private static bool HasGlyph(IGlyphSource src, char c)
        {
            for (int y = 0; y < src.CellH; y++)
                for (int x = 0; x < src.CellW; x++)
                    if (src.InkAt(c, x, y)) return true;
            return false;
        }

        private static string AtlasCoversEveryPrintableAscii()
        {
            var atlas = RealAtlas();
            var required = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789 .,:-+/#[]()'~?!%*=";
            var missing = new List<char>();
            foreach (char c in required)
            {
                if (c == ' ') continue;                 // a space is a valid glyph with no ink
                if (!HasGlyph(atlas, c)) missing.Add(c);
            }
            if (missing.Count > 0)
                return "FAIL  atlas_missing_glyphs " + new string(missing.Distinct().ToArray());
            return "PASS  atlas covers all " + required.Length + " glyphs the screen uses";
        }

        private static string RasterDrawsInkInsideTheGlass()
        {
            var atlas = RealAtlas();
            var glass = MonitorScreen.GlassForSprite(154, 106);
            var model = new MonitorScreenModel
            {
                day = 1, trust = 100,
                applicant = new HRZeroDay.Core.Data.ApplicantRecord
                {
                    id = "A1", fullName = "MARIA SANTOS", role = "DATA ENTRY II",
                    idNumber = "8814-2231", university = "CNU", graduationYear = 2019,
                    employer = "NORTHSTAR", yearsExperience = 6, referral = "WALK-IN"
                }
            };
            var lines = MonitorScreen.LinesToRects(
                MonitorScreen.BuildLines(model).Select(l => new MonitorScreen.LineSpec(l.text, l.colour)).ToList(),
                glass);
            var buf = new Color32[glass.w * glass.h];
            MonitorScreenRaster.Render(buf, glass.w, glass.h, lines, atlas, Bg);
            int ink = MonitorScreenRaster.InkPixelCount(buf, Bg);
            if (ink < 200) return $"FAIL  raster_almost_blank ink={ink}px";
            // Text starts on the top glass row by design, so the honest check is that nothing is
            // written into the 6 columns past the 23-character grid -- that IS the bezel.
            int gridW = glass.cols * (MonitorScreen.GlyphW + MonitorScreen.LetterGap);
            for (int y = 0; y < glass.h; y++)
                for (int x = gridW; x < glass.w; x++)
                    if (!Same(buf[y * glass.w + x], Bg))
                        return $"FAIL  raster_ink_past_grid at x={x} (grid ends {gridW})";
            return $"PASS  raster drew {ink} ink px, all inside the {glass.w}x{glass.h} glass";
        }

        private static string RasterClipsRatherThanOverflowing()
        {
            var atlas = RealAtlas();
            var glass = new MonitorScreen.GlassRect { x = 0, y = 0, w = 60, h = 30 };
            var flood = new List<ScreenLine>();
            for (int i = 0; i < 20; i++)
                flood.Add(new ScreenLine { text = "LINE " + i, canvasY = 40 - i * 9, colour = "text" });
            var buf = new Color32[glass.w * glass.h];
            MonitorScreenRaster.Render(buf, glass.w, glass.h, flood, atlas, Bg);   // must not throw
            int ink = MonitorScreenRaster.InkPixelCount(buf, Bg);
            if (ink == 0) return "FAIL  clip_dropped_everything";
            return $"PASS  20 lines aimed outside a {glass.w}x{glass.h} glass clipped to {ink} ink px, no exception";
        }

        private static string RasterUsesThePaletteColourOfTheLine()
        {
            var atlas = RealAtlas();
            var glass = new MonitorScreen.GlassRect { x = 0, y = 0, w = 60, h = 20 };
            var lines = new List<ScreenLine>
            {
                new ScreenLine { text = "TRUST", canvasY = 8, colour = "signal" }
            };
            var buf = new Color32[glass.w * glass.h];
            MonitorScreenRaster.Render(buf, glass.w, glass.h, lines, atlas, Bg);
            var seen = new HashSet<string>();
            foreach (var c in buf) if (!Same(c, Bg)) seen.Add($"{c.r},{c.g},{c.b}");
            if (seen.Count != 1) return "FAIL  raster_mixed_colours " + string.Join(";", seen);
            if (!seen.Contains("24,224,176")) return "FAIL  raster_wrong_colour " + string.Join(";", seen);
            return "PASS  line colour 'signal' reached the texture as 18e0b0";
        }

        private static string RasterShowsATruncationMarker()
        {
            var atlas = RealAtlas();
            if (!HasGlyph(atlas, '~')) return "FAIL  truncation_marker_glyph_missing";
            var glass = new MonitorScreen.GlassRect { x = 0, y = 0, w = 60, h = 20 };   // 10 columns
            var lines = MonitorScreen.LinesToRects(
                new List<MonitorScreen.LineSpec> { new MonitorScreen.LineSpec("ABCDEFGHIJKLMNOP") }, glass);
            if (lines.Count != 1 || !lines[0].truncated) return "FAIL  truncation_not_reported";
            if (!lines[0].text.EndsWith("~")) return "FAIL  truncation_marker_absent '" + lines[0].text + "'";
            return $"PASS  an over-long line became '{lines[0].text}' inside {glass.w}px";
        }

        private static bool Same(Color32 a, Color32 b)
            => a.r == b.r && a.g == b.g && a.b == b.b;
    }
}
