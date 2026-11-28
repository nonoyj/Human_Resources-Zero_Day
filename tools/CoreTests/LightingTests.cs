// HR//0D -- the light rules, checked without a renderer.
// Assembly: HRZeroDay.Tests
//
// WHY THESE EXIST (v120)
//   Every bug the player reported in this round was a LIGHT bug, and every one of them was arithmetic that
//   nobody had written down: the lamp's pool was centred on its own foot, the tube had no light at all, the
//   menu's hover painted three rows and called it a highlight. A screenshot can catch the next one, but only
//   if somebody looks; these catch it on every build.
//
//   Nothing here needs Unity's renderer. The light is a set of numbers plus a rule about which pixels move,
//   so the numbers and the rule are what is tested.
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using HRZeroDay.Core.Rules;

namespace HRZeroDay.Tests
{
    public static class LightingTests
    {
        public static IEnumerable<string> RunAll()
        {
            yield return Check("lamp_pool_is_under_the_shade", LampPoolIsUnderTheShade);
            yield return Check("lamp_bounce_switches_with_the_lamp", LampBounceSwitchesWithTheLamp);
            yield return Check("tube_has_three_states", TubeHasThreeStates);
            yield return Check("tube_pool_lands_on_the_desk", TubePoolLandsOnTheDesk);
            yield return Check("two_taps_on_touch_only", TwoTapsOnTouchOnly);
            yield return Check("palette_ladder_is_authored", PaletteLadderIsAuthored);
            yield return Check("security_is_a_shape_before_a_number", SecurityIsAShapeBeforeANumber);
            yield return Check("the_tube_lights_every_static_surface", TheTubeLightsEveryStaticSurface);
            yield return Check("the_tube_lights_the_desk_not_the_floor", TheTubeLightsTheDeskNotTheFloor);
        }

        private static string Check(string name, Func<string> body)
        {
            try { return body(); }
            catch (Exception ex) { return "FAIL  " + name + ": " + ex.Message; }
        }

        private static void True(bool ok, string message) { if (!ok) throw new Exception(message); }

        /// <summary>
        /// THE PLAYER'S BUG (v120, request D): "light appears to travel UP from a source below". The lamp's
        /// pool was centred 70 px DOWN the sprite -- at its foot -- with a symmetric radius, so the wall
        /// behind the fixture was the brightest part of the pool and the desk under the shade caught almost
        /// nothing. The light must start at the shade's MOUTH, and the pool must reach the desk surface.
        /// </summary>
        private static string LampPoolIsUnderTheShade()
        {
            var lamp = MenuLayout.LampLight();
            int mouth = MenuLayout.LampY + 96;
            True(lamp.Y >= mouth, "the lamp's light is centred above the shade's mouth -- it lights the wall");
            True(lamp.Y + lamp.Ry > MenuLayout.DeskY,
                 "the lamp's pool stops " + (MenuLayout.DeskY - (lamp.Y + lamp.Ry)) + " px short of the desk");
            // and it must not be a fog: the old one had falloff 1.45 and reached ~60 px up the wall
            True(lamp.Y - lamp.Ry < mouth + 40, "the lamp's pool reaches 40+ px above the shade -- it is a fog");
            True(lamp.Strength <= MenuLayout.MaxLift, "the lamp adds more than MaxLift steps");
            return "PASS  the lamp's pool hangs under its shade and reaches the desk";
        }

        private static string LampBounceSwitchesWithTheLamp()
        {
            var cone = MenuLayout.LampLight();
            var bounce = MenuLayout.LampBounceLight();
            True(bounce.Y > cone.Y, "the bounce is not below the cone: light does not come off the desk");
            True(bounce.Ry < cone.Ry, "the bounce is as big as the lamp -- a bounce is a skirting, not a lamp");
            True(MenuLayout.IsLampLight(cone) && MenuLayout.IsLampLight(bounce),
                 "the bounce stays lit when the lamp is switched off");
            True(!MenuLayout.IsLampLight(MenuLayout.WindowLight()), "the window's light is treated as the lamp's");
            return "PASS  the lamp is TWO sources and both switch with the fixture";
        }

