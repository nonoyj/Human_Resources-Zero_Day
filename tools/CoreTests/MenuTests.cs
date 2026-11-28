// HR//0D -- the front door, as tests.
// Assembly: test only
//
// A menu is where trust is set before the game starts: it is where the player is told what will be recorded
// about them, and where they say yes or no. So the tests here are not "does the list have four items" --
// they are "can the game ever start recording without consent", "can a first run show a dead CONTINUE",
// and "can the player get stuck on a row they cannot choose".
using System;
using System.Collections.Generic;
using HRZeroDay.Core.Rules;

namespace HRZeroDay.Tests
{
    internal static class MenuTests
    {
        public static IEnumerable<string> RunAll()
        {
            yield return CheckContinueOnlyExistsWithASave();
            yield return CheckEveryLabelCanBePainted();
            yield return CheckTheSelectionCannotGetStuck();
            yield return CheckSilenceIsNotConsent();
            yield return CheckTheNoticeIsHonestAndPainting();
            yield return CheckTheVolumeControlsBehave();
            yield return CheckTheCursorLandsOnAnOffGridVolume();
        }

        private static void True(bool condition, string message)
        {
            if (!condition) throw new Exception(message);
        }

        /// <summary>Q20: a first run has no CONTINUE. Not greyed -- absent.</summary>
        private static string CheckContinueOnlyExistsWithASave()
        {
            var first = MenuRules.Root(false, false);
            True(first.Count == 4, "a first run shows " + first.Count + " rows, expected 4");
            foreach (var item in first)
                True(item.Action != MenuAction.Continue, "a first run offers CONTINUE");
            True(first[0].Action == MenuAction.NewRun && first[0].Label == "START",
                 "a first run's first row is not START: " + first[0].Label);

            var resumed = MenuRules.Root(true, true);
            True(resumed[0].Action == MenuAction.Continue, "a save does not offer CONTINUE first");
            True(resumed[0].Label == "CONTINUE THE RUN", "the run label is wrong: " + resumed[0].Label);

            var idle = MenuRules.Root(true, false);
            True(idle[0].Label == "CONTINUE", "a save with no run in progress says: " + idle[0].Label);

            // and the two things that have never had a home are always reachable
            foreach (var list in new List<List<MenuItem>> { first, resumed })
            {
                bool settings = false, consent = false;
                foreach (var item in list)
                {
                    if (item.Action == MenuAction.OpenSettings) settings = true;
                    if (item.Action == MenuAction.OpenConsent) consent = true;
                }
                True(settings, "SETTINGS is not in the menu");
                True(consent, "DATA & CONSENT is not in the menu");
            }
            return "PASS  CONTINUE exists only with a save; SETTINGS and CONSENT are always reachable";
        }

        /// <summary>Every row and every settings/consent row must be paintable: upper case, <= 24 chars.</summary>
        private static string CheckEveryLabelCanBePainted()
        {
            var screens = new List<List<MenuItem>>
            {
                MenuRules.Root(false, false), MenuRules.Root(true, true),
                MenuRules.Settings(false), MenuRules.Settings(true),
                MenuRules.Consent(false, false), MenuRules.Consent(true, true)
            };
            int checkedCount = 0;
            foreach (var screen in screens)
            {
                True(screen.Count > 0, "a menu screen is empty");
                foreach (var item in screen)
                {
                    True(MenuRules.LabelIsPaintable(item.Label),
                         "label cannot be painted with the 5x7 atlas: '" + item.Label + "'");
                    checkedCount++;
                }
            }
            // the long ones are the ones worth naming
            True(MenuRules.LabelIsPaintable("DATA & CONSENT"), "the consent label is unpaintable");
            True(!MenuRules.LabelIsPaintable("Continue the run"), "sentence case was accepted -- the atlas has no lowercase");
            True(!MenuRules.LabelIsPaintable(new string('A', MenuRules.MaxLabelChars + 1)),
                 "an over-long label was accepted");
            return "PASS  all " + checkedCount + " labels are upper-case and <= 24 chars";
        }

        /// <summary>A menu that can park the cursor on a dead row is a menu that looks broken.</summary>
        private static string CheckTheSelectionCannotGetStuck()
        {
            True(MenuRules.Move(0, 4, -1) == 3, "moving up from the top does not wrap");
            True(MenuRules.Move(3, 4, 1) == 0, "moving down from the bottom does not wrap");
            True(MenuRules.Move(0, 0, 1) == 0, "an empty menu did not return 0");

            // a disabled row is skipped in BOTH directions, and never becomes the selection
            var items = new List<MenuItem>
            {
                new MenuItem(MenuAction.Continue, "CONTINUE", true),
                new MenuItem(MenuAction.None, "DISABLED", false),
                new MenuItem(MenuAction.NewRun, "NEW RUN", true)
            };
            True(MenuRules.MoveSkipping(items, 0, 1) == 2, "the cursor landed on a disabled row going down");
            True(MenuRules.MoveSkipping(items, 2, 1) == 0, "the cursor landed on a disabled row wrapping");
            True(MenuRules.MoveSkipping(items, 2, -1) == 0, "the cursor landed on a disabled row going up");

            // all-disabled must not loop forever or throw
            var none = new List<MenuItem> { new MenuItem(MenuAction.None, "A", false) };
            True(MenuRules.MoveSkipping(none, 0, 1) == 0, "an all-disabled menu lost the cursor");
            return "PASS  wrapping works, disabled rows are always skipped, an all-disabled menu cannot hang";
        }

