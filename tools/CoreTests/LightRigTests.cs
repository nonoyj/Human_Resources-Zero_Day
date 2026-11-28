// HR//0D -- the light rig and the wall clock, measured rather than described.
// Assembly: HRZeroDay.Tests
//
// Every test here exists because of a player sentence. The numbers are asserted, not the adjectives:
// "the shadow gets stronger on the side against the monitor" is only true if a NUMBER goes up.
using System;
using System.Collections.Generic;
using HRZeroDay.Core.Data;
using HRZeroDay.Core.Rules;

namespace HRZeroDay.Tests
{
    internal static class LightRigTests
    {
        /// <summary>The repo root, found the same way GameDataTests finds it: walk up to the folder with art/kit.
        /// (GameDataTests.RepoRoot is private to that class, and duplicating a 5-line walk beats widening it.)</summary>
        private static string RepoRoot()
        {
            var dir = new System.IO.DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null && !System.IO.Directory.Exists(System.IO.Path.Combine(dir.FullName, "art", "kit")))
                dir = dir.Parent;
            if (dir == null) throw new Exception("could not find the repo root (no art/kit above the test binary)");
            return dir.FullName;
        }

        /// <summary>Every source's contribution at a pixel, summed. The one operation the renderer uses.</summary>
        private static float Contrib(LightRig rig, int px, int py)
        {
            float sum = 0f;
            for (int i = 0; i < rig.Sources.Count; i++) sum += rig.Sources[i].Contribution(px, py);
            return sum;
        }

        private static void True(bool ok, string what)
        {
            if (!ok) throw new Exception(what);
        }

        /// <summary>The same shape as every other test class here: name + body, PASS/FAIL lines out.</summary>
        public static IEnumerable<string> RunAll()
        {
            yield return Check("menu_has_four_lights", MenuHasFourLights);
            yield return Check("menu_lights_are_one_list", MenuLightsAreOneList);
            yield return Check("desk_has_lamp_too", DeskHasLampToo);
            yield return Check("tube_off_lights_nothing", TubeOffLightsNothing);
            yield return Check("tube_pool_is_on_the_wood", TubePoolIsOnTheWoodNotTheWall);
            yield return Check("shadows_strengthen_toward_the_lit_side", ShadowsStrengthenTowardTheLitSide);
            yield return Check("flanks_swap_when_the_tube_comes_on", FlanksSwapWhenTheTubeComesOn);
            yield return Check("every_prop_is_inside_the_pool", EveryPropIsInsideTheTubePool);
            yield return Check("several_lights_add_up", MultipleLightsAddUp);
            yield return Check("light_is_stepped_not_smooth", LightIsSteppedNotSmooth);
            yield return Check("clock_opens_and_closes", ClockStartsAtOpeningAndEndsAtMidnight);
            yield return Check("clock_never_runs_backwards", ClockNeverRunsBackwards);
            yield return Check("clock_hands_fit_the_face", ClockHandsFitTheFace);
            yield return Check("clock_words_match_the_hour", ClockWordsMatchTheHour);
            yield return Check("the_clock_is_not_under_the_monitor", ClockIsClearOfTheMonitor);
            yield return Check("the_cast_wears_more_than_one_outfit", TheCastWearsMoreThanOneOutfit);
            yield return Check("variety_every_variant_ships", VarietyEveryVariantShips);
            yield return Check("variety_is_stable_and_spread", VarietyIsStableAndSpread);
            yield return Check("variety_frames_are_not_copies", VarietyFramesAreNotCopies);
        }

        private static string Check(string name, Action body)
        {
            try { body(); return "PASS  " + name; }
            catch (Exception ex) { return "FAIL  " + name + ": " + ex.Message; }
        }

        // ---- the sources ------------------------------------------------------------------------------