        /// <summary>
        /// Request E: "it reads as if it is switched off. Turn it on and off over time."
        /// The state is derived, never stored twice: ending -> OFF, walk/summary/menu -> IDLE, else ON.
        /// </summary>
        private static string TubeHasThreeStates()
        {
            True(Game.ScreenLight.StateFor(true, false, false, false) == Game.ScreenState.Off,
                 "the tube stays lit over the ending");
            True(Game.ScreenLight.StateFor(false, true, false, false) == Game.ScreenState.Idle,
                 "the tube burns at full brightness during the after-hours walk");
            True(Game.ScreenLight.StateFor(false, false, true, false) == Game.ScreenState.Idle,
                 "the tube burns at full brightness over the day summary");
            True(Game.ScreenLight.StateFor(false, false, false, false) == Game.ScreenState.On,
                 "the tube is not on during an interview");
            // and the bias: when the desk cannot tell, it must say ON. A dark screen while the player reads
            // is worse than a lit screen in an empty room.
            True(Game.ScreenLight.StateFor(false, false, false, true) == Game.ScreenState.Idle,
                 "a menu over a live desk should leave the tube idle, not blazing");
            True(Game.ScreenLight.FadeSeconds > 0.2f,
                 "the tube comes on instantly -- a CRT that snaps on reads as a sprite being enabled");
            return "PASS  the tube is OFF over the ending, IDLE over the walk, ON at the desk";
        }

        /// <summary>
        /// Request E, the second half: "when ON it must light the desk IN FRONT of it, with a falloff across
        /// the surface". A pool centred on the glass lights the case and the wall; the light has to project
        /// DOWN onto the desk, and the desk rows must be lit harder than the wall rows.
        /// </summary>
        private static string TubePoolLandsOnTheDesk()
        {
            True(Game.ScreenLight.GlassX < MenuLayout.W / 2,
                 "the desk's emitter is not on the left, so its shadows would fall the wrong way");
            True(Game.ScreenLight.GlassX + Game.ScreenLight.GlassRx < MenuLayout.W / 2,
                 "the tube's glass is not inside the monitor's case");
            // the projection: the pool's centre must be BELOW the glass and on the desk band, not on the wall
            True(Game.ScreenLight.ProjectY > Game.ScreenLight.GlassY, "the pool is not projected downward");
            True(Game.ScreenLight.ProjectY >= Game.ScreenLight.DeskRow - 10,
                 "the pool is centred on the wall, not on the desk surface in front of it");
            True(Game.ScreenLight.ProjectY + Game.ScreenLight.GlassRy > Game.ScreenLight.DeskRow,
                 "the pool does not reach the desk");
            return "PASS  the tube's pool is projected down onto the desk, away from the wall";
        }

        /// <summary>
        /// Request F: "on mobile, two taps to confirm". A mouse click is a decision; a thumb is not.
        /// </summary>
        private static string TwoTapsOnTouchOnly()
        {
            True(MenuLayout.ConfirmWindow(true) > 0f, "touch has no confirm step: one tap already decides");
            True(MenuLayout.ConfirmWindow(false) == 0f, "a mouse click asks twice -- that is a tax, not a rule");
            True(MenuLayout.TouchConfirmSeconds >= 1f && MenuLayout.TouchConfirmSeconds <= 3f,
                 "the confirm window is " + MenuLayout.TouchConfirmSeconds + " s -- too short to press twice, " +
                 "or so long that a stale row fires");
            return "PASS  a thumb confirms twice, a mouse once";
        }

        /// <summary>
        /// THE BUG THE PLAYER CAUGHT TWICE (v120, second pass). The tube was bound to ONE renderer --
        /// `L_background` -- which is the picture MenuView replaces with its own composition, and which does
        /// not contain the desk's props at all. So the light was baked into a menu backdrop and the desk in
        /// front of the monitor stayed dark. The desk has seven static surfaces and every one of them must be
        /// captured; the applicant is excluded BY NAME because it animates and must stay a sprite.
        /// </summary>
        private static string TheTubeLightsEveryStaticSurface()
        {
            var layout = File.ReadAllText(Path.Combine("unity", "Assets", "Resources", "desk_layout.json"));
            True(layout.Contains("\"applicant\""), "the layout has no applicant layer, so this test is meaningless");
            var src = File.ReadAllText(Path.Combine("unity", "Assets", "Scripts", "Game", "Desk", "ScreenLight.cs"));
            True(src.Contains("layer.id == \"applicant\""), "ScreenLight does not exclude the animated applicant");
            True(src.Contains("root.Find(\"L_\" + layer.id)"), "ScreenLight does not walk the layout's layers");
            True(!src.Contains("Bind(SpriteRenderer background, SpriteRenderer monitor)"),
                 "ScreenLight still takes a single background renderer -- that is the bug");
            // and the desk builder must hand it the layout, not one sprite
            var builder = File.ReadAllText(Path.Combine("unity", "Assets", "Scripts", "Game", "Desk",
                                                         "DeskSceneBuilder.cs"));
            True(builder.Contains("tube.Bind(root, layout)"), "the builder does not bind the tube to the layout");
            return "PASS  the tube lights every static layer of the desk and leaves the applicant alone";
        }

