// HR//0D -- MenuLayout: the front door's room, frame and light. Pure data, no UnityEngine.
// Assembly: HRZeroDay.Core  (pure C#, no UnityEngine)
//
// THE ROOM (v118, ADR-021) -- the player's reference still, translated into a 480x270 frame:
//
//     cold key, high left            the monitor, larger and nearer        warm practical, right
//     +--------------------+   +------------------------------+   +----------------+
//     |  window  88x122    |   |   the tube the menu lives in  |   | lamp    server  |
//     |  at (8, 10)        |   |   260x186 at (104, 10)        |   | (396,92) (452,80)
//     +--------------------+   +------------------------------+   +----------------+
//          six small screens on a rail at (372, 96), silhouettes against the wall
//     ------------- desk 480x120 at (0, 150): cold pool under the tube, warm pool under the lamp -------
//
//   Every number here is canvas px, 480x270, y DOWN from the top-left -- the same numbers the twin
//   renderer (tools/art/make_menu_composite.py) and the sprite generator draw, so the preview cannot
//   drift from the game.
//
// THE LIGHT (docs/16-light-study.md §10, docs/20-v118-cinematic.md)
//   Six rules, and each one is a line of code somewhere:
//     1. TWO sources with opposite temperature: a COLD key (the tube) and a WARM practical (the lamp).
//        A frame lit by one temperature is a monochrome frame, however pretty the colour is.
//     2. The materials own their from-to ramps (menu_palette.py). Light chooses a STEP; it never
//        repaints a surface in the light's own colour.
//     3. Ambient light adds ONE step (MaxLift). Emitters -- the screens, the funnel, LEDs -- are skipped.
//     4. Falloff is dithered along the distance, not banded: at 480x270 a hard band edge draws a ring.
//     5. Shadows are LIFTED, not crushed: the darkest surface is `void`, never pure black, and the
//        vignette only touches the outer corners.
//     6. Emissive things BREATHE, and nothing else moves except the dust and the smoke.
using System;
using System.Collections.Generic;

namespace HRZeroDay.Core.Rules
{
    public static class MenuLayout
    {
        public const int W = 480, H = 270;

        // ---- the room's furniture, in draw order (back to front)
        public const int WindowX = 8, WindowY = 10;        // menu/window.png        88x122
        public const int StackX = 372, StackY = 96;        // menu/monitor_stack.png 76x66, on a wall rail
        public const int LampX = 424, LampY = 62;          // menu/lamp.png          56x124 -- the warm key
        public const int ServerX = 452, ServerY = 80;      // menu/server.png        26x112, three-quarter view

        // ---- the monitor the menu lives in: menu/crt.png 260x186, glass 228x152 at (26,12)
        public const int CrtX = 104, CrtY = 10;
        public const int GlassX = CrtX + 26;
        public const int GlassY = CrtY + 12;
        public const int GlassW = 228, GlassH = 152;

        /// <summary>
        /// A CRT's tube is a rounded box: every corner is cut and the picture is smaller than the tube.
        /// Text is measured against THIS inset, not against the flat glass rect (v116 shipped a tagline
        /// that sat on the curve).
        /// </summary>
        public const int TubeInset = 4;

        // ---- the desk: a 120 px slab whose top surface starts 26 px into the sprite
        public const int DeskY = 150;                      // menu/desk.png 480x120
        public const int DeskTopY = DeskY + 26;            // the slab's far edge
        public const int DeskSurfaceY = DeskY + 26;        // its top surface, where the props stand
        public const int DeskEdgeY = DeskY + 26 + 38;      // its front edge

        // ---- sprite sizes, mirrored from tools/art/make_menu_room.py. The tests read these.
        public const int CrtW = 260, CrtH = 186;
        public const int WindowW = 88, WindowH = 160;
        public const int StackW = 76, StackH = 66;
        public const int ServerW = 26, ServerH = 112;
        public const int LampW = 56, LampH = 132;
        public const int FloorY = 196;

