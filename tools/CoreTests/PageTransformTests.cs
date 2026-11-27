// HR//0D -- placement maths and page capacity, tested headlessly.
//
// Every bug the player reported on 2026-11-26/27 without an exception behind it was one of these two:
// a screen element placed in the wrong space, or a page that quietly dropped a line. So they are tests.
//   - PageTransform: a top-left-pivot sprite placed from a y-down offset must land inside the page.
//   - SweepScript: the caption band must not overlap the prompt band or the key strip.
//   - Every trace the walk prints must fit the caption: a truncated sentence hides the tell.
using System.Collections.Generic;
using HRZeroDay.Core.Data;
using HRZeroDay.Core.Rules;
using HRZeroDay.Game;
using HRZeroDay.Game.Interaction;

namespace HRZeroDay.Tests
{
    internal static class PageTransformTests
    {
        public static IEnumerable<string> RunAll()
        {
            yield return CheckCanvasOriginMatchesTheDesk();
            yield return CheckKeyBandLandsOnItsBand();
            yield return CheckCaptionBandLandsAtTheTop();
            yield return CheckTopLeftPivotIsRecognised();
            yield return CheckBandsDoNotOverlap();
            yield return CheckEveryTraceFitsTheCaption();
            yield return CheckRowPitchMatchesTheFont();
            yield return CheckTheStripSaysEverythingInText();
            yield return CheckReadOutStaysOnThePage();
        }

        private static void True(bool condition, string message)
        {
            if (!condition) throw new System.Exception(message);
        }

        private static string CheckCanvasOriginMatchesTheDesk()
        {
            // DeskSceneBuilder places art at px/ppu from the canvas bottom-left. The page transform must
            // agree with it or the walk is drawn in a different space from the desk it interrupts.
            var p = PageTransform.CanvasToWorld(160f, 80f, 16f);
            True(System.Math.Abs(p.x - 10f) < 0.001f && System.Math.Abs(p.y - 5f) < 0.001f,
                 "canvas (160,80) px should be world (10,5), got (" + p.x + "," + p.y + ")");
            return "PASS  a canvas pixel converts to the same world point the desk builder uses";
        }

        private static string CheckKeyBandLandsOnItsBand()
        {
            // The prompt is a top-left-pivot sprite placed KeyBandTop px DOWN from the page top. Convert
            // it back to y-up canvas px and check it against the strip it lives in:
            //   strip      y  0 .. 32      (the bottom 32 px of the page)
            //   hint line  y 17 .. 24      (one 7 px glyph row + its bottom padding)
            //   prompt     y 18 .. 33      (one 7 px glyph row, its band, above the hint)
            var top = PageTransform.TopLeftToWorld(0f, SweepScript.KeyBandTop, 16f, SweepScript.PageH);
            float promptTop = top.y * 16f;                                   // y-up, bottom of page = 0
            float promptBottom = promptTop - SweepScript.KeyBandH;           // the sprite hangs DOWN
            float hintTop = SweepScript.PageH - SweepScript.HintTop;
            True(promptTop > 0f && promptTop <= SweepScript.PageH,
                 "the prompt band starts at y=" + promptTop + " px, outside the page");
            True(promptBottom >= hintTop - 0.001f,
                 "the prompt band's bottom (" + promptBottom + ") is below the hint line's top (" + hintTop + ")");
            True(promptTop <= SweepScript.StripH + 1f,
                 "the prompt band reaches y=" + promptTop + ", above the 32 px strip");
            return "PASS  the key prompt sits in the strip, above the player's hint line";
        }

