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
using HRZeroDay.Game;
using UnityEngine;

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
            yield return CheckEveryRowCanBeReachedAndEveryVolumeCanGoBothWays();
            yield return CheckTheConsentRowIsTheDecision();
            yield return CheckTheConceptRowsExistAndFitTheGlass();
            yield return CheckTheRoomHasEveryPieceFromTheBrief();
            yield return CheckEveryLineFitsTheTube();
            yield return CheckTheMaterialsAreRampsNotColours();
            yield return CheckThePointerCanLandOnEveryRow();
            yield return CheckTheGlowOnlyEverAddsLight();
            yield return CheckTheMenuStopsTheClock();
        }

        private static void True(bool condition, string message)
        {
            if (!condition) throw new Exception(message);
        }

        /// <summary>Q20: a first run has no CONTINUE. Not greyed -- absent.</summary>
        private static string CheckContinueOnlyExistsWithASave()
        {
            var first = MenuRules.Root(false, false);
            // v113: the concept's root is exactly five rows with a save and four on a first run
            // (START is the only first-run row; DATA moved inside SETTINGS). Counts are asserted, not
            // assumed: a row that appears or vanishes has to be a decision somebody wrote down.
            True(first.Count == 4, "a first run shows " + first.Count + " rows, expected 4");
            foreach (var item in first)
                True(item.Action != MenuAction.Continue, "a first run offers CONTINUE");
            True(first[0].Action == MenuAction.NewRun && first[0].Label == "START",
                 "a first run's first row is not START: " + first[0].Label);

            var resumed = MenuRules.Root(true, true);
            True(resumed[0].Action == MenuAction.Continue, "a save does not offer CONTINUE first");
            True(resumed[0].Label == "CONTINUE RUN", "the run label is wrong: " + resumed[0].Label);

            var idle = MenuRules.Root(true, false);
            True(idle[0].Label == "CONTINUE", "a save with no run in progress says: " + idle[0].Label);
            True(idle.Count == 5, "a save shows " + idle.Count + " rows, expected 5 (the concept's root)");

            // and the two things that have never had a home are always reachable -- SETTINGS on the root,
            // DATA & CONSENT one row inside it (v113 moved it there to keep the concept's five-row root)
            foreach (var list in new List<List<MenuItem>> { first, resumed })
            {
                bool settings = false;
                foreach (var item in list)
                    if (item.Action == MenuAction.OpenSettings) settings = true;
                True(settings, "SETTINGS is not in the menu");
            }
            bool consent = false;
            foreach (var item in MenuRules.Settings(false))
                if (item.Action == MenuAction.OpenConsent) consent = true;
            True(consent, "DATA & CONSENT is not reachable from SETTINGS");
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

        /// <summary>
        /// BUG-065a/b (v111). What the player actually hit in v110:
        ///   - the volume could only go UP, so turning a sound down was one-way;
        ///   - UP/DOWN could not reach BACK, because the menu row count and the cursor wrapped wrong.
        /// This walks every screen from index 0 with "down" and asserts BACK is reachable on the way round.
        /// </summary>
        private static string CheckEveryRowCanBeReachedAndEveryVolumeCanGoBothWays()
        {
            var screens = new Dictionary<string, List<MenuItem>>
            {
                { "root", MenuRules.Root(true, true) },
                { "settings", MenuRules.Settings(false) },
                { "consent", MenuRules.Consent(false, false) }
            };
            foreach (var kv in screens)
            {
                var items = kv.Value;
                True(items.Count >= 2, kv.Key + " has fewer than two rows");
                int at = 0;
                var seen = new HashSet<int> { 0 };
                for (int i = 0; i < items.Count; i++)
                {
                    at = MenuRules.MoveSkipping(items, at, 1);
                    seen.Add(at);
                }
                True(seen.Count == items.Count,
                     kv.Key + ": down from the top reaches " + seen.Count + " of " + items.Count + " rows");
                bool hasBack = false;
                foreach (var it in items) if (it.Action == MenuAction.Back) hasBack = true;
                True(hasBack || kv.Key == "root", kv.Key + " has no BACK row -- the player would be stuck");
            }

            // a volume must come back down, from anywhere, all the way to zero and back up
            float v = 1f;
            for (int i = 0; i < 12; i++) v = MenuRules.StepVolume(v, -1);
            True(v == 0f, "holding down did not reach silence: " + v);
            for (int i = 0; i < 12; i++) v = MenuRules.StepVolume(v, 1);
            True(Math.Abs(v - 1f) < 0.0001f, "holding up did not reach full: " + v);
            return "PASS  every row is reachable and both volumes go down as well as up";
        }

        /// <summary>
        /// BUG-065c (v111). The consent screen used to paint a checkbox as decoration and put the real
        /// decision on a second row, so SPACE on the thing that looked like the checkbox did nothing. The
        /// rule now: row 0 IS the decision, its label carries the state, and BACK is the only other row.
        /// </summary>
        private static string CheckTheConsentRowIsTheDecision()
        {
            var no = MenuRules.Consent(false, false);
            True(no.Count == 2, "the consent screen should be the decision + BACK, got " + no.Count + " rows");
            True(no[0].Label.StartsWith("[ ]"), "an unanswered consent row should read [ ]: " + no[0].Label);
            True(no[1].Action == MenuAction.Back, "the second consent row is not BACK");

            var yes = MenuRules.Consent(true, true);
            True(yes[0].Label.StartsWith("[X]"), "an allowed consent row should read [X]: " + yes[0].Label);
            True(MenuRules.ConsentState(false, false) == "NOT ASKED YET", "undecided state is wrong");
            True(MenuRules.ConsentState(true, true) == "ON - YOU SAID YES", "granted state is wrong");
            True(MenuRules.ConsentState(false, true) == "OFF - YOU SAID NO", "declined state is wrong");
            True(no[0].Label != yes[0].Label, "the row does not visibly change when it is answered");
            return "PASS  the row that looks like the checkbox is the row that answers";
        }

        /// <summary>
        /// The player asked for a specific screen (concept, 2026-11-28): CONTINUE, NEW SHIFT, FIELD MANUAL,
        /// SETTINGS, CREDITS inside a big monitor. This asserts those rows exist AND that the layout maths
        /// puts every pill inside the glass -- a row that renders off the bezel is a row nobody can press.
        /// </summary>
        private static string CheckTheConceptRowsExistAndFitTheGlass()
        {
            var rows = MenuRules.Root(true, true);
            var wanted = new[] { "CONTINUE RUN", "NEW SHIFT", "FIELD MANUAL", "SETTINGS", "CREDITS" };
            True(MenuRules.Root(false, false).Count == 4, "a first-run root is not four rows");
            for (int i = 0; i < wanted.Length; i++)
                True(rows[i].Label == wanted[i],
                     "row " + i + " is \"" + rows[i].Label + "\", the concept says \"" + wanted[i] + "\"");

            var screen = MenuScreenText.Root(true, true, 0);
            True(screen.Count == rows.Count, "the painted rows and the menu rows disagree");
            True(screen[0].Selected, "the first row is not the selected one");

            // every pill: inside the glass, and the label fits inside the pill
            for (int i = 0; i < rows.Count; i++)
            {
                var pill = MenuLayout.Row(i, "signal", "void");
                True(pill.X >= MenuLayout.GlassX && pill.X + pill.W <= MenuLayout.GlassX + MenuLayout.GlassW,
                     "pill " + i + " runs outside the glass horizontally");
                True(pill.Y >= MenuLayout.GlassY && pill.Y + pill.H <= MenuLayout.GlassY + MenuLayout.GlassH,
                     "pill " + i + " runs outside the glass vertically");
                int w = rows[i].Label.Length * (MenuLayout.GlyphW + MenuLayout.LetterGap) - MenuLayout.LetterGap;
                True(w <= pill.W, "\"" + rows[i].Label + "\" is " + w + " px, the pill is " + pill.W);
            }
            // the tagline is drawn at 1x (v114: the concept's tagline is small), so 7 px, not 2x 14 px
            True(MenuLayout.TaglineTop + MenuLayout.GlyphH <= MenuLayout.Row(0, "a", "b").Y,
                 "the tagline runs into the first pill");
            True(MenuLayout.Row(4, "a", "b").Y + MenuLayout.Row(4, "a", "b").H <= MenuLayout.FooterTop,
                 "the last pill runs into the footer");
            return "PASS  the concept's five rows exist and every pill sits inside the monitor glass";
        }

        /// <summary>
        /// The player's room (v115, ADR-019): an old monitor on the desk with the menu inside it, a table of
        /// white screens behind it, a window with a city and a moon beside the table, a server in the corner,
        /// and a tiled floor with black outlines. This asserts the LAYOUT half -- that every piece is on the
        /// frame, that nothing is hidden behind the monitor, and that the desk actually meets the wall. The
        /// sprites themselves are gated by tools/art/make_menu_composite.py --check.
        /// </summary>
        private static string CheckTheRoomHasEveryPieceFromTheBrief()
        {
            // everything on the canvas
            True(MenuLayout.CrtX >= 0 && MenuLayout.CrtY >= 0 &&
                 MenuLayout.CrtX + MenuLayout.CrtW <= MenuLayout.W &&
                 MenuLayout.CrtY + MenuLayout.CrtH <= MenuLayout.H,
                 "the monitor is off the frame");
            True(MenuLayout.StackX + MenuLayout.StackW <= MenuLayout.W &&
                 MenuLayout.WindowX + MenuLayout.WindowW <= MenuLayout.W &&
                 MenuLayout.ServerX + MenuLayout.ServerW <= MenuLayout.W,
                 "the back wall does not fit the frame");

            // THE v118 ORDER, LEFT TO RIGHT: the cold window, the monitor, the wall screens, the lamp, the
            // server. The player's reference still is what set this, and it is the composition the whole
            // frame is built around -- a screen you can read, a cold source on one side, a warm one on the
            // other, and the dark between them.
            True(MenuLayout.WindowX + MenuLayout.WindowW <= MenuLayout.CrtX,
                 "the window runs into the monitor");
            // v119: the stack now stands BEHIND the monitor -- its table's west end is hidden by the case,
            // which is the depth cue that makes it furniture in a room. What must hold is that enough of it
            // survives the monitor to read as a table, and that the cluster's table is not level with the
            // glass (a table at the glass's height would read as wallpaper).
            True(MenuLayout.StackX + MenuLayout.StackW - (MenuLayout.CrtX + MenuLayout.CrtW) >= 50,
                 "the monitor hides the whole table");
            True(MenuLayout.StackTableTop < MenuLayout.GlassY + MenuLayout.GlassH,
                 "the table is level with the bottom of the glass");
            True(MenuLayout.StackFootY >= MenuLayout.DeskY - 2 && MenuLayout.StackFootY <= MenuLayout.DeskY + 6,
                 "the table's feet do not meet the floor plane behind the desk");
            True(MenuLayout.StackY + MenuLayout.StackH <= MenuLayout.DeskY + 8,
                 "the table's legs run into the desk's front plane");
            // the lamp stands ON the desk in FRONT of the wall screens -- it is drawn after them, and it
            // is the warm practical the whole frame is balanced against. It must be right of the monitor
            // and left of the server, or the two temperatures stop being on opposite sides.
            True(MenuLayout.LampX > MenuLayout.CrtX + MenuLayout.CrtW,
                 "the lamp is not right of the monitor");
            // v119: the test used to compare the lamp's FULL sprite width, which includes the shade's
            // overhang -- and that overhang is on purpose (the shade hangs over the wall behind the server,
            // and the lamp is drawn last so it reads as in front). What must not happen is the POLE, the
            // thing that carries the light, colliding with the cabinet.
            True(MenuLayout.LampPoleX + MenuLayout.LampPoleW <= MenuLayout.ServerX,
                 "the lamp's pole runs into the server");
            True(MenuLayout.LampY + MenuLayout.LampH >= MenuLayout.DeskTopY,
                 "the lamp does not stand on the desk");

            // the desk meets the wall where the floor starts, and the monitor stands on it
            // The desk's slab is at y 176 and the floor line at y 196, so the line crosses the desk's FRONT
            // PANEL -- which is what a desk in front of a wall looks like. The v118 assertion compared the
            // slab's far edge to the floor line and would have failed if the suite had ever been run; it
            // never was, because there was no compiler in the build box until v119. The requirement is that
            // the desk SPANS the floor line: surface above it, front panel below it.
            True(MenuLayout.DeskTopY <= MenuLayout.FloorY && MenuLayout.DeskEdgeY >= MenuLayout.FloorY,
                 "the desk does not stand across the floor line");
            True(MenuLayout.CrtY + MenuLayout.CrtH >= MenuLayout.DeskTopY - 4 &&
                 MenuLayout.CrtY + MenuLayout.CrtH <= MenuLayout.DeskTopY + 30,
                 "the monitor does not sit on the desk");
            True(MenuLayout.CrtY + MenuLayout.CrtH <= MenuLayout.DeskEdgeY,
                 "the monitor hangs over the front edge of the desk");

            // the glass is inside the case, and the menu fits inside the glass
            True(MenuLayout.GlassX > MenuLayout.CrtX && MenuLayout.GlassY > MenuLayout.CrtY,
                 "the glass is not inside the case");
            True(MenuLayout.GlassX + MenuLayout.GlassW < MenuLayout.CrtX + MenuLayout.CrtW &&
                 MenuLayout.GlassY + MenuLayout.GlassH < MenuLayout.CrtY + MenuLayout.CrtH,
                 "the glass runs out of the case");
            True(MenuLayout.FooterTop + MenuLayout.GlyphH <= MenuLayout.GlassY + MenuLayout.GlassH,
                 "the footer runs out of the glass");

            // THE LIGHT: two sources of OPPOSITE temperature, and one step each. "Cinematic dark" is not
            // "cyan everywhere" -- the warm practical is what proves the cold one is cold (docs/17).
            var lights = MenuLayout.Lights();
            // v120: FOUR sources, because the lamp contributes a cone AND its bounce off the desk. The film
            // look still needs the same three THINGS -- cold key, warm practical, and the tube between them --
            // so the assertion is about temperature, not about a count that would forbid the bounce.
            True(lights.Length == 4, "the room is lit by " + lights.Length + " sources; the lamp needs its cone and its bounce");
            foreach (var l in lights)
                True(l.Strength >= 1 && l.Strength <= MenuLayout.MaxLift,
                     "a light adds " + l.Strength + " steps; ambient light may add one and no more");
            // "Opposite sides of the eye line" means the frame's vertical axis: the window's cold key is on
            // the left, the lamp's warm practical on the right, and the tube sits between them. The old
            // assertion compared the lamp to the FRAME width (lamp.X > 480), which can only be true if the
            // lamp is off the canvas -- it could never have passed, and nothing ran it.
            var lamp = MenuLayout.LampLight();
            var window = MenuLayout.WindowLight();
            True(window.X < MenuLayout.W / 2 && lamp.X > MenuLayout.W / 2,
                 "the warm practical and the cold key are not on opposite sides of the frame's axis");
            // ---- THE LAMP SHINES DOWN (v120, the player's catch: "the lamp glows upward") -------------
            // The light's origin must be at the shade's MOUTH, and the pool must reach further down the
            // canvas than up it. The old source sat at the lamp's FOOT with a symmetric radius, so the wall
            // behind the fixture was the brightest thing in the pool and the desk under it caught nothing.
            // ---- THE LAMP SHINES DOWN (v120; rewritten in v126 when the lamp became TWO sources) ----------
            // The old assertion -- "the cone's centre is below the sprite's midpoint" -- was written when one
            // ellipse had to be both the light leaving the shade and the pool on the desk. v126 split them,
            // because that one ellipse could not be both: placed low it lit the lamp's own foot, placed high
            // it lit the wall. So the assertions are now about the two things the player actually sees:
            //   * the CONE hangs under the bulb, does not reach up over the shade, and is narrow;
            //   * the POOL lies on the desk surface, is wider than it is tall, and reaches the mug to the
            //     lamp's left and the papers to its right.
            var cone = MenuLayout.LampLight();
            var pool = MenuLayout.LampBounceLight();
            int bulbMouth = MenuLayout.LampY + 28;              // the shade is sprite rows 8..28
            True(cone.Y > bulbMouth, "the cone's centre is above the bulb's mouth -- it lights the wall");
            True(cone.Y - cone.Ry >= MenuLayout.LampY,
                 "the cone reaches above the shade's own top -- it is a fog, not a cone");
            True(cone.Rx < 60, "the cone is as wide as the desk");
            True(pool.Y > cone.Y, "the pool is not below the cone -- it must lie on the desk surface");
            True(pool.Rx > cone.Rx, "the pool on the desk is narrower than the cone -- it is a spot, not a pool");
            True(pool.Y + pool.Ry > MenuLayout.DeskY && pool.Y - pool.Ry < MenuLayout.DeskY + 60,
                 "the pool does not lie on the desk band");
            True(pool.X - pool.Rx <= 269, "the pool misses the mug at x269");
            True(pool.X + pool.Rx >= 479, "the pool misses the papers at x479");
            // and it is the SAME lamp: switching the fixture off takes both away
            True(MenuLayout.IsLampLight(cone) && MenuLayout.IsLampLight(MenuLayout.LampBounceLight()),
                 "the bounce does not switch with the lamp -- turning it off would leave a warm pool");
            True(MenuLayout.FadeDrop > MenuLayout.FadeReach,
                 "the tube's light must reach further DOWN the desk than sideways");
            True(MenuLayout.CrtY + MenuLayout.CrtH + MenuLayout.FadeDrop > MenuLayout.DeskEdgeY,
                 "the cold fade never reaches the front of the desk");
            return "PASS  the room has every piece in the reference's order, lit by a cold key and a warm practical";
        }

        /// <summary>
        /// "The text is offscreen" (v115 -> v116). A CRT's tube is a rounded box: its corners are cut and the
        /// picture is smaller than the tube, so a line that fits the flat glass rect can still sit on the
        /// curve. Every line the door paints is measured against the TUBE here, not the glass.
        /// </summary>
        private static string CheckEveryLineFitsTheTube()
        {
            True(MenuLayout.TubeInset > 0, "the tube has no corner inset, so text sits on the curve");
            True(MenuLayout.TextW < MenuLayout.GlassW && MenuLayout.TextH < MenuLayout.GlassH,
                 "the picture area is not inside the glass");

            // the title is measured with the SAME function the view draws it with: tracking and bleed are
            // part of the width, and v117's mock shipped a title that overflowed because they were not.
            int titleW = MenuLayout.LineWidth(MenuScreenText.Title, MenuLayout.TitleScale);
            True(titleW <= MenuLayout.TextW,
                 "the title is " + titleW + " px and the tube holds " + MenuLayout.TextW);
            True(MenuLayout.TitleGap < MenuLayout.LetterGap,
                 "the title is not tracked tighter than the body text (the player asked for closer letters)");
            True(MenuLayout.LineWidth("II", MenuLayout.TitleScale) >
                 MenuLayout.LineWidth("I", MenuLayout.TitleScale) + MenuLayout.GlyphW,
                 "the title glyphs are not bled: it is not thicker than the body text");

            int subW = MenuLayout.LineWidth(MenuScreenText.Subtitle, MenuLayout.TitleScale);
            True(subW <= MenuLayout.TextW, "ZERO DAY is " + subW + " px and the tube holds " + MenuLayout.TextW);
            True(MenuLayout.SubtitleTop - (MenuLayout.TitleTop + MenuLayout.TitleScale * MenuLayout.GlyphH) <= 6,
                 "ZERO DAY is not closer to the title (the player asked for it directly under)");

            int tagW = MenuScreenText.Tagline.Length * (MenuLayout.GlyphW + MenuLayout.LetterGap) - MenuLayout.LetterGap;
            True(tagW <= MenuLayout.TextW, "the tagline is " + tagW + " px and the tube holds " + MenuLayout.TextW);

            // vertical: title, subtitle, tagline, five pills, footer -- all inside the picture area
            int titleBottom = MenuLayout.TitleTop + MenuLayout.TitleScale * MenuLayout.GlyphH;
            True(titleBottom <= MenuLayout.SubtitleTop, "the title runs into ZERO DAY");
            int subBottom = MenuLayout.SubtitleTop + MenuLayout.TitleScale * MenuLayout.GlyphH;
            True(subBottom <= MenuLayout.TaglineTop, "ZERO DAY runs into the tagline");
            True(MenuLayout.TaglineTop + MenuLayout.GlyphH <= MenuLayout.PillTop,
                 "the tagline runs into the first row");
            True(MenuLayout.PillTop >= MenuLayout.TextY, "the rows start above the tube");
            int lastPill = MenuLayout.PillTop + 4 * MenuLayout.PillStep + MenuLayout.PillH;
            True(lastPill <= MenuLayout.FooterTop, "the last row runs into the footer");
            True(MenuLayout.FooterTop + MenuLayout.GlyphH <= MenuLayout.TextY + MenuLayout.TextH,
                 "the footer runs out of the tube");
            True(MenuLayout.PillX >= MenuLayout.TextX &&
                 MenuLayout.PillX + MenuLayout.PillW <= MenuLayout.TextX + MenuLayout.TextW,
                 "the rows run out of the tube sideways");

            // the breach is rationed: an alarm that fires every few seconds stops being an alarm
            True(MenuLayout.BreachMin >= 10.0,
                 "the breach fires every " + MenuLayout.BreachMin + "s: that is decoration, not a warning");
            True(MenuLayout.BreachHold < MenuLayout.BreachMin,
                 "the breach lasts longer than the gap between breaches");
            // the dust is deterministic AND it moves: a fall speed of zero is v116's static vignette
            True(MenuLayout.DustHash(11, 7) == MenuLayout.DustHash(11, 7), "the dust hash is not stable");
            True(MenuLayout.DustHash(11, 7) != MenuLayout.DustHash(12, 7), "the dust hash is not spread");
            True(MenuLayout.DustFallSpeed > 0, "the dust does not fall (the player asked for falling grain)");
            True(MenuLayout.DustCount > 0 && MenuLayout.DustCount <= 60,
                 "the dust is " + MenuLayout.DustCount + " motes: the player asked for LESS dust, not more");
            return "PASS  every line, row and footer sits inside the tube, and the breach is rationed";
        }

        /// <summary>
        /// THE V118 MATERIAL RULE. The player's complaint was that nothing read as its own object, and the
        /// cause was that every surface was one colour brightened by a light. A material now has a RAMP, and
        /// the light can only move it along that ramp. This test pins the table's shape: two steps per
        /// material, no duplicate source key (a key with two "next" steps is ambiguous and the second is
        /// silently ignored), and no ramp that runs backwards into a darker step.
        /// </summary>
        private static string CheckTheMaterialsAreRampsNotColours()
        {
            var seen = new Dictionary<string, string>();
            for (int i = 0; i < MenuLayout.Ramps.GetLength(0); i++)
            {
                string from = MenuLayout.Ramps[i, 0];
                string to = MenuLayout.Ramps[i, 1];
                True(!string.IsNullOrEmpty(from) && !string.IsNullOrEmpty(to),
                     "ramp " + i + " has an empty step");
                True(from != to, "ramp " + i + " maps " + from + " to itself");
                string previous;
                True(!seen.TryGetValue(from, out previous),
                     from + " has two next steps (" + (previous ?? "?") + " and " + to +
                     "): the light pass would silently take the first");
                seen[from] = to;
                // a step must belong to exactly one material family, or "one object, one colour" is a myth
                True(from.Substring(0, from.IndexOf('_')) == to.Substring(0, to.IndexOf('_')),
                     "ramp " + from + " -> " + to + " crosses materials");
            }
            True(seen.ContainsKey("wood_1") && seen["wood_1"] == "wood_2",
                 "the desk is not a ramp");
            True(seen.ContainsKey("wall_0") && seen["wall_0"] == "wall_1",
                 "the wall is not a ramp");
            return "PASS  every material is a ramp of its own steps, and no key has two next steps";
        }

        /// <summary>
        /// THE CLOCK STOPS BEHIND THE DOOR (BUG-071). The player's report was "the game is playing while the
        /// menu is displaying". DeskBootstrap.Update is the only place the shift is ticked, so the rule is
        /// asserted at its source rather than at the symptoms: while ShowingMenu or ShowingIntro is true,
        /// nothing may advance.
        /// </summary>
        /// <summary>
        /// v119: the menu is interactive, so every row must be reachable by POINTER as well as by key. The
        /// hit test is the same table the pills are drawn from, and a near-miss of 2 px must still count --
        /// a 13 px row is a small target on a phone. This check walks the centre of every row and the two
        /// pixel band under it, and fails if any of them resolves to the wrong row or to none.
        /// </summary>
        private static string CheckThePointerCanLandOnEveryRow()
        {
            for (int screen = 0; screen < 3; screen++)
            {
                var items = screen == 0 ? MenuRules.Root(true, false)
                          : screen == 1 ? MenuRules.Settings(false)
                          : MenuRules.Consent(false, true);
                for (int i = 0; i < items.Count; i++)
                {
                    int cx = MenuLayout.PillX + MenuLayout.PillW / 2;
                    int cy = MenuLayout.PillTop + i * MenuLayout.PillStep + MenuLayout.PillH / 2;
                    True(MenuLayout.RowAt(cx, cy, items.Count) == i,
                         "the pointer does not land on row " + i + " of screen " + screen);
                    // the row's own bottom pixel must belong to it, and so must its top one
                    True(MenuLayout.RowAt(cx, MenuLayout.PillTop + i * MenuLayout.PillStep + MenuLayout.PillH - 1,
                         items.Count) == i, "the last pixel of row " + i + " belongs to the row above it");
                }
                int mid = MenuLayout.PillX + MenuLayout.PillW / 2;
                // 1 px above the first row and 1 px below the last still land on the block: the slop lives
                // only at the OUTSIDE edges, never between two rows
                True(MenuLayout.RowAt(mid, MenuLayout.PillTop - 1, items.Count) == 0,
                     "a near-miss above the first row misses the block");
                // v119.5: the slop is OUTSIDE the block only, and it is 2 px. The old check allowed 2 px of
                // slop BOTH ways because RowAt used to clamp everything below the block onto the last row --
                // that clamp is exactly BUG-086 ("clicking anywhere outside the monitor chooses whatever is
                // highlighted"), so the assertion moves with the fix: 2 px belongs to the block, 3 px does not.
                True(MenuLayout.RowAt(mid, MenuLayout.PillTop + items.Count * MenuLayout.PillStep + 1, items.Count)
                     == items.Count - 1, "a 1 px near-miss below the last row misses the block");
                True(MenuLayout.RowAt(mid, MenuLayout.PillTop + items.Count * MenuLayout.PillStep + 3, items.Count)
                     == -1, "a 3 px near-miss below the last row still lands on it");
                True(MenuLayout.RowAt(mid, MenuLayout.PillTop + items.Count * MenuLayout.PillStep + 4, items.Count) == -1,
                     "the tube below the last row resolves to a row");
            }

            // ---- THE ROOM IS NOT A BUTTON (v119.5, BUG-086) ------------------------------------------
            // The player: "clicking randomly anywhere outside the monitor clicks whatever is highlighted on
            // the screen". RowAt used to CLAMP (anything below the block counted as the last row, and the
            // tube below the pills was never excluded), and the router then fell back to Choose() for any
            // click that hit nothing. Both are gone; this is the arithmetic they left behind.
            int count = MenuRules.Root(true, false).Count;
            int cx0 = MenuLayout.PillX + MenuLayout.PillW / 2;
            int[][] dead =
            {
                new[] { 6, 40 },                                             // the wall, far left
                new[] { MenuLayout.CrtX + 4, MenuLayout.CrtY + 8 },           // the monitor's own bezel
                new[] { cx0, MenuLayout.GlassY + 6 },                         // inside the glass, above the title
                new[] { MenuLayout.GlassX + 6, MenuLayout.PillTop + 20 },     // inside the glass, left of the pills
                new[] { MenuLayout.GlassX + MenuLayout.GlassW - 6, MenuLayout.PillTop + 20 },  // ... and right
                new[] { cx0, MenuLayout.PillTop + count * MenuLayout.PillStep + 2 },           // under the block
                new[] { cx0, MenuLayout.GlassY + MenuLayout.GlassH - 4 },     // the tube's lower lip
                new[] { 470, 260 },                                           // the floor, bottom right
            };
            for (int i = 0; i < dead.Length; i++)
                True(MenuLayout.RowAt(dead[i][0], dead[i][1], count) == -1,
                     "the pointer resolves to a row at dead point " + i + " (" + dead[i][0] + "," + dead[i][1] + ")");

            // ...and the reverse: every point inside a pill still resolves to that pill, so the fix cannot
            // have narrowed the rows into something unclickable
            for (int i = 0; i < count; i++)
            {
                int y = MenuLayout.PillTop + i * MenuLayout.PillStep + MenuLayout.PillH / 2;
                True(MenuLayout.RowAt(MenuLayout.PillX + 1, y, count) == i, "the left edge of row " + i + " is dead");
                True(MenuLayout.RowAt(MenuLayout.PillX + MenuLayout.PillW - 2, y, count) == i,
                     "the right edge of row " + i + " is dead");
            }
            True(MenuLayout.InRowBlock(cx0, MenuLayout.PillTop + 4, count), "the row block does not claim its own area");
            True(!MenuLayout.InRowBlock(6, 40, count), "the row block claims the wall");

            // ---- THE KEYBOARD'S TEXT WINDOW (v119.5) -------------------------------------------------
            // It must be OFF the tube (the first version covered the rows, and the screenshot of it was the
            // whole reason it moved), entirely on the canvas, and wide enough for its longest line.
            True(MenuLayout.TypeWindowY >= MenuLayout.GlassY + MenuLayout.GlassH,
                 "the keyboard's text window covers the tube");
            True(MenuLayout.InGlass(MenuLayout.TypeWindowX + 4, MenuLayout.TypeWindowY + 4) == false,
                 "the text window's top-left corner is inside the monitor's glass");
            True(MenuLayout.TypeWindowX >= 0 && MenuLayout.TypeWindowY >= 0 &&
                 MenuLayout.TypeWindowX + MenuLayout.TypeWindowW <= MenuLayout.W &&
                 MenuLayout.TypeWindowY + MenuLayout.TypeWindowH <= MenuLayout.H,
                 "the keyboard's text window runs off the canvas");
            // it hangs off the keyboard: the desk draws the keys at (300,186) 84x20, so the window's box must
            // at least reach under them in x, or it reads as a box from nowhere
            True(MenuLayout.TypeWindowX <= 300 && MenuLayout.TypeWindowX + MenuLayout.TypeWindowW >= 384,
                 "the text window does not sit under the keyboard");
            // three rows of type (header + 2 body lines at 9 px) plus padding, and it must fit its longest line:
            // 27 cells at the atlas's 7 px pitch is 189 px, plus 8 px of padding, inside 192
            int cells = (MenuLayout.TypeWindowW - 8) / (MenuLayout.GlyphW + MenuLayout.LetterGap);
            True(cells >= 26, "the text window only holds " + cells + " glyph cells");
            True(MenuLayout.TypeWindowH >= 3 * 9 + 6, "the text window is too short for its three lines");
            // the props: each hit box must be inside the canvas and over the thing it names
            // v119.1: the cluster moved right of the monitor (366+), so the hit test follows it
            True(MenuLayout.StackCellAt(MenuLayout.StackX + 8, MenuLayout.StackY + 10) >= 0,
                 "the monitor cluster is not clickable");
            True(MenuLayout.StackCellAt(MenuLayout.StackX - 4, MenuLayout.StackY + 10) < 0,
                 "the wall left of the cluster is clickable");
            // and the cluster must clear the monitor's case, or its hard edge slices a screen in half
            True(MenuLayout.StackX >= MenuLayout.CrtX + MenuLayout.CrtW,
                 "the monitor cluster starts under the monitor's case");
            True(MenuLayout.InLamp(MenuLayout.LampX + 28, MenuLayout.LampY + 80), "the lamp is not clickable");
            True(MenuLayout.InKeyboard(340, 196), "the keyboard is not clickable");
            True(!MenuLayout.InLamp(MenuLayout.LampX + 28, MenuLayout.FloorY + 40), "the lamp's hit box reaches the floor");
            return "PASS  every row and both interactive props answer the pointer, and the gaps between them do not";
        }

        /// <summary>
        /// The glow layer may ONLY add light. It is built as a strength map over the palette's own ramps, and
        /// the runtime walks each surface UP its own ramp -- so a glow pixel can never be a colour the surface
        /// did not already belong to, except the emissive rim. This check is the arithmetic version of that
        /// rule: for every strength in GlowRamp, the thing it lifts to must be a real palette step and the
        /// ramp must be strictly increasing in value.
        /// </summary>
        private static string CheckTheGlowOnlyEverAddsLight()
        {
            var palette = MonitorScreenView.PaletteCached;
            True(palette != null, "the palette did not load, so nothing can be checked about the light");
            True(MenuLayout.GlowSprites.Length >= 1, "there is no glow layer at all");

            double lastCold = -1, lastWarm = -1;
            for (int i = 0; i < MenuLayout.GlowRamp.Length; i++)
            {
                Color32 c;
                True(palette.TryGetValue(MenuLayout.GlowRamp[i], out c),
                     "the glow ramp names a colour the palette does not have: " + MenuLayout.GlowRamp[i]);
                double v = (0.299 * c.r + 0.587 * c.g + 0.114 * c.b) / 255.0;
                if (i < MenuLayout.GlowHotLen)
                {
                    True(v >= lastCold, "the cold glow ramp is not increasing in value at step " + i);
                    lastCold = v;
                }
                else if (i < MenuLayout.GlowEmissive)
                {
                    True(v >= lastWarm, "the warm glow ramp is not increasing in value at step " + i);
                    lastWarm = v;
                }
                else
                {
                    True(v > 0.5, "an emissive glow step is not bright enough to be a specular: "
                         + MenuLayout.GlowRamp[i]);
                }
            }
            // the warm ramp must actually be WARM, and the cold one cold: this is the two-temperature rule
            Color32 cold0, warm0;
            palette.TryGetValue(MenuLayout.GlowRamp[0], out cold0);
            palette.TryGetValue(MenuLayout.GlowRamp[MenuLayout.GlowHotLen], out warm0);
            True(cold0.b > cold0.r, "the cold glow is not cool");
            True(warm0.r > warm0.b, "the warm glow is not warm");

            // the ramp table must be able to lift the wall and the floor: the two surfaces every halo lands on
            bool wallLit = false, floorLit = false;
            for (int i = 0; i < MenuLayout.Ramps.GetLength(0); i++)
            {
                if (MenuLayout.Ramps[i, 0] == "wall_2") wallLit = true;
                if (MenuLayout.Ramps[i, 0] == "floor_2") floorLit = true;
            }
            True(wallLit && floorLit,
                 "the wall and the floor have no lit step, so the glow can only tint them (the v119 first attempt)");
            return "PASS  the glow adds light up the palette's own ramps, cold on one side and warm on the other";
        }

        private static string CheckTheMenuStopsTheClock()
        {
            // the tests run from the repo root (see ManifestBridgeTests), so this is a plain relative read
            string text = System.IO.File.ReadAllText(
                "unity/Assets/Scripts/Game/Interaction/DeskBootstrap.cs");
            int update = text.IndexOf("private void Update()", StringComparison.Ordinal);
            True(update >= 0, "DeskBootstrap has no Update: the shift clock has no owner");
            int tick = text.IndexOf("Session.Tick(", update, StringComparison.Ordinal);
            True(tick > update, "Session.Tick is not in Update");
            string between = text.Substring(update, tick - update);
            True(between.Contains("ShowingMenu"),
                 "Update ticks the shift while the front door is up: BUG-071 has come back");
            True(between.Contains("ShowingIntro"),
                 "Update ticks the shift while the intro card is up");
            return "PASS  the shift clock stops while the menu or the intro is on screen";
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
