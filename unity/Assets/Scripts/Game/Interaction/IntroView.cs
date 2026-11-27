// HR//0D -- IntroView: the five beats before the first shift, in the game's own pixels.
// Assembly: HRZeroDay.Game  (UnityEngine)
//
// WHAT THIS IS
//   The player's first minute. A dark card over the office with one line of story at a time, the studio's
//   name, and a prompt telling them they can leave. It reads from content/intro/intro_01.json, which the
//   CONTENT TEAM writes (user decision 2026-11-27: "one of the backends will be doing the intro") -- this
//   file only presents whatever that file says, and makes no claim about the story.
//
// WHAT THIS IS NOT
//   Not a cutscene, not a loading screen, not interactive. No camera moves, no animation beyond a fade,
//   no audio yet (audio lands with the sound pass). SPACE/ENTER/click advances one beat, X/ESC skips the
//   whole thing, and after the last beat it closes itself.
//
// THE THREE RULES IT MUST NEVER BREAK
//   1. It can never block the desk. If the script is missing, empty or unusable, Open() returns false and
//      the game boots straight to the desk -- a player must never see an error for a missing story file.
//   2. It is shown ONCE per profile (SaveData.profile.introSeen). A player replaying a run does not sit
//      through it again unless they asked for a new run.
//   3. It is drawn with the same 5x7 atlas as everything else, on the same 480x270 canvas, in one texture,
//      so it cannot be the thing that breaks the pixel grid (see docs/15-art-bible, BUG-055..058).
using System.Collections.Generic;
using HRZeroDay.Core.Rules;
using UnityEngine;

namespace HRZeroDay.Game.Interaction
{
    public sealed class IntroView : MonoBehaviour
    {
        public const string RootName = "IntroView";

        private const string AtlasResource = "fonts/monitor_font_5x7";
        private const float Ppu = 16f;

        // the card: a dark panel inset from the page, with a border, in the same palette keys as the walk
        private const int CardX = 40;
        private const int CardY = 60;
        private const int CardW = 400;
        private const int CardH = 150;
        private const int PadX = 12;
        private const int PadY = 12;
        private const int LineStep = 10;                  // GlyphH 7 + LineGap 3, the project's pitch
        private const int Cols = 56;                      // (400 - 2*12 + 2) / 7 = 54 -> 53 usable, rounded safe

        private DeskBootstrap _game;
        private IntroScript _script;
        private int _index;
        private float _held;
        private float _fadeT;

        private GameObject _root;
        private SpriteRenderer _card;
        private Texture2D _tex;
        private IGlyphSource _glyphs;
        private bool _ready;

        public bool IsOpen { get { return _root != null && _root.activeSelf; } }
        public int BeatIndex { get { return _index; } }
        public int BeatCount { get { return _script == null ? 0 : _script.Beats.Count; } }

        public static IntroView Attach(DeskBootstrap game)
        {
            var go = new GameObject(RootName);
            var view = go.AddComponent<IntroView>();
            view._game = game;
            return view;
        }

        /// <summary>Builds the pixel side once. False when the atlas or the palette is missing.</summary>
        public bool Build()
        {
            if (_ready) return true;
            _glyphs = MonitorScreenView.Load(AtlasResource, MonitorScreenView.PaletteCached);
            if (_glyphs == null)
            {
                Debug.LogWarning("[HR//0D] intro skipped: no glyph atlas (Resources/fonts/monitor_font_5x7).");
                return false;
            }

            _root = new GameObject("IntroCard");
            _root.transform.SetParent(transform, false);

            var go = new GameObject("Card");
            go.transform.SetParent(_root.transform, false);
            _card = go.AddComponent<SpriteRenderer>();
            _card.sortingOrder = 230;                 // over the desk (10..60), the letter (190), the HUD (219)
            _card.transform.localPosition = PageTransform.CanvasToWorld(480f * 0.5f, 270f * 0.5f, Ppu);

            _tex = new Texture2D(480, 270, TextureFormat.RGBA32, false);
            _tex.filterMode = FilterMode.Point;
            _tex.name = "IntroCard";
            _card.sprite = Sprite.Create(_tex, new Rect(0, 0, 480, 270), new Vector2(0.5f, 0.5f), Ppu);

            _root.SetActive(false);
            _ready = true;
            return true;
        }

        /// <summary>
        /// Shows the intro if there is one. Returns false -- and touches nothing -- when the script is empty,
        /// unusable, or the player has already seen it. The caller then boots the desk as usual.
        /// </summary>
        public bool Open(IntroScript script, bool alreadySeen)
        {
            if (alreadySeen) return false;
            if (script == null || !script.HasBeats) return false;
            if (!Build()) return false;

            _script = script;
            _index = 0;
            _held = 0f;
            _fadeT = 0f;
            _root.SetActive(true);
            Paint();
            return true;
        }

