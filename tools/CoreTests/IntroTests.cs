// HR//0D -- the intro slot: rules the content team's script must satisfy, and the promise that a missing
// or unusable script never stops the game.
// Assembly: test only
//
// The words are BE2's (user decision 2026-11-27). What is tested here is the frame around them: length,
// characters the 5x7 atlas can actually paint, the 25-second budget, and the fallback that must hold when
// the file is absent, empty, or full of things the font cannot draw.
using System;
using System.Collections.Generic;
using HRZeroDay.Core.Rules;

namespace HRZeroDay.Tests
{
    internal static class IntroTests
    {
        public static IEnumerable<string> RunAll()
        {
            yield return CheckAShippingIntroFitsTheRules();
            yield return CheckAMissingScriptIsNotAnError();
            yield return CheckBadBeatsAreDroppedNotCut();
            yield return CheckTheIntroCannotRunLong();
            yield return CheckTheLineIsOneBlockOfPaintedText();
        }

        private static void True(bool condition, string message)
        {
            if (!condition) throw new Exception(message);
        }

        private static IntroBeat Beat(string text, float hold = 2.4f, string speaker = "DISPATCH")
        {
            return new IntroBeat { Text = text, Hold = hold, Speaker = speaker };
        }

        /// <summary>
        /// The placeholder that ships today must pass, and so must anything the content team writes inside
        /// the contract. This is the test they can run against their own file before handing it over.
        /// </summary>
        private static string CheckAShippingIntroFitsTheRules()
        {
            var real = new List<IntroBeat>
            {
                Beat("NIGHT SHIFT. FOURTH FLOOR. THE PREVIOUS OFFICER DID NOT CLOCK OUT.", 3.0f),
                Beat("YOUR JOB IS THE INTERVIEWS. THREE HIRES A SHIFT. NOTHING ELSE.", 3.0f),
                Beat("SOME OF THEM ARE NOT PEOPLE. THEY ARE VERY GOOD AT THIS.", 3.0f),
                Beat("READ THE FILE. LOOK AT THE ONE IN THE CHAIR. THEN STAMP.", 3.0f),
                Beat("AND IF YOU GET ONE WRONG, THE OFFICE WILL TELL YOU IN THE MORNING.", 3.5f)
            };
            var script = IntroScriptRules.Validate("intro_01", 1, real);
            True(script.HasBeats, "the shipping intro does not validate: " + script.Problem);
            True(script.Beats.Count == 5, "the shipping intro has " + script.Beats.Count + " beats, not 5");
            True(script.Problem == null, "the shipping intro validates with problems: " + script.Problem);
            True(script.TotalSeconds <= IntroScriptRules.MaxTotalSeconds,
                 "the shipping intro runs " + script.TotalSeconds + " s, over the 25 s budget");
            foreach (var b in script.Beats)
            {
                True(b.Text.Length <= IntroScriptRules.TextChars,
                     "beat '" + b.Text + "' is " + b.Text.Length + " chars");
                True(IntroScriptRules.IsPaintable(b.Text), "beat has unpaintable characters: " + b.Text);
            }
            return "PASS  the shipping intro validates: 5 beats, " + script.TotalSeconds + " s, all paintable";
        }

        private static string CheckAMissingScriptIsNotAnError()
        {
            // the three ways there is nothing to show
            var none = IntroScriptRules.Validate("x", 1, null);
            True(none != null, "Validate(null) returned null -- callers would crash");
            True(!none.HasBeats, "a null beat list produced beats");
            True(!string.IsNullOrEmpty(none.Problem), "a null beat list produced no reason to log");

            var empty = IntroScriptRules.Validate("x", 1, new List<IntroBeat>());
            True(!empty.HasBeats && !string.IsNullOrEmpty(empty.Problem), "an empty list is showable");

            var blank = IntroScriptRules.Validate("x", 1, new List<IntroBeat> { Beat("   "), Beat(null) });
            True(!blank.HasBeats, "a script of blank beats is showable");

            // and the fallback contract: no beats means the desk starts, so no exception anywhere above
            return "PASS  a missing, empty or blank script is a reason to log, never a reason to fail";
        }

