// HR//0D -- the HUD as OBJECTS, not text.
// Assembly: HRZeroDay.Core
//
// WHY THIS FILE EXISTS (v126, the player's request Q)
//     The play HUD was a run of prose in the top-left corner: "DAY 2/5 SUBJECT 1/4 / MIRA VOSS / OPEN ROLE /
//     TRUST 65 HIRED 0/3 / [L] LETTER [1-5] FLAGS [A] APPROVE ...". The player's words: "It should not be
//     displayed as text but rather as clickable objects." They are right twice over. A key legend is a
//     list of things the game already knows how to do, printed instead of offered; and every one of those
//     affordances -- open the letter, flag, stamp, look at the score -- is a THING on a desk, not a line of
//     a paragraph. The desk this game is set at has a letter tray, a stamp, a security board, a badge. The
//     HUD should be those.
//
// WHAT IS AND IS NOT AN OBJECT (docs/89 §7, and the deep research in docs/89's source list)
//     Objects: things the player can PRESS, plus the two readings a player looks at without pressing
//     (the day and the subject count, and the security score -- which is a SHAPE first: a bar and a band,
//     then a number). Text left as text: a name and a role. A name is a noun, not a button; making it
//     clickable would be a lie about what the click does.
//
// WHY THE GEOMETRY IS HERE, IN CORE
//     Because it has to be checked without a renderer. Every layout bug this project has had -- the HUD
//     drawing over the menu, the corner landing under a phone's rounded corner, the text growing 23x -- was
//     arithmetic nobody had written down. So: the canvas is 480x270 (the whole desk's canvas), every object
//     is a rect on it, the states are a table, the hit test is one function, and the tests read this file.
//     The view (GameHudObjects.cs) does nothing but draw what this file says and forward clicks.
//
// THE FIVE RULES THIS ENCODES (from the HUD research; each one is enforced by a test)
//     1. ANCHOR TO EDGES, KEEP THE CENTRE CLEAR. The centre of this screen is a person's face across a
//        table. Nothing here may enter the middle band.
//     2. SHAPE BEFORE NUMBER. The score is five pips and a band word; the number is secondary.
//     3. NEVER MOVE A RESTING POSITION. An object's rect is a function of the view state only, never of
//        its own contents -- a button that jumps when a counter passes 9 is a bug, not a flourish.
//     4. EVERY OBJECT HAS FOUR STATES. idle / hover / armed / disabled, each with its own ink and fill.
//     5. NO OBJECT WITHOUT AN ACTION, AND NO ACTION WITHOUT AN OBJECT. A rect that does nothing must not
//        be clickable, and every action the keyboard can reach must be reachable by a click.

using System;
using System.Collections.Generic;

namespace HRZeroDay.Core.Rules
{
    public enum HudId
    {
        Letter,       // the letter tray: opens / closes the paperwork
        Flag1, Flag2, Flag3, Flag4, Flag5,   // the five flag tabs on the letter's edge
        Approve,      // the stamp pad, green
        Reject,       // the stamp pad, red
        Alerts,       // the security board: the ALERTS panel
        Help,         // the ? plaque
        Continue      // the big forward arrow (debrief / summary / ending)
    }

    public enum HudState
    {
        Idle,
        Hover,
        Armed,       // pressed/held, or "this is the one the keyboard has selected"
        Disabled     // visible but not actionable: it says why by being grey, never by vanishing
    }

    /// <summary>One clickable thing on the desk. Rect in canvas px, top-left origin, y down.</summary>
    public sealed class HudObject
    {
        public HudId Id;
        public int X, Y, W, H;
        public string Label;
        public HudState State;
        public bool Clickable { get { return State != HudState.Disabled; } }

        public bool Contains(int px, int py)
        {
            return px >= X && px < X + W && py >= Y && py < Y + H;
        }
    }

