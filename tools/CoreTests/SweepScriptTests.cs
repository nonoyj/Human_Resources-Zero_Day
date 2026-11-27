// HR//0D -- the sweep's reading experience, tested headlessly.
//
// Sweep.cs is the rule (what is in the office). SweepScript.cs is the page (how it is said). The page is
// where a bug can hide without throwing: a wrap that silently drops a word, a floor that never gets
// visited, a caption with no line under it. Each of those is a test here.
using System.Collections.Generic;
using HRZeroDay.Core.Data;
using HRZeroDay.Core.Rules;
using HRZeroDay.Game;

namespace HRZeroDay.Tests
{
    internal static class SweepScriptTests
    {
        public static IEnumerable<string> RunAll()
        {
            yield return CheckWalkStartsAtTheDesk();
            yield return CheckFloorsFollowTheTraces();
            yield return CheckEveryTraceIsPrinted();
            yield return CheckNothingExceedsThePage();
            yield return CheckCleanDayIsOneDeskPage();
            yield return CheckKeyLineFollowsThePage();
            yield return CheckReceiptIsSilentOnACleanDay();
            yield return CheckWrapNeverDropsAWord();
            yield return CheckMetricsAgreeWithTheFont();
        }

        private static List<SweepTrace> Traces(int corruption)
        {
            var hired = new List<ApplicantRecord>
            {
                new ApplicantRecord { id = "a", archetypeRaw = (int)Archetype.SyntheticPolished, fullName = "Maren Voss" },
                new ApplicantRecord { id = "b", archetypeRaw = (int)Archetype.SyntheticCloned, fullName = "Tomas Reyes" },
                new ApplicantRecord { id = "c", archetypeRaw = (int)Archetype.SyntheticCollusion, fullName = "Iris Bane" },
            };
            return Sweep.AfterShift(corruption, hired);
        }

        private static void True(bool condition, string message)
        {
            if (!condition) throw new System.Exception(message);
        }

        private static string CheckWalkStartsAtTheDesk()
        {
            var pages = SweepScript.Build(Traces(1));
            True(pages.Count > 0, "the walk has no pages");
            True(pages[0].Floor == "desk", "the walk starts at the " + pages[0].Floor + ", not your desk");
            True(pages[0].Lines.Count > 0, "the first page says nothing");
            return "PASS  the walk starts at your own desk, with something on it";
        }

        private static string CheckFloorsFollowTheTraces()
        {
            var one = SweepScript.Build(Traces(1));
            var three = SweepScript.Build(Traces(3));
            True(one.Count == 1, "one hire should be one page, got " + one.Count);
            True(three.Count == 3, "three hires should be three pages, got " + three.Count);
            True(three[1].Floor == "corridor" && three[2].Floor == "lift",
                 "the walk runs desk -> corridor -> lift, got " + three[1].Floor + " -> " + three[2].Floor);
            True(three[2].IsLast && !three[0].IsLast, "only the last page may end the walk");
            return "PASS  one page per floor that has something on it, in walking order";
        }

        private static string CheckEveryTraceIsPrinted()
        {
            var traces = Traces(3);
            var pages = SweepScript.Build(traces);
            var all = new System.Text.StringBuilder();
            foreach (var p in pages) foreach (var l in p.Lines) all.Append(l).Append(' ');
            string text = all.ToString();
            for (int i = 0; i < traces.Count; i++)
            {
                // the Title is the one line the player must be able to read
                string head = traces[i].Title.Length <= 40 ? traces[i].Title : traces[i].Title.Substring(0, 40);
                True(text.Contains(head),
                     "trace '" + traces[i].Id + "' never reaches the page: the walk would silently drop it");
            }
            return "PASS  every trace the rule produced is printed on a page";
        }