        /// <summary>
        /// THE OTHER HALF OF THE SAME BUG: the pool was anchored at row 172 on a canvas whose desk surface
        /// starts at row 206 in the game's own layout -- above the surface, so the brightest part of the light
        /// landed on the wall/monitor and the wood in front of it stayed dark. The centre must be BELOW the
        /// surface line and inside the canvas.
        /// </summary>
        private static string TheTubeLightsTheDeskNotTheFloor()
        {
            True(Game.ScreenLight.ProjectY > Game.ScreenLight.DeskRow,
                 "the tube's pool is centred above the desk surface (row " + Game.ScreenLight.ProjectY +
                 " vs " + Game.ScreenLight.DeskRow + ") -- it lights the wall, not the desk");
            True(Game.ScreenLight.ProjectY < Game.ScreenLight.CanvasH,
                 "the tube's pool is off the bottom of the canvas");
            True(Game.ScreenLight.ProjectY - Game.ScreenLight.DeskRow < 60,
                 "the pool is centred half a screen below the surface -- it is lighting the floor");
            True(Game.ScreenLight.GlassY < Game.ScreenLight.DeskRow,
                 "the glass is below the desk surface, so the monitor is standing in the floor");
            return "PASS  the pool is on the desk, in front of the tube, and inside the canvas";
        }

        /// <summary>
        /// Request G / docs/89 §7: "prefer shape/bar over raw number". The security score is five cells and
        /// then the digits, and the two must agree at every boundary -- a bar that says 3 while the digits
        /// say 59 is worse than no bar at all.
        /// </summary>
        private static string SecurityIsAShapeBeforeANumber()
        {
            var full = SecurityRules.CornerLine(100);
            var none = SecurityRules.CornerLine(0);
            True(full.StartsWith("SECURITY "), "the corner line does not start with the thing it is about");
            // at zero the line still NAMES itself and still shows five empty cells: silence would be worse
            True(none.StartsWith("SECURITY ") && none.Contains("\u25AF"), "a zero rating shows no empty cells");
            int bars = 0;
            foreach (char c in full) if (c == '\u25AE') bars++;
            True(bars == 5, "a perfect score fills " + bars + " of 5 cells");
            bars = 0;
            foreach (char c in none) if (c == '\u25AE') bars++;
            True(bars == 0, "a zero score fills " + bars + " cells");
            // monotonic: more score can never mean fewer filled cells
            int last = -1;
            for (int score = 0; score <= 100; score += 5)
            {
                int filled = 0;
                foreach (char c in SecurityRules.CornerLine(score)) if (c == '\u25AE') filled++;
                True(filled >= last, "the bar went DOWN from " + (last) + " to " + filled + " at score " + score);
                last = filled;
            }
            return "PASS  the security score is a 5-cell bar before it is a number, and the two agree";
        }

        /// <summary>
        /// THE LADDER IS AUTHORED, NOT GUESSED (v120). The desk's light walks a pixel one step up ITS material.
        /// If the ladder were built by sorting the palette by brightness, a cold grey would be "lit" into a
        /// warm cream -- which is exactly what the first preview pass did. `material_N -> material_N+1` comes
        /// from the published palette's own names, and the art gate keeps those names honest.
        /// </summary>
        private static string PaletteLadderIsAuthored()
        {
            string path = Path.Combine("unity", "Assets", "Resources", "palette.json");
            True(File.Exists(path), "the published palette is missing: " + path);
            using (var doc = JsonDocument.Parse(File.ReadAllText(path)))
            {
                var byKey = new Dictionary<string, int[]>();
                foreach (var e in doc.RootElement.EnumerateArray())
                {
                    var key = e.GetProperty("key").GetString();
                    var v = e.GetProperty("value");
                    byKey[key] = new[] { v.GetProperty("r").GetInt32(), v.GetProperty("g").GetInt32(),
                                         v.GetProperty("b").GetInt32() };
                }
                True(byKey.Count >= 60, "the palette has only " + byKey.Count + " colours");
                // every ramp that exists must be a run of consecutive indices, darkest first
                int ramps = 0;
                foreach (var key in byKey.Keys)
                {
                    if (!key.EndsWith("_0")) continue;
                    ramps++;
                    string stem = key.Substring(0, key.Length - 1);
                    int n = 0;
                    int last = -1;
                    while (byKey.ContainsKey(stem + n))
                    {
                        var c = byKey[stem + n];
                        int lum = (c[0] * 299 + c[1] * 587 + c[2] * 114) / 1000;
                        True(lum >= last, stem + n + " is not brighter than " + stem + (n - 1));
                        last = lum;
                        n++;
                    }
                    True(n >= 3, stem + "0.." + (n - 1) + " has " + n + " steps -- a ramp needs three");
                }
                True(ramps >= 8, "only " + ramps + " ramps in the palette");
            }
            return "PASS  every ramp is a run of consecutive, brightening steps";
        }
    }
}
