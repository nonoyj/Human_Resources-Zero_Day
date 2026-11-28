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
            bool any = false;
            // THE CONCEPT'S FRAME (v114): a dark office, the bank of wall screens with cyan data on it,
            // the night city, a server cabinet at the far right, the desk band, the clearance card, and the
            // big monitor in front with the menu inside it. Order is depth: wall -> server -> monitor -> desk.
            any |= Blit("backgrounds/office_L0", 0, 0, true);
            any |= Blit("desk/wall_monitors", MenuLayout.WallX, MenuLayout.WallY, false);
            any |= Blit("desk/wall_labels", MenuLayout.LabelX, MenuLayout.LabelY, false);
            any |= Blit("desk/server_rack", MenuLayout.ServerX, MenuLayout.ServerY, false);
            any |= Blit("desk/menu_monitor", MenuLayout.MonitorX, MenuLayout.MonitorY, false);
            Halo();
            any |= Blit("desk/desk_foreground", 0, MenuLayout.DeskY, false);
            Widget();
            if (!any) Debug.LogWarning("[HR//0D] menu room: no kit sprites found under Resources/" + KitRoot);
            return true;
        }

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
            return mustFill || true;
        }

        /// <summary>The cyan the screen and the window throw. Flat steps, two rings, one pool on the desk.</summary>
        private void Halo()
        {
            var teal = Ink("glow_teal", new Color32(12, 61, 56, 255));
            var dim = Ink("signaldim", new Color32(15, 168, 138, 255));
            int x0 = MenuLayout.MonitorX, y0 = MenuLayout.MonitorY;
            int x1 = x0 + 300, y1 = y0 + 220;      // the case, not the glass: light spills outside the bezel
            for (int i = 1; i <= 2; i++)
            {
                var ink = i == 1 ? dim : teal;
                for (int x = x0 - i; x <= x1 - 1 + i; x++)
                {
                    Put(x, y0 - i, ink);
                    Put(x, y1 - 1 + i, ink);
                }
                for (int y = y0 - i; y <= y1 - 1 + i; y++)
                {
                    Put(x0 - i, y, ink);
                    Put(x1 - 1 + i, y, ink);
                }
            }
            for (int i = 0; i < 2; i++)
            {
                int y = y1 + 2 + i * 3;
                int half = 130 - i * 34;
                for (int x = (x0 + x1) / 2 - half; x < (x0 + x1) / 2 + half; x++) Put(x, y, i == 0 ? teal : dim);
            }
        }

        /// <summary>
        /// The clearance card the concept puts in the top-right corner: a dark panel, a silhouette, the ID and
        /// the clearance tier, and three small bars. Painted, not a sprite, because it is five rectangles and
        /// a row of type -- and it is the one place the front door shows you who you are before you start.
        /// </summary>
        private void Widget()
        {
            var panel = Ink("panel", new Color32(19, 25, 38, 255));
            var edge = Ink("line", new Color32(43, 58, 77, 255));
            var hi = Ink("linehi", new Color32(63, 85, 112, 255));
            int x = MenuLayout.WidgetX, y = MenuLayout.WidgetY, w = 62, h = 30;
            for (int yy = y; yy < y + h; yy++)
                for (int xx = x; xx < x + w; xx++)
                {
                    bool border = yy == y || yy == y + h - 1 || xx == x || xx == x + w - 1;
                    Put(xx, yy, border ? edge : panel);
                }
            // head + shoulders, four px wide: a silhouette, not a portrait
            int cx = x + 11;
            for (int yy = y + 6; yy < y + 12; yy++) Put(cx + (yy % 2), yy, hi);
            for (int yy = y + 13; yy < y + 22; yy++) Put(cx - 2 + (yy - 13) / 3, yy, hi);
            for (int yy = y + 13; yy < y + 22; yy++) Put(cx + 3 - (yy - 13) / 3, yy, hi);
            // three tier bars
            for (int i = 0; i < 3; i++)
                for (int xx = x + 8 + i * 14; xx < x + 18 + i * 14; xx++)
                    Put(xx, y + h - 6, Ink(i == 0 ? "orange" : "linehi", hi));
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
            Dim(buf);

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

        /// <summary>
        /// The room at night: each surface drops one palette step (MenuLayout.Darken); light-emitting
        /// colours are left alone, so the window, the stars and the server LEDs still glow. Done by colour
        /// lookup, not by index, because the room buffer holds whatever the kit sprites drew.
        /// </summary>
        private void Dim(Color32[] buf)
        {
            var palette = MonitorScreenView.PaletteCached;
            if (palette == null) return;
            var step = new Dictionary<Color32, Color32>();
            for (int i = 0; i < MenuLayout.Darken.GetLength(0); i++)
            {
                string from = MenuLayout.Darken[i, 0], to = MenuLayout.Darken[i, 1];
                Color32 a, b;
                if (palette.TryGetValue(from, out a) && palette.TryGetValue(to, out b)) step[a] = b;
            }
            for (int y = 0; y < H; y++)
            {
                for (int x = 0; x < W; x++)
                {
                    if (InGlass(x, y)) continue;
                    int i = y * W + x;
                    Color32 dark;
                    if (buf[i].a != 0 && step.TryGetValue(buf[i], out dark)) buf[i] = dark;
                }
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
