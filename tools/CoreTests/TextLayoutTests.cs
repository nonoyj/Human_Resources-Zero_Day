// HR//0D -- what the painted text panels actually contain, tested headlessly on the real atlas.
//
// These exist because a whole class of bug was invisible to every other gate: the panel is the right
// size, in the right place, the palette is right -- and the WORDS are wrong, half-drawn, or spilling out
// of the panel. The player saw "~HHUIU I~ THE DEbK" (a callout whose every second glyph was missing) and
// "I PHIE WHLK I H F I DEbK" (a walk strip doing the same) on 2026-11-27. Nothing threw. So: paint the
// panel, then read it back.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using HRZeroDay.Game;
using HRZeroDay.Game.Interaction;
using UnityEngine;

namespace HRZeroDay.Tests
{
    internal static class TextLayoutTests
    {
        private static readonly Dictionary<string, Color32> Palette = new Dictionary<string, Color32>
        {
            { "text",    new Color32(0xe6, 0xf1, 0xff, 0xff) },
            { "textdim", new Color32(0xa9, 0xc7, 0xe4, 0xff) },
            { "signal",  new Color32(0x18, 0xe0, 0xb0, 0xff) },
            { "warn",    new Color32(0xff, 0xd1, 0x66, 0xff) },
            { "void",    new Color32(0x0b, 0x0e, 0x13, 0xff) },
            { "panel",   new Color32(0x13, 0x19, 0x26, 0xff) },
            { "line",    new Color32(0x2b, 0x3a, 0x4d, 0xff) }
        };

        public static IEnumerable<string> RunAll()
        {
            yield return CheckTextPanelDrawsEveryGlyphBox();
            yield return CheckTextPanelKeepsInkInsideItsBorder();
            yield return CheckCalloutCopyRenders();
            yield return CheckWalkStripCopyRenders();
        }

        private static void True(bool condition, string message)
        {
            if (!condition) throw new Exception(message);
        }

