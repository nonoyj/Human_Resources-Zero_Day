// HR//0D -- MenuView: the front door, drawn as the room it belongs to.
// Assembly: HRZeroDay.Game  (UnityEngine)
//
// WHAT THE PLAYER ASKED FOR (concept, 2026-11-28; ADR-017/018)
//   "The menu items are on the pixel computer, then a coffee underneath with applicant files on the table,
//    then monitors in the background with an aesthetic view of the city in the night with a server on the
//    side. Overall look has a cyan glow from the night sky and the monitor screen."
//
// WHAT THIS IS
//   One 480x270 texture, built at runtime from kit sprites laid out by MenuLayout:
//       background (night city)  ->  server rack  ->  monitor_big  ->  cyan halo  ->  desk band
//   then dimmed, then the menu is painted INTO the monitor's glass: a 3x title, a red 3x subtitle, the
//   amber tagline, and the option rows as text pills (the selected one filled with the signal colour).
//   Nothing here is a new art style: every pixel comes from the game's atlas and palette.
//
// WHY COMPOSE AT RUNTIME INSTEAD OF SHIPPING ONE BIG PNG
//   The desk already knows where the monitor, the desk band and the props are. A single baked PNG would be
//   a second source of truth -- change the monitor and the menu keeps the old one. This composes from the
//   same sprites the desk uses, at the coordinates in MenuLayout, and the twin renderer
//   (tools/art/make_menu_composite.py) reads the same numbers.
//
// FAILURE IS SILENT: no sprites, no atlas, an exception -- the desk starts as it did before v110. A front
// door that can stop the game opening is worse than no front door.
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

        // the composed room, built once, dimmed per open
        private Color32[] _room;

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
            _tex.name = "MenuRoom";
            _card.sprite = Sprite.Create(_tex, new Rect(0, 0, W, H), new Vector2(0.5f, 0.5f), Ppu);

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
            // THE ROOM, BACK TO FRONT (v115, ADR-019). Every piece is from art/kit/menu/, drawn by
            // tools/art/make_menu_room.py from the player's brief. A missing sprite is skipped and logged:
            // a menu with no server is still a menu, and the desk must never fail to open.
            Blit("menu/shell", 0, 0, true);
            Blit("menu/window", MenuLayout.WindowX, MenuLayout.WindowY, false);
            Blit("menu/monitor_stack", MenuLayout.StackX, MenuLayout.StackY, false);
            Blit("menu/server", MenuLayout.ServerX, MenuLayout.ServerY, false);
            Blit("menu/crt", MenuLayout.CrtX, MenuLayout.CrtY, false);
            Blit("menu/desk", 0, MenuLayout.DeskY, false);
            GlassMaterial();
            Light();
            return true;
        }

        /// <summary>
        /// The tube itself: an off-black phosphor with a scanline every three rows, brightest in the top
        /// third, and one emissive line just inside the bezel on the top and left. Painted before the light
        /// pass (which skips the monitor entirely) and before the menu text.
        /// </summary>
        private void GlassMaterial()
        {
            var palette = MonitorScreenView.PaletteCached;
            if (palette == null) return;
            Color32 paper, wash, dim, edge;
            if (!palette.TryGetValue("void", out paper)) return;
            if (!palette.TryGetValue("glow_teal", out wash)) return;
            if (!palette.TryGetValue("panel", out dim)) return;
            if (!palette.TryGetValue("signaldim", out edge)) return;

            int x0 = MenuLayout.GlassX, y0 = MenuLayout.GlassY;
            int x1 = x0 + MenuLayout.GlassW, y1 = y0 + MenuLayout.GlassH;
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
            // the tube's own edge: a single emissive line on the top and left, never on all four sides
            for (int x = x0; x < x1; x++) _room[y0 * W + x] = edge;
            for (int y = y0; y < y1; y++) _room[y * W + x0] = edge;
        }

        /// <summary>
        /// Paints the ambience. Each light raises the VALUE of the surfaces it reaches, in ramp steps
        /// (MenuLayout.Ramp), and the step boundary is the only place a dither appears. A light never
        /// replaces a colour with its own -- that is what turned the first attempt into cyan blobs.
        /// </summary>
        private void Light()
        {
            var palette = MonitorScreenView.PaletteCached;
            if (palette == null) return;

            // reverse lookup: a room pixel's colour -> its ramp index. One pass, then it is arithmetic.
            var rampIndex = new Dictionary<Color32, int>();
            for (int i = 0; i < MenuLayout.Ramp.Length; i++)
            {
                Color32 c;
                if (palette.TryGetValue(MenuLayout.Ramp[i], out c)) rampIndex[c] = i;
            }
            var ramp = new Color32[MenuLayout.Ramp.Length];
            for (int i = 0; i < ramp.Length; i++)
                ramp[i] = palette.TryGetValue(MenuLayout.Ramp[i], out ramp[i]) ? ramp[i] : ramp[i];

            // glow_teal is a DARK teal: that is the colour of light after it has left the screen and is
            // bouncing off plastic. signaldim is a lit LED and must never be used for ambience.
            Color32 teal, tealDim;
            palette.TryGetValue("glow_teal", out teal);
            palette.TryGetValue("signaldim", out tealDim);

            var lights = MenuLayout.Lights();
            for (int y = 0; y < H; y++)
            {
                for (int x = 0; x < W; x++)
                {
                    int idx = y * W + x;
                    if (_room[idx].a == 0) continue;

                    // THE GLASS IS A SOURCE, NOT A SURFACE. Every pass below skips the monitor's own case
                    // and tube: lighting the screen you are reading made the menu speckle and, worse, let
                    // the cyan fade paint over the title. The screen lights the room; nothing lights it.
                    if (x >= MenuLayout.CrtX && x < MenuLayout.CrtX + 240 &&
                        y >= MenuLayout.CrtY && y < MenuLayout.CrtY + 172) continue;

                    int step;
                    if (!rampIndex.TryGetValue(_room[idx], out step)) continue;   // emitting colours: leave alone

                    double lift = 0;
                    for (int li = 0; li < lights.Length; li++)
                    {
                        var L = lights[li];
                        double dx = (x - L.X) / (double)L.Rx;
                        double dy = (y - L.Y) / (double)L.Ry;
                        double r = Math.Sqrt(dx * dx + dy * dy);
                        if (r >= 1.0) continue;
                        double fall = Math.Pow(1.0 - r, L.Falloff);
                        lift += fall * L.Strength;
                    }

                    int add = (int)Math.Floor(lift);
                    // the fractional part becomes the dither: a 2x2 ordered pattern splits the seam
                    double frac = lift - add;
                    int pat = ((x & 1) + ((y & 1) << 1));
                    if (frac > 0.65) add += 1;
                    else if (frac > 0.35 && pat < 2) add += 1;

                    if (add <= 0) continue;
                    if (add > MenuLayout.MaxLift) add = MenuLayout.MaxLift;
                    int target = Math.Min(step + add, MenuLayout.Ramp.Length - 1);

                    // CYAN ONLY NEAR THE SCREEN. Everywhere else a lit surface is a brighter neutral, which
                    // is what makes the room read as a lit office rather than a blue-lit aquarium.
                    int dxGlass = 0;
                    if (x < MenuLayout.GlassX) dxGlass = MenuLayout.GlassX - x;
                    else if (x >= MenuLayout.GlassX + MenuLayout.GlassW)
                        dxGlass = x - (MenuLayout.GlassX + MenuLayout.GlassW);
                    int dyGlass = 0;
                    if (y < MenuLayout.GlassY) dyGlass = MenuLayout.GlassY - y;
                    else if (y >= MenuLayout.GlassY + MenuLayout.GlassH)
                        dyGlass = y - (MenuLayout.GlassY + MenuLayout.GlassH);
                    bool near = dxGlass + dyGlass < MenuLayout.CyanNear;
                    _room[idx] = near ? (Color32)teal : ramp[target];

                    // THE CYAN FADE. The brief's one explicit art direction: light coming OUT of the
                    // monitor. Measured as distance from the CASE, not from an ellipse around it, so the
                    // wall beside it and the desk directly under it are the first things it touches.
                    // It only ever lands on a DARK surface: a bright surface lifted into teal just becomes a
                    // turquoise panel, which is exactly what the first passes looked like.
                    if (step <= 1)
                    {
                        int fx = 0, fy = 0;
                        if (x < MenuLayout.CrtX) fx = MenuLayout.CrtX - x;
                        else if (x >= MenuLayout.CrtX + 240) fx = x - (MenuLayout.CrtX + 239);
                        if (y < MenuLayout.CrtY) fy = MenuLayout.CrtY - y;
                        else if (y >= MenuLayout.CrtY + 172) fy = y - (MenuLayout.CrtY + 171);
                        int fd = fx > fy ? fx : fy;
                        if (fd < MenuLayout.FadeReach)
                        {
                            double fall = 1.0 - fd / (double)MenuLayout.FadeReach;
                            int thr = (int)(Math.Pow(fall, 1.15) * 15.0);
                            int pat4 = (x & 3) + ((y & 3) << 2);
                            if (pat4 < thr) _room[idx] = teal;
                        }
                    }
                }
            }

            // the CRT's own spill: the 6 px immediately around the case take the brightest cyan step, so the
            // screen reads as the source of the light rather than as a lit object
            Halo(teal, tealDim);
            Vignette();
        }

        private void Halo(Color32 teal, Color32 tealDim)
        {
            for (int ring = 1; ring <= 2; ring++)
            {
                int x0 = MenuLayout.CrtX - ring, y0 = MenuLayout.CrtY - ring;
                int x1 = MenuLayout.CrtX + 240 + ring, y1 = MenuLayout.CrtY + 172 + ring;
                for (int x = x0; x <= x1; x++)
                {
                    PaintHalo(x, y0, ring, teal, tealDim);
                    PaintHalo(x, y1, ring, teal, tealDim);
                }
                for (int y = y0; y <= y1; y++)
                {
                    PaintHalo(x0, y, ring, teal, tealDim);
                    PaintHalo(x1, y, ring, teal, tealDim);
                }
            }
        }

        private void PaintHalo(int x, int y, int ring, Color32 teal, Color32 tealDim)
        {
            if (x < 0 || y < 0 || x >= W || y >= H) return;
            int i = y * W + x;
            if (_room[i].a == 0) return;
            // a DARK teal, and ring 2 is dithered at one-in-four: the bounce off a plastic case is subtle.
            // Painting this in the bright signal colour produced two neon bars over the whole frame.
            if (ring == 2 && ((x + y) & 3) != 0) return;
            _room[i] = teal;
        }

        /// <summary>Ambient: the four corners fall one step. Drawn last, so it never fights a light.</summary>
        private void Vignette()
        {
            Color32 dark;
            var palette = MonitorScreenView.PaletteCached;
            if (palette == null || !palette.TryGetValue("void", out dark)) return;
            int rx = W / 2, ry = H / 2;
            for (int y = 0; y < H; y++)
            {
                for (int x = 0; x < W; x++)
                {
                    int i = y * W + x;
                    if (_room[i].a == 0) continue;
                    if (x >= MenuLayout.CrtX && x < MenuLayout.CrtX + 240 &&
                        y >= MenuLayout.CrtY && y < MenuLayout.CrtY + 172) continue;   // the glass is a source
                    double dx = (x - rx) / (double)rx;
                    double dy = (y - ry) / (double)ry;
                    double r = Math.Sqrt(dx * dx + dy * dy);
                    if (r > 1.25) { _room[i] = dark; continue; }
                    if (r >= 1.0 && ((x + y) & 1) == 0) _room[i] = dark;
                }
            }
        }

        /// <summary>Copies one kit sprite into the room buffer at canvas (x0, y0). Alpha 0 is skipped.</summary>
        private bool Blit(string resource, int x0, int y0, bool mustFill)
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
                    var c = px[(th - 1 - y) * tw + x];   // PNG order is top-down, the buffer is top-down too
                    if (c.a == 0) continue;
                    _room[dy * W + dx] = c;
                }
            }
            return true;
        }

        private void Put(int x, int y, Color32 ink)
        {
            if (x < 0 || y < 0 || x >= W || y >= H) return;
            int i = y * W + x;
            if (_room[i].a == 0) return;
            _room[i] = ink;
        }

        /// <summary>
        /// Opens the root screen. `hasSave` comes from the bootstrap: a fresh profile has no run to
        /// continue, and CONTINUE must not be offered at all (MenuRules.Root decides that, not this view).
        /// </summary>
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
            Paint();
            return true;
        }

        /// <summary>The S shortcut: settings, without moving the cursor there first.</summary>
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

        // ------------------------------------------------------------------ input (called by the router)
        public void Move(int delta)
        {
            if (!_open) return;
            _index = MenuRules.MoveSkipping(_items, _index, delta);
            Paint();
        }

        /// <summary>
        /// LEFT/RIGHT (and A/D). On a settings row this is the whole point: v110 only ever moved a volume up,
        /// so turning one down was one-way. Returns true when the press changed something.
        /// </summary>
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

        /// <summary>SPACE/ENTER. Returns true when the row did something the bootstrap must handle.</summary>
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
                    // the manual is the controls page: same glass, one row back, no second window
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

        /// <summary>ESC: back one screen, or close the menu if it is already at the root.</summary>
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
        private void Paint()
        {
            if (_tex == null || _glyphs == null || _room == null) return;

            var buf = new Color32[W * H];
            Array.Copy(_room, buf, buf.Length);

            var rows = new List<MonitorScreenRaster.RasterRow>();
            var pills = new List<MenuLayout.Pill>();

            // ---- the title block: the concept's proportions, 2x for the title and its red second line
            Centre(buf, _glyphs, Title(), MenuLayout.TitleTop, "text", MenuLayout.TitleScale);
            string sub = Subtitle();
            if (!string.IsNullOrEmpty(sub))
                Centre(buf, _glyphs, sub, MenuLayout.SubtitleTop, "danger", MenuLayout.TitleScale);
            string tag = Tagline();
            if (!string.IsNullOrEmpty(tag)) Centre(buf, _glyphs, tag, MenuLayout.TaglineTop, "warn", 1);

            // ---- the option rows
            var menuRows = BuildRows();
            for (int i = 0; i < menuRows.Count; i++)
            {
                var pill = MenuLayout.Row(i, menuRows[i].Selected ? "signal" : "void",
                                          menuRows[i].Selected ? "void" : "text");
                pills.Add(pill);
                CentreIn(buf, _glyphs, menuRows[i].Label, pill, menuRows[i].Selected ? "void" : "text");
            }

            // ---- the small print, at the bottom of the glass
            string foot = Footer();
            Centre(buf, _glyphs, foot, MenuLayout.FooterTop, "textdim", 1);

            PaintPills(buf, pills);
            MonitorScreenRaster.RenderRows(buf, W, H, rows, _glyphs,
                MonitorScreenRaster.RasterClip.Box(MenuLayout.GlassX, MenuLayout.GlassY,
                                                   MenuLayout.GlassW, MenuLayout.GlassH));
            MonitorScreenRaster.FlipToTextureOrder(buf, W, H);
            _tex.SetPixels32(buf);
            _tex.Apply(false);
        }

        private string Title()
        {
            if (_screen == MenuScreen.Settings) return "SETTINGS";
            if (_screen == MenuScreen.Consent) return "DATA AND CONSENT";
            if (_screen == MenuScreen.Credits) return "CREDITS";
            if (_screen == MenuScreen.Manual) return "FIELD MANUAL";
            return MenuScreenText.Title;
        }

        private string Subtitle()
        {
            if (_screen == MenuScreen.Root) return MenuScreenText.Subtitle;
            return "";
        }

        private string Tagline()
        {
            if (_screen == MenuScreen.Root) return MenuScreenText.Tagline;
            if (_screen == MenuScreen.Consent) return "RECORDS DECISIONS, NEVER YOUR NAME";
            if (_screen == MenuScreen.Manual) return "L INSPECT  A APPROVE  S REJECT";
            if (_screen == MenuScreen.Credits) return "MADE WITH THE ARENA AGENT";
            return "";
        }

        private List<MenuRow> BuildRows()
        {
            switch (_screen)
            {
                case MenuScreen.Manual:
                    var man = new List<MenuRow>();
                    for (int i = 0; i < _items.Count; i++)
                        man.Add(new MenuRow(_items[i].Action, _items[i].Label,
                                            _index == i ? "signal" : "text", _index == i));
                    return man;
                case MenuScreen.Credits:
                    return new List<MenuRow> { new MenuRow(MenuAction.Back, "BACK", "signal", true) };
                case MenuScreen.Settings:
                    return MenuScreenText.Settings(_muted, _roomVolume, _sfxVolume, _index);
                case MenuScreen.Consent:
                    return MenuScreenText.Consent(_consentGranted, _consentDecided, _index);
                default:
                    return MenuScreenText.Root(_hasSave, _runInProgress, _index);
            }
        }

        private static bool InGlass(int x, int y)
        {
            return x >= MenuLayout.GlassX && x < MenuLayout.GlassX + MenuLayout.GlassW &&
                   y >= MenuLayout.GlassY && y < MenuLayout.GlassY + MenuLayout.GlassH;
        }

        /// <summary>A pill: filled body, 1 px edge in the same ink, drawn before the label lands on it.</summary>
        private void PaintPills(Color32[] buf, List<MenuLayout.Pill> pills)
        {
            var edge = Ink("linehi", new Color32(63, 85, 112, 255));
            foreach (var p in pills)
            {
                var fill = Ink(p.Fill, new Color32(29, 39, 53, 255));
                bool selected = p.Fill == "signal";
                for (int y = p.Y; y < p.Y + p.H; y++)
                {
                    if (y < MenuLayout.GlassY || y >= MenuLayout.GlassY + MenuLayout.GlassH) continue;
                    for (int x = p.X; x < p.X + p.W; x++)
                    {
                        if (x < MenuLayout.GlassX || x >= MenuLayout.GlassX + MenuLayout.GlassW) continue;
                        bool border = y == p.Y || y == p.Y + p.H - 1 || x == p.X || x == p.X + p.W - 1;
                        buf[y * W + x] = border && !selected ? edge : fill;
                    }
                }
            }
        }

        /// <summary>
        /// Draws `text` centred in the glass at `top`, at an integer scale.
        ///
        /// BUG-067 (v114): this used to call MonitorScreenRaster.RenderRows with a scaled glyph source. That
        /// renderer steps the pen by the ATLAS cell (5 + 2 px) regardless of the source, so a 3x title drew
        /// three glyphs' worth of pixels into one cell and the title came out as rubble. Scaling has to be
        /// done by the caller that knows the cell size, which is this one.
        /// </summary>
        private void Centre(Color32[] buf, IGlyphSource glyphs, string text, int top, string ink, int scale)
        {
            int cell = MenuLayout.GlyphW + MenuLayout.LetterGap;
            int width = text.Length * cell * scale - MenuLayout.LetterGap * scale;
            int left = MenuLayout.GlassX + (MenuLayout.GlassW - width) / 2;
            DrawScaled(buf, glyphs, text, left, top, ink, scale);
        }

        /// <summary>One string, one scale, one pen step. The only text painter the front door uses.</summary>
        private void DrawScaled(Color32[] buf, IGlyphSource glyphs, string text, int left, int top,
                                string ink, int scale)
        {
            var colour = Ink(ink, new Color32(230, 241, 255, 255));
            int cellW = MenuLayout.GlyphW + MenuLayout.LetterGap;
            for (int ci = 0; ci < text.Length; ci++)
            {
                char ch = text[ci];
                int penX = left + ci * cellW * scale;
                for (int gy = 0; gy < MenuLayout.GlyphH * scale; gy++)
                {
                    int ty = top + gy;
                    if (ty < 0 || ty >= H) continue;
                    for (int gx = 0; gx < MenuLayout.GlyphW * scale; gx++)
                    {
                        int tx = penX + gx;
                        if (tx < 0 || tx >= W) continue;
                        if (!InGlass(tx, ty)) continue;
                        if (!glyphs.InkAt(ch, gx / scale, gy / scale)) continue;
                        buf[ty * W + tx] = colour;
                    }
                }
            }
        }

        private void CentreIn(Color32[] buf, IGlyphSource glyphs, string text, MenuLayout.Pill pill, string ink)
        {
            int width = text.Length * (MenuLayout.GlyphW + MenuLayout.LetterGap) - MenuLayout.LetterGap;
            int left = pill.X + (pill.W - width) / 2;
            int top = pill.Y + (pill.H - MenuLayout.GlyphH) / 2;
            DrawScaled(buf, glyphs, text, left, top, ink, 1);
        }

        private static MonitorScreenRaster.RasterRow Row(string text, int x, int top, string ink)
        {
            return new MonitorScreenRaster.RasterRow { text = text, x = x, top = top, colour = ink };
        }

        private string Footer()
        {
            if (_screen == MenuScreen.Settings) return "[LEFT/RIGHT] CHANGE  [ESC] BACK";
            if (_screen == MenuScreen.Consent)
                return MenuRules.ConsentState(_consentGranted, _consentDecided);
            return _muted ? "[SPACE] CHOOSE   SOUND OFF" : "[SPACE] CHOOSE   [S] SOUND";
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