        // ---- type inside the glass: 2x title with TIGHT tracking, 2x ZERO DAY close under it, 1x rows
        public const int TitleScale = 2;
        /// <summary>
        /// THE PLAYER'S TYPE CHANGE (v118): "the full title thicker and letters closer together".
        /// The atlas is a 5x7 bitmap; the only way to make it THICKER is to bleed each glyph 1 px to the
        /// right, and the only way to tighten it is a 1 px cell gap instead of 2. Both together give a
        /// 2x title that is 15 glyphs wide in 162 px instead of 178 -- and it weighs more, which is what
        /// "thicker" means at this size.
        /// </summary>
        public const int TitleBleed = 1;
        public const int TitleGap = 1;             // cell gap for the title only (rows keep LetterGap)
        public const int TitleTop = GlassY + 9;
        public const int SubtitleTop = GlassY + 26;    // "closer to the title": 3 px under the 2x cap
        public const int TaglineTop = GlassY + 52;
        public const int RuleTop = GlassY + 66;

        // ---- the option rows: 5 rows, 1 px of air, anchored under the rule
        public const int PillX = GlassX + 40;
        public const int PillW = 148;
        public const int PillH = 13;
        public const int PillStep = 13;
        public const int PillTop = GlassY + 72;
        public static int FooterTop { get { return GlassY + GlassH - 12; } }

        // The atlas is 5x7, 2 px letter gap, 3 px line gap (art/kit/fonts/monitor_font_5x7.json).
        public const int GlyphW = 5, GlyphH = 7, LetterGap = 2, LineGap = 3;

        /// <summary>The rect text is allowed to occupy: the glass, pulled in by the tube's corner cut.</summary>
        public static int TextX { get { return GlassX + TubeInset; } }
        public static int TextY { get { return GlassY + TubeInset; } }
        public static int TextW { get { return GlassW - 2 * TubeInset; } }
        public static int TextH { get { return GlassH - 2 * TubeInset; } }

        public static int TextCols { get { return TextW / (GlyphW + LetterGap); } }
        public static int TextRows { get { return (TextH + LineGap) / (GlyphH + LineGap); } }

        /// <summary>
        /// The width of a line drawn at `scale` with the title's tracking and bleed. The view and the
        /// tests measure with THIS function, so a change to the tracking cannot leave the gate behind.
        /// </summary>
        public static int LineWidth(string text, int scale)
        {
            if (string.IsNullOrEmpty(text)) return 0;
            int cell = (GlyphW + TitleGap) * scale;
            return text.Length * cell - TitleGap * scale + TitleBleed;
        }

        // ------------------------------------------------------------------ light
        //
        // HOW THE ROOM IS LIT (docs/16-light-study.md §10)
        //
        // Each material has a ramp of 3-5 steps in menu_palette.py. A light raises a surface ONE step
        // along its own ramp; it never replaces the surface with the light's colour. That is the whole
        // difference between a lit room and a colour filter, and it is why v116 (which swapped pixels
        // for `glow_teal`) looked like an aquarium.

        /// <summary>Material ramps, in order. A light only ever moves a surface from one entry to the next.</summary>
        public static readonly string[,] Ramps =
        {
            { "wall_0", "wall_1" },
            { "wall_1", "wall_2" },
            { "floor_0", "floor_1" },
            { "floor_1", "floor_2" },
            { "wood_0", "wood_1" },
            { "wood_1", "wood_2" },
            { "wood_2", "wood_3" },
            { "wood_3", "wood_4" },
            { "steel_0", "steel_1" },
            { "steel_1", "steel_2" },
            { "key_0", "key_1" },
            { "key_1", "key_2" },
            { "mouse_0", "mouse_1" },
            { "mouse_1", "mouse_2" },
            { "paper_0", "paper_1" },
            { "paper_1", "paper_2" },
            { "cup_0", "cup_1" },
            { "cup_1", "cup_2" }
        };

        /// <summary>
        /// One step, no more. A room lit by two lamps is not twice as bright as a room lit by one, and in
        /// pixel art a second step is the difference between "lit" and "washed out".
        /// </summary>
        public const int MaxLift = 1;

        /// <summary>
        /// How far the tube's cold light reaches. It reaches DOWN much further than sideways, because a
        /// screen lights the desk it stands on and only spills a little past its own edges. An equal
        /// reach on every side drew a visible dome over the wall (v115).
        /// </summary>
        public const int FadeReach = 62;      // sideways
        public const int FadeDrop = 96;       // down, onto the desk and its front face

