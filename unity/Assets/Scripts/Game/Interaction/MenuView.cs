// HR//0D -- MenuView: the front door, drawn as the room it belongs to.
// Assembly: HRZeroDay.Game  (UnityEngine)
//
// WHAT THE PLAYER ASKED FOR (v118, ADR-021), in their words and what each one became:
//   "too clean"                      -> every surface is painted from a MATERIAL RAMP (menu_palette.py):
//                                       3-5 steps, each rotated at least 6 degrees. No surface is one colour.
//   "the screen looks too retro"     -> the CRT's case is near-black with a thin bezel; the object is the tube.
//   "the colour is too bright; we    -> the palette gate now FAILS unless 66% of the material steps are dark
//    lost the cinematic dark"           and nothing but an emitter is above 0.62 value. The frame is low-key.
//   "turn it to 16 bit"              -> 11 ramps, 3-5 steps each, dithered seams, no smoothing, no noise.
//   "keep the cinematic cyan glows"  -> the tube is still the key light, and the lamp is a WARM second
//                                       source, which is what stops cyan being the only colour in the room.
//
// THE ONE THING THIS FILE MUST NOT DO
//   Rebuild the room every frame. The room is composed ONCE (BuildRoom) and then kept; what changes is a
//   small number of live cells: the six wall screens, their flicker, the breach flood, the motes, the
//   glass's scanline roll and the selection pulse. Paint() copies the composed room and stamps those on
//   top, which is why the menu can be animated at 60 Hz without allocating a texture per frame.
//
// THREE BUGS THIS REWRITE FIXES (all three were in the shipped v116)
//   BUG-071  the game kept playing behind the menu: DeskBootstrap.Update ticked the shift clock while the
//            front door was up. Fixed there (it now returns early), and the menu also parks the music.
//   BUG-072  the menu drew NOTHING: Paint() called FlipToTextureOrder and then RenderRows on the same
//            buffer. RenderRows inverts its y again, so every line landed off the top of the glass and
//            was clipped away. The flip is now the LAST thing that happens, and no raster call follows it.
//   BUG-073  the title/subtitle tracking was hard-coded at the atlas's 2 px gap, so "thicker and closer"
//            was impossible. Tracking and bleed are now parameters of LineWidth/DrawScaled.
using System;
using System.Collections.Generic;
using HRZeroDay.Core.Rules;
using HRZeroDay.Game;
using HRZeroDay.Game.Desk;
using UnityEngine;

namespace HRZeroDay.Game.Interaction
{
    public sealed class MenuView : MonoBehaviour
    {
        public const string RootName = "MenuView";
        private const string AtlasResource = "fonts/monitor_font_5x7";
        private const string KitRoot = "Art/Kit/";
        private const float Ppu = 16f;

        private const int W = MenuLayout.W, H = MenuLayout.H;

        private DeskBootstrap _game;
        private MenuScreen _screen = MenuScreen.Root;
        private List<MenuItem> _items = new List<MenuItem>();
        private int _index;
        private bool _open;

        private GameObject _root;
        private SpriteRenderer _card;
        private Texture2D _tex;
        private IGlyphSource _glyphs;
        private bool _ready;

        // the composed room, built once; Paint() copies it and stamps the live cells on top
        private Color32[] _room;

        // live state
        private Color32[] _frame;           // the buffer that is painted and uploaded
        private float _clock;
        private int _breachScreen = -1;
        private float _breachLeft;
        private float _breachNext;
        private readonly float[] _screenPhase = new float[6];

        private bool _hasSave;
        private bool _runInProgress;
        private bool _consentDecided;
        private bool _consentGranted;
        private float _roomVolume = 0.8f;
        private float _sfxVolume = 0.9f;
        private bool _muted;

        public bool IsOpen { get { return _open; } }
        public MenuScreen Screen { get { return _screen; } }
        public int Selected { get { return _index; } }

        public static MenuView Attach(DeskBootstrap game)
        {
            var go = new GameObject(RootName);
            var view = go.AddComponent<MenuView>();
            view._game = game;
            return view;
        }

