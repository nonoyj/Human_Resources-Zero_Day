// HR//0D -- SweepPageComposer: builds one walk page as pixels. No Unity objects, so it is testable.
// Assembly: HRZeroDay.Game
//
// WHY THIS IS NOT INSIDE SweepView (2026-11-27)
//   The first version of the composited page produced a BLANK walk: the art blit, the band blit and the
//   strip blit all had to succeed, and any one of them failing left a transparent texture on screen with
//   no exception the player could report. Composition is the part that has to be right, so it is a pure
//   function over colour buffers, it fills every pixel it owns, and there is a test that reads the result
//   back the way a player reads the page: is the art there, is the band there, is the strip there.
//
// COORDINATES: every buffer here is PNG order -- row 0 is the TOP of the page. The caller flips once
// before handing the buffer to a texture (MonitorScreenRaster.FlipToTextureOrder).
using System.Collections.Generic;
using HRZeroDay.Core.Rules;

namespace HRZeroDay.Game.Interaction
{
    public static class SweepPageComposer
    {
        /// <summary>
        /// Composes a page: `art` (may be null) as the backdrop, the caption band at BandX/BandTop, the
        /// strip along the bottom. Returns a buffer of exactly w*h, every pixel opaque.
        /// </summary>
        public static UnityEngine.Color32[] Compose(
            UnityEngine.Color32[] art, int w, int h,
            IList<string> bandLines, IList<string> bandColours,
            UnityEngine.Color32[] strip, int stripTop,
            IGlyphSource glyphs,
            UnityEngine.Color32 voidInk, UnityEngine.Color32 lineInk)
        {
            var buf = new UnityEngine.Color32[w * h];
            for (int i = 0; i < buf.Length; i++) buf[i] = voidInk;

            if (art != null && art.Length >= w * h)
            {
                for (int i = 0; i < w * h; i++)
                {
                    // keep the page opaque: art with holes must still read as a sheet of paper
                    var c = art[i];
                    if (c.a == 0) { c = voidInk; }
                    else if (c.a != 255) { c = new UnityEngine.Color32(c.r, c.g, c.b, 255); }
                    buf[i] = c;
                }
            }

            // ---- caption band -------------------------------------------------------------
            int rows = bandLines == null ? 0 : bandLines.Count;
            int bandH = SweepScript.BandHeight(rows);
            int bandX = SweepScript.BandX, bandY = SweepScript.BandTop, bandW = SweepScript.BandW;
            for (int y = bandY; y < bandY + bandH && y < h; y++)
                for (int x = bandX; x < bandX + bandW && x < w; x++)
                    buf[y * w + x] = voidInk;
            for (int x = bandX; x < bandX + bandW && x < w; x++)
            {
                buf[bandY * w + x] = lineInk;
                int bottom = bandY + bandH - 1;
                if (bottom >= 0 && bottom < h) buf[bottom * w + x] = lineInk;
            }
            for (int y = bandY; y < bandY + bandH && y < h; y++)
            {
                buf[y * w + bandX] = lineInk;
                int right = bandX + bandW - 1;
                if (right >= 0 && right < w) buf[y * w + right] = lineInk;
            }

            if (glyphs != null && rows > 0)
            {
                // WHERE THE WORDS GO IS CORE'S DECISION (SweepScript.BandRows), in page pixels, y DOWN.
                // This used to be handed to the CRT raster as a GlassRect, which lays a line against the
                // BOTTOM of a box and SKIPS empty strings: with a heading, a spacer and two body lines the
                // four rows lost one row of pitch and the last one landed on the band's bottom border --
                // the "caption bleeding onto the room art" the player photographed. RenderRows paints each
                // row from its top edge and keeps the spacer's pitch, so the band and the copy agree.
                var specs = new List<string>();
                var inks = new List<string>();
                for (int i = 0; i < rows; i++) { specs.Add(bandLines[i]); inks.Add(bandColours[i]); }

                var bandBuf = new UnityEngine.Color32[bandW * bandH];
                for (int i = 0; i < bandBuf.Length; i++) bandBuf[i] = voidInk;

                // the band is its own little canvas: band-local rows, clipped to the band's inside edge
                var bandLocal = new List<MonitorScreenRaster.RasterRow>();
                var coreRows = SweepScript.BandRows(specs, inks, 0);
                for (int i = 0; i < coreRows.Count; i++)
                    bandLocal.Add(new MonitorScreenRaster.RasterRow
                    {
                        text = coreRows[i].Text,
                        x = coreRows[i].X - bandX,
                        top = coreRows[i].Top,
                        colour = coreRows[i].Colour
                    });
                MonitorScreenRaster.RenderRows(bandBuf, bandW, bandH, bandLocal, glyphs,
                                               MonitorScreenRaster.RasterClip.Box(1, 1, bandW - 2, bandH - 2));
                for (int y = 0; y < bandH && bandY + y < h; y++)
                    for (int x = 0; x < bandW && bandX + x < w; x++)
                    {
                        var c = bandBuf[y * bandW + x];
                        if (c.r == voidInk.r && c.g == voidInk.g && c.b == voidInk.b) continue;  // band fill / border
                        buf[(bandY + y) * w + bandX + x] = c;
                    }
            }

            // ---- strip --------------------------------------------------------------------
            if (strip != null)
            {
                int sh = strip.Length / w;
                for (int y = 0; y < sh && stripTop + y < h; y++)
                    for (int x = 0; x < w; x++)
                    {
                        var c = strip[y * w + x];
                        if (c.a == 0) continue;
                        buf[(stripTop + y) * w + x] = new UnityEngine.Color32(c.r, c.g, c.b, 255);
                    }
            }
            return buf;
        }
    }
}
