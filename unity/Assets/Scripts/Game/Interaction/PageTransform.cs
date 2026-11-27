// HR//0D -- PageTransform: where a 480x270 canvas pixel lands in the world, computed once.
// Assembly: HRZeroDay.Game  (UnityEngine)
//
// WHY THIS FILE EXISTS (2026-11-27)
//   Three separate bugs, all of them the same bug: a screen element was placed by hand in world units
//   while the rest of the screen was placed in canvas pixels.
//     1. the after-hours key band, pinned at KeyBandTop/Ppu = 14.5. KeyBandTop is a y-DOWN offset from
//        the top of the page, so the band landed 12 units above where it belongs -- in the ceiling,
//        past the right edge: the streak across the top of the walk page in the player's screenshots;
//     2. the read-out callout scaled its sprite twice (a 176 px sprite at 16 ppu is already 11 units
//        wide; multiplying by w/ppu made it 121), so inspecting ANYTHING opened an 11x-too-big panel;
//     3. the desk HUD moved each line by a flat 0.36 world units while TextMesh spacing is measured in
//        the font's em, so the block marched up the screen instead of down.
//   Every one of them is "the maths was done in the wrong space". So the maths lives here, once, and the
//   views ask it. tools/CoreTests/PageTransformTests.cs pins the numbers so it cannot drift again.
//
// THE SPACES (all three are in use in this repo -- hence the confusion)
//   canvas px : (0,0) = BOTTOM-LEFT of the 480x270 page, x right, y UP. This is the space that
//               art/kit/unity_sprites.json anchors and DeskSceneBuilder.PixelsToUnits already use, and
//               the camera is centred on the canvas rect, so world = px / ppu with the SAME origin.
//   world     : 1 unit = Ppu canvas px, origin at the canvas bottom-left. 480x270 px = 30 x 16.875.
//   texture   : a Sprite.Create texture has its rows bottom-up (row 0 = bottom). A raster painted
//               top-down must be flipped once -- MonitorScreenRaster.FlipToTextureOrder.
using UnityEngine;

namespace HRZeroDay.Game.Interaction
{
    /// <summary>The one place that converts a 480x270 canvas position into a world position.</summary>
    public static class PageTransform
    {
        public const float DefaultPpu = 16f;
        public const int DefaultWidth = 480;
        public const int DefaultHeight = 270;

        /// <summary>World position of a canvas pixel (origin bottom-left, y up).</summary>
        public static Vector3 CanvasToWorld(float x, float y, float ppu)
        {
            float p = ppu > 0f ? ppu : DefaultPpu;
            return new Vector3(x / p, y / p, 0f);
        }

        public static Vector3 CanvasToWorld(float x, float y)
        {
            return CanvasToWorld(x, y, DefaultPpu);
        }

        /// <summary>
        /// World position for a sprite whose pivot is (0,1) -- "put my top-left corner here" -- where
        /// (x, topDownY) is a canvas x and a y measured DOWN from the top of the page. This is the
        /// conversion the key band needed and did not get.
        /// </summary>
        public static Vector3 TopLeftToWorld(float x, float topDownY, float ppu, int pageHeight)
        {
            return CanvasToWorld(x, pageHeight - topDownY, ppu);
        }

        /// <summary>
        /// True when a sprite pivot sits on the sprite's top-left corner: x on the left edge and y at the
        /// top. A 2D texture's rows run bottom-up, so the texture's top-left pivot is (0,1).
        /// </summary>
        public static bool HasTopLeftPivot(Vector2 pivot)
        {
            return Almost(pivot.x, 0f) && Almost(pivot.y, 1f);
        }

        /// <summary>
        /// The pivot every painted page layer must use. Named so the guard has one thing to check: a
        /// top-left-pivot sprite placed with TopLeftToWorld() lands on its canvas rect; anything else
        /// lands a whole sprite-height too high.
        /// </summary>
        public static Vector2 TopLeftPivot { get { return new Vector2(0f, 1f); } }

        /// <summary>
        /// The localScale a painted page sprite needs: NONE. Sprite.Create(texture, ..., ppu) already
        /// yields texture pixels / ppu units. Scaling by px/ppu a second time is bug 2 above.
        /// </summary>
        public static Vector3 NaturalScale { get { return Vector3.one; } }

        private static bool Almost(float a, float b)
        {
            return Mathf.Abs(a - b) < 0.0001f;
        }
    }
}