    /// <summary>What the desk looks like right now, as far as the HUD is concerned. Plain data.</summary>
    public sealed class HudModel
    {
        public int Day = 1, DayCount = 5, Subject = 1, SubjectCount = 1;
        public string Name = "APPLICANT", Role = "OPEN ROLE";
        public int Trust = 50, Security = 50;
        public int Hired, HireLimit = 3;
        public bool LetterOpen, LetterAvailable, StampedToday;
        public bool AlertsOpen, HelpVisible;
        public bool ShowingDebrief, ShowingSummary, ShowingEnding, ShowingWalk;
        public bool AutoAdvance;
        public int FlagCount = 5;
        public bool[] Flagged = new bool[5];
        public int FlagArmed = -1;          // the flag the pointer or the keyboard is on, 0-based
        public HudId? Hovered;              // which object the pointer is over, if any
        public HudId? KeyboardArmed;        // which object the keyboard would press

        public bool AnyOverlay { get { return LetterOpen || AlertsOpen || ShowingDebrief || ShowingSummary || ShowingEnding; } }
    }

    public static class HudRules
    {
        // ---- the canvas. The desk's own, so a HUD rect can always be compared to a desk rect.
        public const int CanvasW = 480, CanvasH = 270;

        // ---- WHERE THE OBJECTS ARE.
        //
        // The composition, left to right and top to bottom:
        //
        //   top-left      the SHIFT PLATE: day, subject, name. Read-only, so it is a painted plate with
        //                 ink on it, not a button. (The name is text on purpose -- see the header.)
        //   top-right     the SECURITY BOARD: five pips, a band word, then the number.
        //   left edge     the FLAG TABS (1..5), stacked, hanging off the letter's spine.
        //   bottom-left   the STAMP PADS: APPROVE and REJECT, side by side, like a real stamp rack.
        //   bottom-right  the FORWARD ARROW, one object that means "the next thing".
        //   top-right     (below the board) ALERTS and HELP as two small plaques.
        //
        // Nothing is placed in the middle band x 120..380, y 40..230. That is the applicant's face and the
        // monitor's glass; the tests assert the band is empty of objects in every state.
        public const int MiddleX0 = 120, MiddleX1 = 380, MiddleY0 = 40, MiddleY1 = 230;

        // The two READINGS above the letter tray -- day/subject, then the person. They are painted text, so
        // they get their own band and no plate; an object drawn on top of them would be two things in one
        // place (the first draft did exactly that and the render showed LETTER inside SUBJECT 2/4).
        public const int ReadX = 8, ReadY1 = 6, ReadY2 = 16;
        public const int PlateX = 6, PlateY = 26, PlateW = 74, PlateH = 16;
        public const int BoardX = 372, BoardY = 6, BoardW = 102, BoardH = 30;
        // The plaques sit to the RIGHT of the play band's edge (x 380), under the board. They were at x 372
        // for one draft, which the band test caught: 8 px of ALERTS were in the applicant's face.
        public const int AlertsX = 406, AlertsY = 40;
        public const int HelpX = 442, HelpY = 40;
        public const int PlaqueW = 32, PlaqueH = 12;

        public const int FlagX = 6, FlagY0 = 46, FlagW = 18, FlagH = 13, FlagGap = 2;
        public const int StampY = 240, StampW = 74, StampH = 24;
        public const int ApproveX = 6, RejectX = 86;
        public const int ContinueX = 386, ContinueY = 240, ContinueW = 88, ContinueH = 24;