        public bool Build()
        {
            if (_ready) return true;
            _glyphs = MonitorScreenView.Load(AtlasResource, MonitorScreenView.PaletteCached);
            if (_glyphs == null)
            {
                Debug.LogWarning("[HR//0D] menu skipped: no glyph atlas.");
                return false;
            }
            if (!BuildRoom()) return false;

            _root = new GameObject("MenuRoom");
            _root.transform.SetParent(transform, false);

            var go = new GameObject("Screen");
            go.transform.SetParent(_root.transform, false);
            _card = go.AddComponent<SpriteRenderer>();
            _card.sortingOrder = 240;              // above the intro card (230): the menu is the frontmost thing
            _card.transform.localPosition = PageTransform.CanvasToWorld(W * 0.5f, H * 0.5f, Ppu);

            _tex = new Texture2D(W, H, TextureFormat.RGBA32, false);
            _tex.filterMode = FilterMode.Point;
            _tex.wrapMode = TextureWrapMode.Clamp;
            _tex.name = "MenuRoom";
            _card.sprite = Sprite.Create(_tex, new Rect(0, 0, W, H), new Vector2(0.5f, 0.5f), Ppu);

            _frame = new Color32[W * H];
            _root.SetActive(false);
            _ready = true;
            return true;
        }

        /// <summary>
        /// Composes the room once. Every sprite is optional: a missing one is skipped and logged, and the
        /// rest of the frame still draws, because a menu with no server is still a menu.
        /// </summary>
        private bool BuildRoom()
        {
            _room = new Color32[W * H];
            // BACK TO FRONT. The order is the room's depth order, not the draw order of the concept:
            // wall, window (the cold key's source), the rail of screens, the lamp (the warm key), the
            // monitor, the server in the corner, and the desk in front of all of it.
            Blit("menu/shell", 0, 0);
            Blit("menu/window", MenuLayout.WindowX, MenuLayout.WindowY);
            Blit("menu/monitor_stack", MenuLayout.StackX, MenuLayout.StackY);
            Blit("menu/crt", MenuLayout.CrtX, MenuLayout.CrtY);
            Blit("menu/server", MenuLayout.ServerX, MenuLayout.ServerY);
            Blit("menu/desk", 0, MenuLayout.DeskY);
            Blit("menu/lamp", MenuLayout.LampX, MenuLayout.LampY);   // it stands ON the desk
            GlassMaterial();
            Light();
            Vignette();
            return true;
        }

        /// <summary>
        /// The tube itself: an off-black phosphor with a scanline every three rows and a faint wash in its
        /// top third. Painted before the light pass (which skips the monitor entirely) and before the text.
        /// </summary>
        private void GlassMaterial()
        {
            int x0 = MenuLayout.GlassX, y0 = MenuLayout.GlassY;
            int x1 = x0 + MenuLayout.GlassW, y1 = y0 + MenuLayout.GlassH;
            Color32 paper = Ink("void", new Color32(9, 11, 15, 255));
            Color32 wash = Ink("teal_0", new Color32(5, 8, 10, 255));
            Color32 dim = Ink("steel_0", new Color32(13, 16, 20, 255));
            for (int y = y0; y < y1; y++)
            {
                bool bright = y < y0 + MenuLayout.GlassH / 3;
                for (int x = x0; x < x1; x++)
                {
                    int i = y * W + x;
                    _room[i] = paper;
                    if ((y - y0) % 3 == 2) _room[i] = bright ? wash : dim;
                }
            }
        }

