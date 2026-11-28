// HR//0D -- MenuView: the front door, drawn in the game's own pixels.
// Assembly: HRZeroDay.Game  (UnityEngine)
//
// WHAT THIS IS
//   One 480x270 texture, painted the same way as the intro card and the walk page: a dark office behind, a
//   panel in front, text from the 5x7 atlas, and a selection line made of atlas characters. Three screens --
//   root, settings, consent -- because those are the three things the save file has never had a home for.
//
// WHAT IT IS NOT
//   Not a splash screen and not a gate. The desk still builds underneath it (GameBoot does that), so pressing
//   START is a menu closing, not a scene change that could fail. That also means the room is already audible
//   when the menu is up -- the player is in the office before they say go.
//
// THE RULES (all enforced by MenuRules tests, not by this file)
//   1. CONTINUE only exists when there is a save.
//   2. Every label is paintable with the atlas (upper case, <= 24 chars).
//   3. Consent is a decision, not a default: the game records nothing until the player says yes, and saying no
//      changes nothing about the game.
using System.Collections.Generic;
using HRZeroDay.Core.Rules;
using UnityEngine;

namespace HRZeroDay.Game.Interaction
{
    public sealed class MenuView : MonoBehaviour
    {
        public const string RootName = "MenuView";
        private const string AtlasResource = "fonts/monitor_font_5x7";
        private const float Ppu = 16f;

        private const int PageW = 480;
        private const int PageH = 270;
        private const int CardX = 60;
        private const int CardY = 34;
        private const int CardW = 360;
        private const int CardH = 202;
        private const int PadX = 14;
        private const int PadY = 14;
        private const int LineStep = 10;
        private const int Cols = 46;

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

        // what the menu needs to know about the save, refreshed on every open
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

            _root = new GameObject("MenuCard");
            _root.transform.SetParent(transform, false);

            var go = new GameObject("Card");
            go.transform.SetParent(_root.transform, false);
            _card = go.AddComponent<SpriteRenderer>();
            _card.sortingOrder = 240;              // above the intro card (230): the menu is the frontmost thing
            _card.transform.localPosition = PageTransform.CanvasToWorld(PageW * 0.5f, PageH * 0.5f, Ppu);

            _tex = new Texture2D(PageW, PageH, TextureFormat.RGBA32, false);
            _tex.filterMode = FilterMode.Point;
            _tex.name = "MenuCard";
            _card.sprite = Sprite.Create(_tex, new Rect(0, 0, PageW, PageH), new Vector2(0.5f, 0.5f), Ppu);

            _root.SetActive(false);
            _ready = true;
            return true;
        }

        /// <summary>
        /// Opens the root screen. `hasSave` comes from the bootstrap: a fresh profile has no run to continue,
        /// and CONTINUE must not be offered at all (MenuRules.Root decides that, not this view).
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

        /// <summary>The S shortcut from the root screen: settings, without moving the cursor there first.</summary>
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
            if (_index == 0) { _roomVolume = MenuRules.StepVolume(_roomVolume, delta); }
            else if (_index == 1) { _sfxVolume = MenuRules.StepVolume(_sfxVolume, delta); }
            else if (_index == 2) { if (delta != 0) _muted = !_muted; }
            else return false;

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

                case MenuAction.Back:
                    _screen = MenuScreen.Root;
                    _items = MenuRules.Root(_hasSave, _runInProgress);
                    _index = 0;
                    Paint();
                    return false;

                case MenuAction.None:
                    // the settings rows ARE the control: choosing one changes it in place
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
            if (_tex == null || _glyphs == null) return;
            int w = _tex.width, h = _tex.height;

            var voidInk = Colour("void", new Color32(11, 14, 19, 255));
            var lineInk = Colour("line", new Color32(43, 58, 77, 255));
            var buf = new Color32[w * h];
            for (int i = 0; i < buf.Length; i++) buf[i] = voidInk;

            // the card
            for (int y = CardY; y < CardY + CardH; y++)
                for (int x = CardX; x < CardX + CardW; x++)
                {
                    bool edge = y == CardY || y == CardY + CardH - 1 || x == CardX || x == CardX + CardW - 1;
                    buf[y * w + x] = edge ? lineInk : voidInk;
                }

            var rows = new List<MonitorScreenRaster.RasterRow>();
            int top = CardY + PadY;

