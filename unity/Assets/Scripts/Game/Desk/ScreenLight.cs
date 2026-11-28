// HR//0D -- the tube is the room's third light, and it is a LIGHT, not a decal.
// Assembly: HRZeroDay.Game
//
// THE PLAYER'S REPORT (v120, request E): "the monitor reads as if it is switched off. When it is ON it must
// light everything in front of it -- the desk, the cup, the keyboard, the mouse, the monitor's sides -- and
// the shadows on the side facing away from the screen must get stronger."
//
// THREE BUGS IN THE FIRST ATTEMPT, all found by looking at the code against the art instead of at a
// screenshot:
//
//   1. IT LIT THE WRONG SPRITE. `Bind` was handed `L_background`, the room painting -- but MenuView REPLACES
//      that renderer's sprite with its own 480x270 composition while the front door is up, and the desk's
//      props, papers and mug are not in it anyway. So the tube's light was being baked into a picture that
//      is only on screen behind a menu. It now lights EVERY static part of the desk, in place.
//   2. IT COULD NOT READ THE ROOM. `assets/Kit/backgrounds` was imported with Read/Write off, so
//      GetPixels32 threw, the warning fired, and the tube quietly did nothing at all. The metas and the
//      importer now ask for readability -- and this component says so loudly if it cannot get it.
//   3. IT LIT THE FLOOR INSTEAD OF THE DESK. The pool was anchored below the desk surface (row 172 on a
//      surface that starts at 150), so the brightest part of it was under the desk's front face -- light
//      that could not reach the paper, the mug or the keys. The pool's centre is derived from the desk
//      surface now, and there is a TEST for it (LightingTests.TubePoolLandsOnTheDesk).
//
// v130: THE NUMBERS ARE NOT IN THIS FILE ANY MORE.
//   The player asked for MORE THAN ONE light on both screens, so the geometry of every source moved into
//   Core/Rules/LightRig.cs, where a headless test can switch each one and measure it. This component keeps
//   its job -- own the captured surfaces, walk each pixel up its own material, dither the fraction -- and
//   takes the geometry, the levels, the props and the shadow flank from `LightRig`. The constants below are
//   ALIASES of the rig's, never copies: two copies of a light's position is exactly how the pool ended up on
//   the wall in v120 while the tests passed.
//
// HOW IT WORKS, and why it is a CPU pass rather than a Light2D:
//   The camera is orthographic and every sprite is point-filtered 16 px-per-unit pixel art. A URP 2D light
//   with normal maps produces a soft blended gradient that fights the dither language the whole game is
//   drawn in. This walks each surface's pixels: a pixel can only be pushed one step UP ITS OWN MATERIAL
//   (wood_1 -> wood_2 -> wood_3 -> wood_4), and the fractional part of the light becomes a 2x2 ordered
//   dither -- exactly the operation the front door's light performs (MenuView.Light), which is what makes
//   the desk and the menu look like one game.
//
// THE COORDINATE TRAP, stated once: Unity's Color32[] from GetPixels32 is BOTTOM-UP (index 0 is the bottom
// left), while every canvas number in this project (desk_layout.json, MenuLayout, the twin renderer) is
// TOP-DOWN. This file converts once, at the edges, and works in top-down rows internally. A gate exists for
// this class of bug; the comment is here so the next person does not have to rediscover it.
using System.Collections.Generic;
using HRZeroDay.Core.Rules;
using UnityEngine;

namespace HRZeroDay.Game
{
    public enum ScreenState { Off = 0, Idle = 1, On = 2 }

    public class ScreenLight : MonoBehaviour
    {
        public const string RootName = "ScreenLight";

