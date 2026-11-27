// HR//0D -- pure geometry for turning a touch into an artifact id.
// No UnityEngine here: the same maths is exercised by the headless tests and by the
// browser interaction twin, so a hotspot that is unreachable on a phone is caught early.
// Owner: Agent (interaction layer).
using System;
using System.Collections.Generic;

namespace HRZeroDay.Game.Interaction
{
    public struct PixelPoint
    {
        public float X;
        public float Y;
        public PixelPoint(float x, float y) { X = x; Y = y; }
    }

    public struct PixelRect
    {
        public float X;
        public float Y;
        public float Width;
        public float Height;
        public PixelRect(float x, float y, float width, float height)
        {
            X = x; Y = y; Width = width; Height = height;
        }

        public bool Contains(PixelPoint p)
        {
            return p.X >= X && p.X <= X + Width && p.Y >= Y && p.Y <= Y + Height;
        }

        public PixelRect Expanded(float padding)
        {
            return new PixelRect(X - padding, Y - padding,
                                 Width + padding * 2f, Height + padding * 2f);
        }
    }

    public sealed class HotspotGeometry
    {
        public string Artifact;
        public PixelRect Rect;       // canvas pixels, y up from bottom-left

        public HotspotGeometry(string artifact, PixelRect rect)
        {
            Artifact = artifact;
            Rect = rect;
        }
    }

    public static class HotspotHitTest
    {
        /// <summary>
        /// Maps a pointer position (screen pixels, y up from bottom-left) into reference canvas
        /// pixels, honouring the device safe area. ADR-005: 480x270, landscape, integer scale.
        /// </summary>
        public static PixelPoint ScreenToCanvas(PixelPoint screen, PixelRect safeArea,
                                                int referenceWidth, int referenceHeight)
        {
            float w = safeArea.Width > 0f ? safeArea.Width : 1f;
            float h = safeArea.Height > 0f ? safeArea.Height : 1f;
            float u = (screen.X - safeArea.X) / w;
            float v = (screen.Y - safeArea.Y) / h;
            u = Math.Min(1f, Math.Max(0f, u));
            v = Math.Min(1f, Math.Max(0f, v));
            return new PixelPoint(u * referenceWidth, v * referenceHeight);
        }

        /// <summary>
        /// Where the player actually touched, in the same y-up canvas pixels the desk layout uses.
        ///
        /// This is the ONLY screen-to-canvas mapping in the game. It used to live inline in
        /// GameInputRouter, which derived the canvas from the camera -- and the camera's centre is not
        /// the canvas origin once the window is wider than 16:9, so every hit drifted by a fraction of
        /// the letterbox bar. Here the canvas rect is passed in explicitly, so the mapping is exact and
        /// testable without Unity: ScreenToCanvasNdc(0,0) is always the canvas's bottom-left corner.
        /// </summary>
        public static PixelPoint ScreenToCanvasNdc(PixelPoint normalised, PixelRect canvasPixels)
        {
            float u = Math.Min(1f, Math.Max(0f, normalised.X));
            float v = Math.Min(1f, Math.Max(0f, normalised.Y));
            return new PixelPoint(u * canvasPixels.Width, v * canvasPixels.Height);
        }

        /// <summary>Applicant-local rect (0,0 at sprite bottom-left) -> canvas pixels.</summary>
        public static PixelRect LocalToCanvas(PixelRect local, float applicantX, float applicantY)
        {
            return new PixelRect(applicantX + local.X, applicantY + local.Y,
                                 local.Width, local.Height);
        }

        /// <summary>
        /// THE placement maths, in one place, and derived from ONE quantity: where the rect's centre is
        /// on the canvas. Both the runtime (DeskRuntimeScene.BuildHotspots) and the headless reach test
        /// call these, so the test cannot agree with itself while the game disagrees with both -- which
        /// is exactly how 9 of 9 hotspots ended up at the applicant's centre.
        ///
        /// Canvas pixels, y up, origin at the canvas's bottom-left. `anchorX/Y` is the layer's anchor
        /// (bottom-left of the drawn sprite), `scale` is the layer's scaleFactor, `rect` is the hotspot's
        /// rect in AUTHORED sprite pixels (192x216 space), `paddingPx` is the touch slop.
        /// </summary>
        public static PixelPoint HotspotCentreCanvas(float anchorX, float anchorY, float scale,
                                                     int[] rect)
        {
            if (rect == null || rect.Length != 4)
                throw new ArgumentException("hotspot rect must be [x, y, w, h]", "rect");
            if (scale <= 0f) scale = 1f;
            // the sprite is drawn from its bottom-left anchor at `scale`, so authored pixels scale once
            return new PixelPoint(anchorX + (rect[0] + rect[2] * 0.5f) * scale,
                                  anchorY + (rect[1] + rect[3] * 0.5f) * scale);
        }

