// HR//0D -- MonitorScreen: what the player READS. The CRT glass is the game's paper.
// Assembly: HRZeroDay.Game  (references UnityEngine for Color32 only)
// Owner: Agent (written) / Dev A (owns it afterwards). Started 2026-11-26.
//
// WHY THIS EXISTS
//   The monitor used to show a painted sheet with invented lines. Every word on it was decorative --
//   nothing was bound to the applicant being interviewed. This type turns the glass into a real
//   surface: it lays out the applicant's actual record (and the day header / trust bar) into a
//   character grid, and CLIPS to the glass so text can never spill out of the bezel again
//   (BUG-028: the painted sheet hung 6 px below the glass).
//
// THE TWO COORDINATE PROBLEMS THIS SOLVES
//   1. The glass rect lives in monitor-local pixels. It comes from desk_layout.json (layer
//      "monitor", field "screen") -- never hard-coded here, so art and code cannot disagree.
//   2. Unity's Canvas y axis points UP, but text lines run DOWN. LineLayout.LinesToRects does the
//      flip in one place, and it is the only place allowed to.
//
// DESIGN RULES (docs/15-art-bible.md 7d + docs/06-decisions.md)
//   * Palette only. Text colours come from SpriteManifest's palette, never from Color.white.
//   * No fake content. If a field is empty, the line is skipped -- the screen never invents a fact.
//   * The screen is diegetic paper: a header, then the record, then the truth line. No debug text.

using System;
using System.Collections.Generic;
using System.Text;
using HRZeroDay.Core.Data;

namespace HRZeroDay.Game
{
    /// <summary>One laid-out line of screen text, already flipped to canvas space.</summary>
    public struct ScreenLine
    {
        public string text;
        public int glyphW;        // width in art px including the 1 px letter gap
        public int lineH;         // height in art px
        public float canvasX;     // bottom-left of the first glyph, canvas px
        public float canvasY;     // baseline box bottom, canvas px (y up)
        public string colour;     // palette key
        public bool truncated;    // true when the line had to be clipped to fit the glass
    }

    /// <summary>Everything the monitor is asked to display for one applicant.</summary>
    public sealed class MonitorScreenModel
    {
        public int day = 1;
        public int trust = 100;
        public ApplicantRecord applicant;
        public bool trustVisible = true;
    }

    public static class MonitorScreen
    {
        // ---------------------------------------------------------------- metrics (art px)
        // READABILITY (user, 2026-11-26: "text too close to each other"):
        //   letterGap 1 -> 2 : glyphs stop touching, so a word reads as a word
        //   lineGap   2 -> 3 : descenders no longer merge with the next row's caps
        // This trades columns (23 -> 20) and rows (10 -> 9) for legibility, which is the right
        // trade on a 142x88 glass read at arm's length on a phone.
        public const int GlyphW = 5;
        public const int GlyphH = 7;
        public const int LetterGap = 2;
        public const int LineGap = 3;

        /// <summary>Width in art px of a string in the bitmap font (including the trailing gap).</summary>
        public static int MeasureWidth(string s)
        {
            if (string.IsNullOrEmpty(s)) return 0;
            return s.Length * (GlyphW + LetterGap) - LetterGap;
        }

        /// <summary>Total height of n lines, bottom-aligned, with LineGap between them.</summary>
        public static int StackHeight(int lines) => lines <= 0 ? 0 : lines * GlyphH + (lines - 1) * LineGap;

        /// <summary>
        /// Fits `s` into `maxCols` characters, appending '~' when it had to be cut.
        /// Truncation is a first-class outcome: the caller is told, so it can be asserted in tests.
        /// </summary>
        public static bool FitToColumns(string s, int maxCols, out string fitted)
        {
            if (maxCols < 1) { fitted = string.Empty; return !string.IsNullOrEmpty(s); }
            if (s == null) { fitted = string.Empty; return false; }
            if (s.Length <= maxCols) { fitted = s; return false; }
            fitted = s.Substring(0, maxCols - 1) + "~";
            return true;
        }

        /// <summary>
        /// Converts top-down line indices into canvas rects. THIS is the y-flip. Everything above the
        /// glass top is clipped away rather than drawn outside the screen.
        /// </summary>
        public static List<ScreenLine> LinesToRects(IList<LineSpec> lines, GlassRect glass)
        {
            var outp = new List<ScreenLine>();
            if (lines == null || lines.Count == 0) return outp;

            // Canvas space is y-UP but text runs DOWN, so the FIRST line sits at the TOP of the
            // glass: y = glassTop - oneGlyph, then each line steps down by glyph+gap. Getting this
            // backwards still "works" (nothing throws) but renders the page upside down -- the test
            // YFlipIsReal exists to keep it that way.
            int maxLines = glass.rows;
            int emitted = 0;
            for (int i = 0; i < lines.Count && emitted < maxLines; i++)
            {
                string text = lines[i].text;
                if (string.IsNullOrEmpty(text)) continue;      // silent fields consume no row

                string fitted;
                bool cut = FitToColumns(text, glass.cols, out fitted);
                int y = glass.y1 - GlyphH - emitted * (GlyphH + LineGap);
                outp.Add(new ScreenLine
                {
                    text = fitted,
                    glyphW = MeasureWidth(fitted),
                    lineH = GlyphH,
                    canvasX = glass.x,
                    canvasY = y,
                    colour = lines[i].colour ?? "text",
                    truncated = cut
                });
                emitted++;
            }
            return outp;
        }