        // ---- the desk's tube, measured off desk/monitor_M.png (192x108 at anchor [268,84], y up):
        //      glass x 268+5=273 .. 268+186=454, rows 170-101=69 .. 170-8=162 top-down, so the glass CENTRE
        //      is x 364, y 115, 91 px x 47 px half-size.
        //
        //      THESE WERE THE OLD LEFT-HAND NUMBERS (90,128,70,44) and that is the whole "lamp lights the wall,
        //      not the desk" report, measured rather than guessed: with the pool centred on x=90 the ellipse
        //      fell on the EMPTY LEFT HALF of the desk and died on the wall, while the monitor's own side --
        //      every prop, the clipboard, the coffee -- was never inside the radius. The measured dump in
        //      tools/comp/preview_screenlight.py showed the desk moving +1.0/255 between tube off and tube on,
        //      which is a no-op. A light is a coordinate before it is a colour.
        //      v129 re-measurement of the MENU's CRT (monitor_M is make_menu_room.crt at 200x143, glass
        //      20,9,175,116 in sprite px, anchored at canvas [256,84] -> top row 43):
        //      glass canvas rows 61..176, columns 276..451, so centre (364,118), half-size (88,58).
        public const int GlassX = LightRig.TubeGlassX;
        public const int GlassY = LightRig.TubeGlassBottom - LightRig.GlassRy;   // 118: the glass's TOP
        public const int GlassRx = LightRig.GlassRx;
        public const int GlassRy = 58;
        /// <summary>The row the desk surface starts on, top-down canvas rows (desk_layout.json: slab top 170).</summary>
        public const int DeskRow = LightRig.DeskRow;
        /// <summary>Where the pool's centre is: ON the desk in front of the tube, never on the wall.</summary>
        public const int ProjectY = LightRig.TubePoolY;
        /// <summary>The canvas: the same 480x270 every other system uses.</summary>
        public const int CanvasW = LightRig.CanvasW, CanvasH = LightRig.CanvasH;

        public const float FadeSeconds = 0.55f;
        public const float FlickerSeconds = 0.18f;
        public const float IdleHz = 0.5f;

        /// <summary>The game's current state, read every frame. Wired by DeskBootstrap.</summary>
        public System.Func<ScreenState> want;
        public ScreenState state = ScreenState.On;

        /// <summary>How much of the tube's light is in the room, 0..1. The tests and the HUD read this.</summary>
        public float Level { get { return _fade; } }

        // ---- the props that stand on the desk, in canvas px, top-down: (left, width, top, height) ------
        // Read off desk/desk_foreground.png at anchor (0,0) of a 64 px band that starts at DeskRow, so these
        // are BELOW the surface line and their shadows run right, away from a tube that sits on the left.
        private struct Prop
        {
            public int X, W, Top, Height;
            public Prop(int x, int w, int top, int h) { X = x; W = w; Top = top; Height = h; }
        }

        private static readonly Prop[] Props =
        {
            // v127: read off desk_layout.json's own anchors, so a shadow starts where its object starts.
            // canvas top-down row = 270 - anchorY - spriteH.
            // v129: the four props the desk actually carries, drawn by make_menu_room into desk_slab_M,
            // in the player's order -- coffee, forms, keyboard, mouse. They stand on the surface: top row
            // 176 (desk far edge 170 + the menu's 6 px inset), the mouse 3 px higher.
            new Prop(34, 44, 176, 30),    // coffee
            new Prop(96, 62, 176, 26),    // forms
            new Prop(262, 84, 176, 20),   // keyboard
            new Prop(356, 20, 173, 32),   // mouse
        };

        // ---- one captured surface ---------------------------------------------------------------------
        private class Part
        {
            public SpriteRenderer renderer;
            public Color32[] basePixels;   // the authored art, top-down
            public Color32[] litPixels;    // what is on screen now
            public Texture2D tex;
            public int w, h, x0, y0;       // its canvas rect, top-down
            public Color originalColor;
            public bool ok;                // did we get the pixels?
        }

        private readonly List<Part> _parts = new List<Part>();
        private float _fade;
        private float _flicker;
        private Dictionary<Color32, Color32> _up;
        private Dictionary<Color32, Color32> _down;

