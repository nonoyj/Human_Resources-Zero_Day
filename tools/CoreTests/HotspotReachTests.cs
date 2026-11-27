// HR//0D -- can the player actually TOUCH the body parts the game asks them to inspect?
//
// The player's report on 2026-11-26: "I can see a guy bobbing and blinking in front of my desk, and I
// can't click anything." The rule that produced it was simple and invisible: every hotspot was placed
// at the applicant's centre, because its offset forgot the layout's scaleFactor, and the router then
// looked for it (object origin +- size/2) instead of where the art actually is. Nothing threw. Nothing
// failed a test. The game was simply unplayable in the one way a spreadsheet cannot see.
//
// So this file does the thing that was missing: it reproduces the RUNTIME placement from the layout
// json -- anchor, scaleFactor, ppu, per-hotspot rect, touch padding -- and then asserts, for every
// hotspot, that a touch on the body part the player sees resolves back to that same artifact.
using System.Collections.Generic;
using HRZeroDay.Game.Interaction;
using HRZeroDay.Game;
using HRZeroDay.Core.Data;

namespace HRZeroDay.Tests
{
    internal static class HotspotReachTests
    {
        private const int CanvasW = 480, CanvasH = 270;
        private const int SpriteW = 192, SpriteH = 216;      // the authored applicant sheet frame
        private const float TouchPaddingPx = 6f;             // DeskRuntimeScene.TouchPaddingPx

        public static IEnumerable<string> RunAll()
        {
            yield return CheckPlacementAndColliderAgree();
            yield return CheckEveryHotspotIsReachable();
            yield return CheckSmallestTargetWins();
            yield return CheckNoHotspotFallsOffTheCanvas();
            yield return CheckTapMapMatchesTheCanvas();
        }

        /// <summary>
        /// Reads the SAME json the game reads -- by file, because the headless runner has no Unity
        /// Resources, using System.Text.Json with fields included (DeskLayout is public fields only).
        /// </summary>
        private static DeskLayout Layout()
        {
            var dir = new System.IO.DirectoryInfo(System.AppContext.BaseDirectory);
            while (dir != null && !(System.IO.Directory.Exists(System.IO.Path.Combine(dir.FullName, "art", "kit")) &&
                                    System.IO.Directory.Exists(System.IO.Path.Combine(dir.FullName, "unity"))))
                dir = dir.Parent;
            if (dir == null) throw new System.Exception("repo root not found from " + System.AppContext.BaseDirectory);
            var path = System.IO.Path.Combine(dir.FullName, "unity", "Assets", "Resources", "desk_layout.json");
            var options = new System.Text.Json.JsonSerializerOptions { IncludeFields = true };
            return System.Text.Json.JsonSerializer.Deserialize<DeskLayout>(System.IO.File.ReadAllText(path), options);
        }

        /// <summary>
        /// The world rect, from the SAME function the runtime uses to place the object and size the
        /// collider. If that function is wrong, this test is wrong the same way and fails with the game.
        /// </summary>
        private static PixelRect WorldRect(DeskLayout layout, DeskHotspot hp)
        {
            var applicant = layout.Layer("applicant");
            float scale = applicant.scaleFactor > 0f ? applicant.scaleFactor : 1f;
            return HotspotHitTest.HotspotCanvasRect(applicant.anchor[0], applicant.anchor[1], scale,
                                                    hp.rect, TouchPaddingPx);
        }

        /// <summary>
        /// The two representations must agree: placing the child object and sizing its collider (what the
        /// runtime does) has to land exactly on the canvas rect the player sees. This is the assertion
        /// that would have caught the missing scaleFactor on its own.
        /// </summary>
        private static string CheckPlacementAndColliderAgree()
        {
            var layout = Layout();
            var applicant = layout.Layer("applicant");
            float scale = applicant.scaleFactor > 0f ? applicant.scaleFactor : 1f;
            float ppu = layout.canvas.pixelsPerUnit;
            for (int i = 0; i < layout.hotspots.Count; i++)
            {
                var hp = layout.hotspots[i];
                var want = WorldRect(layout, hp);
                var centre = HotspotHitTest.HotspotCentreCanvas(applicant.anchor[0], applicant.anchor[1], scale, hp.rect);
                var parentCentre = HotspotHitTest.LayerCentre(applicant.anchor[0], applicant.anchor[1],
                                                              scale, SpriteW, SpriteH);
                var local = HotspotHitTest.HotspotLocalPositionUnits(centre, parentCentre, ppu, scale);
                var size = HotspotHitTest.HotspotColliderSizeUnits(hp.rect, ppu, TouchPaddingPx);
                // local units -> canvas px, through the parent's transform
                float parentX = (float)parentCentre.X;
                float parentY = (float)parentCentre.Y;
                float cx = parentX + (float)local.X * ppu * scale;
                float cy = parentY + (float)local.Y * ppu * scale;
                float w = (float)size.X * ppu * scale;
                float h = (float)size.Y * ppu * scale;
                True(System.Math.Abs(cx - (want.X + want.Width * 0.5f)) < 0.01f &&
                     System.Math.Abs(cy - (want.Y + want.Height * 0.5f)) < 0.01f &&
                     System.Math.Abs(w - want.Width) < 0.01f &&
                     System.Math.Abs(h - want.Height) < 0.01f,
                     "hotspot '" + hp.artifact + "' object/collider do not land on the drawn rect");
            }
            return "PASS  object placement and collider size land exactly on the drawn rect";
        }

