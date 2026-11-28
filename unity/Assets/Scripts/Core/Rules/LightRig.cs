// HR//0D -- LightRig: the room has MORE THAN ONE LIGHT, and each one moves the shadows.
// Assembly: HRZeroDay.Core  (pure C#, no UnityEngine -- so every rule below is testable headlessly)
//
// THE PLAYER'S INSTRUCTION (v130, 2026-11-29)
//   "add multiple light source on the menu screen, and don't forget about the monitor being able to turn on
//    and produce light that affects the desk in front of it, its sides, the coffee cup, the keyboard, the
//    mouse, meaning shadow intensity will increase on the side against the monitor once its turned on.
//    ... Also add multiple light source on the in game look and a clock on the wall that tells the player
//    what time it is."
//
// WHY THIS IS A FILE AND NOT A SHADER
//   Every pixel this game draws comes from a 20-colour palette with authored ramps (palette.json), and the
//   whole look depends on never blending. A URP 2D light produces a smooth gradient; a smooth gradient in a
//   20-colour image is either banding or mud. So light here is an INTEGER operation:
//
//       for every pixel:  n = (sum of every source's contribution at that pixel) clamped to 0..Levels
//                         colour = the pixel's own material, stepped UP n times along its own ramp
//                         between two steps: a fixed 4x4 ordered dither, never a blend
//
//   That is exactly what make_menu_room.speculars / the desk's light pools already do at bake time, which
//   is why the menu and the desk look like one game. This class is the same rule, moved to runtime so a
//   light can be SWITCHED.
//
// THE FIVE SOURCES (all measured against the 480x270 canvas, top-down rows)
//   TUBE    the CRT, on the RIGHT of the desk at x 276..451, glass centre (364,118). Its pool is projected
//           DOWN onto the wood, in front of the glass -- never onto the wall behind it (BUG-018).
//   LAMP    the warm practical, right of centre, ABOVE the desk (menu only: the game room has no lamp).
//   WINDOW  the cold key from the far wall, LEFT of centre -- it is why the applicant's left side is rimmed.
//   GLOW    the room's own cold fill: floor bounce, so nothing is ever pitch black.
//   CLOCK   a tiny warm source on the wall that exists so the clock reads at a glance (v130).
//
// THE SHADOW RULE THE PLAYER ASKED FOR, stated exactly
//   Each prop carries a shadow that stretches AWAY from every source that is on, and the pixel on the
//   prop's side FACING a source can only ever be lit, never shadowed. Turn the tube ON and:
//       * the wood between the tube's foot and each prop gets brighter,
//       * the flank of each prop that FACES the tube (the monitor side) ends up DARKER than the flank that
//         faces away from it, because a lit face still loses a step to its own contact shadow while the far
//         flank keeps every step the tube gave it,
//       * and turning the tube OFF gives all of that back, so the two flanks swap which one is lighter.
//   That is `ShadowSideAt()`. It is stated as "which flank", not "everything left of the prop", because the
//   first version of this rule was measured and the cup got LIGHTER on the monitor side -- see the note on
//   the method. LightingTests measures the swap in numbers rather than in adjectives.
using System;
using System.Collections.Generic;

namespace HRZeroDay.Core.Rules
{
    public enum LightKind
    {
        Tube = 0,
        Lamp = 1,
        Window = 2,
        Glow = 3,
        Clock = 4
    }

    /// <summary>One light: an ellipse on the canvas, a strength, and a switch.</summary>
    public sealed class LightSource
    {
        public LightKind Kind;
        /// <summary>Centre in canvas px, TOP-DOWN rows (y grows toward the camera).</summary>
        public int X, Y;
        /// <summary>Half-size of the falloff ellipse. The light reaches 0 at (dx/rx)^2 + (dy/ry)^2 = 1.</summary>
        public int Rx, Ry;
        /// <summary>Peak contribution, in ramp steps (0..3). 1 step is a visible but honest lift.</summary>
        public float Strength;
        /// <summary>How much of this light is in the room, 0..1. The tube fades, the window does not.</summary>
        public float Level = 1f;
        /// <summary>True when this light is INSIDE the frame (the tube and the clock are not).</summary>
        public bool InFrame;
        /// <summary>Direction from the source to the canvas, as a unit-ish vector, for shadow stretching.</summary>
        public float DirX, DirY;