        /// <summary>
        /// Paints the ambience. Each light raises the VALUE of the surfaces it reaches by ONE ramp step, in
        /// the surface's own ramp, and the step boundary is the only place a dither appears. A light never
        /// replaces a colour with its own -- that is what turned the v115 attempt into cyan blobs.
        /// </summary>
        private void Light()
        {
            var palette = MonitorScreenView.PaletteCached;
            if (palette == null) return;

            // reverse lookup: a room pixel's colour -> the next step of its own material. One pass, then
            // it is arithmetic. Emitters (white, teal_4, scr_2, LEDs) are absent from this table and are
            // therefore never lifted.
            var up = new Dictionary<Color32, Color32>();
            for (int i = 0; i < MenuLayout.Ramps.GetLength(0); i++)
            {
                Color32 from, to;
                if (!palette.TryGetValue(MenuLayout.Ramps[i, 0], out from)) continue;
                if (!palette.TryGetValue(MenuLayout.Ramps[i, 1], out to)) continue;
                if (!up.ContainsKey(from)) up[from] = to;
            }

            var lights = MenuLayout.Lights();
            for (int y = 0; y < H; y++)
            {
                for (int x = 0; x < W; x++)
                {
                    int idx = y * W + x;
                    if (_room[idx].a == 0) continue;

                    // THE GLASS IS A SOURCE, NOT A SURFACE. Lighting the screen you are reading made the
                    // menu speckle and let a fade paint over the title. Nothing lights the tube.
                    if (x >= MenuLayout.CrtX && x < MenuLayout.CrtX + MenuLayout.CrtW &&
                        y >= MenuLayout.CrtY && y < MenuLayout.CrtY + MenuLayout.CrtH) continue;

                    Color32 lifted;
                    if (!up.TryGetValue(_room[idx], out lifted)) continue;   // emitting: leave alone

                    double lift = 0;
                    for (int li = 0; li < lights.Length; li++)
                    {
                        var L = lights[li];
                        double dx = (x - L.X) / (double)L.Rx;
                        double dy = (y - L.Y) / (double)L.Ry;
                        double r = Math.Sqrt(dx * dx + dy * dy);
                        if (r >= 1.0) continue;
                        lift += Math.Pow(1.0 - r, L.Falloff) * L.Strength;
                    }

                    // the fractional part becomes the dither: a 2x2 ordered pattern splits the seam
                    int add = (int)Math.Floor(lift);
                    double frac = lift - add;
                    int pat = ((x & 1) + ((y & 1) << 1));
                    if (frac > 0.62) add += 1;
                    else if (frac > 0.30 && pat < 2) add += 1;
                    if (add <= 0) continue;
                    if (add > MenuLayout.MaxLift) add = MenuLayout.MaxLift;

                    Color32 once = lifted;
                    if (add > 1)
                    {
                        Color32 twice;
                        once = up.TryGetValue(lifted, out twice) ? twice : lifted;
                    }
                    _room[idx] = once;
                }
            }
        }

        /// <summary>
        /// The vignette: the outer corners fall to `void`, and nothing else. No half-dither ring across the
        /// whole frame -- at this size a checker boundary draws a visible dome over the room.
        /// </summary>
        private void Vignette()
        {
            Color32 dark = Ink("void", new Color32(9, 11, 15, 255));
            int rx = W / 2, ry = H / 2;
            for (int y = 0; y < H; y++)
            {
                for (int x = 0; x < W; x++)
                {
                    int i = y * W + x;
                    if (_room[i].a == 0) continue;
                    if (x >= MenuLayout.CrtX && x < MenuLayout.CrtX + MenuLayout.CrtW &&
                        y >= MenuLayout.CrtY && y < MenuLayout.CrtY + MenuLayout.CrtH) continue;
                    double dx = (x - rx) / (double)rx;
                    double dy = (y - ry) / (double)ry;
                    double r = Math.Sqrt(dx * dx + dy * dy);
                    if (r > 1.24) _room[i] = dark;
                    else if (r > 1.12 && MenuLayout.Bayer4[((y & 3) << 2) | (x & 3)] < 5) _room[i] = dark;
                }
            }
        }

        /// <summary>Copies one kit sprite into the room buffer at canvas (x0, y0). Alpha 0 is skipped.</summary>
        private bool Blit(string resource, int x0, int y0)
        {
            var tex = Resources.Load<Texture2D>(KitRoot + resource);
            if (tex == null)
            {
                Debug.LogWarning("[HR//0D] menu room: Resources/" + KitRoot + resource + " missing.");
                return false;
            }
            Color32[] px;
            try { px = tex.GetPixels32(); }
            catch (Exception) { return false; }        // not readable: skip rather than crash the front door

            int tw = tex.width, th = tex.height;
            for (int y = 0; y < th; y++)
            {
                int dy = y0 + y;
                if (dy < 0 || dy >= H) continue;
                for (int x = 0; x < tw; x++)
                {
                    int dx = x0 + x;
                    if (dx < 0 || dx >= W) continue;
                    var c = px[(th - 1 - y) * tw + x];   // GetPixels32 is bottom-up; the buffer is top-down
                    if (c.a == 0) continue;
                    _room[dy * W + dx] = c;
                }
            }
            return true;
        }

