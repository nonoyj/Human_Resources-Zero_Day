// HR//0D -- the tube is the room's third light, and it is a LIGHT, not a decal.
// Assembly: HRZeroDay.Game
//
// THE PLAYER'S REPORT (v120, request E), in their words: "the monitor reads as if it is switched off.
// Turn it on over time; when it is ON it must light everything in front of it -- the desk in front of the
// monitor with a falloff across the surface, the coffee cup, the keyboard, the mouse, the sides of the
// monitor -- and the shadows on the side facing away from the screen must get stronger. Tell me if that is
// possible."
//
// Yes, and this is how. Three pieces, none of which is a shader:
//
//   1. THE EMITTER has three states (On / Idle / Off) and a clock. On = the applicant is in the chair and
//      the player is reading; Idle = nobody is being interviewed (the doors are shut, the walk is up);
//      Off = the shift is over or the ending is on screen. The transition is a fade with a flicker on the
//      way IN, because a CRT that comes on instantly reads as a sprite being enabled.
//
//   2. THE FALLOFF is a lamp of exactly the shape a screen has: WIDER THAN IT IS TALL and cut off at the
//      glass. A 154x106 tube throws an ellipse about 2.4x wider than the box it sits in, centred on the
//      glass and biased DOWNWARD onto the desk, because that is where a screen's light actually goes --
//      it leaves the glass and lands on the surface in front of it.
//
//   3. THE SHADOW is the part that makes it read. For every prop standing on the desk near the tube, the
//      desk BEHIND it (on the side away from the screen, i.e. to its RIGHT, because the monitor is on the
//      left) is pushed DOWN the desk's ramp. The further from the tube and the taller the prop, the
//      longer the shadow. The screen's own case lights on the side that faces the glass, and the keyboard
//      and the mug get their near faces lifted. Nothing is drawn on top of the art: the art is one baked
//      sprite, so the light is stamped INTO a copy of it once per state change, exactly the way
//      MenuView.Paint() lightens its own room (docs/89 §2, one palette two renderers).
//
// WHY A BAKED PASS AND NOT A LIGHT2D: the camera is orthographic and every sprite in the room is point
// filtered pixel art. A URP 2D light with a normal map on a 16 px-per-unit sprite produces a soft, blended
// gradient that fights the dither language the whole game is drawn in. The pass below quantises to the
// palette's own steps and dithers the fractional part, so the screen's light is made of the same material
// as the lamp's.
using System.Collections.Generic;
using UnityEngine;

namespace HRZeroDay.Game
{
    public enum ScreenState { Off = 0, Idle = 1, On = 2 }

    public class ScreenLight : MonoBehaviour
    {
        public const string RootName = "ScreenLight";

        /// <summary>The row the desk surface starts on, in canvas px (twin_desk / desk_layout agree).</summary>
        public const int DeskRow = 206;

        // ---- the desk's tube, measured off desk/monitor_small.png (154x106 at anchor 14,86, y up):
        //      case x 14..168, glass x 20..161 / rows 84..171. The glass's centre is the emitter.
        public const int GlassX = 90;
        public const int GlassY = 128;
        public const int GlassRx = 70;
        public const int GlassRy = 44;
        /// <summary>Where the pool's centre is: on the DESK, in front of the tube, never on the wall.</summary>
        public const int ProjectY = 196;

        /// <summary>Seconds for the tube to reach full brightness, and the flicker's length.</summary>
        public const float FadeSeconds = 0.55f;
        public const float FlickerSeconds = 0.18f;

        /// <summary>The idle phosphor breathe: 0.5 Hz, the slowest thing on the desk.</summary>
        public const float IdleHz = 0.5f;