        /// <summary>THE ETHICS TEST. No decision means no telemetry, and no consent means the game still plays.</summary>
        private static string CheckSilenceIsNotConsent()
        {
            True(!ConsentRules.TelemetryAllowed(false, false), "undecided + declined allowed telemetry");
            True(!ConsentRules.TelemetryAllowed(false, true), "undecided + granted allowed telemetry -- silence is not consent");
            True(ConsentRules.TelemetryAllowed(true, true), "decided + granted did not allow telemetry");
            True(!ConsentRules.TelemetryAllowed(true, false), "decided + declined allowed telemetry");

            // a new version of the notice must be re-asked, an old decision is not carried forward silently
            True(ConsentRules.MustAsk(false, 0), "a player who never decided is not asked");
            True(ConsentRules.MustAsk(true, ConsentRules.Version - 1), "a changed notice is not re-asked");
            True(!ConsentRules.MustAsk(true, ConsentRules.Version), "an up-to-date decision is asked again");

            // the summary is honest in all three states
            True(ConsentRules.Summary(false, false) == "DATA: NOT ASKED YET", "undecided summary is wrong");
            True(ConsentRules.Summary(true, true) == "DATA: SHARED", "granted summary is wrong");
            True(ConsentRules.Summary(true, false) == "DATA: NOT SHARED", "declined summary is wrong");

            // THERE IS NO THIRD ROW ON THE CONSENT SCREEN: asking again later would be a deferral, which reads
            // as "yes" in the data while telling the player the opposite.
            var consent = MenuRules.Consent(false, false);
            foreach (var item in consent)
                True(item.Label != "ASK ME LATER", "the consent screen offers a deferral");
            return "PASS  telemetry needs an explicit yes; silence is a no; a changed notice is re-asked";
        }

        /// <summary>The consent text must be readable by the atlas and must not promise more than it can keep.</summary>
        private static string CheckTheNoticeIsHonestAndPainting()
        {
            var lines = ConsentRules.Notice();
            True(lines.Length >= 6, "the consent notice is too short to be honest: " + lines.Length + " lines");
            int chars = 0;
            foreach (var line in lines)
            {
                True(line.Length <= IntroScriptRules.TextChars,
                     "a consent line is " + line.Length + " chars, over the atlas budget: " + line);
                True(IntroScriptRules.IsPaintable(line), "a consent line is unpaintable: " + line);
                chars += line.Length;
            }
            // it must SAY the things a player needs: what, never, why, off by default, changeable
            string all = string.Join(" ", lines);
            True(all.Contains("NEVER RECORDS"), "the notice does not say what it never records");
            True(all.Contains("OFF UNTIL YOU SAY YES"), "the notice does not say it is off by default");
            True(all.Contains("CHANGE YOUR MIND"), "the notice does not say it can be changed");
            True(all.Contains("NAME"), "the notice does not name the thing it protects (the player's name)");
            return "PASS  the consent notice is paintable, " + lines.Length + " lines, and says what, never, why, default, changeable";
        }

        /// <summary>
        /// The off-grid case that actually bit us: a slider sitting at 85% (an old save, or a drag) must move
        /// to the NEXT honest step, not snap-then-jump. Up from 0.85 is 0.9 and down from 0.85 is 0.8 --
        /// snap-first-then-add gives 1.0 and 0.9, which is the bug this test exists to keep dead.
        /// </summary>
        private static string CheckTheCursorLandsOnAnOffGridVolume()
        {
            True(Math.Abs(MenuRules.StepVolume(0.85f, 1) - 0.9f) < 0.0001f,
                "up from an off-grid 85% should reach 90%, got " + MenuRules.StepVolume(0.85f, 1));
            True(Math.Abs(MenuRules.StepVolume(0.85f, -1) - 0.8f) < 0.0001f,
                "down from an off-grid 85% should reach 80%, got " + MenuRules.StepVolume(0.85f, -1));
            True(Math.Abs(MenuRules.StepVolume(0.9f, 1) - 1.0f) < 0.0001f, "90% up should reach 100%");
            True(Math.Abs(MenuRules.StepVolume(1.0f, -1) - 0.9f) < 0.0001f, "100% down should reach 90%");
            True(MenuRules.StepVolume(0.5f, 0) == 0.5f, "a zero step moved the slider");
            return "PASS  an off-grid volume lands on the next honest step in both directions";
        }

        /// <summary>Volume: ten honest steps, clamped, and a bar made of atlas characters.</summary>
        private static string CheckTheVolumeControlsBehave()
        {
            True(MenuRules.StepVolume(0.8f, 1) > 0.8f, "volume up did nothing");
            True(MenuRules.StepVolume(0.0f, -1) == 0f, "volume went below zero");
            True(MenuRules.StepVolume(1.0f, 1) == 1f, "volume went above one");
            True(Math.Abs(MenuRules.StepVolume(0.85f, 1) - 0.9f) < 0.001f, "steps are not snapped to 10%");

            True(MenuRules.Bar(0f) == "----------", "the empty bar is wrong: " + MenuRules.Bar(0f));
            True(MenuRules.Bar(1f) == "##########", "the full bar is wrong: " + MenuRules.Bar(1f));
            True(MenuRules.Bar(0.5f) == "#####-----", "the half bar is wrong: " + MenuRules.Bar(0.5f));
            True(MenuRules.Bar(4f) == "##########", "an out-of-range volume did not clamp to full");
            True(MenuRules.Percent(0.8f) == "80%", "the percentage is wrong: " + MenuRules.Percent(0.8f));
            return "PASS  volume moves in 10% steps and clamps; the bar is exactly ten atlas cells";
        }
    }
}
