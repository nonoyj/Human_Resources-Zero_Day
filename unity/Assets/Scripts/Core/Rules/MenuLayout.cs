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
        // v119: the cluster is BIGGER and it stands on a TABLE WITH LEGS. The sprite now carries the table
        // top AND its legs, down to the floor plane behind the desk (StackFootY), so the whole thing is
        // attached to the ground instead of hanging on the wall -- that was the player's "the table floats
        // together with the monitor". It also had to move LEFT: a 116 px wide sprite at 372 would have run
        // into the server.
        // v119.1: 366, not 330 -- the cluster must start RIGHT of the monitor's case (which ends at 364) or
        // the case's hard edge slices two screens in half. See the note in make_menu_room.py::monitor_stack.
        public const int StackX = 366, StackY = 44;        // menu/monitor_stack.png 70x108, table on the floor
        public const int LampX = 422, LampY = 62;          // menu/lamp.png          56x132 -- the warm key
                                                           // 422, not 424: the pole is at sprite
                                                           // x+27 and the server starts at 452
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
        public const int StackW = 70, StackH = 108;

        /// <summary>
        /// The six tubes of the cluster, in canvas px: (x, y, w, h). They are NOT a grid -- the top row is
        /// 30/32/30 wide, the bottom row 28/30/34, the rows interlock by 2-3 px and the whole block sags to
        /// the right, because that is what "disarranged while sticking to each other" looks like. The art
        /// (make_menu_room.py) and MenuView both index this table, so a click on a screen and the screen
        /// that lights up can never disagree.
        /// </summary>
        /// <summary>
        /// The GLOW LAYER (v119). Each entry is a sprite and where it sits on the canvas. These are blitted
        /// AFTER the light pass and AFTER the vignette: a halo must not be darkened by the corner it is
        /// glowing through, and keeping the layer separate is what lets one source be tuned alone. Every
        /// glow sprite is transparent everywhere it does not emit, so the blit is a straight overlay.
        /// </summary>
        public static readonly string[] GlowSprites =
        {
            // ONE sprite, the size of the canvas: it is a distance-transform band map over the room's real
            // emitters (the glazing, the lit tubes, the lamp's mouth), so it hugs whatever shapes the art
            // gives it. Splitting it per-object would mean three overlapping band maps fighting at their
            // intersections; one map has one answer everywhere.
            "menu/glow",
        };

        /// <summary>The canvas position of each GlowSprites entry.</summary>
        public static readonly int[][] GlowPos =
        {
            new[] { 0, 0 },        // 480x270
        };

        /// <summary>
        /// How much light a halo pixel carries, WEAKEST FIRST. A glow sprite's colour is not a colour to
        /// paint with, it is a STRENGTH: the index in this ramp says how many steps to lift the surface
        /// under it (0 lifts nothing, 4 is the hot core and is painted as-is). This is why the glow layer
        /// can never put a cyan blob on the wall -- it can only walk a material up its own ramp.
        /// </summary>
        public static readonly string[] GlowRamp =
        {
            "teal_2", "cyan_0", "cyan_1",                                    // 0-2, cold: lift 1, then 2
            "amber_1", "amber_2", "amber_3",                                 // 3-5, warm: the lamp's steps
            "cyan_2",                                                        // 6, emissive: a specular in the
                                                                             //    highlight pass, painted as-is
            "amber_4",
        };

        /// <summary>The index of a glow colour in GlowRamp, or -1 when the pixel is not part of the ramp.</summary>
        public static int GlowStrength(string paletteKey)
        {
            for (int i = 0; i < GlowRamp.Length; i++) if (GlowRamp[i] == paletteKey) return i;
            return -1;
        }

        public const int GlowHotLen = 3;      // the length of each half of GlowRamp
        public const int GlowEmissive = 6;    // the first index that is PAINTED rather than lifting
        public const int GlowTintDiv = 6;     // leftover strength -> dithered tint: 1/8 of the pixels per step

        public static readonly int[][] StackCells = new int[][]
        {
            new[] { 367, 44, 32, 24 }, new[] { 399, 46, 34, 23 },
            new[] { 366, 69, 33, 26 }, new[] { 398, 68, 35, 27 },
            new[] { 368, 95, 31, 24 }, new[] { 400, 96, 33, 23 },
        };

        public const int StackTableTop = 120;              // y of the table's slab (its front edge), canvas px
        public const int StackFootY = 152;                 // the feet meet the floor plane here, behind the desk
                                                           // slab: the desk is drawn after the stack, so it
                                                           // interrupts the legs exactly at its far edge
        public const int ServerW = 26, ServerH = 112;
        public const int LampW = 56, LampH = 132;
        public const int LampPoleX = LampX + 27, LampPoleW = 2;    // see MenuTests: the POLE must clear the
                                                                   // server; the shade's overhang is deliberate
                                                                   // and hangs over the wall behind it
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
        public const int PillStep = 14;            // v119.4: 14, not 13 -- one pixel of air between rows
        public const int PillTop = GlassY + 70;    // v119.4: 70, not 72 -- the block sits a row higher so
                                                   // the footer has room below it instead of sitting on the
                                                   // tube's bottom lip (BUG-085)
        public static int FooterTop { get { return GlassY + GlassH - 12; } }

        // ---- THE KEYBOARD'S TEXT WINDOW (v119.5). Off the tube, on the desk's front face, hanging under
        // the keyboard the player clicked (desk.png puts the keys at (300,186) 84x20). 192 px wide holds
        // 27 glyph cells at the atlas's 7 px pitch, which is what the line table in MenuView is sized to.
        // X sits so the box is centred under the keys: keyboard (300..384) centre 342, box 192 wide -> 246.
        public const int TypeWindowX = 246, TypeWindowY = 206;
        public const int TypeWindowW = 192, TypeWindowH = 34;

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
            // v119: the LIT steps. The wall and the floor needed one more entry each or the glow had no
            // value left to lift into -- see menu_palette.py's v119 note.
            { "wall_2", "wall_3" },
            { "floor_0", "floor_1" },
            { "floor_1", "floor_2" },
            { "floor_2", "floor_3" },
            { "floor_3", "floor_4" },
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
        /// WARM light: the lamp. It is the only warm source in the frame (rule 1).
        ///
        /// THE LAMP'S POOL WAS UPSIDE DOWN (v120, the player's catch). The light used to sit at
        /// (LampX+28, LampY+70) -- 70 px down the sprite, which is the lamp's FOOT -- with a 108x122 radius.
        /// That put the brightest part of the pool on the WALL behind the fixture and on the base, and reached
        /// only 52 px below the foot, so the desk under the shade caught almost nothing and the paper under it
        /// caught nothing at all. A player standing in front of the picture reads that as light shining
        /// UPWARD, which is exactly what was reported.
        ///
        /// The bulb is INSIDE the shade, so the pool is centred under the shade's mouth and offset toward the
        /// shade's overhang; the radius is asymmetric -- long downward onto the desk, short upward onto the
        /// wall -- and a second, dimmer BOUNCE source sits on the desk surface itself, which is what makes the
        /// paper, the mug and the keys pick up a warm skirting instead of stopping dead at the pool's edge.
        /// </summary>
        public static LightSource LampLight()
        {
            // the shade's mouth: the sprite's shade spans y+20..y+96, its mouth is the bottom of that
            int mouthY = LampY + 96;
            // Falloff 1.15 (tighter than before) so the pool has an EDGE and reads as a cone of light rather
            // than a fog; Ry 132 downward-biased by the offset below.
            return new LightSource(LampX + 26, mouthY + 14, 104, 132, 1.15, 1);
        }

        /// <summary>
        /// THE BOUNCE (v120). Light that lands on the desk does not stop at the desk: it comes back up off the
        /// paper and the varnish. One dim source lying ON the surface, a step weaker than the lamp, so the
        /// things standing in the pool are lit from BELOW-BEHIND instead of floating dark inside a bright oval.
        /// It is not a second lamp -- it is the same lamp, seen twice.
        /// </summary>
        public static LightSource LampBounceLight()
        {
            return new LightSource(LampX + 26, LampY + 118, 74, 54, 2.10, 1);
        }

        /// <summary>True when this light IS the lamp's -- so the front door can switch it off.</summary>
        public static bool IsLampLight(LightSource l)
        {
            // BOTH lamp sources switch with the fixture: turning the lamp off must also take its bounce away,
            // or the desk keeps a warm pool with nothing above it making it.
            var cone = LampLight();
            var bounce = LampBounceLight();
            return (l.X == cone.X && l.Y == cone.Y) || (l.X == bounce.X && l.Y == bounce.Y);
        }

        public static LightSource[] Lights()
        {
            // v120: the lamp now contributes TWO sources (the cone and its bounce off the desk). The order is
            // back to front: the window's cold fill, the lamp's cone, the bounce, then the tube's key.
            return new[] { WindowLight(), LampLight(), LampBounceLight(), MonitorLight() };
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
        /// <summary>
        /// v119: the tubes are painted INSIDE their own case now, so they are no longer a grid. The old
        /// (StackScreenX/Y/W/H) grid is what made the cluster read as one wall of grey rectangles in v118:
        /// the paint code drew an identical box in the same six places no matter how the art was arranged.
        /// </summary>
        public const int StackBezel = 4;                  // case thickness around the tube
        public const int StackChin = 6;                   // ...and the moulding under it, where the badge lives

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

        // ------------------------------------------------------------------ selection & confirm
        //
        // SELECTION vs CONFIRM (v120). Two states, two looks, and the rules live here so the view cannot
        // invent a third:
        //
        //   ARMED      the row the pointer / keys are on. A CYAN frame and a glowing label, no fill -- it is a
        //              target being aimed at, not a decision.
        //   CONFIRMED  the row that was pressed and is waiting for its second press on TOUCH. The frame goes
        //              WHITE-hot and the label inverts to `void` on a lit fill: the only place in the UI where
        //              a fill is brighter than the text, which is what makes "already pressed" unmissable.
        //
        // Mouse and keyboard never see CONFIRMED for longer than one frame: a mouse click IS a decision, and
        // asking a desktop player to press twice would be a tax. Touch asks twice because a thumb on a glass
        // panel has no hover, and a single tap that fires is how a player credits the wrong screen.
        public const float TouchConfirmSeconds = 1.5f;

        /// <summary>How long a touch must wait between arming a row and confirming it. 0 disables the step.</summary>
        public static float ConfirmWindow(bool touchInput) { return touchInput ? TouchConfirmSeconds : 0f; }

        // ------------------------------------------------------------------ rows
        public struct Pill
        {
            public int X, Y, W, H;
            public string Fill, TextInk;
        }

        /// <summary>
        /// HIT TESTING (v119). Every rectangle the pointer can land on is derived here, from the same
        /// numbers the art and the text are drawn from, so a hover highlight can never sit somewhere the
        /// pointer is not. Row 0's pill starts at PillTop and each row is PillStep tall; the tube's rows
        /// extend 2 px past the pill on every side so a near-miss still counts -- a 13 px target is small
        /// enough without asking the player to be pixel-perfect.
        /// </summary>
        public static int RowAt(int x, int y, int count)
        {
            if (count <= 0) return -1;

            // ---- v119.5, BUG-086: THE FRONT DOOR IS NOW A DOOR, NOT A HIT-REGION -------------------
            //
            // The player reported: "clicking randomly anywhere outside the monitor clicks whatever is
            // highlighted on the screen". Two separate holes did that, and both were in this function:
            //
            //   1. the ROW bands were clamped instead of rejected -- `i >= count` returned `count - 1`, so
            //      anything below the last pill counted as the last pill, and `y < top` returned row 0. A
            //      click anywhere in the tube's lower half therefore chose CREDITS;
            //   2. the only horizontal test was the pill's own 2 px slop -- the tube's glass, the bezel and
            //      the whole 480x270 frame were not consulted at all.
            //
            // The rule that replaces it is physical: a row is under the pointer only if the pointer is on
            // that row's pill and inside the monitor's glass. Everything else is the room, and a click on
            // the room does nothing (see DeskBootstrap.MenuClick -- it no longer falls back to Choose).
            if (!InGlass(x, y)) return -1;
            if (x < PillX - 2 || x >= PillX + PillW + 2) return -1;
            int top = PillTop, bottom = PillTop + count * PillStep;
            // The 2 px slop is OUTSIDE the block only, and it is TWO pixels -- not the whole tube. Inside
            // the block the bands tile exactly, so borders belong to a row; below the block the last row
            // owns exactly its 2 px of slop and nothing further. The old code clamped EVERY y below the block
            // onto the last row, which is why a click anywhere on the lower half of the screen chose
            // CREDITS (BUG-086).
            if (y < top - 2 || y >= bottom + 2) return -1;
            if (y >= bottom) return count - 1;            // the 2 px slop under the block belongs to the last row
            int i = (y - top) / PillStep;                 // exact band, no rounding, no overlap
            if (i < 0) i = 0;
            if (i >= count) return -1;                    // unreachable by construction; kept as a guard
            return i;
        }

        /// <summary>Is this canvas point on the monitor's picture area? The front door's input boundary.</summary>
        public static bool InGlass(int x, int y)
        {
            return x >= GlassX && x < GlassX + GlassW && y >= GlassY && y < GlassY + GlassH;
        }

        /// <summary>
        /// The FIVE-row block as one rect: the pills plus their 2 px slop, inside the glass. The pointer
        /// uses it to light the whole block's edge, so it is obvious which part of the screen is live.
        /// </summary>
        public static bool InRowBlock(int x, int y, int count)
        {
            if (count <= 0) return false;
            if (!InGlass(x, y)) return false;
            return x >= PillX - 2 && x < PillX + PillW + 2 &&
                   y >= PillTop - 2 && y < PillTop + count * PillStep + 2;
        }

        /// <summary>Which tube of the monitor cluster the pointer is over, or -1. Uses StackCells.</summary>
        public static int StackCellAt(int x, int y)
        {
            for (int i = 0; i < StackCells.Length; i++)
            {
                int[] c = StackCells[i];
                if (x >= c[0] && x < c[0] + c[2] && y >= c[1] && y < c[1] + c[3]) return i;
            }
            return -1;
        }

        /// <summary>The lamp's chimney and shade: clicking it toggles the room light.</summary>
        public static bool InLamp(int x, int y)
        {
            return x >= LampX + 10 && x < LampX + 46 && y >= LampY + 20 && y < LampY + 112;
        }

        /// <summary>The keyboard on the desk: the first hint that the desk is a surface, not a backdrop.</summary>
        public static bool InKeyboard(int x, int y)
        {
            // desk.png draws it at (300, desky+26+10) = (300, 186), 84x20 -- see make_menu_room.py
            return x >= 300 && x < 384 && y >= 186 && y < 206;
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
