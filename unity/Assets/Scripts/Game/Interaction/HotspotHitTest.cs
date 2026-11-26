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

        /// <summary>Applicant-local rect (0,0 at sprite bottom-left) -> canvas pixels.</summary>
        public static PixelRect LocalToCanvas(PixelRect local, float applicantX, float applicantY)
        {
            return new PixelRect(applicantX + local.X, applicantY + local.Y,
                                 local.Width, local.Height);
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