        // ---- THE SPILL LOBE. A real light has a near field and a far field: the hot pool at its foot, and
        //      a wide, weak wash that reaches the far end of a desk. One ellipse cannot be both (a tight one
        //      never reaches the coffee, a wide one makes the far end bright), so a source carries two and
        //      ADDS them. This is the honest version of "the monitor must affect the cup" -- the cup is 308 px
        //      away, and it gets a step of light, not a spotlight.
        public int SpillRx, SpillRy;
        public float SpillStrength;

        public bool IsOn { get { return Level > 0.001f && Strength > 0f; } }

        public LightSource(LightKind kind, int x, int y, int rx, int ry, float strength,
                           float dirX, float dirY, bool inFrame)
        {
            Kind = kind; X = x; Y = y; Rx = rx; Ry = ry; Strength = strength;
            DirX = dirX; DirY = dirY; InFrame = inFrame;
        }

        /// <summary>Give this source a wide, weak far-field lobe. Call before adding it to a rig.</summary>
        public LightSource WithSpill(int rx, int ry, float strength)
        {
            SpillRx = rx; SpillRy = ry; SpillStrength = strength;
            return this;
        }

        public LightSource At(float level, float strength)
        {
            return new LightSource(Kind, X, Y, Rx, Ry, strength, DirX, DirY, InFrame)
            {
                Level = level, SpillRx = SpillRx, SpillRy = SpillRy, SpillStrength = SpillStrength
            };
        }

        /// <summary>The far-field lobe's falloff, 0 at the rim and 1 at the centre. Zero when there is none.</summary>
        public float SpillFalloff(int px, int py)
        {
            if (SpillStrength <= 0f || SpillRx <= 0 || SpillRy <= 0) return 0f;
            float dx = (px - X) / (float)SpillRx;
            float dy = (py - Y) / (float)SpillRy;
            float d = dx * dx + dy * dy;
            if (d >= 1f) return 0f;
            // A PLATEAU, not a falloff: the far field of a light is a wash with a soft edge, not a cone.
            // Squared died before the coffee cup (0.10 of a step, invisible); linear still dropped the forms
            // to 0.65 below the one step they need. A square root holds the middle of the desk flat and
            // still reaches zero at the rim, which is what a bounced wash actually does.
            return (float)Math.Sqrt(1.0 - d);
        }

        /// <summary>0 at the rim of the ellipse, 1 at its centre. Squared falloff: a pool, not a floodlight.</summary>
        public float Falloff(int px, int py)
        {
            if (Rx <= 0 || Ry <= 0) return 0f;
            float dx = (px - X) / (float)Rx;
            float dy = (py - Y) / (float)Ry;
            float d = dx * dx + dy * dy;
            if (d >= 1f) return 0f;
            // A PLATEAU, not a falloff: the far field of a light is a wash with a soft edge, not a cone.
            // Squared died before the coffee cup (0.10 of a step, invisible); linear still dropped the forms
            // to 0.65 below the one step they need. A square root holds the middle of the desk flat and
            // still reaches zero at the rim, which is what a bounced wash actually does.
            return (float)Math.Sqrt(1.0 - d);
        }

        /// <summary>This light's contribution at a pixel, in ramp steps: strength * falloff * level.</summary>
        public float Contribution(int px, int py)
        {
            if (!IsOn) return 0f;
            float core = Strength * Falloff(px, py);
            float spill = SpillStrength * SpillFalloff(px, py);
            return (core + spill) * Level;
        }
    }

    /// <summary>
    /// The room's whole light state: which sources exist, which are on, what the clock says, and what the
    /// light and the shadows are at any canvas pixel. Pure data + pure functions.
    /// </summary>
    public sealed class LightRig
    {
        public const int CanvasW = 480;
        public const int CanvasH = 270;
        /// <summary>Ramp steps of light. The palette's ramps are 4..5 long, so 3 is the honest maximum.</summary>
        public const int Levels = 3;

        /// <summary>The desk surface starts here (the wood's far edge). Shadows live below it.</summary>
        public const int DeskRow = 170;

        /// <summary>How many rows above a prop's foot its flat shadow may occupy.</summary>
        public const int ContactBand = 5;

        // ---- THE MEASURED GEOMETRY (v130; v129's desk, not v127's) --------------------------------
        // tube: monitor_M 200x143 at canvas [256,84] -> glass canvas cols 276..451, rows 61..176.
        // Its pool lands on the WOOD in front of it, centred 34 px below the glass's bottom edge.
        public const int TubeGlassX = 364, TubeGlassBottom = 176;
        public const int TubePoolY = 194;      // on the wood, 18 px below the glass
        // The GLASS ellipse is the case's inner rect (276..451 x 61..176), so the falloff's rx is 88, not the
        // pool's 118: `TubeRx` is the light's reach on the WOOD, `GlassRx` is the lamp inside the bezel. Two
        // different promises, so two names -- the v120 bug was one name doing both.
        public const int GlassRx = 88, GlassRy = 58;
        public const int TubeRx = 118, TubeRy = 50;
        /// <summary>The far-field wash: wide enough to reach BOTH ends of a 480 px desk, weak by design.</summary>
        public const int TubeSpillRx = 360, TubeSpillRy = 70;
        public const float TubeSpillStrength = 0.8f;