        /// <summary>Cold light: the tube's own, measured from the case. Strength 1 step, tight falloff.</summary>
        public static LightSource MonitorLight()
        {
            return new LightSource(GlassX + GlassW / 2, GlassY + GlassH / 2, 232, 196, 1.70, 1);
        }

        /// <summary>Cold fill: the window, high and left. It lifts the back wall and never the desk.</summary>
        public static LightSource WindowLight()
        {
            return new LightSource(WindowX + 44, WindowY + 60, 116, 128, 1.95, 1);
        }

        /// <summary>
        /// WARM light: the lamp. Its pool is small and hard, it lands on the desk's right end and the
        /// wall behind it, and it is the only warm light in the frame (rule 1).
        /// </summary>
        public static LightSource LampLight()
        {
            return new LightSource(LampX + 28, LampY + 70, 108, 122, 1.45, 1);
        }

        public static LightSource[] Lights()
        {
            return new[] { WindowLight(), LampLight(), MonitorLight() };
        }

        /// <summary>
        /// Dust: a thin film of motes that FALL. The player asked for exactly this -- v116's dust was a
        /// static vignette, which reads as dirt on the lens, not as air in a room.
        ///
        /// Counts are small on purpose: 30 motes over 480x270 is air; 300 is a snowstorm.
        /// </summary>
        public const int DustCount = 30;
        public const double DustFallSpeed = 5.5;      // px per second, downward
        public const double DustDriftSpeed = 1.7;     // px per second, leftward (the room's draught)
        public const int DustBrightest = 2;           // the mote's brightest step (a 1-2 px dot)

        /// <summary>
        /// A Wang-mix hash for a mote's properties. (x*73856093 ^ y*19349663) &amp; 15 is periodic at 16 px
        /// and draws a screen-door mesh; this one is uncorrelated between neighbours.
        /// </summary>
        public static uint DustHash(int x, int y)
        {
            uint h = (uint)(x * 374761393 + y * 668265263);
            h = (h ^ (h >> 13)) * 1274126177u;
            h ^= h >> 16;
            return h;
        }

        public static readonly int[] Bayer4 =
        {
             0,  8,  2, 10,
            12,  4, 14,  6,
             3, 11,  1,  9,
            15,  7, 13,  5
        };

        public struct LightSource
        {
            public int X, Y;         // centre, canvas px
            public int Rx, Ry;       // radius of the outermost reach
            public double Falloff;   // 1.0 linear, >1 tighter, <1 broader
            public int Strength;     // ramp steps added at the core (1..MaxLift)

            public LightSource(int x, int y, int rx, int ry, double falloff, int strength)
            {
                X = x; Y = y; Rx = rx; Ry = ry; Falloff = falloff; Strength = strength;
            }
        }

        // ------------------------------------------------------------------ the six wall screens
        /// <summary>
        /// The rail of screens behind the monitor. Each screen runs its own cycle, offset so the six never
        /// blink together; one of them is the BREACH screen, which floods red at random and is the only
        /// place the AI's colour appears in the room.
        /// </summary>
        public static readonly int[] StackScreenX = { 375, 400, 425 };
        public static readonly int[] StackScreenY = { 98, 120 };
        public const int StackScreenW = 22, StackScreenH = 18;

        /// <summary>The cybersecurity readouts the screens cycle through. Two lines, 5x7, upper case.</summary>
        public static readonly string[][] StackReadouts =
        {
            new[] { "NODE 07/12", "LIVE 98.2%" },
            new[] { "AUTH 0X4F2", "HANDSHAKE" },
            new[] { "ICE BREACH", "TRACE FAILED" },
            new[] { "PORT 22", "MONITOR" },
            new[] { "SYS.ROOT", "?? INTRUDER" },
            new[] { "KEY ROTATE", "DONE" }
        };

        /// <summary>
        /// The breach beat: a random screen, every 14 to 26 seconds, for 1.4 s. Rationed on purpose --
        /// an alarm that fires constantly stops being an alarm (docs/17 §4, motion_manifest.py).
        /// </summary>
        public const double BreachMin = 14.0, BreachMax = 26.0, BreachHold = 1.4;