        /// <summary>The rect the player can actually touch, in canvas px (centre +- scaled half size).</summary>
        public static PixelRect HotspotCanvasRect(float anchorX, float anchorY, float scale,
                                                  int[] rect, float paddingPx)
        {
            var centre = HotspotCentreCanvas(anchorX, anchorY, scale, rect);
            if (scale <= 0f) scale = 1f;
            float w = (rect[2] + paddingPx * 2f) * scale;
            float h = (rect[3] + paddingPx * 2f) * scale;
            return new PixelRect((float)centre.X - w * 0.5f, (float)centre.Y - h * 0.5f, w, h);
        }

        /// <summary>
        /// The child object's LOCAL position, in units, for a parent already placed by its centre at
        /// `parentCentre`. The parent (L_applicant) carries the layer's scaleFactor, so the delta is
        /// divided by the ppu AND by the scale -- the omission of the scale is the bug this pair exists
        /// to make impossible.
        /// </summary>
        public static PixelPoint HotspotLocalPositionUnits(PixelPoint centre, PixelPoint parentCentre,
                                                           float pixelsPerUnit, float scale)
        {
            float ppu = pixelsPerUnit > 0f ? pixelsPerUnit : 1f;
            if (scale <= 0f) scale = 1f;
            return new PixelPoint(((float)centre.X - (float)parentCentre.X) / ppu / scale,
                                  ((float)centre.Y - (float)parentCentre.Y) / ppu / scale);
        }

        /// <summary>The BoxCollider2D size, in the parent's local units (the parent's scale applies after).</summary>
        public static PixelPoint HotspotColliderSizeUnits(int[] rect, float pixelsPerUnit, float paddingPx)
        {
            if (rect == null || rect.Length != 4)
                throw new ArgumentException("hotspot rect must be [x, y, w, h]", "rect");
            float ppu = pixelsPerUnit > 0f ? pixelsPerUnit : 1f;
            return new PixelPoint((rect[2] + paddingPx * 2f) / ppu, (rect[3] + paddingPx * 2f) / ppu);
        }

        /// <summary>Where a layer's drawn sprite is centred on the canvas (anchor is its bottom-left).</summary>
        public static PixelPoint LayerCentre(float anchorX, float anchorY, float scale, int spriteW, int spriteH)
        {
            if (scale <= 0f) scale = 1f;
            return new PixelPoint(anchorX + spriteW * scale * 0.5f, anchorY + spriteH * scale * 0.5f);
        }

        /// <summary>
        /// Returns the artifact under the point, or null. With padded targets, the smallest
        /// rect wins so a large region (hands) cannot swallow a small one (badge). The timed
        /// no_blink target takes precedence over mirror only after enough observation time.
        /// </summary>
        public static string Resolve(IList<HotspotGeometry> hotspots, PixelPoint point,
                                     float touchPadding, float observedSeconds)
        {
            string best = null;
            float bestArea = float.MaxValue;
            for (int i = 0; i < hotspots.Count; i++)
            {
                var hp = hotspots[i];
                if (hp == null || string.IsNullOrEmpty(hp.Artifact)) continue;
                var rect = hp.Rect.Expanded(touchPadding);
                if (rect.Contains(point) == false) continue;

                float area = rect.Width * rect.Height;
                bool timedWins = best == "mirror" && hp.Artifact == EvidenceMap.NoBlinkArtifact &&
                                 observedSeconds >= EvidenceMap.NoBlinkObserveSeconds;
                if (area < bestArea || timedWins)
                {
                    best = hp.Artifact;
                    bestArea = area;
                }
            }
            return best;
        }

        /// <summary>Builds the geometry the scene builder writes into BoxCollider2D objects.</summary>
        public static HotspotGeometry FromLayoutRect(string artifact, int[] rect)
        {
            if (rect == null || rect.Length != 4)
                throw new ArgumentException("hotspot rect must be [x, y, w, h]", "rect");
            return new HotspotGeometry(artifact,
                new PixelRect(rect[0], rect[1], rect[2], rect[3]));
        }
    }
}