        // ------------------------------------------------------------------ open / close

        public bool Open(bool hasSave, bool runInProgress, bool consentDecided, bool consentGranted,
                         float roomVolume, float sfxVolume, bool muted)
        {
            if (!Build()) return false;
            _hasSave = hasSave;
            _runInProgress = runInProgress;
            _consentDecided = consentDecided;
            _consentGranted = consentGranted;
            _roomVolume = roomVolume;
            _sfxVolume = sfxVolume;
            _muted = muted;
            _screen = MenuScreen.Root;
            _items = MenuRules.Root(_hasSave, _runInProgress);
            _index = 0;
            _open = true;
            _root.SetActive(true);
            // the six screens start at six different points in their cycles, or they blink in unison
            for (int i = 0; i < _screenPhase.Length; i++)
                _screenPhase[i] = (MenuLayout.DustHash(i, 7) & 0xFFFF) / 65535f * MenuLayout.StackCycle;
            _breachNext = UnityEngine.Random.Range((float)MenuLayout.BreachMin, (float)MenuLayout.BreachMax);
            _breachScreen = -1;
            _breachLeft = 0f;
            Paint();
            return true;
        }

        public void OpenSettings()
        {
            if (!_open) return;
            _screen = MenuScreen.Settings;
            _items = MenuRules.Settings(_muted);
            _index = 0;
            Paint();
        }

        public void Close()
        {
            _open = false;
            if (_root != null) _root.SetActive(false);
        }

        /// <summary>
        /// THE ANIMATION. Everything that moves in the front door moves here: the six wall screens cycle
        /// their readouts, one of them can flood red for the breach, and the dust falls. Nothing else
        /// moves -- a menu where the furniture drifts is a menu nobody can read.
        /// </summary>
        private void Update()
        {
            if (!_open || _tex == null) return;
            float dt = Time.unscaledDeltaTime;
            _clock += dt;

            // the breach: a random screen, rationed to one every 14-26 s
            if (_breachLeft > 0f)
            {
                _breachLeft -= dt;
                if (_breachLeft <= 0f) _breachScreen = -1;
            }
            else
            {
                _breachNext -= dt;
                if (_breachNext <= 0f)
                {
                    _breachScreen = UnityEngine.Random.Range(0, 6);
                    _breachLeft = (float)MenuLayout.BreachHold;
                    _breachNext = UnityEngine.Random.Range((float)MenuLayout.BreachMin, (float)MenuLayout.BreachMax);
                }
            }
            Paint();
        }

        // ------------------------------------------------------------------ input (called by the router)

        public void Move(int delta)
        {
            if (!_open) return;
            _index = MenuRules.MoveSkipping(_items, _index, delta);
            Paint();
        }

        public bool Step(int delta)
        {
            if (!_open || _screen != MenuScreen.Settings) return false;
            if (_index == 0) _roomVolume = MenuRules.StepVolume(_roomVolume, delta);
            else if (_index == 1) _sfxVolume = MenuRules.StepVolume(_sfxVolume, delta);
            else if (_index == 2) { if (delta != 0) _muted = !_muted; }
            else return false;              // DATA & CONSENT and BACK are chosen, not stepped

            if (_game != null) _game.ApplyAudioSettings(_roomVolume, _sfxVolume, _muted);
            _items = MenuRules.Settings(_muted);
            Paint();
            return true;
        }

        public bool Choose()
        {
            if (!_open || _index < 0 || _index >= _items.Count) return false;
            var item = _items[_index];
            if (!item.Enabled) return false;

            switch (item.Action)
            {
                case MenuAction.OpenSettings:
                    _screen = MenuScreen.Settings;
                    _items = MenuRules.Settings(_muted);
                    _index = 0;
                    Paint();
                    return false;

                case MenuAction.OpenConsent:
                    _screen = MenuScreen.Consent;
                    _items = MenuRules.Consent(_consentGranted, _consentDecided);
                    _index = 0;
                    Paint();
                    return false;

                case MenuAction.OpenCredits:
                    _screen = MenuScreen.Credits;
                    _items = new List<MenuItem> { new MenuItem(MenuAction.Back, "BACK", true) };
                    _index = 0;
                    Paint();
                    return false;

                case MenuAction.OpenManual:
                    _screen = MenuScreen.Manual;
                    _items = MenuScreenText.ManualRows();
                    _index = 0;
                    Paint();
                    return false;

                case MenuAction.Back:
                    _screen = MenuScreen.Root;
                    _items = MenuRules.Root(_hasSave, _runInProgress);
                    _index = 0;
                    Paint();
                    return false;

                case MenuAction.None:
                    ApplySettingRow();
                    return false;

                default:
                    return true;         // Continue / NewRun: the bootstrap decides what that means
            }
        }