        /// <summary>How fast a screen's readout changes. Slow: these are machines, not a slot machine.</summary>
        public const double StackCycle = 2.6;

        // ------------------------------------------------------------------ rows
        public struct Pill
        {
            public int X, Y, W, H;
            public string Fill, TextInk;
        }

        public static Pill Row(int index, string fill, string ink)
        {
            return new Pill
            {
                X = PillX,
                Y = PillTop + index * PillStep,
                W = PillW,
                H = PillH,
                Fill = fill,
                TextInk = ink
            };
        }
    }

    /// <summary>One row of the front door: a label, its ink, and what it is.</summary>
    public sealed class MenuRow
    {
        public MenuAction Action;
        public string Label;
        public string Ink;
        public bool Selected;

        public MenuRow(MenuAction action, string label, string ink, bool selected)
        {
            Action = action; Label = label; Ink = ink; Selected = selected;
        }
    }

    /// <summary>The front door's words: the concept's rows, and nothing that is not a row.</summary>
    public static class MenuScreenText
    {
        public const string Title = "HUMAN RESOURCES";
        public const string Subtitle = "ZERO DAY";
        public const string Tagline = "NOT EVERY APPLICANT IS A PERSON";

        public static List<MenuRow> Root(bool hasSave, bool runInProgress, int index)
        {
            var rows = new List<MenuRow>();
            rows.Add(Row(MenuAction.Continue,
                hasSave ? (runInProgress ? "CONTINUE RUN" : "CONTINUE") : "START", index == 0));
            rows.Add(Row(MenuAction.NewRun, "NEW SHIFT", index == 1));
            rows.Add(Row(MenuAction.OpenManual, "FIELD MANUAL", index == 2));
            rows.Add(Row(MenuAction.OpenSettings, "SETTINGS", index == 3));
            rows.Add(Row(MenuAction.OpenCredits, "CREDITS", index == 4));
            return rows;
        }

        public static List<MenuRow> Settings(bool muted, float room, float sfx, int index)
        {
            var rows = new List<MenuRow>();
            rows.Add(Row(MenuAction.None, Volume("ROOM", room), index == 0));
            rows.Add(Row(MenuAction.None, Volume("SOUND", sfx), index == 1));
            rows.Add(Row(MenuAction.None, muted ? "SOUND: OFF" : "SOUND: ON", index == 2));
            rows.Add(Row(MenuAction.OpenConsent, "DATA & CONSENT", index == 3));
            rows.Add(Row(MenuAction.Back, "BACK", index == 4));
            return rows;
        }

        public static List<MenuRow> Consent(bool granted, bool decided, int index)
        {
            var rows = new List<MenuRow>();
            rows.Add(Row(MenuAction.None, granted ? "[X] SHARE DATA" : "[ ] SHARE DATA", index == 0));
            rows.Add(Row(MenuAction.Back, "BACK", index == 1));
            return rows;
        }

        /// <summary>The controls page: the desk's own keys, written down where a player can find them.</summary>
        public static List<MenuItem> ManualRows()
        {
            return new List<MenuItem>
            {
                new MenuItem(MenuAction.None, "CLICK A PERSON  INSPECT", false),
                new MenuItem(MenuAction.None, "L  OPEN THE LETTER", false),
                new MenuItem(MenuAction.None, "A  APPROVE", false),
                new MenuItem(MenuAction.None, "S  REJECT", false),
                new MenuItem(MenuAction.None, "SPACE  NEXT APPLICANT", false),
                new MenuItem(MenuAction.None, "H  HELP    ESC  MENU", false),
                new MenuItem(MenuAction.Back, "BACK", true)
            };
        }

        public static string Volume(string what, float level)
        {
            return what + " " + MenuRules.Bar(level).Substring(2) + " " + MenuRules.Percent(level);
        }

        private static MenuRow Row(MenuAction action, string label, bool selected)
        {
            return new MenuRow(action, label, selected ? "cyan_2" : "paper_2", selected);
        }
    }
}