        private static string CheckCaptionBandLandsAtTheTop()
        {
            var top = PageTransform.TopLeftToWorld(SweepScript.BandX, SweepScript.BandTop, 16f, SweepScript.PageH);
            float topPx = top.y * 16f;
            float bottomPx = topPx - SweepScript.BandH;
            True(System.Math.Abs(topPx - (SweepScript.PageH - SweepScript.BandTop)) < 0.001f,
                 "the caption's top edge should be " + (SweepScript.PageH - SweepScript.BandTop) + " px up");
            float promptTopPx = SweepScript.PageH - SweepScript.KeyBandTop;   // y UP, same space
            True(bottomPx > promptTopPx,
                 "the caption band (bottom " + bottomPx + " px) runs into the prompt band (top " + promptTopPx + " px)");
            return "PASS  the caption band starts " + SweepScript.BandTop + " px below the page top and clears the prompt";
        }

        private static string CheckTopLeftPivotIsRecognised()
        {
            True(PageTransform.HasTopLeftPivot(PageTransform.TopLeftPivot), "the named pivot is not top-left");
            True(!PageTransform.HasTopLeftPivot(new UnityEngine.Vector2(0.5f, 0.5f)),
                 "a centre pivot must not pass the top-left check");
            return "PASS  the guard can tell a top-left pivot from a centre one";
        }

        private static string CheckBandsDoNotOverlap()
        {
            // caption: rows 0 .. BandH-1 measured DOWN from the page top, offset by BandTop
            // rows measured DOWN from the page top: caption occupies BandTop .. BandTop+BandH
            int captionBottom = SweepScript.BandTop + SweepScript.BandH;
            True(captionBottom <= SweepScript.KeyBandTop,
                 "caption ends at row " + captionBottom + " (down from the page top), prompt starts at " + SweepScript.KeyBandTop);
            int promptBottom = SweepScript.KeyBandTop + SweepScript.KeyBandH;
            True(promptBottom == SweepScript.HintTop,
                 "prompt ends at " + promptBottom + ", the hint line starts at " + SweepScript.HintTop);
            True(SweepScript.HintTop + MonitorScreen.GlyphH <= SweepScript.HintBottom,
                 "the hint glyphs run past their own reserved row");
            True(SweepScript.HintBottom < SweepScript.PageH,
                 "the hint line runs off the bottom edge of the page");
            return "PASS  caption, prompt and key strip stack without overlapping";
        }

        private static string CheckRowPitchMatchesTheFont()
        {
            // SweepScript mirrors the font metrics because Core may not reference the Game assembly.
            // The mirror had the glyph WIDTH where the glyph HEIGHT belongs, so every caption band was
            // sized 20 % short and its last lines were painted onto the room art.
            int real = MonitorScreen.GlyphH + MonitorScreen.LineGap;
            True(SweepScript.RowStep == real,
                 "caption row pitch is " + SweepScript.RowStep + " px but the font lays rows out at " + real);
            True(SweepScript.BandHeight(SweepScript.MaxRows) == SweepScript.MaxRows * real + 2 * SweepScript.PadY,
                 "BandHeight is not (rows x row pitch) + padding");
            return "PASS  the caption's row pitch matches the font the raster draws with (" + real + " px)";
        }

        private static string CheckTheStripSaysEverythingInText()
        {
            // The walk's prompts must all be text the atlas can draw, on a band that fits 5x7 glyphs.
            // The old strip drew four neon caps whose marking was a single unreadable glyph -- decoration
            // pretending to be a control, which is why the walk had to be guessed at.
            int cols = (SweepScript.PageW - 2 * SweepScript.StripPadX + MonitorScreen.LetterGap) /
                       (MonitorScreen.GlyphW + MonitorScreen.LetterGap);
            int promptCols = SweepScript.PageW / (MonitorScreen.GlyphW + MonitorScreen.LetterGap);
            True(SweepScript.HintLine().Length <= promptCols,
                 "the hint line is " + SweepScript.HintLine().Length + " cols, the strip holds " + promptCols);
            True(!string.IsNullOrEmpty(SweepScript.HintLine()), "the walk has no hint line");
            var pages = SweepScript.Build(new List<SweepTrace>
            {
                new SweepTrace { Floor = "desk", Title = "YOUR DESK", Detail = "SOMETHING MOVED." }
            });
            var key = SweepScript.KeyLine(pages[0], 0, 1);
            True(key.IndexOf("[SPACE]", System.StringComparison.Ordinal) >= 0,
                 "the prompt does not name the key that advances the walk: " + key);
            True(key.Length <= promptCols, "the prompt is " + key.Length + " cols, the band holds " + promptCols);
            True(SweepScript.KeyBandH >= 7 + 2 * SweepScript.StripPadY,
                 "the prompt band is too short for a 7 px glyph row");
            int lastRow = SweepScript.HintTop + MonitorScreen.GlyphH;
            True(lastRow <= SweepScript.HintBottom,
                 "the hint line ends at row " + lastRow + ", past its reserved row " + SweepScript.HintBottom);
            True(lastRow <= SweepScript.PageH - 1,
                 "the hint line ends at row " + lastRow + ", past the page bottom");
            return "PASS  every control the walk shows is readable text inside the 32 px strip";
        }

