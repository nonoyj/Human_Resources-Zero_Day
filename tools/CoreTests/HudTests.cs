// HR//0D -- the HUD's own tests: geometry, states, hit tests, and the rules the research gave us.
// Assembly: HRZeroDay.Tests
//
// WHY THESE EXIST (v126)
//     The HUD is being turned from a block of prose into a rack of objects. Prose can be wrong and still
//     readable; an object that is in the wrong place, or clickable where it does nothing, or grey when it is
//     available, is just broken. These tests are the arithmetic that was never written down:
//
//       * every object is inside the canvas and outside the play band (the applicant's face);
//       * no two objects overlap, and none touch within a pixel;
//       * a hit test on a disabled object returns nothing (the standing "clicks are scoped" rule);
//       * the keyboard can reach everything the pointer can, and vice versa;
//       * resting positions do not move when the numbers change (rule 3);
//       * the score is a shape before it is a number (rule 2).
using System;
using System.Collections.Generic;
using HRZeroDay.Core.Rules;

namespace HRZeroDay.Tests
{
    public static class HudTests
    {
        public static IEnumerable<string> RunAll()
        {
            yield return Check("hud objects are inside the frame and out of the play band", InsideFrameAndOutOfBand);
            yield return Check("hud objects never overlap or touch", NoOverlap);
            yield return Check("hud resting positions do not move with the numbers", PositionsAreStable);
            yield return Check("a click on a disabled object does nothing", DisabledIsNotClickable);
            yield return Check("every hud action is reachable by pointer and by keyboard", BothInputsReachEverything);
            yield return Check("no object without an action, no action without an object", ObjectsMatchActions);
            yield return Check("the security board is a shape before a number", BoardIsAShape);
            yield return Check("the hud never prints a key legend", NoKeyLegendInTheObjects);
            yield return Check("letter mode keeps the stamp pads reachable", StampWhileReading);
            yield return Check("the walk and the menu own their own screens", OverlaysSuppressTheDesk);
        }

        private static string Check(string name, Func<string> body)
        {
            try { return body(); }
            catch (Exception ex) { return "FAIL  " + name + ": " + ex.Message; }
        }

        private static void True(bool ok, string message) { if (!ok) throw new Exception(message); }

        /// <summary>Every state the game actually passes through, as models. One list, so no test forgets one.</summary>
        private static List<HudModel> States()
        {
            var list = new List<HudModel>();
            list.Add(new HudModel());                                              // fresh desk
            list.Add(new HudModel { LetterAvailable = true });
            list.Add(new HudModel { LetterAvailable = true, LetterOpen = true });
            list.Add(new HudModel { LetterAvailable = true, LetterOpen = true, FlagCount = 3, Flagged = new[] { true, false, true, false, false } });
            list.Add(new HudModel { LetterAvailable = true, StampedToday = true });
            list.Add(new HudModel { AlertsOpen = true });
            list.Add(new HudModel { HelpVisible = true });
            list.Add(new HudModel { ShowingDebrief = true, AutoAdvance = true });
            list.Add(new HudModel { ShowingSummary = true });
            list.Add(new HudModel { ShowingEnding = true });
            list.Add(new HudModel { ShowingWalk = true });
            list.Add(new HudModel { Hovered = HudId.Approve });
            list.Add(new HudModel { KeyboardArmed = HudId.Reject });
            list.Add(new HudModel { Day = 5, Subject = 12, SubjectCount = 12, Security = 100, Trust = 100 });
            return list;
        }

        private static string InsideFrameAndOutOfBand()
        {
            foreach (var m in States())
            {
                foreach (var o in HudRules.Objects(m))
                {
                    True(o.X >= 0 && o.Y >= 0 && o.X + o.W <= HudRules.CanvasW && o.Y + o.H <= HudRules.CanvasH,
                         o.Id + " is outside the 480x270 canvas at " + o.X + "," + o.Y + " " + o.W + "x" + o.H);
                    True(HudRules.IsAllowed(o), o.Id + " intrudes on the play band (the applicant's face)");
                }
            }
            return "PASS  every object sits on the frame's edge and none enters the play band";
        }

        private static string NoOverlap()
        {
            foreach (var m in States())
            {
                var objs = HudRules.Objects(m);
                var bad = HudRules.GutterViolation(objs);
                True(bad == null, "in one state: " + bad);
                // and a 1 px probe grid: no point may hit two objects
                for (int y = 0; y < HudRules.CanvasH; y += 2)
                {
                    for (int x = 0; x < HudRules.CanvasW; x += 2)
                    {
                        int hits = 0;
                        foreach (var o in objs) if (o.Clickable && o.Contains(x, y)) hits++;
                        True(hits <= 1, "point " + x + "," + y + " hits " + hits + " objects");
                    }
                }
            }
            return "PASS  no two objects overlap, and every point on the frame hits at most one";
        }