        /// <summary>The lamp's state, pushed in by LampView. False = the desk's second practical is off.</summary>
        public bool lampOn = true;
        /// <summary>How much of the room's own light (window + floor bounce) is present, 0..1: the daylight
        /// stages multiply this, so a night shift does not look like a noon one with a different tint.</summary>
        public float roomLevel = 1f;

        public static ScreenLight Attach(Transform root)
        {
            if (root == null) return null;
            var go = new GameObject(RootName);
            go.transform.SetParent(root, false);
            return go.AddComponent<ScreenLight>();
        }

        /// <summary>
        /// Captures every static surface the tube can light. `layout` is the desk layout (the same json the
        /// scene was built from), so each surface's canvas rect is EXACT rather than guessed from pivots --
        /// a light pass whose coordinates are 40 px out is a light pass that lights the ceiling.
        /// </summary>
        public void Bind(Transform root, DeskLayout layout)
        {
            _parts.Clear();
            _up = PaletteLadder.Up;
            _down = PaletteLadder.Down;
            if (root == null || layout == null) return;

            for (int i = 0; i < layout.layers.Count; i++)
            {
                var layer = layout.layers[i];
                // the applicant is ANIMATED: it must stay a sprite, so it is not part of the bake.
                if (layer.id == "applicant") continue;
                var child = root.Find("L_" + layer.id);
                if (child == null) continue;
                var sr = child.GetComponent<SpriteRenderer>();
                if (sr == null || sr.sprite == null) continue;

                int w = Mathf.RoundToInt(sr.sprite.rect.width);
                int h = Mathf.RoundToInt(sr.sprite.rect.height);
                var part = new Part
                {
                    renderer = sr,
                    w = w, h = h,
                    x0 = layer.anchor[0],
                    // desk_layout anchors are BOTTOM-LEFT in y-up canvas space; this file works top-down
                    y0 = CanvasH - (layer.anchor[1] + h),
                    originalColor = sr.color,
                };
                try
                {
                    var raw = sr.sprite.texture.GetPixels32();
                    if (raw == null || raw.Length < w * h) continue;
                    part.basePixels = TopDown(raw, w, h, sr.sprite.texture.width, sr.sprite.texture.height);
                    part.litPixels = new Color32[w * h];
                    part.tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
                    part.tex.filterMode = FilterMode.Point;
                    part.tex.wrapMode = TextureWrapMode.Clamp;
                    part.tex.name = "Lit_" + layer.id;
                    part.ok = true;
                }
                catch (System.Exception)
                {
                    part.ok = false;
                }
                if (!part.ok)
                {
                    Debug.LogWarning("[HR//0D] ScreenLight cannot read '" + layer.id + "', so the tube will " +
                                     "not light it. Run HR//0D > Import Art Kit (it sets Read/Write on the " +
                                     "room, the desk and the monitor), or python tools/art/write_unity_metas.py");
                    continue;
                }
                _parts.Add(part);
                // the sprite is replaced by the lit copy ONCE; every later frame writes into its texture, so
                // nothing is allocated per frame and the object's transform is never touched
                var pivot = new Vector2(0.5f, 0.5f);
                sr.sprite = Sprite.Create(part.tex, new Rect(0, 0, w, h), pivot, 16f);
            }
            Repaint();
        }

        /// <summary>
        /// Unity hands back BOTTOM-UP rows; every coordinate in this project is top-down. Convert once here.
        /// The texture may be an atlas (frames are packed), so the copy is bounded by both rects.
        /// </summary>
        private static Color32[] TopDown(Color32[] raw, int w, int h, int texW, int texH)
        {
            var outPx = new Color32[w * h];
            for (int y = 0; y < h; y++)
            {
                int srcRow = h - 1 - y;                       // flip
                for (int x = 0; x < w; x++)
                {
                    int si = srcRow * texW + x;
                    outPx[y * w + x] = si >= 0 && si < raw.Length ? raw[si] : new Color32(0, 0, 0, 0);
                }
            }
            return outPx;
        }