        // the props whose shadows the tube casts, in canvas px: (left, right, top) of each. These are
        // read off the approved desk_foreground painting, not guessed -- each one is the box of a prop that
        // stands ON the surface, so its shadow starts at the box and runs away from the tube.
        private struct Prop
        {
            public int X, W, Top;       // left, width, the row of its highest pixel
            public int Height;          // how far it stands proud of the desk: sets the shadow's length
            public Prop(int x, int w, int top, int height) { X = x; W = w; Top = top; Height = height; }
        }

        private static readonly Prop[] Props =
        {
            new Prop(152, 90, 232, 26),    // keyboard: wide, low, closest to the tube
            new Prop(248, 12, 236, 10),    // mouse
            new Prop(300, 26, 214, 44),    // folder stack: tall, so it throws the longest shadow
            new Prop(370, 24, 222, 34),    // coffee mug
            new Prop(196, 14, 234, 12),    // notepad + pen
        };

        /// <summary>
        /// THE TUBE'S STATE, DERIVED FROM THE GAME, not from a timer (v120). One function so the desk, the
        /// walk, the debrief and the ending cannot disagree about whether the screen is on:
        ///
        ///   OFF   the ending is on screen, or the player is in the front door menu with no run behind it.
        ///         (A menu over a live desk keeps the tube IDLE, not off: the run is still there.)
        ///   IDLE  nothing is being interviewed -- the walk is up, the day summary is up, the desk is empty
        ///         between applicants. This is the phosphor breathe, 0.5 Hz.
        ///   ON    somebody is at the desk: the interview, the letter, the debrief, the alert panel.
        ///
        /// The bias is deliberate: when this function cannot tell, it says ON. A dark screen while the
        /// player is reading is worse than a lit screen in an empty room.
        /// </summary>
        public static ScreenState StateFor(bool ending, bool walk, bool summary, bool menuOpen)
        {
            if (ending) return ScreenState.Off;
            if (menuOpen) return ScreenState.Idle;
            if (walk || summary) return ScreenState.Idle;
            return ScreenState.On;
        }

        public ScreenState state = ScreenState.On;

        /// <summary>WHO ASKS (v120): a function returning the state the game is in, read every frame.</summary>
        public System.Func<ScreenState> want;
        private float _fade;                 // 0..1, how much of the tube's light is in the room
        private float _flicker;              // counts down while the tube strikes

        private Color32[] _room;             // the baked room, before any light
        private Color32[] _lit;              // the buffer the screen renderer draws
        private int _w, _h;
        private SpriteRenderer _background;  // L_background, the only sprite the tube repaints
        private SpriteRenderer _monitor;     // L_monitor: its case catches the glass on the inside
        private Texture2D _tex;
        private float _ppu = 16f;
        private Dictionary<Color32, Color32> _table;   // colour -> the next step of its own ramp
        private Dictionary<Color32, Color32> _down;    // ... and the step below, for the shadows


        /// <summary>How much of the tube's light is in the room, 0..1. The tests read this.</summary>
        public float Level { get { return _fade; } }

        public static ScreenLight Attach(Transform root)
        {
            if (root == null) return null;
            var go = new GameObject(RootName);
            go.transform.SetParent(root, false);
            return go.AddComponent<ScreenLight>();
        }

        /// <summary>
        /// Wires the tube to the room. `background` is the L_background renderer; `monitor` is optional (the
        /// case lights only if it is there). The room's pixels are captured from the CURRENT sprite, so this
        /// must run after the stage sprite for the first light has been chosen.
        /// </summary>
        public void Bind(SpriteRenderer background, SpriteRenderer monitor)
        {
            _background = background;
            _monitor = monitor;
            Capture();
        }