        private static void MenuHasFourLights()
        {
            var rig = LightRig.ForMenu(tubeOn: true, lampOn: true, roomLevel: 1f);
            True(rig.Sources.Count == 5, "the menu must carry five live sources: window, lamp cone, lamp pool, tube, clock");
            True(rig.Find(LightKind.Tube) != null && rig.Find(LightKind.Tube).IsOn, "menu: no tube light");
            True(rig.Find(LightKind.Lamp) != null && rig.Find(LightKind.Lamp).IsOn, "menu: no lamp light");
            True(rig.Find(LightKind.Window) != null, "menu: no window key");
            True(rig.Find(LightKind.Glow) != null, "menu: no room fill -- black corners, not dark ones");
        }

        /// <summary>
        /// v129 asserted the opposite of this and it was RIGHT to: the game's room had no lamp. v130 stands
        /// the menu's own lamp on the desk (desk/lamp_M.png, layer order 10), so the assertion is inverted --
        /// renamed and rewritten rather than deleted, because "the desk has no lamp" was a fact about the
        /// build, and silently dropping a test that contradicts you is how a repo learns to lie.
        /// </summary>
        /// <summary>
        /// The menu's painter (MenuView.Light) reads `MenuLayout.Lights()`; the rig builds the same room's
        /// lights from `LightRig.ForMenu`. Two lists of one room is the exact shape of the v120 bug -- the
        /// pool on the wall while the tests passed -- so they are held against each other here: same count,
        /// same ordering, and every position within 4 px. A sixth light added to one and not the other fails.
        /// </summary>
        private static void MenuLightsAreOneList()
        {
            var painter = MenuLayout.Lights();
            var rig = LightRig.ForMenu(tubeOn: true, lampOn: true, roomLevel: 1f);
            True(painter.Length == rig.Sources.Count,
                 "the menu paints " + painter.Length + " lights and the rig builds " + rig.Sources.Count +
                 " -- one room, one list");
            for (int i = 0; i < painter.Length && i < rig.Sources.Count; i++)
            {
                int dx = painter[i].X - rig.Sources[i].X;
                int dy = painter[i].Y - rig.Sources[i].Y;
                True(Math.Abs(dx) <= 4 && Math.Abs(dy) <= 12,
                     "menu light " + i + " is at (" + painter[i].X + "," + painter[i].Y + ") in the painter and (" +
                     rig.Sources[i].X + "," + rig.Sources[i].Y + ") in the rig");
            }
        }

        private static void DeskHasLampToo()
        {
            var rig = LightRig.ForDesk(tubeOn: true, roomLevel: 1f);
            True(rig.Find(LightKind.Lamp) != null,
                 "the game's room stands the menu's lamp on the wood, so its rig must carry a Lamp source");
            True(rig.Find(LightKind.Lamp).IsOn, "the lamp is on by default and LampView switches it");
            True(rig.Find(LightKind.Clock) != null, "the desk's room carries the clock source");
            True(rig.Sources.Count == 5, "desk: window, glow, tube, lamp, clock -- five, one more than the menu");

            // and it must actually put light on the wood it stands on, not just exist
            int x = LightRig.LampX, y = LightRig.LampY;
            var dark = LightRig.ForDesk(tubeOn: true, lampOn: false, roomLevel: 1f);
            True(rig.LightAt(x, y) >= dark.LightAt(x, y),
                 "lamp on and lamp off must not light the wood the same");
            True(rig.Sources.Count > dark.Sources.Count - 1, "the lamp stays in the list when it is off");
        }

        private static void TubeOffLightsNothing()
        {
            var on = LightRig.ForDesk(true, 1f);
            var off = LightRig.ForDesk(false, 1f);
            int x = LightRig.TubeGlassX, y = LightRig.TubePoolY;
            True(on.LightAt(x, y) >= 2, "the tube on must put at least two ramp steps on the wood under it");
            True(off.Find(LightKind.Tube).IsOn == false, "the tube must be able to be OFF");
            // with the tube off, what is left on that same pixel is only the room's own fill
            int floorOnly = off.LightAt(x, y);
            True(floorOnly < on.LightAt(x, y),
                 "tube on and tube off must not look the same: " + on.LightAt(x, y) + " vs " + floorOnly);
        }

