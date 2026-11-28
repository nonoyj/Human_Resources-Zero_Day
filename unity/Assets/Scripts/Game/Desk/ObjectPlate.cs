// HR//0D -- one renderer for every clickable object on screen: HUD plates, plaques, flag tabs, stamps.
// Assembly: HRZeroDay.Game
//
// WHY ONE (v126)
//     The menu draws its rows in MenuView, the HUD drew nothing but prose in GameHud, and the ALERTS
//     popup drew its own box in DeskEvidenceOverlay. Three hand-written boxes is three chances to get the
//     state colours wrong -- and the menu already did: a selected row was painted void-on-void for two
//     rounds and read as static. So a plate is a RECT plus a STATE, drawn by this file, with the fill, ink
//     and edge coming from HudRules (Core) where the tests can see them.
//
// WHAT A PLATE LOOKS LIKE (16-bit, palette-gated)
//     A sunk 1 px border, a fill from the palette, a single bright edge on the top and left of a lit plate
//     (a bevel, the way a 16-bit button caught the room's light), and the label in the game's own 5x7 font
//     at 1x, centred. No rounded corners, no gradients, no drop shadow: this is a desk in a dark office, and
//     the cheapest way to make a plate look glued on is to round it.
//
//     The glyphs come from the same atlas as the CRT and the menu, so a label on a stamp pad and the text
//     on the monitor are the same typeface -- which is the point of the player's request P (the in-game look
//     must be the menu's look). A plate is drawn at integer canvas pixels, so it never blurs.
using System.Collections.Generic;
using HRZeroDay.Core.Rules;
using UnityEngine;

namespace HRZeroDay.Game.Desk
{
    public static class ObjectPlate
    {
        /// <summary>
        /// Draws one object into an RGBA canvas buffer (row 0 = top), lifted, using the palette keys that
        /// HudRules returned for its state. `glyphs` paints the label. Returns the number of ink pixels so a
        /// caller (or a test) can prove the plate is not empty -- an invisible button is a bug, not a style.
        /// </summary>
        public static int Draw(Color32[] buffer, int w, int h, HudObject o, IDictionary<string, Color32> palette,
                               IGlyphSource glyphs, bool flagged = false)
        {
            if (buffer == null || o == null || palette == null) return 0;

            string fillKey = IsFlag(o.Id) ? HudRules.FlagFillKey(flagged, o.State) : HudRules.FillKey(o.Id, o.State);
            string inkKey = IsFlag(o.Id) ? HudRules.FlagInkKey(flagged, o.State) : HudRules.InkKey(o.Id, o.State);
            string edgeKey = HudRules.EdgeKey(o.Id, o.State);

            Color32 fill = Key(palette, fillKey);
            Color32 ink = Key(palette, inkKey);
            Color32 edge = Key(palette, edgeKey);
            Color32 sunk = Key(palette, o.State == HudState.Disabled ? "steel_0" : "void");

            int painted = 0;

            // ---- the body: fill inside a 1 px sunk border
            for (int y = o.Y + 1; y < o.Y + o.H - 1; y++)
            {
                for (int x = o.X + 1; x < o.X + o.W - 1; x++)
                {
                    int i = y * w + x;
                    if (i < 0 || i >= buffer.Length) continue;
                    buffer[i] = fill;
                    painted++;
                }
            }

            // ---- the border: sunk all round, with a lit bevel on the top and left of a live plate
            for (int x = o.X; x < o.X + o.W; x++)
            {
                Plot(buffer, w, h, x, o.Y, sunk);
                Plot(buffer, w, h, x, o.Y + o.H - 1, sunk);
            }
            for (int y = o.Y; y < o.Y + o.H; y++)
            {
                Plot(buffer, w, h, o.X, y, sunk);
                Plot(buffer, w, h, o.X + o.W - 1, y, sunk);
            }
            if (o.State != HudState.Disabled)
            {
                for (int x = o.X + 1; x < o.X + o.W - 1; x++) Plot(buffer, w, h, x, o.Y + 1, edge);
                for (int y = o.Y + 1; y < o.Y + o.H - 1; y++) Plot(buffer, w, h, o.X + 1, y, edge);
            }

            // ---- the label: centred in the object, on the glyph grid, never on a half pixel
            if (glyphs != null && !string.IsNullOrEmpty(o.Label))
            {
                int cw = glyphs.CellW, ch = glyphs.CellH;
                int cols = o.Label.Length;
                int startX = o.X + (o.W - cols * cw) / 2;
                int startY = o.Y + (o.H - ch) / 2;
                for (int c = 0; c < cols; c++)
                {
                    char g = o.Label[c];
                    for (int gy = 0; gy < ch; gy++)
                    {
                        for (int gx = 0; gx < cw; gx++)
                        {
                            if (!glyphs.InkAt(g, gx, gy)) continue;
                            Plot(buffer, w, h, startX + c * cw + gx, startY + gy, ink);
                            painted++;
                        }
                    }
                }
            }

            // ---- a flag tab shows its own state as a tick, not as a colour only (rule 2: shape first)
            if (IsFlag(o.Id) && flagged)
            {
                int tx = o.X + o.W - 5, ty = o.Y + 3;
                for (int i = 0; i < 3; i++) Plot(buffer, w, h, tx + i, ty + i, ink);
                for (int i = 0; i < 2; i++) Plot(buffer, w, h, tx - 1 - i, ty + 3 + i, ink);
            }
            return painted;
        }