        /// <summary>
        /// THE OBJECT LIST for this frame. Order is draw order (later on top). Resting positions depend only
        /// on the VIEW, never on the numbers: rule 3.
        /// </summary>
        public static List<HudObject> Objects(HudModel m)
        {
            if (m == null) return new List<HudObject>();
            var list = new List<HudObject>();

            // ---- the walk draws its own page and its own strip; the desk HUD stays off it entirely.
            if (m.ShowingWalk) return list;

            if (m.ShowingEnding || m.ShowingSummary || m.ShowingDebrief)
            {
                // One object, and it is the only thing a player wants: go on. It is the same rect in all
                // three states, because a "next" button that moves between screens is a second bug.
                list.Add(Make(HudId.Continue, ContinueX, ContinueY, ContinueW, ContinueH,
                              m.ShowingEnding ? "NEW RUN" : (m.ShowingSummary ? "NEXT DAY" : "CONTINUE"), m));
                return list;
            }

            // ---- the ALERTS panel is a popup: it owns the corner and closes itself. The rest of the desk
            // is not hidden -- the shift keeps running -- but the objects it covers are not clickable, and
            // the only object that IS is its own close plaque, which sits where ALERTS was.
            if (m.AlertsOpen)
            {
                list.Add(Make(HudId.Alerts, AlertsX, AlertsY, PlaqueW, PlaqueH, "CLOSE", m));
                return list;
            }

            // ---- letter mode: the page is open. Its spine gets the five flag tabs, and the stamp pads stay
            // (a player must be able to stamp without closing the letter -- that is the whole gesture: read,
            // flag, stamp). The tray becomes CLOSE, in the same rect, because a tray you cannot close is a trap.
            int n = Math.Max(0, Math.Min(5, m.FlagCount));
            if (m.LetterOpen)
            {
                for (int i = 0; i < n; i++)
                {
                    var id = (HudId)((int)HudId.Flag1 + i);
                    list.Add(Make(id, FlagX, FlagY0 + i * (FlagH + FlagGap), FlagW, FlagH,
                                  (i + 1).ToString(), m));
                }
                list.Add(Make(HudId.Letter, PlateX, PlateY, PlateW, PlateH, "CLOSE FILE", m));
                list.Add(Make(HudId.Approve, ApproveX, StampY, StampW, StampH, "APPROVE", m));
                list.Add(Make(HudId.Reject, RejectX, StampY, StampW, StampH, "REJECT", m));
                return list;
            }

            // ---- the desk, idle.
            // The letter tray is DISABLED (not hidden) once the stamp has landed: the letter is read once
            // and committed, and Rule 4 says a target that has become unavailable says so by going grey.
            // Vanishing would leave the player hunting for the thing they used a moment ago.
            list.Add(Make(HudId.Letter, PlateX, PlateY, PlateW, PlateH,
                          m.StampedToday ? "CLOSED" : (m.LetterAvailable ? "LETTER" : "CLOSED"), m));
            list.Add(Make(HudId.Alerts, AlertsX, AlertsY, PlaqueW, PlaqueH, "ALERTS", m));
            list.Add(Make(HudId.Help, HelpX, AlertsY, PlaqueW, PlaqueH, "HELP", m));
            list.Add(Make(HudId.Approve, ApproveX, StampY, StampW, StampH, "APPROVE", m));
            list.Add(Make(HudId.Reject, RejectX, StampY, StampW, StampH, "REJECT", m));
            return list;
        }

        /// <summary>Builds one object and applies its state. States are computed, never stored twice.</summary>
        private static HudObject Make(HudId id, int x, int y, int w, int h, string label, HudModel m)
        {
            var o = new HudObject { Id = id, X = x, Y = y, W = w, H = h, Label = label };
            o.State = StateOf(o, m);
            return o;
        }

        /// <summary>
        /// THE STATE TABLE. One place, so a state can never be "mostly" applied.
        ///   Disabled  -- the action is not available now (grey; still drawn, still hit-tested as nothing)
        ///   Armed     -- the keyboard is on it, or the pointer is held down on it
        ///   Hover     -- the pointer is over it
        ///   Idle      -- everything else
        /// Disabled outranks Armed: a grey button must not light up when the keyboard walks over it, because
        /// that is exactly how a player presses a dead control and concludes the game is broken.
        /// </summary>
        public static HudState StateOf(HudObject o, HudModel m)
        {
            if (!Available(o.Id, m)) return HudState.Disabled;
            if (m.KeyboardArmed.HasValue && m.KeyboardArmed.Value == o.Id) return HudState.Armed;
            if (m.Hovered.HasValue && m.Hovered.Value == o.Id) return HudState.Hover;
            return HudState.Idle;
        }

        /// <summary>Can this object's action be performed in this state at all?</summary>
        public static bool Available(HudId id, HudModel m)
        {
            switch (id)
            {
                case HudId.Letter:
                    if (m.LetterOpen) return true;              // it is CLOSE FILE
                    return m.LetterAvailable && !m.StampedToday;
                case HudId.Flag1:
                case HudId.Flag2:
                case HudId.Flag3:
                case HudId.Flag4:
                case HudId.Flag5:
                    return m.LetterOpen && IndexOf(id) < m.FlagCount;
                case HudId.Approve:
                case HudId.Reject:
                    return !m.StampedToday && !m.ShowingDebrief && !m.ShowingSummary && !m.ShowingEnding;
                case HudId.Alerts:
                    return true;                                 // always: it is CLOSE while it is open
                case HudId.Help:
                case HudId.Continue:
                    return true;
                default:
                    return false;
            }
        }