        public void Close()
        {
            if (_root != null) _root.SetActive(false);
        }

        /// <summary>SPACE/ENTER/click: next beat, or close after the last one. Returns true when consumed.</summary>
        public bool Advance()
        {
            if (!IsOpen) return false;
            _index++;
            if (_script == null || _index >= _script.Beats.Count) { Finish(); return true; }
            _held = 0f;
            Paint();
            return true;
        }

        /// <summary>X/ESC: leave the intro now. Skipping is not a failure state; it is a courtesy.</summary>
        public void Skip() { if (IsOpen) Finish(); }

        private void Finish()
        {
            Close();
            if (_game != null) _game.OnIntroFinished();
        }

        private void Update()
        {
            if (!IsOpen || _script == null) return;

            // the card fades in over a third of a second, then simply stays
            if (_fadeT < 1f)
            {
                _fadeT = Mathf.Clamp01(_fadeT + Time.unscaledDeltaTime * 3f);
                ApplyFade();
            }

            // beats hold for their own `hold`, then advance on their own. The prompt says so, and a player
            // who wants to read can press SPACE to hold... actually no: SPACE advances, so a slow reader
            // needs the hold to be generous. That is why DefaultHold is 2.4 s and MinHold is 0.5 s.
            _held += Time.unscaledDeltaTime;
            if (_script.Beats[_index].Hold > 0f && _held >= _script.Beats[_index].Hold) Advance();
        }

        private void ApplyFade()
        {
            if (_card != null) _card.color = new Color(1f, 1f, 1f, Mathf.Lerp(0.35f, 1f, _fadeT));
        }

        // ------------------------------------------------------------------ paint
        /// <summary>
        /// One texture for the whole card: dark fill, 1 px border, the beat's wrapped lines, the progress
        /// line and the prompt. Same approach as the walk page -- one sprite, nothing to misalign.
        /// </summary>
        private void Paint()
        {
            if (_tex == null || _glyphs == null || _script == null) return;
            int w = _tex.width, h = _tex.height;

            var voidInk = Colour("void", new Color32(11, 14, 19, 255));
            var lineInk = Colour("line", new Color32(43, 58, 77, 255));
            var buf = new Color32[w * h];
            for (int i = 0; i < buf.Length; i++) buf[i] = voidInk;

            // the card, with a closed border
            for (int y = CardY; y < CardY + CardH; y++)
                for (int x = CardX; x < CardX + CardW; x++)
                {
                    bool edge = y == CardY || y == CardY + CardH - 1 || x == CardX || x == CardX + CardW - 1;
                    buf[y * w + x] = edge ? lineInk : voidInk;
                }

            var beat = _script.Beats[_index];
            var rows = new List<string>();

            // header: the studio line, then the beat itself
            rows.Add("HUMAN RESOURCES // ZERO DAY");
            rows.Add("");

            var body = IntroScriptRules.Wrap(IntroScriptRules.LineFor(beat), Cols);
            for (int i = 0; i < body.Count && rows.Count < 8; i++) rows.Add(body[i]);

            var raster = new List<MonitorScreenRaster.RasterRow>();
            int top = CardY + PadY;
            int bodyRows = rows.Count;
            for (int i = 0; i < bodyRows; i++)
            {
                raster.Add(new MonitorScreenRaster.RasterRow
                {
                    text = rows[i],
                    x = CardX + PadX,
                    top = top + i * LineStep,
                    colour = i == 0 ? "signal" : "text"
                });
            }

            // The prompt is PINNED TO THE BOTTOM of the card, not stacked under the words: with a
            // two-line beat the first draft left 100 px of empty card and the only instruction on screen
            // floating in the middle of it. Two fixed things -- header at the top, prompt at the bottom --
            // and the story breathes between them.
            raster.Add(new MonitorScreenRaster.RasterRow
            {
                text = "BEAT " + (_index + 1) + " / " + _script.Beats.Count +
                       "     [SPACE] NEXT   [X] SKIP",
                x = CardX + PadX,
                top = CardY + CardH - PadY - MonitorScreen.GlyphH,
                colour = "textdim"
            });

            MonitorScreenRaster.RenderRows(buf, w, h, raster, _glyphs,
                MonitorScreenRaster.RasterClip.Box(CardX + 1, CardY + 1, CardW - 2, CardH - 2));
            MonitorScreenRaster.FlipToTextureOrder(buf, w, h);
            _tex.SetPixels32(buf);
            _tex.Apply(false);

            _fadeT = 0f;
            ApplyFade();
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
