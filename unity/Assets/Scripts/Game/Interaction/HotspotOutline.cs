// HR//0D -- the visual answer to "what am I supposed to click?".
// Assembly: HRZeroDay.Game
//
// The player, 2026-11-26: "I want to make what to click look more clickable, because right now it just
// looks like a random piece of drawing."  Correct: the applicant is one painterly sprite, and six of the
// nine hotspots are invisible regions of it.
//
// The answer is deliberately NOT an arrow or a glow. It is a TARGET: a 1 px corner-bracket outline drawn
// ON the body part, in the palette's 'signal' colour, at 55% alpha at rest and full strength on hover. It
// reads as "this is a thing you can pick up" without painting over the art, and it costs one tiny texture
// per hotspot (a few hundred bytes each, generated at build time, not shipped as art).
using UnityEngine;
using HRZeroDay.Core.Data;

namespace HRZeroDay.Game.Interaction
{
    public static class HotspotOutline
    {
        // Rest is deliberately faint (30%): nine bright brackets turn the applicant into a wireframe and
        // undo the painterly look. They are a HINT -- hover is what makes one obvious.
        private static readonly Color32 Rest = new Color32(0x18, 0xE0, 0xB0, 0x4D);    // signal @ 30%
        private static readonly Color32 Hover = new Color32(0x18, 0xE0, 0xB0, 0xFF);

        /// <summary>
        /// Corner brackets rather than a full box: the eye reads four corners as a selection, and an
        /// unbroken rectangle over a face looks like a UI bug.
        /// </summary>
        public static Sprite Make(int w, int h, int arm)
        {
            if (w < 6) w = 6;
            if (h < 6) h = 6;
            if (arm < 2) arm = 2;
            if (arm > w / 2) arm = w / 2;
            if (arm > h / 2) arm = h / 2;

            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Point;
            var px = new Color32[w * h];
            for (int i = 0; i < px.Length; i++) px[i] = new Color32(0, 0, 0, 0);

            // four corners, each an L: along the top/bottom edge for `arm` px, down the side for `arm` px
            for (int a = 0; a < arm; a++)
            {
                px[a] = Hover;                                     // top-left along
                px[a * w] = Hover;                                 // top-left down
                px[w - 1 - a] = Hover;                             // top-right along
                px[a * w + w - 1] = Hover;                         // top-right down
                px[(h - 1) * w + a] = Hover;                       // bottom-left along
                px[(h - 1 - a) * w] = Hover;                       // bottom-left up
                px[(h - 1) * w + w - 1 - a] = Hover;               // bottom-right along
                px[(h - 1 - a) * w + w - 1] = Hover;               // bottom-right up
            }
            tex.SetPixels32(px);
            tex.Apply(false, true);                                // true: no longer readable, saves memory
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 16f);
        }

        public static void SetRest(SpriteRenderer sr) { if (sr != null) sr.color = Rest; }
        public static void SetHover(SpriteRenderer sr) { if (sr != null) sr.color = Hover; }
    }
}