        public bool Back()
        {
            if (!_open) return false;
            if (_screen != MenuScreen.Root)
            {
                _screen = MenuScreen.Root;
                _items = MenuRules.Root(_hasSave, _runInProgress);
                _index = 0;
                Paint();
                return false;
            }
            return true;                  // the bootstrap closes it and starts the desk
        }

        private void ApplySettingRow()
        {
            if (_screen == MenuScreen.Settings)
            {
                Step(1);                       // SPACE on a volume is "up"; LEFT/RIGHT is how you go down
                return;
            }
            if (_screen == MenuScreen.Consent && _index == 0)
            {
                // BUG-065: the row that LOOKS like the decision is the decision. One key, one toggle, saved.
                _consentDecided = true;
                _consentGranted = !_consentGranted;
                if (_game != null) _game.ApplyConsent(_consentGranted);
                _items = MenuRules.Consent(_consentGranted, _consentDecided);
                Paint();
            }
        }

        public MenuAction SelectedAction()
        {
            if (_index < 0 || _index >= _items.Count) return MenuAction.None;
            return _items[_index].Action;
        }

        // ------------------------------------------------------------------ paint

        /// <summary>
        /// One frame. Order matters and is asserted by the tests:
        ///   room -> live wall screens -> dust -> text -> flip -> upload
        /// The flip is LAST. Calling any raster helper after it inverts the y again and clips every line
        /// off the top of the glass (BUG-072: the shipped menu drew nothing at all).
        /// </summary>
        private void Paint()
        {
            if (_tex == null || _glyphs == null || _room == null) return;
            if (_frame == null || _frame.Length != W * H) _frame = new Color32[W * H];

            Array.Copy(_room, _frame, _frame.Length);

            StackScreens(_frame);
            Dust(_frame);
            Text(_frame);

            MonitorScreenRaster.FlipToTextureOrder(_frame, W, H);
            _tex.SetPixels32(_frame);
            _tex.Apply(false);
        }