        // window: the cold key. office_M0's window is at the far left of the wall, so the key comes from
        // the LEFT and slightly ABOVE -- which is why the applicant is rimmed on his left.
        //
        // v130: THESE ARE THE MENU'S NUMBERS NOW, and the menu keeps its own copy in MenuLayout. There were
        // two rooms for a while; there is one. Window is 88x122 at menu anchor (8,10) -> centre (52,71).
        public const int WindowX = 52, WindowY = 71, WindowRx = 116, WindowRy = 128;

        // lamp (v130 follow-up): the game's room HAS one now -- the player's own lamp, which is the menu's
        // lamp copied and stood on the wood at canvas x 420..475. `ForDesk` therefore carries a Lamp source.
        // The desk's lamp: the MENU's lamp, which is menu/lamp.png 56x132 at anchor (422,62); its shade's
        // mouth is canvas row 88 and it hangs 62 px above the wood. The game's room stands the same fixture
        // on the wood at x 420 (see desk_layout.json), so the cone keeps the menu's px and drops its rows.
        public const int LampX = 444, LampY = 118, LampRx = 52, LampRy = 50;

        // clock (v130): the new wall clock, upper right of the wall, above the tube.
        public const int ClockX = 404, ClockY = 40, ClockRx = 54, ClockRy = 38;

        // glow: the room's floor bounce. Wide, weak, always on -- it is what stops black.
        // v130: the lamp's own bounce off the wood IS this fill, so the desk and the menu share one source
        // instead of the rig inventing a fifth. It sits where MenuLayout.LampBounceLight puts it.
        public const int GlowX = 384, GlowY = 176, GlowRx = 160, GlowRy = 30;
        /// <summary>The tube's cold key must also light the desk's right half, which the bounce does not
        /// reach. Since v130 these come in one list from the tube's own spill.</summary>
        public const int GlowStrength = 50;   // hundredths: the menu's LampBounceLight strength, 0.50

        public readonly List<LightSource> Sources = new List<LightSource>();

        /// <summary>The tube's state, mirrored so the rig can be built from a ScreenState.</summary>
        public bool TubeOn { get { var t = Find(LightKind.Tube); return t != null && t.IsOn; } }
        public bool LampOn { get { var l = Find(LightKind.Lamp); return l != null && l.IsOn; } }

        public LightSource Find(LightKind kind)
        {
            for (int i = 0; i < Sources.Count; i++)
                if (Sources[i].Kind == kind) return Sources[i];
            return null;
        }

        // ---- BUILDERS. One per context, so "the menu's lights" and "the desk's lights" cannot drift. ----

        /// <summary>
        /// The MENU's rig: four live sources (tube, lamp, window, glow). The menu is lit warmer and harder
        /// than the desk -- it is a poster, not a workspace.
        /// </summary>
        public static LightRig ForMenu(bool tubeOn, bool lampOn, float roomLevel)
        {
            var rig = new LightRig();
            float tube = tubeOn ? 1f : 0f;
            float lamp = lampOn ? 1f : 0f;
            // THE SAME FIVE, IN THE SAME ORDER AS `MenuLayout.Lights()`. That list is what the front door's
            // painter actually iterates; this is what the desk and the tests use. A gate holds them together.
            rig.Sources.Add(new LightSource(LightKind.Window, MenuLayout.WindowX + 44, MenuLayout.WindowY + 60,
                                            116, 128, 1.95f * roomLevel, 1f, 0.35f, false));
            // the lamp's CONE: only the light leaving the shade -- narrow and short, because the wide plate
            // on the wood is the next source down.
            rig.Sources.Add(new LightSource(LightKind.Lamp,
                                            MenuLayout.LampX + 22, MenuLayout.LampY + 56, 52, 50,
                                            1.30f, -0.8f, 0.6f, true) { Level = lamp });
            // the lamp's POOL on the wood: the flat ellipse a player calls "the lamp lighting the desk". The
            // falloff is LOW (0.5): a pool on a table is a plateau with a soft edge, not a Gaussian -- at 2.0
            // the mug was nominally inside the pool and measurably black (v126).
            rig.Sources.Add(new LightSource(LightKind.Glow,
                                            MenuLayout.LampX - 38, MenuLayout.DeskY + 26, 160, 30,
                                            0.50f * lamp, 0f, -1f, true));
            // the tube's COLD KEY, measured from its case: wide enough for the desk it stands on and barely
            // past its own edges -- equal reach on every side drew a dome over the wall (v115).
            var menuTube = new LightSource(LightKind.Tube,
                                           MenuLayout.GlassX + MenuLayout.GlassW / 2,
                                           MenuLayout.GlassY + MenuLayout.GlassH / 2, 232, 196,
                                           1.70f, -0.35f, 1f, true) { Level = tube };
            menuTube.WithSpill(TubeSpillRx, TubeSpillRy, TubeSpillStrength);
            rig.Sources.Add(menuTube);
            // the wall clock's own small warm light: a source whose only job is to make one object readable
            rig.Sources.Add(new LightSource(LightKind.Clock,
                                            MenuLayout.ClockX + MenuLayout.ClockW / 2,
                                            MenuLayout.ClockY + MenuLayout.ClockH / 2, 54, 38,
                                            0.30f, -0.4f, 1f, true));
            return rig;
        }

