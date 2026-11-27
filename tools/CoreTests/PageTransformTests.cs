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
            // The prompt is a top-left-pivot sprite placed at KeyBandTop px DOWN from the page top.
            // Its bottom edge (top + KeyBandH px) must sit above the key strip's top edge.
            var top = PageTransform.TopLeftToWorld(0f, SweepScript.KeyBandTop, 16f, SweepScript.PageH);
            float topPx = top.y * 16f;                          // world -> canvas px, y UP from the page bottom
            float bottomPx = topPx - SweepScript.KeyBandH;      // the sprite hangs DOWN from its pivot
            float stripTopPx = SweepScript.KeyStripH;           // the key strip is the bottom 22 px: y 0..22
            True(topPx <= SweepScript.PageH + 0.001f,
                 "the prompt band starts at y=" + topPx + " px, above the 270 px page");
            True(bottomPx >= stripTopPx - 0.001f,
                 "the prompt band ends at y=" + bottomPx + " px, inside the key strip (which ends at " + stripTopPx + ")");
            return "PASS  the key prompt lands between the page top and the key strip";
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
            True(promptBottom == SweepScript.PageH - SweepScript.KeyStripH,
                 "prompt ends at " + promptBottom + ", key strip starts at " + (SweepScript.PageH - SweepScript.KeyStripH));
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
            True(SweepScript.BandHeight(SweepScript.MaxRows) == SweepScript.MaxRows * real + 2 * SweepScript.PadY - 3,
                 "BandHeight is not (rows x row pitch) + padding");
            return "PASS  the caption's row pitch matches the font the raster draws with (" + real + " px)";
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