        /// <summary>
        /// THE TUBE'S STATE, DERIVED FROM THE GAME, not from a timer. One function, so the desk, the walk,
        /// the summary, the menu and the ending cannot disagree about whether the screen is on.
        /// </summary>
        public static ScreenState StateFor(bool ending, bool walk, bool summary, bool menuOpen)
        {
            if (ending) return ScreenState.Off;
            if (menuOpen) return ScreenState.Idle;
            if (walk || summary) return ScreenState.Idle;
            return ScreenState.On;
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            if (want != null) SetState(want());

            float target = state == ScreenState.On ? 1f : (state == ScreenState.Idle ? 0.45f : 0f);
            if (_flicker > 0f)
            {
                _flicker = Mathf.Max(0f, _flicker - dt);
                target *= (Mathf.Repeat(_flicker * 9f, 1f) > 0.45f) ? 1f : 0.15f;
            }
            float before = _fade;
            _fade = Mathf.MoveTowards(_fade, target, dt / FadeSeconds);
            if (state == ScreenState.Idle && _flicker <= 0f)
                _fade = 0.45f + 0.05f * Mathf.Sin(Time.unscaledTime * IdleHz * Mathf.PI * 2f);
            if (Mathf.Abs(_fade - before) > 0.002f) Repaint();
        }

        public void SetState(ScreenState wanted)
        {
            if (wanted == state) return;
            state = wanted;
            if (wanted == ScreenState.On) _flicker = FlickerSeconds;
        }

        /// <summary>Bakes the tube's light into every captured surface. Runs on a state change, not per frame.</summary>
        public void Repaint()
        {
            if (_up == null || _parts.Count == 0) return;
            // THE RIG IS REBUILT WHEN THE STATE CHANGES, not per pixel: five sources, one array.
            // The tube's Level IS this component's fade, so the flicker and the pool cannot disagree.
            bool tubeWants = Mathf.RoundToInt(_fade * LightRig.Levels) > 0;
            var rig = LightRig.ForDesk(tubeWants, lampOn: lampOn, roomLevel: roomLevel);
            var tube = rig.Find(LightKind.Tube);
            if (tube != null) tube.Level = _fade;
            float level = _fade;

            for (int p = 0; p < _parts.Count; p++)
            {
                var part = _parts[p];
                if (!part.ok) continue;
                System.Array.Copy(part.basePixels, part.litPixels, part.basePixels.Length);
                if (level > 0.01f || lampOn)
                {
                    // 1. THE LIGHT, FROM THE RIG. Every source adds, then one clamp: the tube, the lamp, the
                    //    window and the floor glow all land through the same two calls, so adding a sixth
                    //    light is a change to LightRig and nothing here.
                    //    A pixel on the WALL above the desk takes a third of what the desk takes -- the
                    //    pool is projected onto the wood, never onto the wall behind it (the v120 report).
                    for (int y = 0; y < part.h; y++)
                    {
                        int canvasY = part.y0 + y;
                        bool onDesk = canvasY >= DeskRow;
                        for (int x = 0; x < part.w; x++)
                        {
                            int i = y * part.w + x;
                            if (part.litPixels[i].a == 0) continue;
                            int cx2 = part.x0 + x;
                            int n = rig.LightAt(cx2, canvasY);
                            float frac = rig.FractionAt(cx2, canvasY);
                            if (!onDesk) { n = n / 3; frac *= 0.34f; }
                            if (n > 0) Lift(part, i, n);
                            if (frac > 0.02f) Lift(part, i, frac);
                        }
                    }
                    // 2. the shadows: a smear at each prop's foot, on the flank facing the glass
                    CastShadows(part, rig);
                }
                // 3. and the surface itself is re-uploaded; its tint is never touched, because the light is
                //    already IN the pixels above
                Upload(part);
            }
        }