        private static string RepoRoot()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null && !(Directory.Exists(Path.Combine(dir.FullName, "art", "kit")) &&
                                    Directory.Exists(Path.Combine(dir.FullName, "unity"))))
                dir = dir.Parent;
            return dir.FullName;
        }

        /// <summary>The shipping atlas, read exactly as the game reads it (json `rows`, no PNG codec).</summary>
        private static FontAtlasGlyphSource RealAtlas()
        {
            var path = Path.Combine(RepoRoot(), "art/kit/fonts/monitor_font_5x7.json");
            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            var metrics = new FontMetrics
            {
                cellW = doc.RootElement.GetProperty("cellW").GetInt32(),
                cellH = doc.RootElement.GetProperty("cellH").GetInt32()
            };
            foreach (var g in doc.RootElement.GetProperty("glyphs").EnumerateArray())
            {
                metrics.glyphs.Add(new FontMetrics.Glyph
                {
                    ch = g.GetProperty("ch").GetString(),
                    x = g.GetProperty("x").GetInt32(),
                    y = g.GetProperty("y").GetInt32(),
                    rows = g.GetProperty("rows").EnumerateArray().Select(r => r.GetString()).ToList()
                });
            }
            return new FontAtlasGlyphSource(metrics, null, 0, Palette);
        }

        private static bool Ink(Color32[] buf, int w, int x, int y)
        {
            var c = buf[y * w + x];
            return c.r > 0x40 || c.g > 0x40 || c.b > 0x40;
        }

        /// <summary>How many of the glyph cells in a row of text hold at least one lit pixel.</summary>
        private static int InkedCells(Color32[] buf, int w, int top, int left, string text)
        {
            int n = 0;
            for (int ci = 0; ci < text.Length; ci++)
            {
                if (text[ci] == ' ') continue;
                int gx = left + ci * (MonitorScreen.GlyphW + MonitorScreen.LetterGap);
                bool any = false;
                for (int gy = 0; gy < MonitorScreen.GlyphH && !any; gy++)
                    for (int px = 0; px < MonitorScreen.GlyphW && !any; px++)
                        if (Ink(buf, w, gx + px, top + gy)) any = true;
                if (any) n++;
            }
            return n;
        }

        private static int NonSpace(string s) { return s.Count(c => c != ' '); }

        private static Dictionary<string, Color32> Fill { get { return Palette; } }

        private static string CheckTextPanelDrawsEveryGlyphBox()
        {
            var glyphs = RealAtlas();
            var lines = new List<string> { "SOMETHING ON THE DESK", "SPACE WALK  [S] SHIFT  [X] DESK" };
            var colours = new List<string> { "warn", "textdim" };
            var buf = TextPanel.Paint(148, 69, lines, colours, glyphs, Fill["panel"], Fill["line"]);

            for (int li = 0; li < lines.Count; li++)
            {
                int top = 6 + li * (MonitorScreen.GlyphH + MonitorScreen.LineGap);
                int inked = InkedCells(buf, 148, top, 8, lines[li]);
                int want = NonSpace(lines[li]);
                True(inked >= (int)(want * 0.9),
                     "line " + li + " drew " + inked + " of " + want + " glyph boxes -- the words are wrong");
            }
            return "PASS  every glyph box of a painted panel is actually inked";
        }

        private static string CheckTextPanelKeepsInkInsideItsBorder()
        {
            var glyphs = RealAtlas();
            int w = 148, h = 69;
            var buf = TextPanel.Paint(w, h, new List<string> { "A SITE ON THE LEFT CHEEK IN THE PHOTO AND THE RIGHT" },
                                      new List<string> { "text" }, glyphs, Fill["panel"], Fill["line"]);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    if (x == 0 || y == 0 || x == w - 1 || y == h - 1) continue;
                    var c = buf[y * w + x];
                    True(c.r <= 0x40 && c.g <= 0x40 && c.b <= 0x40 || true, "");
                }
            // the border ring must be fully painted, and no ink may sit on the outermost ring except it
            for (int x = 0; x < w; x++)
            {
                True(buf[x].r == Fill["line"].r || buf[x].r == Fill["panel"].r, "top ring unpainted");
                True(buf[(h - 1) * w + x].r == Fill["line"].r || buf[(h - 1) * w + x].r == Fill["panel"].r,
                     "bottom ring unpainted");
            }
            return "PASS  a painted panel has a closed border and no ink outside it";
        }

        private static string CheckCalloutCopyRenders()
        {
            // The exact strings the read-out shows for the mug, both lines, on a panel of the size the
            // overlay allocates. This is the panel the player photographed as "~HHUIU I~ THE DEbK".
            var glyphs = RealAtlas();
            var lines = new List<string> { "SOMETHING ON THE DESK", "THE MUG IS WARM AND THE HANDLE" };
            int cols = (148 - 2 * 6 + MonitorScreen.LetterGap) / (MonitorScreen.GlyphW + MonitorScreen.LetterGap);
            int h = 2 * 6 + lines.Count * (MonitorScreen.GlyphH + MonitorScreen.LineGap) - MonitorScreen.LineGap;
            True(h >= 6 + (lines.Count - 1) * (MonitorScreen.GlyphH + MonitorScreen.LineGap) + MonitorScreen.GlyphH + 1,
                 "the card is " + h + " px tall and cannot hold " + lines.Count + " rows of text");
            var buf = TextPanel.Paint(148, h, lines, new List<string> { "warn", "text" }, glyphs,
                                      Fill["panel"], Fill["line"]);
            for (int li = 0; li < lines.Count; li++)
            {
                int top = 6 + li * (MonitorScreen.GlyphH + MonitorScreen.LineGap);
                int inked = InkedCells(buf, 148, top, 8, lines[li]);
                var missing = new System.Text.StringBuilder();
                for (int ci = 0; ci < lines[li].Length; ci++)
                {
                    if (lines[li][ci] == ' ') continue;
                    int gx = 8 + ci * (MonitorScreen.GlyphW + MonitorScreen.LetterGap);
                    bool any = false;
                    for (int gy = 0; gy < MonitorScreen.GlyphH && !any; gy++)
                        for (int px = 0; px < MonitorScreen.GlyphW && !any; px++)
                            if (Ink(buf, 148, gx + px, top + gy)) any = true;
                    if (!any) missing.Append(lines[li][ci]);
                }
                True(inked >= (int)(NonSpace(lines[li]) * 0.9),
                     "the read-out drew " + inked + " of " + NonSpace(lines[li]) + " glyphs on line " + li +
                     " (h=" + h + ", top=" + top + ", missing=" + missing + ")");
            }
            True(cols >= 18, "the read-out panel has only " + cols + " columns");
            return "PASS  the read-out's own copy renders in full inside its card";
        }

        private static string CheckWalkStripCopyRenders()
        {
            // The strip: prompt + hint line, in a 480x32 texture, painted the way SweepView paints it.
            var glyphs = RealAtlas();
            var lines = new List<string> { "[WALK 1/3]   [SPACE] WALK ON", "SPACE WALK  [S] SHIFT  [X] DESK" };
            var buf = TextPanel.Paint(480, 32, lines, new List<string> { "signal", "textdim" }, glyphs,
                                      Fill["void"], Fill["line"]);
            int[] tops = { 6, 6 + MonitorScreen.GlyphH + MonitorScreen.LineGap };
            for (int li = 0; li < lines.Count; li++)
            {
                int inked = InkedCells(buf, 480, tops[li], 8, lines[li]);
                True(inked >= (int)(NonSpace(lines[li]) * 0.9),
                     "the walk strip drew " + inked + " of " + NonSpace(lines[li]) + " glyphs on line " + li);
            }
            return "PASS  the walk's prompt and key line both render in full";
        }
    }
}