        /// <summary>The glass rect, in monitor-layer local art px, y up.</summary>
        public struct GlassRect
        {
            public int x, y, w, h;
            public int cols => (w + LetterGap) / (GlyphW + LetterGap);
            // the last line needs no trailing LineGap, so add one gap before dividing:
            // 88 px of glass holds 10 seven-px lines with nine 2 px gaps exactly.
            public int rows => (h + LineGap) / (GlyphH + LineGap);
            public int x1 => x + w;
            public int y1 => y + h;
            public bool Contains(int px, int py) => px >= x && px < x1 && py >= y && py < y1;
        }

        /// <summary>
        /// Glass rect for a monitor sprite of `spriteW` x `spriteH`, matching the generator's drawing
        /// (promote_composition_L.py::monitor_small draws glass at 6,6 .. w-7,h-13). The json value is
        /// checked against this at build time by GameDataTests, so a redraw can never silently move it.
        /// </summary>
        public static GlassRect GlassForSprite(int spriteW, int spriteH)
        {
            const int x0 = 6, yTop = 6, insetRight = 6, insetBottom = 12;
            int w = spriteW - x0 - insetRight;
            int h = spriteH - yTop - insetBottom;
            // json/local y is up: the glass bottom sits insetBottom px above the sprite bottom
            return new GlassRect { x = x0, y = insetBottom, w = w, h = h };
        }

        // ---------------------------------------------------------------- layout
        public struct LineSpec
        {
            public string text;
            public string colour;
            public LineSpec(string text, string colour = "text")
            {
                this.text = text;
                this.colour = colour;
            }
        }

        /// <summary>
        /// Builds the screen from the record. Top to bottom:
        ///   DAY n / TRUST bar / rule / name / role / ID / facts / rule / footer
        /// Nothing here is invented: a null or empty field produces no line at all.
        /// </summary>
        public static List<LineSpec> BuildLines(MonitorScreenModel model)
        {
            if (model == null || model.applicant == null)
                throw new ArgumentNullException(nameof(model), "monitor screen needs an applicant");

            var a = model.applicant;
            // A CARD, not a document. 20x9 cells is for the day, the trust meter, who is sitting in
            // front of you, and the prompt that the real reading happens in the letter view
            // (user decision 2026-11-26, option c). Long fields are abbreviated for the same reason.
            var lines = new List<LineSpec>
            {
                new LineSpec("DAY " + model.day.ToString("00"), "textdim"),
                new LineSpec(model.trustVisible ? TrustBar(model.trust) : "TRUST  --", "signal"),
                new LineSpec(Safe(a.fullName).ToUpperInvariant(), "text")
            };
            if (!string.IsNullOrEmpty(a.role)) lines.Add(new LineSpec(a.role, "textdim"));
            lines.Add(new LineSpec("ID " + Safe(a.idNumber), "paperdim"));

            var facts = new List<string>();
            if (a.graduationYear > 0) facts.Add("GRAD " + a.graduationYear);
            if (a.yearsExperience > 0) facts.Add("EXP " + a.yearsExperience + "Y");
            if (facts.Count > 0) lines.Add(new LineSpec(string.Join("  ", facts), "textdim"));

            if (!string.IsNullOrEmpty(a.referral)) lines.Add(new LineSpec("REF " + a.referral, "paperdim"));
            if (!string.IsNullOrEmpty(a.coverLetterId))
                lines.Add(new LineSpec("> READ LETTER", "warn"));
            return lines;
        }

        /// <summary>"[####------] 62" -- a 10-cell bar plus the number. Fixed width, so it never reflows.</summary>
        public static string TrustBar(int trust)
        {
            int clamped = trust < 0 ? 0 : (trust > 100 ? 100 : trust);
            int filled = (clamped + 5) / 10;                          // 0..10 cells
            var sb = new StringBuilder(20);
            sb.Append('[');
            for (int i = 0; i < 10; i++) sb.Append(i < filled ? '#' : '-');
            sb.Append("] ");
            sb.Append(clamped.ToString().PadLeft(3, ' '));   // fixed width: 100 vs 0 must not reflow
            return sb.ToString();
        }

        private static string Safe(string s) => s ?? string.Empty;

        /// <summary>
        /// Every line fits the glass, or it is truncated on purpose. Returns the offending lines.
        /// Called by tests and by the editor menu; a caller that ignores it still cannot overflow,
        /// because LinesToRects clips -- this only reports the ones that lost characters.
        /// </summary>
        public static List<string> OverflowingLines(IList<LineSpec> lines, GlassRect glass)
        {
            var bad = new List<string>();
            if (lines == null) return bad;
            for (int i = 0; i < lines.Count; i++)
            {
                if (string.IsNullOrEmpty(lines[i].text)) continue;
                if (MeasureWidth(lines[i].text) > glass.w) bad.Add(lines[i].text);
            }
            if (StackHeight(lines.Count) > glass.h)
                bad.Add("<stack " + StackHeight(lines.Count) + "px > glass " + glass.h + "px>");
            return bad;
        }
    }
}