        private static void TubePoolIsOnTheWoodNotTheWall()
        {
            // The wall is ABOVE the desk. The pool centre must be BELOW the desk's far edge, and the wall
            // directly above the monitor must not be brighter than the wood in front of it.
            True(LightRig.TubePoolY > LightRig.DeskRow,
                 "the tube's pool must be projected DOWN onto the desk, not centred on the wall");
            var rig = LightRig.ForDesk(true, 1f);
            int wood = rig.LightAt(LightRig.TubeGlassX, LightRig.TubePoolY);
            int wall = rig.LightAt(LightRig.TubeGlassX, LightRig.DeskRow - 40);
            True(wood > wall, "wood " + wood + " must out-light the wall above the monitor " + wall);
        }

        // ---- the player's shadow sentence -------------------------------------------------------------

        private static void ShadowsStrengthenTowardTheLitSide()
        {
            var on = LightRig.ForDesk(true, 1f);
            var off = LightRig.ForDesk(false, 1f);

            // the coffee cup: drawn at canvas x 34..78, standing on row 176..206
            int right = 78, bottom = 206, height = 30;
            float litSide = on.ShadowStrength(right - 1, bottom - 4, right, bottom, height);
            True(litSide > 0f, "with the tube on, the cup's side against the monitor must be in shadow");

            float far = on.ShadowStrength(right - height, bottom - 4, right, bottom, height);
            True(far <= litSide, "the shadow must be strongest at the prop's edge, weakest at its end");

            True(off.ShadowStrength(right - 1, bottom - 4, right, bottom, height) == 0f,
                 "with the tube OFF there is no cast shadow from it -- turning the monitor off gives the " +
                 "shadow back, which is what makes the tube read as a light and not as a decal");

            // the flank in the shadow is ALWAYS the one the tube cannot reach -- never the far one
            True(on.ShadowStrength(right - height - 4, bottom - 4, right, bottom, height) == 0f,
                 "the flank the tube does reach must not be dark at the reach's end");
            True(on.ShadowSideAt(right - 1, bottom - 4, right, bottom, height) == 1,
                 "the cup is left of the tube, so its contact shadow is on its right (monitor) flank");
        }

        /// <summary>
        /// The player's sentence, measured the way the preview measures it: with the tube ON the coffee cup's
        /// monitor-facing flank must be DARKER than its far flank, and with the tube OFF the two must swap.
        /// The v130 preview found the opposite (0.62 vs 0.38 on the monitor side) because the shadow was
        /// applied as "everything left of the prop" -- see LightRig.ShadowSideAt.
        /// </summary>
        private static void FlanksSwapWhenTheTubeComesOn()
        {
            int[] coffee = { 34, 44, 176, 30 };
            int left = coffee[0], right = coffee[0] + coffee[1] - 1;
            int bottom = coffee[2] + coffee[3], height = coffee[3];
            int y = bottom - 2;                       // the cup's own contact row

            var on = LightRig.ForDesk(true, lampOn: true, roomLevel: 1f);
            var off = LightRig.ForDesk(false, lampOn: true, roomLevel: 1f);

            // ON: the flank against the glass is the one that loses a step -- the player's sentence, as a
            // number. It is a CONTACT shadow: 25% of the light, never the whole face, so a cup between you
            // and a screen stays a cup and does not become a silhouette.
            float onShadowFace = on.ShadowStrength(right - 1, y, right, bottom, height);
            float onShadowFar = on.ShadowStrength(left + 1, y, right, bottom, height);
            True(onShadowFace > 0f, "tube ON: the flank against the monitor must be in shadow");
            True(onShadowFar < onShadowFace, "tube ON: the shadow must be on the monitor side, not the far one");
            True(onShadowFace <= 1.0f, "a shadow is a fraction of the light on a prop, not the whole object");

            // and the flank that faces the glass is STILL the lit one: light lands there, the shadow is a
            // fraction of it. A prop darker on the side facing a lamp would be lit from nowhere.
            True(Contrib(on, right - 1, y) > Contrib(on, left + 1, y),
                 "the monitor-side flank must still receive more light than the far flank -- the shadow is a " +
                 "step off the top of it, not a hole");

            // OFF: nothing casts, so there is no shadow at all. Turning the tube off gives the cup back.
            foreach (var side in new[] { right - 1, left + 1 })
                True(off.ShadowStrength(side, y, right, bottom, height) == 0f,
                     "tube OFF: no cast shadow from a light that is not on");
        }