        /// <summary>
        /// A shadow is a smear at the prop's FOOT, on the flank facing the lit screen -- not a bar up the
        /// prop's side and not a wedge across the desk. `LightRig.ShadowSideAt` decides which flank; this
        /// method only paints it down the pixel's own ramp. v129 ran the shadows LEFT because the monitor
        /// used to be on the left; the rig's geometry is the only place that can be wrong now.
        /// </summary>
        private void CastShadows(Part part, LightRig rig)
        {
            for (int s = 0; s < Props.Length; s++)
            {
                var prop = Props[s];
                int right = prop.X + prop.W - 1;
                int bottom = prop.Top + prop.Height;
                for (int row = bottom - LightRig.ContactBand; row <= bottom; row++)
                {
                    int py = row - part.y0;
                    if (py < 0 || py >= part.h) continue;
                    for (int cx2 = prop.X; cx2 <= right; cx2++)
                    {
                        int px = cx2 - part.x0;
                        if (px < 0 || px >= part.w) continue;
                        int idx = py * part.w + px;
                        if (part.litPixels[idx].a == 0) continue;
                        float k = rig.ShadowStrength(cx2, row, right, bottom, prop.Height);
                        if (k <= 0f) continue;
                        // the fraction of a step, dithered: a shadow never becomes a black bar
                        if (k * 16f > Dither[cx2 & 3, row & 3]) Darken(part, idx, 1);
                    }
                }
            }
        }

        /// <summary>
        /// Walks one pixel UP its own material by `steps`, and dithers a FRACTION of a step with the same 4x4
        /// ordered matrix the art bakes with. Never a blend: the pixel lands on a palette step or it does not
        /// move. `steps` is what LightRig.LightAt returned, so the number here is the rig's, not a guess.
        /// </summary>
        private void Lift(Part part, int i, int steps)
        {
            if (steps <= 0) return;
            Color32 c = part.litPixels[i];
            Color32 up;
            for (int s = 0; s < steps; s++)
            {
                if (!_up.TryGetValue(c, out up)) break;     // an emitter, or a colour with no step: leave it
                c = up;
            }
            part.litPixels[i] = c;
        }

        private void Lift(Part part, int i, float fraction)
        {
            if (fraction <= 0.02f) return;
            int x = i % part.w, y = i / part.w;
            if (fraction * 16f <= Dither[x & 3, y & 3]) return;      // the 4x4 threshold, not a pair rule
            Lift(part, i, 1);
        }

        /// <summary>The 4x4 ordered dither every generator and the menu's light already use.</summary>
        private static readonly int[,] Dither =
        {
            { 0, 8, 2, 10 }, { 12, 4, 14, 6 }, { 3, 11, 1, 9 }, { 15, 7, 13, 5 }
        };

        /// <summary>Walks one pixel DOWN its own material: the shadow side of a form.</summary>
        private void Darken(Part part, int i, int steps)
        {
            Color32 down;
            for (int s = 0; s < steps && _down.TryGetValue(part.litPixels[i], out down); s++)
                part.litPixels[i] = down;
        }

        private static void Upload(Part part)
        {
            if (part.tex == null) return;
            // back to bottom-up for the GPU, in place, one row at a time (no second buffer)
            var tmp = new Color32[part.w];
            int h = part.h;
            for (int y = 0; y < h / 2; y++)
            {
                int top = y * part.w, bot = (h - 1 - y) * part.w;
                System.Array.Copy(part.litPixels, top, tmp, 0, part.w);
                System.Array.Copy(part.litPixels, bot, part.litPixels, top, part.w);
                System.Array.Copy(tmp, 0, part.litPixels, bot, part.w);
            }
            part.tex.SetPixels32(part.litPixels);
            part.tex.Apply(false);
            // and back, so the next repaint does not work on a flipped buffer
            for (int y = 0; y < h / 2; y++)
            {
                int top = y * part.w, bot = (h - 1 - y) * part.w;
                System.Array.Copy(part.litPixels, top, tmp, 0, part.w);
                System.Array.Copy(part.litPixels, bot, part.litPixels, top, part.w);
                System.Array.Copy(tmp, 0, part.litPixels, bot, part.w);
            }
        }
    }
}
