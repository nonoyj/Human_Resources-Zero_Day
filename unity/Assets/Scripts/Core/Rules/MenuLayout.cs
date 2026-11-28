// HR//0D -- MenuLayout: the front door's room, frame and light. Pure data, no UnityEngine.
// Assembly: HRZeroDay.Core  (pure C#, no UnityEngine)
//
// THE ROOM (player brief 2026-11-28, ADR-019; art in art/kit/menu/, drawn by tools/art/make_menu_room.py)
//
//        furthest wall: window (city, moon)          table of 6 white screens + 2 under it
//        right corner:  database server
//        left, in front: an old CRT with the title and the menu inside its glass
//        desk under it: coffee with smoke, keyboard, mouse, pile of application forms
//        floor: tiles with black grout
//
//   Coordinates are canvas px, 480x270, y DOWN from the top-left -- the same numbers the twin renderer
//   (tools/art/make_menu_composite.py) draws, so the preview cannot drift from the game.
//
// THE LIGHT (docs/16-light-study.md, and the research behind it)
//   1. ONE dominant source: the CRT. Everything else is subordinate.
//   2. falloff is BANDS with dithered seams -- never a global checker (that was the v113 mistake).
//   3. bright cores stay solid; dither lives on boundaries.
//   4. shadows lift toward blue-teal instead of crushing to black, so the room stays legible.
//   5. warmth is rationed: the coffee's steam and one amber LED are the only warm pixels in the frame.
using System;
using System.Collections.Generic;

namespace HRZeroDay.Core.Rules
{
    public static class MenuLayout
    {
        public const int W = 480, H = 270;

        // ---- the room's own furniture, in draw order (back to front)
        public const int WindowX = 368, WindowY = 34;     // menu/window.png         82x108, hung on the wall
        public const int StackX = 260, StackY = 102;      // menu/monitor_stack.png 100x100, on the floor
        public const int ServerX = 454, ServerY = 90;     // menu/server.png         26x112, on the floor

        // ---- the CRT the menu lives in: menu/crt.png 240x186, glass 208x136
        public const int CrtX = 14, CrtY = 30;            // stands ON the desk: CrtY + CrtH == DeskSurfaceY
        public const int GlassX = 28;
        public const int GlassY = CrtY + 14;              // always relative to the case
        public const int GlassW = 212, GlassH = 126;

        // ---- the desk band and what sits on it
        public const int DeskY = 184;                     // menu/desk.png 480x64 (18 px of steam headroom)
        public const int DeskSurfaceY = 202;              // DeskY + the sprite's 18 px of steam headroom

        // ---- sprite sizes, mirrored from tools/art/make_menu_room.py. The tests read these.
        public const int CrtW = 240, CrtH = 172;
        public const int WindowW = 82, WindowH = 108;
        public const int StackW = 100, StackH = 100;
        public const int ServerW = 26, ServerH = 112;
        public const int FloorY = 196;

        // ---- type inside the glass: 3x title, 1x rows
        public const int TitleScale = 2;   // 206 px of the 228 px glass: a title, not a caption
        public const int TitleTop = GlassY + 8;
        public const int SubtitleTop = GlassY + 24;
        public const int TaglineTop = GlassY + 40;

        // ---- the option rows
        public const int PillX = GlassX + 52;
        public const int PillW = 122;
        public const int PillH = 11;
        public const int PillStep = 13;
        public static int PillTop { get { return GlassY + 52; } }
        public static int FooterTop { get { return GlassY + GlassH - 9; } }

        // The atlas is 5x7, 2 px letter gap, 3 px line gap (art/kit/fonts/monitor_font_5x7.json).
        public const int GlyphW = 5, GlyphH = 7, LetterGap = 2, LineGap = 3;

        public static int TextCols { get { return (GlassW - 12) / (GlyphW + LetterGap); } }
        public static int TextRows { get { return (GlassH + LineGap) / (GlyphH + LineGap); } }