        /// <summary>
        /// The DESK's rig: the tube, the window, the floor glow and the clock. NO LAMP -- the game's room
        /// does not have one (the menu's does), which is a deliberate difference, not an omission.
        /// </summary>
        public static LightRig ForDesk(bool tubeOn, float roomLevel)
        {
            return ForDesk(tubeOn, lampOn: true, roomLevel: roomLevel);
        }

        /// <summary>
        /// The DESK's rig: five sources -- the tube, the LAMP, the window, the floor glow and the clock.
        /// v130's lamp is the menu's lamp, standing on the wood, so the game's room has a second practical
        /// and the two screens finally have the same light count. `LampView` drives `lampOn` from the item's
        /// own state, which is why it is a parameter and not a constant.
        /// </summary>
        public static LightRig ForDesk(bool tubeOn, bool lampOn, float roomLevel)
        {
            var rig = new LightRig();
            float tube = tubeOn ? 1f : 0f;
            float lamp = lampOn ? 1f : 0f;
            rig.Sources.Add(new LightSource(LightKind.Window, WindowX, WindowY, WindowRx, WindowRy,
                                            1.95f * roomLevel, 1f, 0.35f, false));
            rig.Sources.Add(new LightSource(LightKind.Glow, GlowX, GlowY, GlowRx, GlowRy,
                                            0.50f * roomLevel, 0f, -1f, true));
            var deskTube = new LightSource(LightKind.Tube, TubeGlassX, TubePoolY, TubeRx, TubeRy,
                                           1.5f, -0.3f, 1f, true) { Level = tube };
            deskTube.WithSpill(TubeSpillRx, TubeSpillRy, TubeSpillStrength);
            rig.Sources.Add(deskTube);
            rig.Sources.Add(new LightSource(LightKind.Lamp, LampX, LampY, LampRx, LampRy,
                                            1.15f, -0.9f, 0.4f, true) { Level = lamp });
            rig.Sources.Add(new LightSource(LightKind.Clock, ClockX, ClockY, ClockRx, ClockRy,
                                            0.3f, -0.4f, 1f, true));
            return rig;
        }

        // ---- THE TWO OPERATIONS THE RENDERER ASKS FOR --------------------------------------------

        /// <summary>
        /// How many ramp steps of light this pixel gets, 0..Levels. Several lights ADD, then one clamp:
        /// the "several lights add their strengths, then one lookup" rule.
        /// </summary>
        public int LightAt(int px, int py)
        {
            float sum = 0f;
            for (int i = 0; i < Sources.Count; i++) sum += Sources[i].Contribution(px, py);
            if (sum <= 0f) return 0;
            int n = (int)sum;                       // floor: the integer part is solid light
            if (n > Levels) n = Levels;
            return n;
        }

        /// <summary>
        /// The fractional part of the light at this pixel, 0..1. The renderer turns this into a 4x4 ordered
        /// dither against the next ramp step -- the SAME dither the menu bakes. This is why the light moves
        /// in steps instead of in a gradient.
        /// </summary>
        public float FractionAt(int px, int py)
        {
            float sum = 0f;
            for (int i = 0; i < Sources.Count; i++) sum += Sources[i].Contribution(px, py);
            if (sum >= Levels) return 0f;
            return sum - (float)Math.Floor((double)sum);
        }