        /// <summary>
        /// The rail of screens. Each one cycles a cybersecurity readout; a random one floods red for the
        /// breach. The tubes are the room's only other light, so they are drawn at full brightness and the
        /// wall behind them is left alone -- the screens are small enough not to need their own light pass.
        /// </summary>
        private void StackScreens(Color32[] buf)
        {
            // the six readouts cycle in this order; the table lives in MenuLayout so the twin renderer
            // (tools/art/make_menu_composite.py) paints exactly what the game paints
            var readouts = MenuLayout.StackReadouts;
            Color32 on = Ink("scr_1", new Color32(97, 104, 100, 255));
            Color32 hot = Ink("scr_2", new Color32(200, 238, 240, 255));
            Color32 dim = Ink("teal_1", new Color32(20, 50, 55, 255));
            Color32 off = Ink("void", new Color32(9, 11, 15, 255));
            Color32 red = Ink("red_2", new Color32(224, 63, 84, 255));

            for (int i = 0; i < 3; i++)
            {
                for (int r = 0; r < 2; r++)
                {
                    int sx = MenuLayout.StackScreenX[i] + 2;
                    int sy = MenuLayout.StackScreenY[r] + 2;
                    int sw = MenuLayout.StackScreenW - 4;
                    int sh = MenuLayout.StackScreenH - 4;
                    bool breached = (_breachScreen == i + r * 3 && _breachLeft > 0f);

                    // the flicker: a 3-step brightness on a fast, non-periodic beat
                    float phase = _clock * 2.2f + _screenPhase[i + r * 3] + r * 1.7f;
                    int flick = (int)(phase * 3f) % 5;
                    Color32 bg = breached ? red : (flick == 4 ? dim : (flick == 3 ? off : on));

                    for (int y = sy; y < sy + sh; y++)
                        for (int x = sx; x < sx + sw; x++)
                        {
                            if (x < 0 || y < 0 || x >= W || y >= H) continue;
                            buf[y * W + x] = bg;
                        }

                    // the readout itself: two lines of cybersecurity text, painted as bars of the
                    // glyph width the screen can hold. At 18 px tall, a paragraph is noise; two lines of
                    // "AUTH 0X4F2 / HANDSHAKE" is a machine reporting.
                    var lines = readouts[(i + r * 3) % readouts.Length];
                    for (int line = 0; line < lines.Length && line < 2; line++)
                    {
                        int ly = sy + 3 + line * 6;
                        int lw = Math.Min(lines[line].Length, sw - 4);
                        for (int c = 0; c < lw; c++)
                        {
                            int x = sx + 2 + c;
                            if (x < 0 || x >= W || ly < 0 || ly >= H) continue;
                            buf[ly * W + x] = breached ? hot : (flick == 4 ? dim : hot);
                            if (ly + 1 < H) buf[(ly + 1) * W + x] = breached ? red : (flick == 4 ? dim : on);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Dust that FALLS. Each mote is a 1 px dot with a hash-derived start position, speed and phase; it
        /// wraps around the frame as it falls, drifting left with the room's draught. Nothing about it is
        /// random per frame, so the motes travel instead of sparkling.
        /// </summary>
        private void Dust(Color32[] buf)
        {
            if (MenuLayout.DustCount <= 0) return;
            Color32 grain = Ink("steel_1", new Color32(26, 31, 38, 255));
            Color32 faint = Ink("wall_2", new Color32(24, 28, 32, 255));
            for (int i = 0; i < MenuLayout.DustCount; i++)
            {
                uint h1 = MenuLayout.DustHash(i, 3);
                uint h2 = MenuLayout.DustHash(i, 11);
                double speed = MenuLayout.DustFallSpeed * (0.55 + (h2 & 0xFF) / 255.0 * 0.9);
                double x0 = (h1 & 0xFFFF) / 65535.0 * W;
                double y0 = ((h1 >> 16) & 0xFFFF) / 65535.0 * H;
                double y = (y0 + _clock * speed) % H;
                double x = (x0 - _clock * MenuLayout.DustDriftSpeed * (0.6 + ((h2 >> 8) & 0xFF) / 255.0)
                            + Math.Sin(_clock * 0.7 + i) * 1.6) % W;
                if (x < 0) x += W;
                int xi = (int)x, yi = (int)y;
                if (xi < 0 || yi < 0 || xi >= W || yi >= H) continue;
                int p = yi * W + xi;
                if (buf[p].a == 0) continue;
                // never on the glass: a mote on the screen you are reading is a dead pixel
                if (xi >= MenuLayout.GlassX && xi < MenuLayout.GlassX + MenuLayout.GlassW &&
                    yi >= MenuLayout.GlassY && yi < MenuLayout.GlassY + MenuLayout.GlassH) continue;
                buf[p] = (i % 3 == 0) ? grain : faint;
                if (i % 7 == 0 && xi + 1 < W) buf[yi * W + xi + 1] = faint;
            }
        }

        /// <summary>Every line the door paints. Nothing here may touch the buffer after the flip.</summary>
        private void Text(Color32[] buf)
        {
            // the title block: 2x, tight tracking, glyphs bled 1 px for weight; ZERO DAY 4 px under it
            Title(buf, Title_(), MenuLayout.TitleTop, "white", MenuLayout.TitleScale);
            string sub = Subtitle_();
            if (!string.IsNullOrEmpty(sub))
                Title(buf, sub, MenuLayout.SubtitleTop, "red_2", MenuLayout.TitleScale);
            string tag = Tagline_();
            if (!string.IsNullOrEmpty(tag)) Centre(buf, tag, MenuLayout.TaglineTop, "amber_3", 1);

            // a rule between the title block and the rows: the concept has one and it is what stops the
            // header reading as a fourth row
            Rule(buf, MenuLayout.RuleTop);

            var rows = BuildRows();
            for (int i = 0; i < rows.Count; i++)
            {
                var pill = MenuLayout.Row(i, rows[i].Selected ? "cyan_1" : "void",
                                          rows[i].Selected ? "void" : "paper_2");
                DrawPill(buf, pill, rows[i].Selected);
                CentreIn(buf, rows[i].Label, pill, rows[i].Selected ? "void" : "paper_2");
            }

            Centre(buf, Footer_(), MenuLayout.FooterTop, "paper_2", 1);
        }

        private void Rule(Color32[] buf, int y)
        {
            Color32 c = Ink("steel_1", new Color32(26, 31, 38, 255));
            int x0 = MenuLayout.TextX + 14, x1 = MenuLayout.TextX + MenuLayout.TextW - 14;
            if (y < 0 || y >= H) return;
            for (int x = x0; x < x1; x++)
            {
                // the rule fades at both ends: a solid line across the tube reads as a divider, a fading
                // one reads as a highlight
                int d = Math.Min(x - x0, x1 - x);
                if (d < 10 && ((x + y) & 1) == 0) continue;
                if (d < 4 && ((x & 1) == 1)) continue;
                buf[y * W + x] = c;
            }
        }

        private void DrawPill(Color32[] buf, MenuLayout.Pill p, bool selected)
        {
            Color32 fill = Ink(p.Fill, new Color32(9, 11, 15, 255));
            Color32 edge = Ink(selected ? "cyan_2" : "steel_1", new Color32(26, 31, 38, 255));
            for (int y = p.Y; y < p.Y + p.H; y++)
            {
                if (y < MenuLayout.GlassY || y >= MenuLayout.GlassY + MenuLayout.GlassH) continue;
                for (int x = p.X; x < p.X + p.W; x++)
                {
                    if (x < MenuLayout.GlassX || x >= MenuLayout.GlassX + MenuLayout.GlassW) continue;
                    bool border = y == p.Y || y == p.Y + p.H - 1;
                    if (!selected && (x == p.X || x == p.X + p.W - 1)) border = true;
                    buf[y * W + x] = border ? edge : fill;
                }
            }
            // the selection breathes: one row of the pill's cap is one step brighter at 1 Hz
            if (selected && Math.Sin(_clock * 3.1) > 0.55)
            {
                Color32 pulse = Ink("cyan_2", new Color32(101, 222, 224, 255));
                int y = p.Y + p.H - 1;
                if (y >= 0 && y < H)
                    for (int x = p.X + 2; x < p.X + p.W - 2 && x < W; x++) buf[y * W + x] = pulse;
            }
        }

        /// <summary>
        /// Draws `text` centred on the TUBE at `top`, with the title's tracking and bleed. Scaling has to be
        /// done by the caller that knows the cell size: MonitorScreenRaster steps the pen by the ATLAS cell
        /// regardless of the source, so a 2x title drawn through it comes out as rubble (BUG-067).
        /// </summary>
        private void Title(Color32[] buf, string text, int top, string ink, int scale)
        {
            int width = MenuLayout.LineWidth(text, scale);
            int left = MenuLayout.TextX + (MenuLayout.TextW - width) / 2;
            DrawScaled(buf, text, left, top, ink, scale, MenuLayout.TitleGap, MenuLayout.TitleBleed);
        }

        private void Centre(Color32[] buf, string text, int top, string ink, int scale)
        {
            int cell = (MenuLayout.GlyphW + MenuLayout.LetterGap) * scale;
            int width = text.Length * cell - MenuLayout.LetterGap * scale;
            int left = MenuLayout.TextX + (MenuLayout.TextW - width) / 2;
            DrawScaled(buf, text, left, top, ink, scale, MenuLayout.LetterGap, 0);
        }

        private void CentreIn(Color32[] buf, string text, MenuLayout.Pill pill, string ink)
        {
            int width = text.Length * (MenuLayout.GlyphW + MenuLayout.LetterGap) - MenuLayout.LetterGap;
            int left = pill.X + (pill.W - width) / 2;
            int top = pill.Y + (pill.H - MenuLayout.GlyphH) / 2;
            DrawScaled(buf, text, left, top, ink, 1, MenuLayout.LetterGap, 0);
        }

        /// <summary>
        /// One string, one scale, one pen step. `gap` and `bleed` are the tracking controls: the title uses
        /// (1, 1) so it is tighter and heavier than every other line (v118's type change), the rows use the
        /// atlas's own (2, 0).
        /// </summary>
        private void DrawScaled(Color32[] buf, string text, int left, int top, string ink, int scale,
                                int gap, int bleed)
        {
            Color32 colour = Ink(ink, new Color32(230, 241, 255, 255));
            int cellW = MenuLayout.GlyphW + gap;
            for (int ci = 0; ci < text.Length; ci++)
            {
                char ch = text[ci];
                if (ch == ' ') continue;
                int penX = left + ci * cellW * scale;
                for (int gy = 0; gy < MenuLayout.GlyphH * scale; gy++)
                {
                    int ty = top + gy;
                    if (ty < 0 || ty >= H) continue;
                    for (int gx = 0; gx < MenuLayout.GlyphW * scale + bleed; gx++)
                    {
                        int tx = penX + gx;
                        if (tx < 0 || tx >= W) continue;
                        if (!InGlass(tx, ty)) continue;
                        // the bleed column repeats the glyph's rightmost ink column, which thickens it
                        int srcX = gx / scale;
                        if (srcX >= MenuLayout.GlyphW) srcX = MenuLayout.GlyphW - 1;
                        if (!_glyphs.InkAt(ch, srcX, gy / scale)) continue;
                        buf[ty * W + tx] = colour;
                    }
                }
            }
        }

        /// <summary>
        /// The tube's picture area, not the flat glass rect. A CRT's corners are cut, so a glyph that fits
        /// the rect can still hang off the tube -- which is what the player saw as "the text is offscreen".
        /// </summary>
        private static bool InGlass(int x, int y)
        {
            int x0 = MenuLayout.TextX, y0 = MenuLayout.TextY;
            return x >= x0 && x < x0 + MenuLayout.TextW && y >= y0 && y < y0 + MenuLayout.TextH;
        }

        private string Title_()
        {
            if (_screen == MenuScreen.Settings) return "SETTINGS";
            if (_screen == MenuScreen.Consent) return "DATA AND CONSENT";
            if (_screen == MenuScreen.Credits) return "CREDITS";
            if (_screen == MenuScreen.Manual) return "FIELD MANUAL";
            return MenuScreenText.Title;
        }

        private string Subtitle_()
        {
            if (_screen == MenuScreen.Root) return MenuScreenText.Subtitle;
            return "";
        }

        private string Tagline_()
        {
            if (_screen == MenuScreen.Root) return MenuScreenText.Tagline;
            if (_screen == MenuScreen.Consent) return "RECORDS DECISIONS, NEVER YOUR NAME";
            if (_screen == MenuScreen.Manual) return "L INSPECT  A APPROVE  S REJECT";
            if (_screen == MenuScreen.Credits) return "MADE WITH THE ARENA AGENT";
            return "";
        }

        private string Footer_()
        {
            if (_screen == MenuScreen.Settings) return "[LEFT/RIGHT] CHANGE   [ESC] BACK";
            if (_screen == MenuScreen.Consent)
                return MenuRules.ConsentState(_consentGranted, _consentDecided);
            return _muted ? "[SPACE] CHOOSE   SOUND OFF" : "[SPACE] CHOOSE   [S] SOUND";
        }

        private List<MenuRow> BuildRows()
        {
            switch (_screen)
            {
                case MenuScreen.Manual:
                    var man = new List<MenuRow>();
                    for (int i = 0; i < _items.Count; i++)
                        man.Add(new MenuRow(_items[i].Action, _items[i].Label,
                                            _index == i ? "cyan_2" : "paper_2", _index == i));
                    return man;
                case MenuScreen.Credits:
                    return new List<MenuRow> { new MenuRow(MenuAction.Back, "BACK", "cyan_2", true) };
                case MenuScreen.Settings:
                    return MenuScreenText.Settings(_muted, _roomVolume, _sfxVolume, _index);
                case MenuScreen.Consent:
                    return MenuScreenText.Consent(_consentGranted, _consentDecided, _index);
                default:
                    return MenuScreenText.Root(_hasSave, _runInProgress, _index);
            }
        }

        private static Color32 Ink(string key, Color32 fallback)
        {
            Color32 value;
            var palette = MonitorScreenView.PaletteCached;
            return palette != null && palette.TryGetValue(key, out value) ? value : fallback;
        }

        private void OnDestroy()
        {
            if (_tex != null) Destroy(_tex);
        }
    }
}
