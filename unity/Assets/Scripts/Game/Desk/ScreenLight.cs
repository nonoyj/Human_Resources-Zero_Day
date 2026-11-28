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
using UnityEngine;

namespace HRZeroDay.Game
{
    public enum ScreenState { Off = 0, Idle = 1, On = 2 }

    public class ScreenLight : MonoBehaviour
    {
        public const string RootName = "ScreenLight";

        // ---- the desk's tube, measured off desk/monitor_small.png (154x106 at anchor 14,86, y up):
        //      case x 14..168, glass x 20..161, rows 84..171 (top-down canvas rows).
        public const int GlassX = 90;
        public const int GlassY = 128;
        public const int GlassRx = 70;
        public const int GlassRy = 44;
        /// <summary>The row the desk surface starts on, top-down canvas rows (desk_layout.json).</summary>
        public const int DeskRow = 206;
        /// <summary>Where the pool's centre is: ON the desk in front of the tube, never on the wall.</summary>
        public const int ProjectY = 236;
        /// <summary>The canvas: the same 480x270 every other system uses.</summary>
        public const int CanvasW = 480, CanvasH = 270;

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
            new Prop(300, 84, 216, 22),   // keyboard
            new Prop(388, 14, 217, 18),   // mouse
            new Prop(120, 48, 210, 26),   // the forms on the left of the desk
            new Prop( 56, 30, 208, 24),   // the mug
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
            float level = _fade;
            float rx = GlassRx * (1.30f + 0.75f * level);
            float ry = GlassRy * (0.86f + 0.34f * level);

            for (int p = 0; p < _parts.Count; p++)
            {
                var part = _parts[p];
                if (!part.ok) continue;
                System.Array.Copy(part.basePixels, part.litPixels, part.basePixels.Length);
                if (level > 0.01f)
                {
                    // 1. the pool: an ellipse centred on the desk in front of the tube, with a falloff
                    for (int y = 0; y < part.h; y++)
                    {
                        int canvasY = part.y0 + y;
                        bool onDesk = canvasY >= DeskRow;
                        float dy = (canvasY - ProjectY) / ry;
                        for (int x = 0; x < part.w; x++)
                        {
                            int i = y * part.w + x;
                            if (part.litPixels[i].a == 0) continue;
                            float dx = (part.x0 + x - GlassX) / rx;
                            float r = Mathf.Sqrt(dx * dx + dy * dy);
                            if (r >= 1f) continue;
                            // the desk takes the light; the wall above it takes a third of it
                            float amount = (1f - r) * (1f - r) * level * (onDesk ? 1f : 0.34f);
                            Lift(part, i, amount);
                        }
                    }
                    // 2. the shadows: the desk RIGHT of each prop goes down its ramp, away from the tube
                    CastShadows(part, level);
                }
                // 3. and the surface itself is re-uploaded; its tint is never touched, because the light is
                //    already IN the pixels above
                Upload(part);
            }
        }

        private void CastShadows(Part part, float level)
        {
            for (int s = 0; s < Props.Length; s++)
            {
                var prop = Props[s];
                int length = Mathf.RoundToInt(prop.Height * 0.55f * level);
                int startX = prop.X + prop.W;
                for (int i = 0; i < length; i++)
                {
                    int canvasX = startX + i;
                    float k = 1f - i / (float)Mathf.Max(1, length);
                    if (k < 0.35f) continue;
                    int px = canvasX - part.x0;
                    if (px < 0 || px >= part.w) continue;
                    for (int row = prop.Top; row < prop.Top + prop.Height; row++)
                    {
                        int py = row - part.y0;
                        if (py < 0 || py >= part.h) continue;
                        if (k < 0.7f && ((canvasX + row) & 1) != 0) continue;   // dithered: never a hard wedge
                        int idx = py * part.w + px;
                        if (part.litPixels[idx].a == 0) continue;
                        Darken(part, idx, 1);
                    }
                }
                // the prop's own near face (its LEFT, facing the glass) catches the tube
                int faceX = prop.X - part.x0;
                for (int i = 0; i < 2; i++)
                {
                    int px = faceX + i;
                    if (px < 0 || px >= part.w) continue;
                    for (int row = prop.Top; row < prop.Top + prop.Height; row++)
                    {
                        int py = row - part.y0;
                        if (py < 0 || py >= part.h) continue;
                        int idx = py * part.w + px;
                        if (part.litPixels[idx].a == 0) continue;
                        Lift(part, idx, 0.85f * level);
                    }
                }
            }
        }

        /// <summary>Walks one pixel UP its own material by `amount` (0..1, two steps maximum), dithering the fraction.</summary>
        private void Lift(Part part, int i, float amount)
        {
            if (amount <= 0.02f) return;
            Color32 up;
            if (!_up.TryGetValue(part.litPixels[i], out up)) return;      // an emitter, or a colour with no step
            float scaled = amount * 2f;
            float whole = Mathf.Floor(scaled);
            float frac = scaled - whole;
            int pat = (i % part.w & 1) + ((i / part.w & 1) << 1);
            if (frac > 0.62f || (frac > 0.30f && pat < 2)) whole += 1f;
            Color32 c = part.litPixels[i];
            for (int s = 0; s < (int)whole; s++)
            {
                if (!_up.TryGetValue(c, out up)) break;
                c = up;
            }
            part.litPixels[i] = c;
        }

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