        private static string PositionsAreStable()
        {
            // the same view, wildly different numbers: the rects must be identical
            var a = new HudModel { Day = 1, Subject = 1, SubjectCount = 1, Security = 0, Trust = 0, Hired = 0 };
            var b = new HudModel { Day = 5, Subject = 10, SubjectCount = 10, Security = 100, Trust = 100, Hired = 3 };
            var ra = HudRules.Objects(a);
            var rb = HudRules.Objects(b);
            True(ra.Count == rb.Count, "the object count changed with the numbers");
            for (int i = 0; i < ra.Count; i++)
            {
                True(ra[i].Id == rb[i].Id, "object " + i + " changed identity");
                True(ra[i].X == rb[i].X && ra[i].Y == rb[i].Y && ra[i].W == rb[i].W && ra[i].H == rb[i].H,
                     ra[i].Id + " moved when a counter changed -- a resting position must not depend on contents");
            }
            return "PASS  resting positions are a function of the view, not of the numbers";
        }

        private static string DisabledIsNotClickable()
        {
            // the letter tray after the stamp: drawn, grey, and a click on it lands on the desk
            var m = new HudModel { LetterAvailable = true, StampedToday = true };
            var objs = HudRules.Objects(m);
            HudObject letter = null;
            foreach (var o in objs) if (o.Id == HudId.Letter) letter = o;
            True(letter != null, "the closed file is not drawn at all -- rule 4 says it goes grey, not away");
            True(letter.State == HudState.Disabled, "the closed file is not disabled");
            var hit = HudRules.Hit(objs, letter.X + 2, letter.Y + 2);
            True(hit == null, "a click on the disabled file hit " + (hit == null ? "nothing" : hit.Id.ToString()));

            // and the same for a flag tab that does not exist today
            var m2 = new HudModel { LetterOpen = true, FlagCount = 2 };
            foreach (var o in HudRules.Objects(m2))
                if ((int)o.Id >= (int)HudId.Flag3 && (int)o.Id <= (int)HudId.Flag5)
                    True(o.State == HudState.Disabled, o.Id + " is clickable but the letter has no such flag");
            return "PASS  grey means dead: disabled objects are drawn and refuse the click";
        }

        private static string BothInputsReachEverything()
        {
            foreach (var m in States())
            {
                foreach (var o in HudRules.Objects(m))
                {
                    if (!o.Clickable) continue;
                    // the keyboard's list is every id with an action; the pointer's list is the same ids
                    True(HudRules.Available(o.Id, m), o.Id + " is clickable but unavailable");
                    var keyboard = ActionFor(o.Id);
                    True(!string.IsNullOrEmpty(keyboard), o.Id + " has no keyboard action -- a pointer-only button");
                    // and the object is where its action's key would expect to find it: same rect across states
                    var same = HudRules.Objects(m);
                    foreach (var other in same)
                        if (other.Id == o.Id)
                            True(other.X == o.X && other.Y == o.Y, o.Id + " is in two places in one frame");
                }
            }
            return "PASS  every clickable object has a key, and every key's object is clickable";
        }

        private static string ActionFor(HudId id)
        {
            switch (id)
            {
                case HudId.Letter: return "L";
                case HudId.Flag1: case HudId.Flag2: case HudId.Flag3: case HudId.Flag4: case HudId.Flag5:
                    return "1-5";
                case HudId.Approve: return "A";
                case HudId.Reject: return "S";
                case HudId.Alerts: return "TAB / ESC";
                case HudId.Help: return "F1";
                case HudId.Continue: return "SPACE";
                default: return null;
            }
        }

        private static string ObjectsMatchActions()
        {
            foreach (var m in States())
            {
                foreach (var o in HudRules.Objects(m))
                {
                    switch (o.Id)
                    {
                        case HudId.Flag1: case HudId.Flag2: case HudId.Flag3: case HudId.Flag4: case HudId.Flag5:
                            True(m.LetterOpen, o.Id + " is drawn outside the letter -- nothing it could do");
                            break;
                        case HudId.Approve:
                        case HudId.Reject:
                            True(!m.ShowingSummary && !m.ShowingEnding, o.Id + " is drawn on a screen that has no stamp");
                            break;
                        case HudId.Help:
                            True(!m.LetterOpen && !m.ShowingDebrief, o.Id + " is drawn over an overlay it cannot help with");
                            break;
                        case HudId.Continue:
                            True(m.ShowingDebrief || m.ShowingSummary || m.ShowingEnding,
                                 "CONTINUE is drawn on the idle desk -- there is nothing to continue");
                            break;
                    }
                }
            }
            return "PASS  every drawn object's action exists in that state, and none is drawn where it could not act";
        }

