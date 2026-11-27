// HR//0D -- what the after-hours walk SAYS, laid out for a 480x270 page. Pure C#, no UnityEngine.
//
// Sweep.cs owns the rule (which traces exist and why). This file owns the reading experience:
// one trace per floor, the walk order, and the columns the text has to fit in. It is separate for the
// same reason LetterLayout is separate from LetterView -- the layout can be tested without Unity, and
// a page that silently drops a line is the kind of bug that only shows up on a phone.
//
// THE WALK (docs/16-after-hours-sweep.md section 3)
//   desk -> corridor -> lift. The player presses SPACE to walk on. There is no timer, no collision, no
//   free movement: the sweep is a READ, not a platformer, and the 45 second WalkSeconds budget is
//   deliberately not shown anywhere.
//
// The style break is art, not text: flat 2-tone room, neon key caps along the bottom. The type is the
// same 5x7 atlas the paperwork uses, because the player is still in the same building.
using System;
using System.Collections.Generic;
using HRZeroDay.Core.Data;

namespace HRZeroDay.Core.Rules
{
    /// <summary>One floor of the walk: a background key, a caption, and the traces found there.</summary>
    public sealed class SweepPage
    {
        public string Floor;                     // desk | corridor | lift
        public string BackgroundKey;             // Resources/Art/Kit/sweep/sweep_<floor>_480x270
        public string Heading;                   // the one line at the top of the page
        public List<string> Lines = new List<string>();   // wrapped body: the traces
        public bool IsLast;                      // the lift page ends the walk
    }

    public static class SweepScript
    {
        // ---------------------------------------------------------------- page metrics (art px)
        // 480x270 page, 5x7 glyphs, letterGap 2, lineGap 3 -> 68 columns x 27 rows.
        // The sweep only uses the top: a caption bar reads badly at the very bottom of a 270 px
        // page on a phone, and the bottom 22 px are the neon key caps.
        public const int PageW = 480;
        public const int PageH = 270;
        public const int KeyStripH = 22;                       // sweep_keys_480x22.png (neon caps, decoration)
        // The PROMPT is painted text on its own black band above the caps, never on top of them.
        public const int KeyBandH = 15;
        public const int KeyBandTop = PageH - KeyStripH - KeyBandH;   // 233
        // The caption band: a solid 'void' bar behind the words. First draft drew the text straight onto
        // the room art and it was unreadable wherever the background had detail -- the walk's text is the
        // only thing on this page that must never be hard to read, so it gets its own stock.
        public const int BandX = 8;
        public const int BandTop = 14;
        public const int BandW = 464;
        public const int PadX = 8;
        public const int PadY = 6;
        public const int TextX = BandX + PadX;                 // 16
        public const int TextTop = BandTop + PadY;             // 20, y DOWN from page top
        public const int TextW = BandW - 2 * PadX;             // 448 px -> 64 columns
        // The row pitch of the font: glyph HEIGHT + lineGap. This mirrored MonitorGlyph (the glyph
        // WIDTH, 5) for a year of rounds, so every band was sized at 8 px per row while the raster laid
        // the rows out at 10. Consequence: the last two lines of any caption were painted BELOW the band
        // -- text straight onto the room art, which is the "unreadable, streaky caption" in the player's
        // screenshots. PageTransformTests now asserts this equals MonitorScreen.GlyphH + LineGap.
        public const int RowStep = MonitorGlyphH + MonitorLineGap;   // 7 + 3 = 10

        /// <summary>Height of the band that must be drawn behind `rows` lines of text.</summary>
        public static int BandHeight(int rows)
        {
            if (rows <= 0) return 0;
            return rows * RowStep + 2 * PadY - 3;
        }

        /// <summary>
        /// Height of the caption texture: one allocation, the worst case, so the band never has to be
        /// resized mid-walk. <see cref="MaxRows"/> is what fits between BandTop and the prompt band at the
        /// real 10 px row pitch: 14 + 20*10 + 2*6 - 3 = 223, and the prompt starts at 233.
        /// </summary>
        public static int BandH { get { return BandHeight(MaxRows); } }

        public const int MaxRows = 20;

        public static int Cols
        {
            get { return (TextW + MonitorGap) / (MonitorGlyph + MonitorGap); }
        }

        // Mirror of MonitorScreen's metrics. Duplicated as constants on purpose: Core must not depend
        // on the Game assembly (the Game assembly depends on Core). The test project asserts these
        // two agree, so a change to the font metrics cannot silently break the walk's wrap.
        private const int MonitorGlyph = 5;      // glyph width (columns)
        private const int MonitorGlyphH = 7;     // glyph height (rows) -- the row pitch is built from THIS
        private const int MonitorGap = 2;        // letterGap
        private const int MonitorLineGap = 3;