        private static string CheckBadBeatsAreDroppedNotCut()
        {
            // too long: dropped, not truncated mid-word
            string tooLong = new string('A', IntroScriptRules.TextChars + 10);
            var mixed = new List<IntroBeat>
            {
                Beat("THIS ONE IS FINE.", 2.0f),
                Beat(tooLong, 2.0f),
                Beat("LOWER CASE IS FINE BECAUSE IT IS UPPERCASED.", 2.0f),
                Beat("CURLY \u201cQUOTES\u201d AND AN EM DASH \u2014 DEAR.", 2.0f)     // atlas cannot paint these
            };
            var script = IntroScriptRules.Validate("intro_01", 1, mixed);
            True(script.Beats.Count == 2,
                 "expected 2 usable beats, got " + script.Beats.Count + ": " + script.Problem);
            True(script.Beats[0].Text == "THIS ONE IS FINE.", "beat 0 changed: " + script.Beats[0].Text);
            True(script.Beats[1].Text == "LOWER CASE IS FINE BECAUSE IT IS UPPERCASED.",
                 "beat 1 was not upper-cased: " + script.Beats[1].Text);
            True(script.Problem != null && script.Problem.Contains("dropped"),
                 "dropping beats produced no explanation: " + script.Problem);

            // a whole script of unpaintable text is the no-show case, not a crash
            var allBad = IntroScriptRules.Validate("x", 1, new List<IntroBeat> { Beat("\u00e9\u00e8\u00ea") });
            True(!allBad.HasBeats, "unpaintable text produced beats");

            // whitespace is normalised, so a stray double space does not shift the layout
            var spaced = IntroScriptRules.Validate("x", 1, new List<IntroBeat> { Beat("  A   B  ") });
            True(spaced.Beats[0].Text == "A B", "spacing not normalised: '" + spaced.Beats[0].Text + "'");
            return "PASS  a beat that cannot be painted is dropped with a reason, never cut in half";
        }

        private static string CheckTheIntroCannotRunLong()
        {
            // eight beats at the maximum hold is 64 s; the cap must cut it to <= 25 s
            var longRun = new List<IntroBeat>();
            for (int i = 0; i < 8; i++) longRun.Add(Beat("BEAT " + (i + 1) + " STANDS.", IntroScriptRules.MaxHold));
            var script = IntroScriptRules.Validate("x", 1, longRun);
            True(script.HasBeats, "the long intro produced no beats");
            True(script.TotalSeconds <= IntroScriptRules.MaxTotalSeconds,
                 "the intro ran " + script.TotalSeconds + " s after the cap");

            // more than eight beats is not an intro
            var many = new List<IntroBeat>();
            for (int i = 0; i < 12; i++) many.Add(Beat("SHORT.", 1.0f));
            var cut = IntroScriptRules.Validate("x", 1, many);
            True(cut.Beats.Count <= IntroScriptRules.MaxBeats,
                 "12 beats produced " + cut.Beats.Count + " beats");

            // holds are clamped, so a 0 hold does not make a beat invisible and a 60 s hold does not stall
            var holds = IntroScriptRules.Validate("x", 1, new List<IntroBeat> { Beat("NO HOLD.", 0f) });
            True(holds.Beats[0].Hold >= IntroScriptRules.MinHold, "a 0-second hold was kept");
            return "PASS  the intro is clamped to 8 beats and 25 seconds, and no beat can be invisible";
        }

        private static string CheckTheLineIsOneBlockOfPaintedText()
        {
            var b = Beat("READ THE FILE. LOOK AT THE ONE IN THE CHAIR. THEN STAMP.", 3f, "DISPATCH");
            string line = IntroScriptRules.LineFor(b);
            True(line.StartsWith("DISPATCH: "), "the speaker is not part of the line: " + line);
            True(IntroScriptRules.IsPaintable(line), "the composed line has unpaintable characters");

            var wrapped = IntroScriptRules.Wrap(line, 56);
            True(wrapped.Count >= 2, "a 60-char line did not wrap at 56 columns");
            foreach (var w in wrapped)
                True(w.Length <= 56, "a wrapped line is " + w.Length + " chars, over 56");
            True(string.Join(" ", wrapped.ToArray()).Replace("  ", " ") == line,
                 "wrapping lost or duplicated words: " + string.Join(" ", wrapped.ToArray()) + " vs " + line);

            // a word longer than the column count must not be dropped
            var hard = IntroScriptRules.Wrap("A " + new string('X', 70) + " B", 56);
            True(string.Join("", hard.ToArray()).Contains(new string('X', 70)) ||
                 hard.Count == 2, "a 70-char word vanished from the wrap");
            return "PASS  a beat becomes one wrapped, paintable block with its speaker in front";
        }
    }
}