        /// <summary>
        /// A palette key -> its colour, with the failure made visible. A missing key paints magenta so a
        /// screenshot shows the mistake instead of a plate quietly becoming the room's own colour (which is
        /// how the void-on-void selected row survived two rounds).
        /// </summary>
        public static Color32 Key(IDictionary<string, Color32> palette, string key)
        {
            Color32 c;
            if (palette != null && key != null && palette.TryGetValue(key, out c)) return c;
            Debug.LogWarning("[HR//0D] palette key '" + key + "' is missing -- painting magenta so it shows");
            return new Color32(255, 0, 255, 255);
        }

        public static bool IsFlag(HudId id)
        {
            return (int)id >= (int)HudId.Flag1 && (int)id <= (int)HudId.Flag5;
        }

        private static void Plot(Color32[] buffer, int w, int h, int x, int y, Color32 c)
        {
            if (x < 0 || y < 0 || x >= w || y >= h) return;
            buffer[y * w + x] = c;
        }

        /// <summary>
        /// The shift plate and the security board are READINGS, not buttons: painted straight onto the
        /// canvas with the room's ink, no plate, no border. They are the two things a player glances at, and
        /// a glance should not have to step past a button's chrome to read it.
        /// </summary>
        public static int DrawReading(Color32[] buffer, int w, int h, int x, int top, string text,
                                      IDictionary<string, Color32> palette, IGlyphSource glyphs, string inkKey, string shadowKey)
        {
            if (buffer == null || palette == null || glyphs == null || string.IsNullOrEmpty(text)) return 0;
            Color32 ink = Key(palette, inkKey);
            Color32 shadow = Key(palette, shadowKey);
            int painted = 0;
            for (int c = 0; c < text.Length; c++)
            {
                for (int gy = 0; gy < glyphs.CellH; gy++)
                {
                    for (int gx = 0; gx < glyphs.CellW; gx++)
                    {
                        if (!glyphs.InkAt(text[c], gx, gy)) continue;
                        int px = x + c * glyphs.CellW + gx;
                        int py = top + gy;
                        Plot(buffer, w, h, px + 1, py + 1, shadow);   // a 1 px shadow, not a glow
                        Plot(buffer, w, h, px, py, ink);
                        painted++;
                    }
                }
            }
            return painted;
        }