            if (_screen == MenuScreen.Root)
            {
                Row(rows, "HUMAN RESOURCES // ZERO DAY", top, "signal");
                Row(rows, "", top + LineStep, "text");
                for (int i = 0; i < _items.Count; i++)
                    Row(rows, (_index == i ? "> " : "  ") + _items[i].Label, top + (2 + i) * LineStep,
                        _index == i ? "text" : "textdim");
                Row(rows, AudioHint(), CardY + CardH - PadY - 3 * LineStep, "textdim");
                Row(rows, "[UP/DOWN] MOVE", CardY + CardH - PadY - 2 * LineStep, "textdim");
                Row(rows, "[SPACE] CHOOSE", CardY + CardH - PadY - LineStep, "textdim");
            }
            else if (_screen == MenuScreen.Settings)
            {
                Row(rows, "SETTINGS", top, "signal");
                Row(rows, "", top + LineStep, "text");
                for (int i = 0; i < _items.Count; i++)
                {
                    string label = _items[i].Label;
                    string extra = "";
                    if (i == 0) extra = "  " + MenuRules.Bar(_roomVolume) + " " + MenuRules.Percent(_roomVolume);
                    else if (i == 1) extra = "  " + MenuRules.Bar(_sfxVolume) + " " + MenuRules.Percent(_sfxVolume);
                    Row(rows, (_index == i ? "> " : "  ") + label + extra, top + (2 + i) * LineStep,
                        _index == i ? "text" : "textdim");
                }
                Row(rows, ConsentRules.Summary(_consentDecided, _consentGranted),
                    CardY + CardH - PadY - 3 * LineStep, "textdim");
                Row(rows, "[LEFT/RIGHT] CHANGES", CardY + CardH - PadY - 2 * LineStep, "textdim");
                Row(rows, "[ESC] BACK", CardY + CardH - PadY - LineStep, "textdim");
            }
            else if (_screen == MenuScreen.Consent)
            {
                Row(rows, "DATA & CONSENT", top, "signal");
                var notice = ConsentRules.Notice();
                int y = top + LineStep + 4;
                for (int i = 0; i < notice.Length && i < 10; i++)
                {
                    Row(rows, notice[i], y, "textdim");
                    y += LineStep;
                }
                // THE DECISION IS A ROW, AND IT IS THE FIRST ROW (BUG-065). v110 painted the checkbox as
                // decoration under the notice and put the real toggle on a second row -- so the thing that
                // looked like a checkbox was not the thing that answered. Now the cursor sits on it.
                int baseY = CardY + CardH - PadY - 4 * LineStep;
                for (int i = 0; i < _items.Count; i++)
                    Row(rows, (_index == i ? "> " : "  ") + _items[i].Label, baseY + i * LineStep,
                        _index == i ? "text" : "textdim");
                // the state and the key hint share a line: five rows under the notice would run into the border
                Row(rows, "STATE: " + MenuRules.ConsentState(_consentGranted, _consentDecided),
                    baseY + 2 * LineStep, "textdim");
            }
            else
            {
                Row(rows, "CREDITS", top, "signal");
                Row(rows, "", top + LineStep, "text");
                Row(rows, "A GAME ABOUT TELLING PEOPLE", top + 2 * LineStep, "text");
                Row(rows, "FROM THINGS WEARING PEOPLE.", top + 3 * LineStep, "text");
                Row(rows, "", top + 4 * LineStep, "text");
                Row(rows, "MADE WITH THE ARENA AGENT.", top + 5 * LineStep, "textdim");
                Row(rows, "[ESC] BACK", CardY + CardH - PadY - LineStep, "textdim");
            }

            MonitorScreenRaster.RenderRows(buf, w, h, rows, _glyphs,
                MonitorScreenRaster.RasterClip.Box(CardX + 1, CardY + 1, CardW - 2, CardH - 2));
            MonitorScreenRaster.FlipToTextureOrder(buf, w, h);
            _tex.SetPixels32(buf);
            _tex.Apply(false);
        }

        private string AudioHint()
        {
            return _muted ? "SOUND OFF   [S] SETTINGS" : "SOUND ON    [S] SETTINGS";
        }

        private static void Row(List<MonitorScreenRaster.RasterRow> rows, string text, int top, string colour)
        {
            rows.Add(new MonitorScreenRaster.RasterRow
            {
                text = text,
                x = CardX + PadX,
                top = top,
                colour = colour
            });
        }

        private Color32 Colour(string key, Color32 fallback)
        {
            return TextPanel.Ink(MonitorScreenView.PaletteCached, key, fallback);
        }

        private void OnDestroy()
        {
            if (_tex != null) Destroy(_tex);
        }
    }
}