        private static string CheckReadOutStaysOnThePage()
        {
            // Every anchor an evidence callout can have, at every panel size up to the cap: the box must
            // be fully inside 480x270. The player's "opens it too big and goes off screen" was a panel
            // with no clamp; this is the clamp.
            int[] sizes = { 74, 100, 148 };
            float[] heights = { 30f, 45f, 69f };
            for (int sx = 0; sx < sizes.Length; sx++)
            {
                for (int sy = 0; sy < heights.Length; sy++)
                {
                    for (int ax = 0; ax <= 480; ax += 24)
                    {
                        for (int ay = 0; ay <= 270; ay += 18)
                        {
                            var b = CalloutPlacement.Fit(ax, ay, sizes[sx], heights[sy], 480, 270);
                            string where = "anchor (" + ax + "," + ay + ") panel " + sizes[sx] + "x" + heights[sy];
                            True(b.Left >= 0f && b.Right <= 480f, "off the page horizontally: " + where);
                            True(b.Bottom >= 0f && b.Top <= 270f, "off the page vertically: " + where);
                        }
                    }
                }
            }
            // and the preferred position is honoured when it fits: anchor 316 -> right edge at 316
            var exact = CalloutPlacement.Fit(316f, 214f, 148f, 45f, 480, 270);
            True(System.Math.Abs(exact.Right - 316f) < 0.001f && System.Math.Abs(exact.Top - 214f) < 0.001f,
                 "a panel that fits should sit exactly on its anchor");
            return "PASS  the read-out can never leave the page, and honours its anchor when it fits";
        }

        private static string CheckEveryTraceFitsTheCaption()
        {
            // The player's report: the walk's trace lines were cut off mid-word. A caption that cannot
            // hold a trace must either wrap it (it does) or fail this test -- never silently truncate.
            for (int n = 1; n <= 3; n++)
            {
                var hired = new List<ApplicantRecord>();
                var archetypes = new[]
                {
                    Archetype.SyntheticPolished, Archetype.SyntheticCloned, Archetype.SyntheticCollusion
                };
                for (int i = 0; i < n; i++)
                    hired.Add(new ApplicantRecord { id = "a" + i, archetypeRaw = (int)archetypes[i], fullName = "Test Person" });
                var traces = Sweep.AfterShift(n, hired);
                foreach (var page in SweepScript.Build(traces))
                {
                    int rows = 2 + page.Lines.Count;
                    True(rows <= SweepScript.MaxRows,
                         "page '" + page.Heading + "' needs " + rows + " rows, the caption has " + SweepScript.MaxRows);
                    foreach (var line in page.Lines)
                        True(line.Length <= SweepScript.Cols,
                             "line overflows the page width (" + line.Length + " > " + SweepScript.Cols + "): " + line);
                    True(SweepScript.BandHeight(rows) <= SweepScript.BandH,
                         "a " + rows + "-row page is taller than the band texture");
                }
            }
            return "PASS  every trace fits: rows, columns and band height";
        }
    }
}