        /// <summary>
        /// One pip of the security board, as a solid block: 4x7 px, filled or hollow. This is the "shape
        /// before number" rule made of pixels -- the glyph atlas has no block character, so five blocks are
        /// DRAWN. A filled pip is a solid rectangle; an empty one is its 1 px outline, which reads as "a
        /// slot" rather than as a gap.
        /// </summary>
        public static void DrawPip(Color32[] buffer, int w, int h, int x, int y, int pw, int ph,
                                   bool filled, Color32 colour)
        {
            if (filled)
            {
                for (int iy = 0; iy < ph; iy++)
                    for (int ix = 0; ix < pw; ix++)
                        Plot(buffer, w, h, x + ix, y + iy, colour);
                return;
            }
            for (int iy = 0; iy < ph; iy++)
            {
                Plot(buffer, w, h, x, y + iy, colour);
                Plot(buffer, w, h, x + pw - 1, y + iy, colour);
            }
            for (int ix = 0; ix < pw; ix++)
            {
                Plot(buffer, w, h, x + ix, y, colour);
                Plot(buffer, w, h, x + ix, y + ph - 1, colour);
            }
        }

        /// <summary>
        /// THE WHOLE HUD LAYER, in one call, shared by the live view and the offline dump. If these two ever
        /// drew the HUD with separate code, the dump would stop being evidence -- and the dump is the only
        /// thing between a change and the player's next screenshot. Returns the ink-pixel count.
        /// </summary>
        public static int DrawHud(Color32[] buffer, int w, int h, HudModel m, IList<HudObject> objects,
                                  IDictionary<string, Color32> palette, IGlyphSource glyphs)
        {
            if (buffer == null || m == null || objects == null) return 0;
            int painted = 0;
            for (int i = 0; i < objects.Count; i++)
            {
                var o = objects[i];
                bool flagged = false;
                if (IsFlag(o.Id))
                {
                    int idx = HudRules.IndexOf(o.Id);
                    flagged = idx >= 0 && idx < 5 && m.Flagged[idx];
                }
                painted += Draw(buffer, w, h, o, palette, glyphs, flagged);
            }

            // ---- the two READINGS: painted text, no plate, because they are not buttons
            bool readings = !m.ShowingWalk && !m.ShowingEnding && !m.ShowingSummary && !m.ShowingDebrief
                            && !m.LetterOpen && !m.AlertsOpen;
            if (readings)
            {
                painted += DrawReading(buffer, w, h, HudRules.ReadX, HudRules.ReadY1,
                                       HudRules.PlateLine(m), palette, glyphs, "paper_2", "void");
                painted += DrawReading(buffer, w, h, HudRules.ReadX, HudRules.ReadY2,
                                       HudRules.NameLine(m), palette, glyphs, "white", "void");
                painted += DrawReading(buffer, w, h, HudRules.BoardX + 4, HudRules.BoardY + 4,
                                       HudRules.BoardLine(m.Security), palette, glyphs, "cyan_2", "void");
                // the pips, under the band word: five blocks, filled by score
                Color32 pip = Key(palette, "cyan_2");
                for (int i = 0; i < HudRules.Pips; i++)
                {
                    int px, py, pw, ph;
                    HudRules.PipRect(i, out px, out py, out pw, out ph);
                    DrawPip(buffer, w, h, px, py, pw, ph, HudRules.PipFilled(m.Security, i), pip);
                    painted += pw * ph;
                }
                painted += DrawReading(buffer, w, h, HudRules.BoardX + 4, HudRules.BoardY + 23,
                                       "TRUST " + m.Trust + " HIRED " + m.Hired + "/" + m.HireLimit,
                                       palette, glyphs, "paper_1", "void");
            }
            return painted;
        }

        /// <summary>Centres a reading on an object's rect, for the labels that sit on a plate.</summary>
        public static int CentredX(HudObject o, string text, IGlyphSource glyphs)
        {
            if (o == null || glyphs == null || string.IsNullOrEmpty(text)) return o == null ? 0 : o.X;
            return o.X + (o.W - text.Length * glyphs.CellW) / 2;
        }
    }
}
