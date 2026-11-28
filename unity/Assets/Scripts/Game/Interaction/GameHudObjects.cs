// HR//0D -- the play HUD as CLICKABLE OBJECTS, drawn as one painted layer.
// Assembly: HRZeroDay.Game
//
// THE PLAYER'S REQUEST (v126, Q): "It should not be displayed as text but rather as clickable objects."
//
// WHAT WAS THERE BEFORE
//     GameHud: one TextMesh in the engine's font, sixteen lines of prose in the top-left corner, plus a
//     TextMesh in the bottom-right for the score. The key legend "[L] LETTER  [1-5] FLAGS  [A] APPROVE
//     [S] REJECT  [TAB] ALERTS" was the sixth line of that prose. Every one of those five things is an
//     ACTION the game already had -- and the player had to read a sentence to discover it.
//
// WHAT REPLACES IT
//     Core/Rules/HudRules decides WHAT the objects are, WHERE they are, what STATE each is in, and what
//     they look like (palette keys). This file draws exactly that, once per state change, into a texture
//     the size of the desk canvas -- one sprite for the whole HUD, so a state change is one texture upload
//     and the HUD can never be half-repainted (the class of bug that produced a void-on-void selected row).
//     Clicks come in through the same screen->canvas mapping the hotspot router already uses, hit-test with
//     HudRules.Hit, and are forwarded to DeskBootstrap's existing actions. Every object's action is a call
//     the keyboard also makes, so the two input paths cannot disagree.
//
// WHY A PAINTED LAYER AND NOT SPRITE PER BUTTON
//     Because the plates are 12-24 px tall and this camera is 16 px per unit: eight SpriteRenderers at
//     fractional positions and eight sorting orders is eight chances to land on a half pixel and blur, and
//     the palette-gated 16-bit look cannot survive a blur. One texture at 480x270, point-filtered, integer
//     canvas coordinates, zero scale.
//
// WHAT THE PLAYER SEES, IN ORDER (this is also what a screenshot check should look for):
//     top-left      the SHIFT PLATE -- a painted plate, "DAY 2/5 SUBJECT 1/4" and the applicant's name
//     top-right     the SECURITY BOARD -- five pips, a band word, the score: shape before number
//     under it      two plaques, ALERTS and HELP
//     left edge     five FLAG TABS, only while the letter is open (amber when set, ticked)
//     bottom-left   APPROVE and REJECT pads, side by side, like a stamp rack
//     bottom-right  one big forward object: CONTINUE / NEXT DAY / NEW RUN
//     and NOTHING in the middle band, because that is the applicant's face.
using System.Collections.Generic;
using HRZeroDay.Core.Data;
using HRZeroDay.Core.Rules;
using HRZeroDay.Game.Desk;
using UnityEngine;