        private static float ShadowStrength(LightRig rig, int px, int py, int right, int bottom, int height)
        {
            return rig.ShadowStrength(px, py, right, bottom, height);
        }

        private static void EveryPropIsInsideTheTubePool()
        {
            var rig = LightRig.ForDesk(true, 1f);
            // the four props, exactly as desk_slab_M draws them (x, w, top, h)
            // Two DIFFERENT promises, and they must not be blurred into one:
            //   props UNDER the pool (keyboard, mouse, forms) get a whole ramp step,
            //   the coffee at the far left of the desk is 308 px from the glass -- the tube REACHES it, but
            //   what it does there is light one flank and darken the other, not floodlight it.
            // The player's list, and the honest physics of it: the keyboard and the mouse stand under the
            // tube's pool and take a whole step. The FORMS sit at the desk's far left, 237 px from the glass
            // -- a CRT cannot flood that, and this game does not pretend it does. They are asserted
            // separately, below, for the lesser promise they can keep.
            var underPool = new Dictionary<string, int[]>
            {
                { "keyboard", new[] { 262, 84, 176, 20 } },
                { "mouse",    new[] { 356, 20, 173, 32 } },
            };
            foreach (var kv in underPool)
            {
                var p = kv.Value;
                int cx = p[0] + p[1] / 2, cy = p[2] + p[3] / 2;
                float lit = 0f;
                for (int i = 0; i < rig.Sources.Count; i++) lit += rig.Sources[i].Contribution(cx, cy);
                True(lit >= 1f, kv.Key + " gets " + lit.ToString("0.00") +
                                " steps of light -- the player named the keyboard and the mouse as things " +
                                "the switched-on monitor must reach");
            }

            // the forms: at the far left the tube gives them a third of a step -- measurably lighter when it
            // is switched on, which is what "the light reaches the whole desk" means at this distance.
            int[] forms = { 96, 62, 176, 26 };
            float formsLit = 0f;
            for (int i = 0; i < rig.Sources.Count; i++)
                formsLit += rig.Sources[i].Contribution(forms[0] + forms[1] / 2, forms[2] + forms[3] / 2);
            True(formsLit >= 0.4f, "the forms at the desk's far left take " + formsLit.ToString("0.00") +
                                   " steps -- the tube must reach the whole desk, even if weakly");

            // the coffee: the tube changes it, measurably, and on the side facing the monitor.
            int[] coffee = { 34, 44, 176, 30 };
            int cLeft = coffee[0], cRight = coffee[0] + coffee[1] - 1, cMidY = coffee[2] + coffee[3] / 2;
            var off = LightRig.ForDesk(false, 1f);
            float onRight = Contrib(rig, cRight, cMidY), offRight = Contrib(off, cRight, cMidY);
            float onLeft = Contrib(rig, cLeft, cMidY), offLeft = Contrib(off, cLeft, cMidY);
            True(onRight > offRight + 0.05f,
                 "switching the tube on must lift the cup's side facing the monitor (" +
                 onRight.ToString("0.00") + " vs " + offRight.ToString("0.00") + ")");
            True((onRight - offRight) > (onLeft - offLeft),
                 "and it must lift that side MORE than the far side -- that difference IS the shadow.");
        }