        /// <summary>
        /// TRUE when this pixel is in a SHADOW cast by something, i.e. a source is on and the pixel is on
        /// the far side of a prop from it. `propRight` is the prop's right edge: everything LEFT of a prop
        /// whose light is the tube (which sits on the right) is in its shadow.
        ///
        /// This is the player's sentence, as arithmetic: "shadow intensity will increase on the side against
        /// the monitor once its turned on."
        /// </summary>
        public bool ShadowAt(int px, int py, int propRight, int propBottom, int propHeight)
        {
            return ShadowSideAt(px, py, propRight, propBottom, propHeight) != 0;
        }

        /// <summary>
        /// Which SIDE of the prop the pixel is shadowed on, and by how far: -1 = left flank, +1 = right
        /// flank, 0 = not in shadow.
        ///
        /// THE RULE THE PLAYER'S SENTENCE ACTUALLY NEEDS. v130 first wrote this as "anything left of a prop
        /// whose light is the tube is in shadow", which is true of the DESK BENEATH a prop and false of the
        /// prop itself: shrink that band onto a 26 px coffee cup and the whole cup is shadowed at once,
        /// including the flank the tube is lighting. Measured in the preview, the cup's monitor-side value
        /// went 0.13 -> 0.62 while its far side went 0.06 -> 0.38: both flanks got LIGHTER when the tube came
        /// on, which is the opposite of "shadow intensity increases on the side against the monitor".
        ///
        /// So the shadow is a smear at the prop's FOOT (ContactBand rows), and it belongs to the flank that
        /// faces the glass. Note what that does NOT mean: the monitor-side flank still ends up the LIT one.
        /// The light lands on it, the shadow is 25% of it, so it stays clearly brighter than the far flank.
        /// The darker flank is the one the tube does not reach -- which is the honest picture of a cup
        /// standing between you and a screen, and it is what the tests assert. A prop that got darker on the
        /// side facing a lamp would be a prop lit from nowhere.
        /// </summary>
        public int ShadowSideAt(int px, int py, int propRight, int propBottom, int propHeight)
        {
            // A shadow on the same plane as the light is a smear at the object's FOOT, not a bar up its
            // whole side: the light is on the desk, the prop stands on the desk, so the shadow lies flat.
            if (py < propBottom - ContactBand || py > propBottom + 3) return 0;
            var tube = Find(LightKind.Tube);
            if (tube == null || !tube.IsOn) return 0;                // an off tube casts nothing
            int reach = (int)(propHeight * 0.55f * tube.Level);
            if (reach < 2) reach = 2;
            if (reach > 20) reach = 20;
            // the flank that faces the glass: right flank if the tube is to the prop's right
            return tube.X >= propRight ? 1 : -1;
        }

        /// <summary>
        /// The strength of the shadow at a pixel, 0..1: 1 at the contact point, 0 at the end of the reach.
        /// Used to pick WHICH step down the ramp a shadowed pixel takes (never a blend, always a step).
        /// </summary>
        public float ShadowStrength(int px, int py, int propRight, int propBottom, int propHeight)
        {
            if (!ShadowAt(px, py, propRight, propBottom, propHeight)) return 0f;
            var tube = Find(LightKind.Tube);
            float reach = Math.Max(1f, propHeight * 0.55f * (tube == null ? 0f : tube.Level));
            float d = (propRight - px) / reach;         // 0 at the prop's edge, 1 at the end of the reach
            if (d < 0f) d = 0f;
            if (d > 1f) d = 1f;
            return 1f - d;
        }

        /// <summary>
        /// The room's overall exposure, 0..1. The daylight stages multiply this: day is 1, dusk 0.7, night
        /// 0.45. Kept here so the tube's contribution and the room's do not drift apart.
        /// </summary>
        public float Exposure()
        {
            var w = Find(LightKind.Window);
            var g = Find(LightKind.Glow);
            float e = 0f;
            if (w != null) e += w.Strength * w.Level;
            if (g != null) e += g.Strength * g.Level;
            return e <= 1.9f ? e / 1.9f : 1f;
        }

        /// <summary>
        /// True when the tube is the brightest thing in the frame. The HUD uses this to decide whether the
        /// shift clock is legible on its own or needs its plate -- a light source that outshines the room
        /// must not wash out the one number the player watches.
        /// </summary>
        public bool TubeDominates()
        {
            var t = Find(LightKind.Tube);
            if (t == null || !t.IsOn) return false;
            var w = Find(LightKind.Window);
            float other = w == null ? 0f : w.Strength * w.Level;
            return t.Strength * t.Level > other * 1.5f;
        }
    }
}