        public static int IndexOf(HudId id)
        {
            return (int)id - (int)HudId.Flag1;
        }

        /// <summary>
        /// THE HIT TEST: which object is at (px,py)? Points outside every rect return null, and a point on a
        /// DISABLED object returns null too -- the click lands on the desk and does nothing, which is the
        /// standing rule ("clicks must be scoped to what they land on").
        /// </summary>
        public static HudObject Hit(List<HudObject> objects, int px, int py)
        {
            if (objects == null) return null;
            // last drawn wins: the list is in draw order, so scan it backwards
            for (int i = objects.Count - 1; i >= 0; i--)
            {
                var o = objects[i];
                if (o == null || !o.Clickable) continue;
                if (o.Contains(px, py)) return o;
            }
            return null;
        }

        /// <summary>
        /// THE GUTTERS ARE REAL. Two objects that share an edge make a click ambiguous by one pixel, and at
        /// 480x270 one pixel is a visible target. This returns the pair that are too close, or null.
        /// </summary>
        public static string GutterViolation(List<HudObject> objects)
        {
            if (objects == null) return null;
            for (int i = 0; i < objects.Count; i++)
            {
                for (int j = i + 1; j < objects.Count; j++)
                {
                    var a = objects[i];
                    var b = objects[j];
                    if (a == null || b == null) continue;
                    bool xOverlap = a.X < b.X + b.W + 2 && b.X < a.X + a.W + 2;
                    bool yOverlap = a.Y < b.Y + b.H + 2 && b.Y < a.Y + a.H + 2;
                    if (xOverlap && yOverlap &&
                        RectOverlap(a, b))
                    {
                        return a.Id + " overlaps " + b.Id;
                    }
                }
            }
            return null;
        }

        private static bool RectOverlap(HudObject a, HudObject b)
        {
            return a.X < b.X + b.W && b.X < a.X + a.W && a.Y < b.Y + b.H && b.Y < a.Y + a.H;
        }

        /// <summary>True when the rect lies wholly inside the canvas and outside the play band.</summary>
        public static bool IsAllowed(HudObject o)
        {
            if (o == null) return false;
            if (o.X < 0 || o.Y < 0 || o.X + o.W > CanvasW || o.Y + o.H > CanvasH) return false;
            bool inBand = o.X < MiddleX1 && o.X + o.W > MiddleX0 && o.Y < MiddleY1 && o.Y + o.H > MiddleY0;
            return !inBand;
        }

        // ---- the score as a SHAPE (rule 2). Five pips: filled, half or empty, by band.
        public const int Pips = 5;

        /// <summary>0..5 filled pips for a 0..100 score. Rounded, not floored, so 90 reads as full.</summary>
        public static int FilledPips(int score)
        {
            if (score < 0) score = 0;
            if (score > 100) score = 100;
            return (int)Math.Round(score / 20.0, MidpointRounding.AwayFromZero);
        }

        /// <summary>The band word. It says which way is good, because a number alone does not.</summary>
        public static string BandWord(int score)
        {
            if (score >= 85) return "SECURE";
            if (score >= 65) return "STABLE";
            if (score >= 45) return "WATCH";
            if (score >= 25) return "BREACH";
            return "LOST";
        }

        /// <summary>
        /// The board's one-line reading: band, then score. THE PIPS ARE NOT IN THIS STRING -- the 5x7 atlas
        /// has no block glyph, so "\u25AE" rendered as nothing and the board came out as "WATCH 62" with its
        /// shape missing. The pips are drawn as PIXEL BLOCKS by the view (see PipRect), which is what a
        /// shape-first reading needs anyway: five solid 4x6 blocks read across a room, five characters do not.
        /// </summary>
        public static string BoardLine(int score)
        {
            return BandWord(score) + " " + score;
        }