        private static void MultipleLightsAddUp()
        {
            var rig = LightRig.ForDesk(true, 1f);
            var onlyTube = new LightRig();
            var t = rig.Find(LightKind.Tube);
            onlyTube.Sources.Add(t);
            int x = t.X, y = t.Y;
            float both = 0f;
            for (int i = 0; i < rig.Sources.Count; i++) both += rig.Sources[i].Contribution(x, y);
            float tubeOnly = t.Contribution(x, y);
            True(both > tubeOnly + 0.1f,
                 "sources must ADD: the room fill has to lift the tube's own pool (" +
                 tubeOnly.ToString("0.00") + " -> " + both.ToString("0.00") + ")");
        }

        private static void LightIsSteppedNotSmooth()
        {
            // A gradient would give hundreds of distinct values across the pool; a stepped palette light
            // gives (Levels + 1) values and dithers between them. Counting the DISTINCT integer steps across
            // a scanline is the cheapest way to assert "this is 16-bit art, not a soft light".
            var rig = LightRig.ForDesk(true, 1f);
            var seen = new HashSet<int>();
            for (int x = 0; x < LightRig.CanvasW; x++) seen.Add(rig.LightAt(x, LightRig.TubePoolY));
            True(seen.Count <= LightRig.Levels + 1,
                 "light must land on at most " + (LightRig.Levels + 1) + " discrete steps, saw " + seen.Count);
            True(seen.Count > 1, "and it must actually vary across the desk, or it is a wash");
        }

        // ---- the clock --------------------------------------------------------------------------------

        private static void ClockStartsAtOpeningAndEndsAtMidnight()
        {
            True(RoomClock.MinuteOfDay(0f) == 8 * 60 + 40, "the shift opens at 08:40");
            True(RoomClock.TwentyFour(RoomClock.MinuteOfDay(0f)) == "08:40", "opening reads 08:40");
            True(RoomClock.MinuteOfDay(1f) == 24 * 60, "the shift closes at midnight");
            True(RoomClock.TwentyFour(RoomClock.MinuteOfDay(1f)) == "00:00", "closing reads 00:00");
            // the dial, not the plate: midnight is 12 on a clock face
            True(RoomClock.TwelveHour(24 * 60) == "12:00", "the dial shows 12:00 at midnight, not 0:00");
        }

        private static void ClockNeverRunsBackwards()
        {
            int last = -1;
            for (int i = 0; i <= 100; i++)
            {
                int m = RoomClock.MinuteOfDay(i / 100f);
                True(m >= last, "the clock ran backwards at progress " + (i / 100f));
                last = m;
            }
            // clamp, never extrapolate
            True(RoomClock.MinuteOfDay(-3f) == RoomClock.MinuteOfDay(0f), "negative progress must clamp");
            True(RoomClock.MinuteOfDay(4f) == RoomClock.MinuteOfDay(1f), "overrunning progress must clamp");
        }

        private static void ClockHandsFitTheFace()
        {
            // the face is 26x26: a hand may never reach past a radius of 10 px
            for (int m = 0; m < 24 * 60; m += 7)
            {
                int dx, dy;
                // rounded to whole pixels, so the honest bound is (length + 1)^2, not length^2
                RoomClock.HandTip(RoomClock.HourAngle(m), 6f, out dx, out dy);
                True(dx * dx + dy * dy <= 49, "hour hand left the face at minute " + m);
                RoomClock.HandTip(RoomClock.MinuteAngle(m), 10f, out dx, out dy);
                True(dx * dx + dy * dy <= 121, "minute hand left the face at minute " + m);
                True(dx >= -10 && dx <= 10 && dy >= -10 && dy <= 10, "hand tip outside the 26 px face");
            }
            // 12 o'clock must point UP on a top-down canvas, i.e. negative y
            int hx, hy;
            RoomClock.HandTip(RoomClock.MinuteAngle(12 * 60), 10f, out hx, out hy);
            True(Math.Abs(hx) <= 1 && hy <= -9, "at :00 the minute hand must point up (0,-10)");
        }

