// HR//0D -- MenuLayout: where everything on the front door goes. Pure data, no UnityEngine.
// Assembly: HRZeroDay.Core  (pure C#, no UnityEngine)
//
// THE FRAME (concept, player 2026-11-28; ADR-017/018)
//   The front door is the ROOM, not a card over it: a 480x270 composition of
//
//       background (night city)     y  0 .. 205
//       desk band (coffee, files)   y 206 .. 269
//       big monitor   x 14..273,  y 30..199      glass x 26..261, y 42..185  (236x144)
//       server rack   x 436..469, y 109..200
//
//   Everything in this file is stated in that frame, in canvas px, y DOWN from the top-left -- the same
//   coordinates tools/art/make_menu_composite.py renders, so the twin and the game cannot drift. The
//   glass rect here is checked against desk_layout.json's menuFrame.glass by tests/CoreTests.
//
// WHY THE TITLE IS BIG AND THE ROWS ARE SMALL
//   A 5x7 glyph is 5 px wide, so "HUMAN RESOURCES" at 1x is 103 px: a caption. The concept's title is a
//   third of the screen wide, which is 3x -- so the title row is drawn by repeating each glyph pixel into
//   a 3x3 cell (MenuGlyph.InkAt takes a scale, and the raster is asked for a 15x21 cell). Same atlas,
//   same palette, no new font: it is the same trick the concept's lettering implies.
using System;
using System.Collections.Generic;

namespace HRZeroDay.Core.Rules
{
    /// <summary>The whole front door frame, in canvas px, y down. One place, so nothing is guessed twice.</summary>
    public static class MenuLayout
    {
        public const int W = 480;
        public const int H = 270;

        // ---- the monitor the menu lives in: desk/menu_monitor.png, 300x220, glass 272x170
        public const int MonitorX = 4, MonitorY = 10;
        public const int GlassX = 18, GlassY = 24, GlassW = 312, GlassH = 172;

        // ---- the rest of the concept's frame
        public const int WallX = 326, WallY = 40;         // desk/wall_monitors.png, 150x104
        public const int LabelX = 350, LabelY = 150;      // desk/wall_labels.png, 150x30 (right of the bezel)
        public const int ServerX = 430, ServerY = 92;    // desk/server_rack.png, 48x160
        public const int DeskY = 206;                     // desk/desk_foreground.png, 480x64
        public const int WidgetX = 412, WidgetY = 6;      // the clearance/ID card, top right

        // ---- type: 3x title, 1x rows. The concept's title is a third of the screen wide.
        public const int TitleScale = 3;
        public const int TitleTop = GlassY + 6, SubtitleTop = GlassY + 30, TaglineTop = GlassY + 56;

        // ---- the option pills: filled, rounded, stacked under the title
        public const int PillX = 108, PillW = 156, PillH = 15, PillStep = 17;
        public static int PillTop { get { return GlassY + 70; } }
        public static int FooterTop { get { return GlassY + GlassH - 14; } }

        // The atlas is 5x7 with a 2 px letter gap and a 3 px line gap (art/kit/fonts/monitor_font_5x7.json).
        // Stated here rather than referenced, because this assembly has no UnityEngine; the Unity-side
        // compile check asserts these against MonitorScreen's own numbers.
        public const int GlyphW = 5, GlyphH = 7, LetterGap = 2, LineGap = 3;

        public static int TextCols { get { return (GlassW - 16) / (GlyphW + LetterGap); } }
        public static int TextRows { get { return (GlassH + LineGap) / (GlyphH + LineGap); } }

        /// <summary>
        /// The room at night: every surface drops one palette step, while anything that emits light stays
        /// lit. A checker dim was tried in v113 and read as noise at 1x; a step map is smooth, stays inside
        /// the palette, and keeps the window, the wall screens and the server LEDs glowing.
        /// </summary>
        public static readonly string[,] Darken =
        {
            { "panel2", "panel" }, { "panel", "void" }, { "line", "panel" }, { "linehi", "line" },
            { "muted", "line" }, { "textdim", "muted" }, { "text", "textdim" }, { "white", "textdim" },
            { "paper", "paperdim" }, { "paperdim", "ink" }, { "ink", "void" },
            { "signal", "signaldim" }, { "ok", "signaldim" }, { "synth", "synthdim" }
        };

        /// <summary>A pill: the concept's filled row behind the selected option.</summary>
        public struct Pill
        {
            public int X, Y, W, H;
            public string Fill;
            public string TextInk;
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

    /// <summary>
    /// The front door's type, laid out for the glass. The labels are the ones the concept uses, because the
    /// screen the player asked for is the screen they should get.
    /// </summary>
    public static class MenuScreenText
    {
        public const string Title = "HUMAN RESOURCES";
        public const string Subtitle = "ZERO DAY";
        public const string Tagline = "NOT EVERY APPLICANT IS A PERSON";

        /// <summary>Root rows, in concept order. CONTINUE only when there is something to continue.</summary>
        public static List<MenuRow> Root(bool hasSave, bool runInProgress, int index)
        {
            var rows = new List<MenuRow>();
            string first = hasSave ? (runInProgress ? "CONTINUE RUN" : "CONTINUE") : "START";
            rows.Add(Row(MenuAction.Continue, first, index == rows.Count));
            rows.Add(Row(MenuAction.NewRun, "NEW SHIFT", index == rows.Count));
            rows.Add(Row(MenuAction.OpenManual, "FIELD MANUAL", index == rows.Count));
            rows.Add(Row(MenuAction.OpenSettings, "SETTINGS", index == rows.Count));
            rows.Add(Row(MenuAction.OpenCredits, "CREDITS", index == rows.Count));
            return rows;
        }

        public static List<MenuRow> Settings(bool muted, float room, float sfx, int index)
        {
            var rows = new List<MenuRow>();
            rows.Add(Row(MenuAction.None, Volume("ROOM", room), index == rows.Count));
            rows.Add(Row(MenuAction.None, Volume("SOUND", sfx), index == rows.Count));
            rows.Add(Row(MenuAction.None, muted ? "SOUND: OFF" : "SOUND: ON", index == rows.Count));
            rows.Add(Row(MenuAction.Back, "BACK", index == rows.Count));
            return rows;
        }

        public static List<MenuRow> Consent(bool granted, bool decided, int index)
        {
            var rows = new List<MenuRow>();
            rows.Add(Row(MenuAction.None, granted ? "[X] SHARE DATA" : "[ ] SHARE DATA", index == rows.Count));
            rows.Add(Row(MenuAction.Back, "BACK", index == rows.Count));
            return rows;
        }

        /// <summary>
        /// The manual is the controls page, and the controls are the desk's own: these are the keys the
        /// hint bar already uses, written down where a player can find them. Info rows are not selectable
        /// (Enabled false), so the cursor lands on BACK and nothing here can be triggered by mistake.
        /// </summary>
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

        /// <summary>"ROOM ####--- 80%" in one row: the concept has no room for a separate bar column.</summary>
        public static string Volume(string what, float level)
        {
            string bar = MenuRules.Bar(level);
            // eight cells keeps the whole row inside the pill at 1x
            return what + " " + bar.Substring(2) + " " + MenuRules.Percent(level);
        }

        private static MenuRow Row(MenuAction action, string label, bool selected)
        {
            return new MenuRow(action, label, selected ? "signal" : "text", selected);
        }
    }
}