        // ------------------------------------------------------------------ light
        //
        // HOW THE ROOM IS LIT (docs/16-light-study.md)
        //
        // A pixel-art light is not a coloured fog over the frame. It raises the VALUE of the surfaces it
        // falls on, in a small number of hard steps, and the step boundaries are the only place dithering
        // belongs. So every palette colour is given a position on a ramp, and a light adds steps to that
        // position. The first version of this file replaced pixels with a light colour instead, which is
        // why the room came out as cyan blobs sitting on top of the art.
        //
        // The ramp runs: void -> panel -> panel2 -> line -> linehi -> (muted). Nothing past `linehi` is lit
        // by ambience: text, white screens and LEDs are already emitting and must not be washed out.

        /// <summary>
        /// Position of each surface colour on the light ramp. Anything absent is not liftable. Note the top
        /// of the ramp is `line`, a cool grey-blue -- NOT a cyan. A lit wall is a brighter wall; only the
        /// 30 px around the glass takes actual cyan (see CyanNear), which is what stops the room turning
        /// turquoise. v115's first pass made every lit surface signaldim and the room looked like a fish tank.
        /// </summary>
        public static readonly string[] Ramp = { "void", "panel", "panel2", "line" };

        /// <summary>Inside this distance from the glass, a lit surface takes the cyan ramp instead.</summary>
        public const int CyanNear = 14;

        /// <summary>How far the monitor's cyan fade reaches out from its case, in px. 84 px is about a
        /// third of the frame width: enough to light the desk under the screen, not enough to reach the
        /// window across the room.</summary>
        public const int FadeReach = 84;

        /// <summary>
        /// The most steps ANY combination of lights may add to a surface. One. A room lit by two lamps is
        /// not twice as bright as a room lit by one -- and in pixel art a second step is the difference
        /// between 'lit' and 'washed out'. The first two passes of v115 lifted by 2-3 and turned the frame
        /// into a gradient; this single clamp is what makes the light read as light.
        /// </summary>
        public const int MaxLift = 1;

        public struct LightSource
        {
            public int X, Y;         // centre, canvas px
            public int Rx, Ry;       // radius of the outermost reach
            public double Falloff;   // 1.0 linear, >1 tighter, <1 broader
            public int Strength;     // ramp steps added at the core (1..MaxLift)

            public LightSource(int x, int y, int rx, int ry, double falloff, int strength)
            {
                X = x;
                Y = y;
                Rx = rx;
                Ry = ry;
                Falloff = falloff;
                Strength = strength;
            }
        }

        /// <summary>The CRT. The key light: broad, cyan, and the reason the desk is the brightest place.</summary>
        public static LightSource MonitorLight()
        {
            return new LightSource(GlassX + GlassW / 2, GlassY + GlassH / 2, 232, 176, 1.55, 2);
        }

        /// <summary>The window. Cold and high: it lifts the back wall, never the desk.</summary>
        public static LightSource WindowLight()
        {
            return new LightSource(WindowX + 41, WindowY + 54, 92, 106, 1.90, 1);
        }

        /// <summary>The table of white screens. Many small sources: shallow reach, strong near the faces.</summary>
        public static LightSource StackLight()
        {
            return new LightSource(StackX + 50, StackY + 26, 96, 88, 2.10, 1);
        }

        public static LightSource[] Lights()
        {
            return new[] { StackLight(), WindowLight(), MonitorLight() };
        }

        /// <summary>
        /// The tint a lit surface takes. Cyan light on a warm surface reads as a cool wash, so the lift is
        /// expressed as a swap into the CYAN RAMP (glow_teal -> signaldim -> line) rather than the neutral
        /// one. Neutral surfaces (wall, floor, desk) use the neutral ramp, which is what keeps the room grey
        /// instead of turquoise.
        /// </summary>
        public static readonly string[,] CyanRamp =
        {
            { "void", "glow_teal" }, { "panel", "glow_teal" }, { "panel2", "signaldim" }, { "line", "signal" }
        };

        // ------------------------------------------------------------------ rows        // ------------------------------------------------------------------ rows
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
            Action = action;
            Label = label;
            Ink = ink;
            Selected = selected;
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
            return new MenuRow(action, label, selected ? "signal" : "text", selected);
        }
    }
}