        private static void True(bool condition, string message)
        {
            if (!condition) throw new System.Exception(message);
        }

        private static string CheckEveryHotspotIsReachable()
        {
            var layout = Layout();
            True(layout.hotspots.Count > 0, "the layout declares no hotspots at all");

            var geometry = new List<HotspotGeometry>();
            var expected = new List<PixelRect>();
            for (int i = 0; i < layout.hotspots.Count; i++)
            {
                var hp = layout.hotspots[i];
                var rect = WorldRect(layout, hp);
                geometry.Add(new HotspotGeometry(hp.artifact, rect));
                expected.Add(rect);
            }

            for (int i = 0; i < geometry.Count; i++)
            {
                // the player touches the middle of the body part they can see
                var centre = new PixelPoint(expected[i].X + expected[i].Width * 0.5f,
                                            expected[i].Y + expected[i].Height * 0.5f);
                string hit = HotspotHitTest.Resolve(geometry, centre, 0f, 99f);
                True(hit != null,
                     "hotspot '" + geometry[i].Artifact + "' cannot be hit where it is drawn at " +
                     centre.X + "," + centre.Y);
                // a tap on a small body part may legitimately resolve to a smaller overlapping target
                // (that is the no_blink rule); it must never resolve to nothing or to an unrelated part
                var own = geometry[i].Rect;
                bool overlapsHit = false;
                for (int j = 0; j < geometry.Count; j++)
                {
                    if (geometry[j].Artifact != hit) continue;
                    var other = geometry[j].Rect;
                    overlapsHit = other.X <= own.X + own.Width && own.X <= other.X + other.Width &&
                                  other.Y <= own.Y + own.Height && own.Y <= other.Y + other.Height;
                }
                True(overlapsHit, "a tap on '" + geometry[i].Artifact + "' resolved to unrelated '" + hit + "'");
            }
            return "PASS  all " + geometry.Count + " hotspots resolve where the art draws them";
        }

        private static string CheckSmallestTargetWins()
        {
            // The eyes carry two tells: 'mirror' and the smaller timed 'no_blink'. The smaller one must
            // win once the player has looked long enough, and 'mirror' must win before that.
            var layout = Layout();
            var geometry = new List<HotspotGeometry>();
            for (int i = 0; i < layout.hotspots.Count; i++)
                geometry.Add(new HotspotGeometry(layout.hotspots[i].artifact, WorldRect(layout, layout.hotspots[i])));

            PixelRect eyes = new PixelRect(0, 0, 0, 0);
            for (int i = 0; i < layout.hotspots.Count; i++)
                if (layout.hotspots[i].artifact == "mirror") eyes = geometry[i].Rect;
            var point = new PixelPoint(eyes.X + eyes.Width * 0.5f, eyes.Y + eyes.Height * 0.5f);

            string early = HotspotHitTest.Resolve(geometry, point, TouchPaddingPx, 0f);
            string late = HotspotHitTest.Resolve(geometry, point, TouchPaddingPx, 99f);
            True(early == "mirror", "before observing, the eyes should resolve to 'mirror', got '" + early + "'");
            True(late == EvidenceMap.NoBlinkArtifact,
                 "after observing, the eyes should resolve to '" + EvidenceMap.NoBlinkArtifact + "', got '" + late + "'");
            return "PASS  the eyes resolve to 'mirror' instantly and to the timed tell once observed";
        }

        private static string CheckNoHotspotFallsOffTheCanvas()
        {
            var layout = Layout();
            for (int i = 0; i < layout.hotspots.Count; i++)
            {
                var r = WorldRect(layout, layout.hotspots[i]);
                True(r.X >= -4f && r.Y >= -4f &&
                     r.X + r.Width <= CanvasW + 4f && r.Y + r.Height <= CanvasH + 4f,
                     "hotspot '" + layout.hotspots[i].artifact + "' is off the canvas at " +
                     r.X + "," + r.Y + " " + r.Width + "x" + r.Height);
            }
            return "PASS  every hotspot sits inside the 480x270 canvas";
        }

        private static string CheckTapMapMatchesTheCanvas()
        {
            // The mapping the router uses. Its corners must be the canvas corners, in every window shape,
            // because the pointer arrives already normalised against the game view.
            var canvas = new PixelRect(0f, 0f, CanvasW, CanvasH);
            var bl = HotspotHitTest.ScreenToCanvasNdc(new PixelPoint(0f, 0f), canvas);
            var tr = HotspotHitTest.ScreenToCanvasNdc(new PixelPoint(1f, 1f), canvas);
            var mid = HotspotHitTest.ScreenToCanvasNdc(new PixelPoint(0.5f, 0.5f), canvas);
            True(bl.X == 0f && bl.Y == 0f, "bottom-left of the game view is not the canvas origin");
            True(tr.X == CanvasW && tr.Y == CanvasH, "top-right of the game view is not the canvas corner");
            True(mid.X == CanvasW * 0.5f && mid.Y == CanvasH * 0.5f, "the centre of the view is not the centre of the canvas");
            // out-of-view points clamp instead of producing a negative hit
            var outside = HotspotHitTest.ScreenToCanvasNdc(new PixelPoint(-1f, 2f), canvas);
            True(outside.X == 0f && outside.Y == CanvasH, "a pointer outside the view is not clamped");
            return "PASS  pointer -> canvas is exact at the corners, the centre, and outside the view";
        }
    }
}