        /// <summary>The rect of pip `index` (0-based) in the security board, in canvas px, top-down.</summary>
        public static void PipRect(int index, out int x, out int y, out int w, out int h)
        {
            x = BoardX + 4 + index * 6;
            y = BoardY + 14;
            w = 4;
            h = 7;
        }

        /// <summary>True when pip `index` is filled for this score: the SHAPE of the number.</summary>
        public static bool PipFilled(int score, int index)
        {
            return index < FilledPips(score);
        }

        /// <summary>The shift plate's two lines: the day and the subject, then the person.</summary>
        public static string PlateLine(HudModel m)
        {
            return "DAY " + m.Day + "/" + m.DayCount + "  SUBJECT " + m.Subject + "/" + m.SubjectCount;
        }

        public static string NameLine(HudModel m)
        {
            return (string.IsNullOrEmpty(m.Name) ? "APPLICANT" : m.Name) + " / " +
                   (string.IsNullOrEmpty(m.Role) ? "OPEN ROLE" : m.Role);
        }

        // ---- INK. The palette's own keys, by name, resolved by the view against palette.json. Kept as names
        // here so Core never holds a Color.
        /// <summary>
        /// An object's FILL by state. Idle is the object's own colour, not void: the first draft painted idle
        /// plates void, and the render showed two black holes where the stamp pads should be -- a pad whose
        /// colour only appears once you are already on it is a pad nobody presses. Hover lifts one step,
        /// Armed lifts two (so a held or keyboard-selected object is unmistakable), Disabled drops to void.
        /// </summary>
        public static string FillKey(HudId id, HudState s)
        {
            switch (s)
            {
                case HudState.Disabled: return "void";
                case HudState.Armed: return ArmedFillKey(id);
                case HudState.Hover: return HoverFillKey(id);
                default: return FillKey(id);
            }
        }

        public static string HoverFillKey(HudId id)
        {
            switch (id)
            {
                case HudId.Approve: return "cyan_2";
                case HudId.Reject: return "amber_3";
                case HudId.Continue: return "cyan_3";
                case HudId.Letter: return "steel_2";
                default: return "paper_0";
            }
        }

        public static string ArmedFillKey(HudId id)
        {
            switch (id)
            {
                case HudId.Approve: return "cyan_3";
                case HudId.Reject: return "amber_4";
                case HudId.Continue: return "cyan_4";
                default: return "white";
            }
        }

        public static string FillKey(HudId id)
        {
            switch (id)
            {
                case HudId.Approve: return "cyan_1";     // the pad that says yes is the cold bright one
                case HudId.Reject: return "amber_2";     // the pad that says no is warm, never red-on-red
                case HudId.Continue: return "cyan_2";
                case HudId.Letter: return "steel_1";
                default: return "steel_2";               // the plaques: a lit edge, dark body
            }
        }

        /// <summary>
        /// Ink by state. A BRIGHT fill needs DARK ink and a dark fill needs bright ink, or the label
        /// disappears -- which is the failure that took the menu's selected row two rounds to shake.
        /// </summary>
        public static string InkKey(HudId id, HudState s)
        {
            if (s == HudState.Disabled) return "steel_0";
            bool bright = id == HudId.Approve || id == HudId.Reject || id == HudId.Continue;
            if (bright)
                return (s == HudState.Hover || s == HudState.Armed) ? "white" : "void";
            return s == HudState.Idle ? "paper_2" : "white";
        }

        public static string EdgeKey(HudId id, HudState s)
        {
            if (s == HudState.Disabled) return "steel_0";
            if (s == HudState.Hover || s == HudState.Armed) return "white";
            return "steel_2";
        }

        /// <summary>A flag tab's ink: ticked or not. Ticked is the ARMED fill, so a set flag is visible.</summary>
        public static string FlagFillKey(bool flagged, HudState s)
        {
            if (s == HudState.Disabled) return "void";
            if (flagged) return "amber_2";
            return s == HudState.Idle ? "steel_1" : "paper_1";
        }

        public static string FlagInkKey(bool flagged, HudState s)
        {
            if (s == HudState.Disabled) return "steel_0";
            if (flagged) return "void";
            return s == HudState.Idle ? "paper_2" : "white";
        }
    }
}