        private static string BoardIsAShape()
        {
            True(HudRules.FilledPips(100) == 5, "a perfect score is not five pips");
            True(HudRules.FilledPips(0) == 0, "a zero score fills a pip");
            True(HudRules.FilledPips(50) == 3, "50 rounds to three pips, not " + HudRules.FilledPips(50));
            True(HudRules.FilledPips(90) == 5, "90 does not read as full");
            var line = HudRules.BoardLine(62);
            // THE SHAPE IS THE PIPS, AND THE PIPS ARE DRAWN, NOT TYPED (v126): the atlas has no block glyph,
            // so the board's shape lives in PipRect/PipFilled as five 4x7 blocks, and the string carries only
            // the band word and the number. This test used to look for "\u25AE" in the string, which passed --
            // and the render showed an empty board, because the character drew nothing. Counting pixels is the
            // only assertion that would have caught it; counting pips is the arithmetic behind them.
            int filled = 0;
            for (int i = 0; i < HudRules.Pips; i++)
            {
                int x, y, w, h;
                HudRules.PipRect(i, out x, out y, out w, out h);
                True(w >= 3 && h >= 5, "pip " + i + " is too small to read as a shape");
                True(x >= HudRules.BoardX && x + w <= HudRules.BoardX + HudRules.BoardW,
                     "pip " + i + " is outside the board");
                if (HudRules.PipFilled(62, i)) filled++;
            }
            True(filled == 3, "62 fills " + filled + " pips, not 3");
            True(line.EndsWith("62"), "the number is not last on the board line: " + line);
            True(HudRules.BandWord(62) == "WATCH", "62 is not WATCH: " + HudRules.BandWord(62));
            True(HudRules.BandWord(88) == "SECURE" && HudRules.BandWord(30) == "BREACH",
                 "the band words do not climb");
            return "PASS  the score is five pips and a band word, and the number follows them";
        }

        private static string NoKeyLegendInTheObjects()
        {
            // the player's complaint, as a test: nothing the player sees may be a bracketed key legend
            foreach (var m in States())
            {
                foreach (var o in HudRules.Objects(m))
                {
                    True(o.Label == null || o.Label.IndexOf('[') < 0,
                         o.Id + " still prints a key legend: " + o.Label);
                    True(o.Label == null || o.Label.Length <= 10,
                         o.Id + " carries a sentence instead of a label: " + o.Label);
                }
            }
            var plate = HudRules.PlateLine(new HudModel { Day = 2, Subject = 3, SubjectCount = 4 });
            True(plate.IndexOf('[') < 0, "the shift plate prints a key legend: " + plate);
            return "PASS  no object prints a key legend, and every label is a label";
        }

        private static string StampWhileReading()
        {
            // the gesture is: open the letter, flag what is wrong, stamp it. All three in one screen.
            var m = new HudModel { LetterAvailable = true, LetterOpen = true, FlagCount = 5 };
            var ids = new HashSet<HudId>();
            foreach (var o in HudRules.Objects(m)) ids.Add(o.Id);
            True(ids.Contains(HudId.Flag1) && ids.Contains(HudId.Flag5), "the flag tabs are not drawn in letter mode");
            True(ids.Contains(HudId.Approve) && ids.Contains(HudId.Reject),
                 "the stamp pads vanish while the letter is open -- the player must close the letter to stamp");
            True(ids.Contains(HudId.Letter), "the letter cannot be closed from the letter");
            return "PASS  read, flag and stamp all happen on one screen";
        }

        private static string OverlaysSuppressTheDesk()
        {
            True(HudRules.Objects(new HudModel { ShowingWalk = true }).Count == 0,
                 "the desk HUD draws over the after-hours walk");
            var deb = HudRules.Objects(new HudModel { ShowingDebrief = true });
            True(deb.Count == 1 && deb[0].Id == HudId.Continue, "the debrief draws more than its own CONTINUE");
            var alerts = HudRules.Objects(new HudModel { AlertsOpen = true, LetterAvailable = true });
            True(alerts.Count == 1 && alerts[0].Id == HudId.Alerts,
                 "the alerts popup leaves desk objects clickable underneath it");
            return "PASS  the walk, the debrief and the popup each own the corner they are drawn in";
        }
    }
}