namespace HRZeroDay.Game.Interaction
{
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class GameHudObjects : MonoBehaviour
    {
        public int referenceWidth = HudRules.CanvasW;
        public int referenceHeight = HudRules.CanvasH;
        public float pixelsPerUnit = 16f;

        private DeskBootstrap _game;
        private SpriteRenderer _renderer;
        private Texture2D _tex;
        private Color32[] _buffer;
        private IGlyphSource _glyphs;
        private Dictionary<string, Color32> _palette;
        private bool _ready;

        private readonly List<HudObject> _objects = new List<HudObject>();
        private HudId? _hovered;
        private HudId? _armed;
        private string _paintedSignature;      // the state we last painted, so repaint is once per change
        private bool _lastPaintEmpty = true;

        public static GameHudObjects Attach(DeskBootstrap game)
        {
            var go = new GameObject("HR_HudObjects");
            if (game != null) go.transform.SetParent(game.transform, false);
            var hud = go.AddComponent<GameHudObjects>();
            hud.Bind(game);
            return hud;
        }

        public void Bind(DeskBootstrap game) { _game = game; }

        private void Awake()
        {
            _renderer = GetComponent<SpriteRenderer>();
            if (_renderer == null) _renderer = gameObject.AddComponent<SpriteRenderer>();
            _renderer.sortingOrder = 221;             // above the desk art and the letter, below nothing
            _renderer.color = Color.white;
            _palette = MonitorScreenView.PaletteCached;
            _glyphs = MonitorScreenView.Load("fonts/monitor_font_5x7", _palette);
            _buffer = new Color32[referenceWidth * referenceHeight];
            _tex = new Texture2D(referenceWidth, referenceHeight, TextureFormat.RGBA32, false);
            _tex.filterMode = FilterMode.Point;
            _tex.wrapMode = TextureWrapMode.Clamp;
            _tex.name = "HudObjects";
            _renderer.sprite = Sprite.Create(_tex, new Rect(0, 0, referenceWidth, referenceHeight),
                                             new Vector2(0f, 1f), pixelsPerUnit);
            // the layer is the whole page, so it sits on the page's own origin, unscaled
            transform.localPosition = Vector3.zero;
            transform.localScale = PageTransform.NaturalScale;
            _ready = _palette != null && _palette.Count > 0 && _glyphs != null;
            if (!_ready)
                Debug.LogWarning("[HR//0D] the HUD objects cannot paint: palette or glyphs missing. " +
                                 "Run HR//0D > Import Art Kit.");
        }

        // ------------------------------------------------------------------ state
        /// <summary>
        /// The model the HUD draws. Every field is READ from the run; nothing here decides anything. Built
        /// fresh each frame so a stale counter is impossible -- this is the bug class the old GameHud had to
        /// work around with RefreshNow().
        /// </summary>
        private HudModel BuildModel()
        {
            var m = new HudModel();
            if (_game == null || _game.Session == null) { m.ShowingWalk = true; return m; }   // draws nothing
            var s = _game.Session;
            m.Day = s.Day;
            m.DayCount = RunLedger.ArcLength;
            m.Subject = s.Shift.CurrentIndex + 1;
            m.SubjectCount = s.Shift.Today.Count;
            var v = s.Current;
            if (v != null) { m.Name = v.FullName; m.Role = v.Role; }
            m.Trust = s.Trust;
            m.Security = _game.SecurityNow;
            m.Hired = s.Shift.SyntheticsHired;
            m.HireLimit = ShiftController.HireLimitForDefeat;
            m.LetterOpen = _game.LetterOpen;
            m.LetterAvailable = s.Paperwork != null && s.Paperwork.CanOpen;
            m.StampedToday = _game.StampedToday;
            m.AlertsOpen = _game.AlertsOpen;
            m.HelpVisible = _game.HelpVisible;
            m.ShowingDebrief = _game.ShowDebrief;
            m.ShowingSummary = _game.ShowDaySummary;
            m.ShowingEnding = _game.ShowEnding;
            m.ShowingWalk = _game.ShowingSweep || _game.ShowingMenu || _game.ShowingIntro;
            m.AutoAdvance = _game.autoAdvance;
            m.Hovered = _hovered;
            m.KeyboardArmed = _armed;
            var p = s.Paperwork;
            if (p != null && p.flags != null)
            {
                int n = Mathf.Min(5, p.flags.Count);
                m.FlagCount = n;
                for (int i = 0; i < n; i++) m.Flagged[i] = p.flagged.Contains(p.flags[i].id);
            }
            return m;
        }

        private void Update()
        {
            if (!_ready || _game == null) return;
            var m = BuildModel();
            var list = HudRules.Objects(m);
            _objects.Clear();
            _objects.AddRange(list);

            string sig = Signature(m, list);
            if (sig == _paintedSignature) return;
            _paintedSignature = sig;
            Paint(m, list);
        }

        /// <summary>
        /// Everything that can change a pixel, as a string. Cheap, and it means the HUD repaints on a real
        /// change instead of every frame -- 480x270 of texture upload per frame would be a fine way to make
        /// a 16-bit game run at nine frames a second.
        /// </summary>
        private static string Signature(HudModel m, List<HudObject> list)
        {
            var sb = new System.Text.StringBuilder(64);
            sb.Append(m.Day).Append(',').Append(m.Subject).Append(',').Append(m.Security).Append(',').Append(m.Trust)
              .Append(',').Append(m.Name).Append(',').Append(m.Role)
              .Append(m.LetterOpen ? 'L' : '-').Append(m.StampedToday ? 'S' : '-')
              .Append(m.AlertsOpen ? 'A' : '-').Append(m.HelpVisible ? 'H' : '-')
              .Append(m.ShowingDebrief ? 'D' : '-').Append(m.ShowingSummary ? 'Y' : '-')
              .Append(m.ShowingEnding ? 'E' : '-').Append(m.ShowingWalk ? 'W' : '-');
            sb.Append(m.Hovered.HasValue ? m.Hovered.Value.ToString() : ".");
            sb.Append(m.KeyboardArmed.HasValue ? m.KeyboardArmed.Value.ToString() : ".");
            for (int i = 0; i < 5; i++) sb.Append(m.Flagged[i] ? '1' : '0');
            return sb.ToString();
        }

        private void Paint(HudModel m, List<HudObject> list)
        {
            // clear to fully transparent: the HUD is an overlay, not a picture of the room
            var clear = new Color32(0, 0, 0, 0);
            for (int i = 0; i < _buffer.Length; i++) _buffer[i] = clear;

            // ONE PAINTER, ALSO USED BY THE OFFLINE DUMP (see ObjectPlate.DrawHud): if the dump and the
            // live layer had separate code, the screenshot would stop being evidence of what Unity draws.
            int painted = ObjectPlate.DrawHud(_buffer, referenceWidth, referenceHeight, m, list, _palette, _glyphs);

            _lastPaintEmpty = painted == 0;
            MonitorScreenRaster.FlipToTextureOrder(_buffer, referenceWidth, referenceHeight);
            _tex.SetPixels32(_buffer);
            _tex.Apply(false);
            _renderer.enabled = painted > 0;
        }

        // ------------------------------------------------------------------ input
        /// <summary>
        /// A pointer position in canvas px (y UP, as the rest of the desk uses). Returns true when the HUD
        /// consumed the click -- the router checks this BEFORE the desk hotspots, because the HUD is on top
        /// of the room and a stamp pad that let a click through to the applicant behind it would be a way to
        /// inspect a person by pressing APPROVE.
        /// </summary>
        public bool Click(int canvasX, int canvasYTopDown)
        {
            var hit = HudRules.Hit(_objects, canvasX, canvasYTopDown);
            if (hit == null) return false;
            Act(hit.Id);
            return true;
        }

        /// <summary>Hover: which object the pointer is over. Repaints only on a change of target.</summary>
        public void Hover(int canvasX, int canvasYTopDown)
        {
            var hit = HudRules.Hit(_objects, canvasX, canvasYTopDown);
            var id = hit == null ? (HudId?)null : hit.Id;
            if (id.Equals(_hovered)) return;
            _hovered = id;
        }

        /// <summary>The keyboard's selection, so the one object a key would press is the one that lights up.</summary>
        public void Arm(HudId? id)
        {
            if (id.Equals(_armed)) return;
            _armed = id;
        }

        /// <summary>
        /// THE ACTION TABLE. One switch, and every branch calls the SAME method the keyboard calls. If a
        /// branch ever grows its own logic, the two input paths start disagreeing and this stops being a HUD.
        /// </summary>
        public void Act(HudId id)
        {
            if (_game == null) return;
            switch (id)
            {
                case HudId.Letter:
                    if (_game.LetterOpen) _game.CloseLetter(); else _game.ToggleLetter();
                    break;
                case HudId.Flag1: _game.NumberKey(1); break;
                case HudId.Flag2: _game.NumberKey(2); break;
                case HudId.Flag3: _game.NumberKey(3); break;
                case HudId.Flag4: _game.NumberKey(4); break;
                case HudId.Flag5: _game.NumberKey(5); break;
                case HudId.Approve: _game.Approve(); break;
                case HudId.Reject: _game.Reject(); break;
                case HudId.Alerts:
                    if (_game.AlertsOpen) _game.CloseAlerts(); else _game.ToggleAlerts();
                    break;
                case HudId.Help: _game.ToggleHelp(); break;
                case HudId.Continue: _game.Continue(); break;
            }
        }

        /// <summary>True when the last paint drew nothing (walk / menu / intro): the router can skip the HUD.</summary>
        public bool IsEmpty { get { return _lastPaintEmpty; } }

        /// <summary>For the screenshot checks and the tests: the objects as they were last painted.</summary>
        public IList<HudObject> Painted { get { return _objects; } }
    }
}