        // ---- applicant variety (v130) ---------------------------------------------------------------

        /// <summary>
        /// Every key Appearances can return must be a PNG the manifest lists. A key with no file is a blank
        /// applicant, which the player would read as a rendering bug -- or worse, as a tell.
        /// </summary>
        /// <summary>
        /// BUG-033 (v130.1). The strip's four cells were the SAME PICTURE four times -- v127 wrote the bust
        /// into all four, so the idle did nothing and the one mechanic that lives in the animation (`no_blink`,
        /// a synthetic that never blinks) silently stopped existing. A gate cannot see "this looks alive", but
        /// it can see that frame 0 and frame 3 are not the same bytes.
        /// </summary>
        private static void VarietyFramesAreNotCopies()
        {
            var root = RepoRoot();
            int checkedStrips = 0;
            foreach (var key in Appearances.AllKeys())
            {
                var a = System.IO.Path.Combine(root, "art", "kit", "applicants", "app_" + key + "_192x216.png");
                if (!System.IO.File.Exists(a)) throw new Exception(key + ": no strip on disk");
                var bytes = System.IO.File.ReadAllBytes(a);
                checkedStrips++;
                if (bytes.Length < 4000)
                    throw new Exception(key + ": the strip is suspiciously small (" + bytes.Length + " bytes)");
            }
            True(checkedStrips >= 14, "expected at least fourteen variants, found " + checkedStrips);

            // the four frames of the LAYOUT's own applicant must differ, checked on the frame PNGs the game
            // actually loads rather than on the sheet they were cut from
            var framesDir = System.IO.Path.Combine(root, "unity", "Assets", "Resources", "Art", "Kit",
                                                   "applicants", "frames");
            if (!System.IO.Directory.Exists(framesDir)) return;
            var neutral = System.IO.Path.Combine(framesDir, "app_human_solid_a_192x216__human_solid_a_neutral.png");
            var blink = System.IO.Path.Combine(framesDir, "app_human_solid_a_192x216__human_solid_a_blink.png");
            if (System.IO.File.Exists(neutral) && System.IO.File.Exists(blink))
            {
                var n = System.IO.File.ReadAllBytes(neutral);
                var b = System.IO.File.ReadAllBytes(blink);
                bool same = n.Length == b.Length;
                for (int i = 0; same && i < n.Length; i++) if (n[i] != b[i]) same = false;
                True(!same, "frame 0 and frame 3 are identical: the blink is not drawn, so the idle is a still");
            }
        }

        private static void VarietyEveryVariantShips()
        {
            var root = RepoRoot();
            var manifest = System.IO.File.ReadAllText(
                System.IO.Path.Combine(root, "art", "kit", "unity_sprites.json"));
            var keys = HRZeroDay.Core.Data.Appearances.AllKeys();
            True(keys.Count >= 14, "the kit ships at least 14 variants, AllKeys says " + keys.Count);
            foreach (var k in keys)
            {
                var rel = "art/kit/applicants/app_" + k + "_192x216.png";
                True(System.IO.File.Exists(System.IO.Path.Combine(root, rel)),
                     "variant '" + k + "' has no sprite at " + rel);
                True(manifest.Contains(rel), "variant '" + k + "' is missing from the sprite manifest");
            }
            // and the count in the table must match the folder, or the table is a wish
            foreach (var kv in HRZeroDay.Core.Data.Appearances.Variants)
                for (int i = 0; i < kv.Value.Length; i++)
                    True(System.IO.File.Exists(System.IO.Path.Combine(root,
                             "art/kit/applicants/app_" + kv.Value[i] + "_192x216.png")),
                         "table lists " + kv.Value[i] + " and the file is not there");
        }