        private static string CheckNothingExceedsThePage()
        {
            var pages = SweepScript.Build(Traces(3));
            int maxRows = (SweepScript.PageH - SweepScript.TextTop - SweepScript.StripH - 6 +
                           MonitorScreen.LineGap) / (MonitorScreen.GlyphH + MonitorScreen.LineGap);
            foreach (var p in pages)
            {
                True(p.Lines.Count + 2 <= maxRows,
                     "page '" + p.Floor + "' wants " + (p.Lines.Count + 2) + " rows, the page holds " + maxRows);
                foreach (var l in p.Lines)
                    True(l.Length <= SweepScript.Cols,
                         "line overflows the page (" + l.Length + " > " + SweepScript.Cols + "): " + l);
            }
            return "PASS  every page fits " + maxRows + " rows of " + SweepScript.Cols + " columns";
        }

        private static string CheckCleanDayIsOneDeskPage()
        {
            // A clean day never reaches the walk in the game (Sweep.HasSweep is false), but if it is
            // built anyway it must still make sense: one page, at the desk, saying the office is fine.
            var pages = SweepScript.Build(Sweep.AfterShift(0, null));
            True(pages.Count == 1 && pages[0].Floor == "desk", "a clean day is not a single desk page");
            True(pages[0].Lines.Count > 0, "a clean day's page is blank");
            return "PASS  a clean day is one quiet page at the desk";
        }

        private static string CheckKeyLineFollowsThePage()
        {
            var pages = SweepScript.Build(Traces(2));
            var first = SweepScript.KeyLine(pages[0], 0, pages.Count);
            var last = SweepScript.KeyLine(pages[pages.Count - 1], pages.Count - 1, pages.Count);
            True(first.Contains("WALK ON"), "a middle page does not offer to walk on: " + first);
            True(last.Contains("CLOCK OUT"), "the last page does not clock out: " + last);
            True(first.Contains("1/" + pages.Count), "the key strip does not say where you are: " + first);
            return "PASS  the key strip pages forward and clocks out at the lift";
        }

        private static string CheckReceiptIsSilentOnACleanDay()
        {
            True(SweepScript.SummaryLine(0, Sweep.AfterShift(0, null)) == null,
                 "a clean day still put a receipt on the summary");
            string line = SweepScript.SummaryLine(2, Traces(2));
            True(!string.IsNullOrEmpty(line), "a day with hires produced no receipt");
            True(line.Contains("CORRIDOR"), "the receipt names the wrong floor: " + line);
            return "PASS  the summary gets a receipt only when there is something to account for: '" + line + "'";
        }

        private static string CheckWrapNeverDropsAWord()
        {
            string text = "The interview chair is still warm. The badge you filed is face down.";
            var lines = SweepScript.Wrap(text, 20);
            string rejoined = string.Join(" ", lines.ToArray());
            True(rejoined == text, "wrap changed the words: '" + rejoined + "'");
            foreach (var l in lines) True(l.Length <= 20, "wrapped line too long: '" + l + "'");
            // a word longer than the line must be split, never dropped
            var hard = SweepScript.Wrap("Supercalifragilistic", 8);
            True(string.Join("", hard.ToArray()) == "Supercalifragilistic", "a long word was lost in the wrap");
            return "PASS  wrapping keeps every word, and hard-splits one that cannot fit";
        }

        private static string CheckMetricsAgreeWithTheFont()
        {
            // SweepScript is in Core and may not reference the Game assembly, so it mirrors the font
            // metrics as constants. If someone changes the font, the walk's wrap silently changes too --
            // this test is the only thing that notices.
            int cols = (SweepScript.TextW + MonitorScreen.LetterGap) / (MonitorScreen.GlyphW + MonitorScreen.LetterGap);
            True(cols == SweepScript.Cols,
                 "Core and Game disagree on the column count: " + SweepScript.Cols + " vs " + cols);
            True(SweepScript.PageW == 480 && SweepScript.PageH == 270, "the sweep page is not the game's 480x270");
            return "PASS  the walk's column count matches the font the game actually draws with (" + cols + ")";
        }
    }
}