        /// <summary>Order the floors are walked in. The last floor always carries the worst trace.</summary>
        public static readonly string[] FloorOrder = { "desk", "corridor", "lift" };

        /// <summary>Builds every page of the walk for a finished day. Never empty.</summary>
        public static List<SweepPage> Build(IList<SweepTrace> traces)
        {
            var pages = new List<SweepPage>();
            if (traces == null || traces.Count == 0) traces = Sweep.AfterShift(0, null);

            // group traces by floor, keeping author order, then walk the floors in order
            for (int f = 0; f < FloorOrder.Length; f++)
            {
                string floor = FloorOrder[f];
                var here = new List<SweepTrace>();
                for (int i = 0; i < traces.Count; i++)
                    if (traces[i] != null && traces[i].Floor == floor) here.Add(traces[i]);

                // A floor with nothing on it is skipped -- except the floor the walk STARTS on, which
                // always gets a page. A silent office starts at your own desk; that is the point.
                if (here.Count == 0 && f > 0) continue;

                var page = new SweepPage
                {
                    Floor = floor,
                    BackgroundKey = "sweep/sweep_" + floor + "_480x270",
                    Heading = HeadingFor(floor)
                };
                if (here.Count == 0)
                    page.Lines.AddRange(Wrap(traces[0].Title, Cols));
                for (int i = 0; i < here.Count; i++)
                {
                    page.Lines.AddRange(Wrap(here[i].Title, Cols));
                    if (!string.IsNullOrEmpty(here[i].Detail))
                        page.Lines.AddRange(Wrap(here[i].Detail, Cols));
                    page.Lines.Add(string.Empty);           // air between traces, one row
                }
                while (page.Lines.Count > 0 && string.IsNullOrEmpty(page.Lines[page.Lines.Count - 1]))
                    page.Lines.RemoveAt(page.Lines.Count - 1);
                pages.Add(page);
            }

            if (pages.Count > 0) pages[pages.Count - 1].IsLast = true;
            return pages;
        }

        /// <summary>
        /// The caption for a floor. Deliberately flat: the fright is what the lines say, not how the
        /// heading shouts. "LOCKED IN" on the last page is the only thing the walk ever asserts.
        /// </summary>
        public static string HeadingFor(string floor)
        {
            switch (floor)
            {
                case "corridor": return "CORRIDOR - 00:14";
                case "lift":     return "LIFT - LOCKED IN";
                default:         return "YOUR DESK - 00:06";
            }
        }

        /// <summary>The key strip line: the only non-diegetic UI in the game (docs/16 section 6).</summary>
        public static string KeyLine(SweepPage page, int index, int count)
        {
            string keys = page != null && page.IsLast ? "[SPACE] CLOCK OUT" : "[SPACE] WALK ON";
            return "[WALK " + (index + 1) + "/" + count + "]   " + keys;
        }

        /// <summary>Wrap on words at `cols`. Same rule as LetterLayout.Wrap, which cannot be reused here
        /// because Core must not reference the Game assembly.</summary>
        public static List<string> Wrap(string text, int cols)
        {
            var outp = new List<string>();
            if (string.IsNullOrEmpty(text)) return outp;
            if (cols < 1) cols = 1;
            var line = new System.Text.StringBuilder();
            foreach (var word in text.Split(' '))
            {
                if (word.Length == 0) continue;
                if (line.Length == 0)
                {
                    if (word.Length <= cols) { line.Append(word); continue; }
                    // a single word longer than the line: hard-split, never silently dropped
                    string rest = word;
                    while (rest.Length > cols) { outp.Add(rest.Substring(0, cols)); rest = rest.Substring(cols); }
                    line.Append(rest);
                    continue;
                }
                if (line.Length + 1 + word.Length <= cols) { line.Append(' ').Append(word); continue; }
                outp.Add(line.ToString());
                line.Length = 0;
                string tail = word;
                while (tail.Length > cols) { outp.Add(tail.Substring(0, cols)); tail = tail.Substring(cols); }
                line.Append(tail);
            }
            if (line.Length > 0) outp.Add(line.ToString());
            return outp;
        }

        /// <summary>
        /// The one line the summary screen adds when the walk happened: a receipt, not a moral.
        /// Returns null on a clean day, because there is nothing to report.
        /// </summary>
        public static string SummaryLine(int corruption, IList<SweepTrace> traces)
        {
            if (!Sweep.HasSweep(corruption) || traces == null || traces.Count == 0) return null;
            int worst = 0;
            for (int i = 0; i < traces.Count; i++) if (traces[i].Severity > worst) worst = traces[i].Severity;
            string where = Sweep.FinalFloor(traces).ToUpperInvariant();
            return "THE WALK ENDED AT THE " + where + ". " + traces.Count +
                   (traces.Count == 1 ? " THING" : " THINGS") + " WERE NOT YOURS.";
        }
    }
}