        private void Capture()
        {
            if (_background == null || _background.sprite == null) return;
            var src = _background.sprite;
            _w = (int)src.rect.width;
            _h = (int)src.rect.height;
            // Read/Write must be on in the .meta (write_unity_metas.py does that for the kit). If it is not,
            // the copy fails and the room simply keeps its authored light: no tube light, no crash.
            // `catch` without a type: in Unity a non-readable texture throws UnityException, and the stub
            // assembly the headless tests build against has no Unity.Exception type at all. The behaviour is
            // identical and the file compiles in both places, which is what the compile gate checks.
            try { var px = src.texture.GetPixels32(); _room = px.Length >= _w * _h ? px : null; }
            catch (System.Exception) { _room = null; }
            if (_room == null)
            {
                Debug.LogWarning("[HR//0D] ScreenLight: the background is not readable, so the tube cannot " +
                                 "light it. Run HR//0D > Import Art Kit (or write_unity_metas.py).");
                return;
            }
            _lit = new Color32[_room.Length];
            _tex = new Texture2D(_w, _h, TextureFormat.RGBA32, false);
            _tex.filterMode = FilterMode.Point;
            _tex.wrapMode = TextureWrapMode.Clamp;
            _tex.name = "RoomLit";
            _ppu = _background.sprite.pixelsPerUnit;      // remember it: the old sprite is replaced below
            _background.sprite = Sprite.Create(_tex, new Rect(0, 0, _w, _h), new Vector2(0.5f, 0.5f), _ppu);
            _table = PaletteLadder.Up;
            _down = PaletteLadder.Down;
        }


        /// <summary>The tube is wanted in this state (the desk decides; see DeskRuntimeScene).</summary>
        public void SetState(ScreenState want)
        {
            if (want == state) return;
            state = want;
            if (want == ScreenState.On) _flicker = FlickerSeconds;
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            if (want != null) SetState(want());
            float target = state == ScreenState.On ? 1f : (state == ScreenState.Idle ? 0.45f : 0f);
            if (_flicker > 0f)
            {
                _flicker = Mathf.Max(0f, _flicker - dt);
                // three strikes: a CRT does not come on, it tries to
                target *= (Mathf.Repeat(_flicker * 9f, 1f) > 0.45f) ? 1f : 0.15f;
            }
            float before = _fade;
            _fade = Mathf.MoveTowards(_fade, target, dt / FadeSeconds);
            // the idle breathe only exists when the tube is idle, and it never swings more than a tenth
            if (state == ScreenState.Idle && _flicker <= 0f)
                _fade = 0.45f + 0.05f * Mathf.Sin(Time.unscaledTime * IdleHz * Mathf.PI * 2f);
            if (Mathf.Abs(_fade - before) > 0.002f) Repaint();
        }