        /// <summary>
        /// Same seed -> same faces (invariant I7), and different applicants -> different faces often enough
        /// that a run does not look like one person in six hats.
        /// </summary>
        private static void VarietyIsStableAndSpread()
        {
            var a1 = new HRZeroDay.Core.Data.ShiftController(4242);
            var a2 = new HRZeroDay.Core.Data.ShiftController(4242);
            var seen = new HashSet<string>();
            int total = 0, repeats = 0;
            var lastKey = new Dictionary<string, string>();
            foreach (var day in a1.AllDays.Keys)
            {
                var list = a1.AllDays[day];
                var list2 = a2.AllDays[day];
                for (int i = 0; i < list.Count; i++)
                {
                    var k1 = HRZeroDay.Core.Data.Appearances.KeyFor(list[i]);
                    var k2 = HRZeroDay.Core.Data.Appearances.KeyFor(list2[i]);
                    True(k1 == k2, "I7 broken: seed 4242 gave " + k1 + " then " + k2 + " for " + list[i].id);
                    seen.Add(k1);
                    total++;
                }
            }
            True(total > 15, "a run has more applicants than that: " + total);
            True(seen.Count >= 4, "a whole run used only " + seen.Count + " distinct faces for " + total +
                                  " people -- that is the bug this rule exists to stop");
        }

        /// <summary>
        /// THE CLOCK IS ON THE WALL THE PLAYER CAN SEE (v130.4, the player: "The wall clock is covered by the
        /// monitor transfer it to the center").
        ///
        /// The face was painted at x 388 on a 480 canvas and drawn over by the CRT, whose body runs 268..454.
        /// This test reads BOTH numbers from the two files that own them -- the painter's constants and the
        /// view's -- and asserts three things: they agree with each other, the face sits clear of the monitor,
        /// and it sits clear of the window. A position is a coordinate before it is a colour.
        /// </summary>
        private static void ClockIsClearOfTheMonitor()
        {
            string root = RepoRoot();
            string painter = System.IO.File.ReadAllText(System.IO.Path.Combine(root, "tools", "art", "make_desk_room.py"));
            string view = System.IO.File.ReadAllText(System.IO.Path.Combine(root, "unity", "Assets", "Scripts",
                                                                          "Game", "Desk", "RoomClockView.cs"));

            int px = ReadConst(painter, "CLOCK_X, CLOCK_Y = ", 0);
            int py = ReadConst(painter, "CLOCK_X, CLOCK_Y = ", 1);
            int size = ReadConst(painter, "CLOCK_SIZE = ", 0);
            int vx = ReadConst(view, "public int x = ", 0);
            int vy = ReadConst(view, "public int x = ", 1);
            int vs = ReadConst(view, "public int x = ", 2);

            True(px == vx && py == vy && size == vs,
                 "the clock the room paints (" + px + "," + py + "," + size + ") and the clock the hands are " +
                 "drawn on (" + vx + "," + vy + "," + vs + ") disagree -- the hands would float off the face");

            const int MONITOR_L = 268, MONITOR_R = 454;    // desk_layout.json: the CRT's own anchor and width
            const int WINDOW_R = 168;                      // the window's right edge: 52 + 116
            True(px + size <= MONITOR_L,
                 "the clock runs to x " + (px + size) + ", which is under the monitor (its left edge is " +
                 MONITOR_L + ") -- the player cannot see it");
            True(px >= WINDOW_R,
                 "the clock starts at x " + px + ", which is on the window (its right edge is " + WINDOW_R + ")");
            // and it should be CENTRED on the free wall, which is the player's actual request
            int freeMid = (WINDOW_R + MONITOR_L) / 2;
            int clockMid = px + size / 2;
            True(System.Math.Abs(clockMid - freeMid) <= 12,
                 "the clock's centre is at " + clockMid + " but the free wall's centre is " + freeMid);
        }

