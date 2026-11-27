// HR//0D -- CalloutPlacement: where the read-out panel goes, as arithmetic nobody has to trust.
// Assembly: HRZeroDay.Game  (no UnityEngine types beyond a rect struct of its own, so it is testable)
//
// Two player reports, one bug: "clicking on something opens it too big" (2026-11-26) and "clicking coffee
// opens something big at the bottom of the screen which goes off screen" (2026-11-27). The panel was
// anchored by a corner the router did not use, and nothing clamped it to the 480x270 page.
//
// The rule, in words:
//   1. the panel hangs to the LEFT of its anchor and DOWN from it (the anchor is beside the head);
//   2. if that would leave the page on any side, slide it back in;
//   3. the result is always fully on the page -- a card that is half off screen is unreadable and
//      untappable, which is worse than a card in a slightly wrong place.
using System;

namespace HRZeroDay.Game.Interaction
{
    public struct CalloutBox
    {
        public float Left;
        public float Top;
        public float Width;
        public float Height;
        public float Right { get { return Left + Width; } }
        public float Bottom { get { return Top - Height; } }
    }

    public static class CalloutPlacement
    {
        /// <summary>Pixels kept clear of the page edge, so a border is never flush against the frame.</summary>
        public const float Margin = 4f;

        /// <summary>
        /// Places a `w` x `h` panel whose preferred TOP-RIGHT corner is (anchorX, anchorY) on a
        /// `pageW` x `pageH` page (canvas px, y up). The panel is kept fully inside the page.
        /// </summary>
        public static CalloutBox Fit(float anchorX, float anchorY, float w, float h, int pageW, int pageH)
        {
            if (w <= 0f) w = 1f;
            if (h <= 0f) h = 1f;

            float left = anchorX - w;      // hangs LEFT of the anchor
            float top = anchorY;           // and DOWN from it

            float minLeft = Margin;
            float maxLeft = pageW - Margin - w;
            if (maxLeft < minLeft) maxLeft = minLeft;      // panel wider than the page: flush left
            if (left > maxLeft) left = maxLeft;
            if (left < minLeft) left = minLeft;

            float maxTop = pageH - Margin;
            float minTop = Margin + h;
            if (top > maxTop) top = maxTop;
            if (top < minTop) top = minTop;

            return new CalloutBox { Left = left, Top = top, Width = w, Height = h };
        }
    }
}