        private void Repaint()
        {
            if (_room == null || _lit == null || _tex == null) return;
            System.Array.Copy(_room, _lit, _room.Length);
            float level = _fade;
            if (level <= 0.01f)
            {
                _tex.SetPixels32(_lit);
                _tex.Apply(false);
                if (_monitor != null) _monitor.color = Color.white;
                return;
            }

            // ---- 1. THE FALLOFF, across the whole canvas, from the glass outward
            // THE DESK'S TUBE IS NOT THE MENU'S CRT. Both are lights, but they are not in the same place: the
            // menu's CRT stands in the middle of a different room (MenuLayout.MonitorLight is that one), and
            // the desk's monitor is the 154x106 case at anchor (14, 86) with its glass at x 20..161, y 84..171.
            // One function, two emitters -- deriving the desk's light from the menu's number is how a fix in
            // one room silently moves the light in the other. See DeskTube() below for where these come from.
            // THE POOL IS ON THE DESK, NOT ON THE WALL (v120, second pass). A screen faces the player, so
            // its light lands on the surface between the tube and the viewer, and on the props standing
            // there. Centring the ellipse on the glass lit the wall behind the monitor -- the same mistake
            // the lamp had -- and left the keyboard, the mug and the folders at their authored value.
            float rx = GlassRx * (1.30f + 0.75f * level);   // ~2x the tube wide at full
            float ry = GlassRy * (0.86f + 0.34f * level);
            float cy = ProjectY;                        // projected DOWN onto the desk in front of it
            float cx = GlassX;
            int rowsFromDesk = Mathf.Max(1, _h - DeskRow);
            for (int y = 0; y < _h; y++)
            {
                for (int x = 0; x < _w; x++)
                {
                    float dx = (x - cx) / rx;
                    float dy = (y - (float)cy) / ry;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    if (r >= 1f) continue;
                    // the desk surface is lit MORE than the wall: light lands on it and stops there
                    float onDesk = y >= DeskRow ? 1f : 0.30f;
                    float amount = (1f - r) * (1f - r) * level * onDesk;
                    Lift(_lit, y * _w + x, amount);
                }
            }

            // ---- 2. THE SHADOWS, cast away from the tube: the desk RIGHT of each prop goes down its ramp
            for (int p = 0; p < Props.Length; p++)
            {
                var prop = Props[p];
                int length = Mathf.RoundToInt(prop.Height * 0.55f * level);
                int startX = prop.X + prop.W;
                for (int i = 0; i < length; i++)
                {
                    int x = startX + i;
                    if (x >= _w) break;
                    // the umbra tapers: full strength at the prop, gone by the shadow's end
                    float k = 1f - i / (float)Mathf.Max(1, length);
                    if (k < 0.35f) continue;
                    for (int y = DeskRow; y < DeskRow + prop.Height; y++)
                    {
                        if (y >= _h) break;
                        if (((x + y) & 1) != 0 && k < 0.7f) continue;   // dithered, never a hard wedge
                        Darken(_lit, y * _w + x, 1);
                    }
                }
                // and the prop's own NEAR face (its left, facing the glass) catches the tube
                for (int y = Mathf.Max(DeskRow, prop.Top); y < DeskRow + prop.Height; y++)
                {
                    if (y >= _h) break;
                    for (int i = 0; i < 2; i++)
                    {
                        int x = prop.X + i;
                        if (x >= 0 && x < _w) Lift(_lit, y * _w + x, 0.85f * level);
                    }
                }
            }

            // ---- 3. THE MONITOR'S OWN CASE (the sprite sorting -20, not the glass): the inside of the bezel is the brightest metal on the desk,
            //          because it is the same distance from the glass as the glass is from itself
            // The case and the stand are separate sprites (sorting -20). They are not repainted -- they are
            // ONE sprite each, already painted at the approved key -- so the only honest thing to do is let
            // the tube's light reach them the way it reaches the room: a small, fixed lift of their tint,
            // which cannot go beyond white and therefore cannot burn the art. This is the "the sides of the
            // monitor light up" half of the request; the glass itself is MonitorScreen's and is untouched.
            if (_monitor != null)
            {
                float k = 1f + 0.06f * level;                  // +6% of the case's own colour at full
                _monitor.color = new Color(k, k, k, 1f);
            }

            _tex.SetPixels32(_lit);
            _tex.Apply(false);
        }

        /// <summary>Walks one pixel up its own ramp by `amount` (0..1), dithering the fraction.</summary>
        private void Lift(Color32[] buf, int i, float amount)
        {
            if (amount <= 0.02f || _table == null) return;
            Color32 up;
            if (!_table.TryGetValue(buf[i], out up)) return;       // an emitter or an off-palette pixel
            float whole = Mathf.Floor(amount * 2.4f);              // up to TWO steps at the tube's mouth
            float frac = amount * 2.4f - whole;
            if (frac > 0.62f || (frac > 0.30f && ((i + (i / _w)) & 1) == 0)) whole += 1f;
            Color32 c = buf[i];
            for (int s = 0; s < (int)whole; s++) { if (_table.TryGetValue(c, out up)) c = up; else break; }
            buf[i] = c;
        }

        /// <summary>Walks one pixel DOWN its own ramp: the shadow side of a form.</summary>
        private void Darken(Color32[] buf, int i, int steps)
        {
            if (steps <= 0) return;
            Color32 down;
            for (int s = 0; s < steps && _down != null && _down.TryGetValue(buf[i], out down); s++)
                buf[i] = down;
        }
    }
}