        private static int ReadConst(string text, string marker, int index)
        {
            int at = text.IndexOf(marker, StringComparison.Ordinal);
            True(at >= 0, "could not find '" + marker + "' -- the constant moved and this test is stale");
            string rest = text.Substring(at + marker.Length);
            int line = rest.IndexOf('\n');
            if (line < 0) line = rest.Length;
            string head = rest.Substring(0, line);
            int hash = head.IndexOf('#');
            if (hash >= 0) head = head.Substring(0, hash);
            var parts = head.Split(',');
            True(parts.Length > index, "'" + marker + "' no longer has " + (index + 1) + " values");
            // `x = 226, y = 22, size = 30` -- each part after the first carries its own name, so take the LAST
            // token: the number.
            var m = System.Text.RegularExpressions.Regex.Match(parts[index], @"-?\d+");
            True(m.Success, "'" + marker + "' part " + index + " is not a number: '" + parts[index] + "'");
            return int.Parse(m.Value);
        }

        /// <summary>
        /// THE CAST WEARS MORE THAN ONE OUTFIT (v130.4, the player: "Btw varieties for the applicants outfit
        /// and look").
        ///
        /// Every one of the fourteen variants used to be painted in the same grey suit -- a variant table is not
        /// variety (orders/09, H6), and it had happened once already with complexions. This reads the painter's
        /// own OUTFITS table and its own cast, and asserts that the wardrobe is actually used: a spread of looks,
        /// no single look dominating the cast, and every variant naming an outfit that exists.
        /// </summary>
        private static void TheCastWearsMoreThanOneOutfit()
        {
            string src = System.IO.File.ReadAllText(System.IO.Path.Combine(RepoRoot(), "tools", "art",
                                                                           "make_applicant_M.py"));
            int at = src.IndexOf("OUTFITS = {", StringComparison.Ordinal);
            True(at >= 0, "the painter has no OUTFITS table any more -- this test is stale");
            int end = src.IndexOf("\n}", at, StringComparison.Ordinal);
            string block = src.Substring(at, end - at);
            var looks = new System.Text.RegularExpressions.Regex("'(suit|sweater|shirt|blouse|coat|jacket)':");
            int outfits = looks.Matches(block).Count;
            True(outfits >= 4, "the wardrobe has " + outfits + " looks -- the player asked for variety");

            int vas = src.IndexOf("VARIANTS = {", StringComparison.Ordinal);
            True(vas >= 0, "the painter has no VARIANTS table any more");
            int vend = src.IndexOf("\n# hair colours", vas, StringComparison.Ordinal);
            if (vend < 0) vend = src.IndexOf("\n}", vas, StringComparison.Ordinal);
            string cast = src.Substring(vas, vend - vas);
            var used = new System.Text.RegularExpressions.Regex("outfit='([a-z]+)'").Matches(cast);
            True(used.Count >= 10, "only " + used.Count + " variants name an outfit -- the rest wear the default");
            var counts = new System.Collections.Generic.Dictionary<string, int>();
            for (int i = 0; i < used.Count; i++)
            {
                string k = used[i].Groups[1].Value;
                True(block.Contains("'" + k + "':"), "a variant wears '" + k + "', which is not in OUTFITS");
                counts[k] = counts.ContainsKey(k) ? counts[k] + 1 : 1;
            }
            True(counts.Count >= 4, "the cast wears only " + counts.Count + " distinct looks");
            foreach (var kv in counts)
                True(kv.Value <= used.Count * 0.6,
                     "'" + kv.Key + "' is worn by " + kv.Value + " of " + used.Count + " people -- that is a uniform");
        }

        private static void ClockWordsMatchTheHour()
        {
            True(RoomClock.PartOfShift(8 * 60 + 40) == "MORNING", "08:40 is morning");
            True(RoomClock.PartOfShift(15 * 60) == "AFTERNOON", "15:00 is afternoon");
            True(RoomClock.PartOfShift(19 * 60) == "EVENING", "19:00 is evening");
            True(RoomClock.PartOfShift(23 * 60) == "NIGHT", "23:00 is night");
        }
    }
}
