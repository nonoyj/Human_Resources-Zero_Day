// HR//0D -- SweepView: the after-hours walk, drawn on the game's own page.
// Assembly: HRZeroDay.Game  (UnityEngine)
//
// WHAT THIS IS
//   After a day where a synthetic got through, the player walks the office once. This view owns that
//   walk: one 480x270 page per floor (desk -> corridor -> lift), the traces rendered with the SAME 5x7
//   atlas the paperwork uses, and a neon key strip along the bottom -- the one piece of non-diegetic UI
//   in the game, and the reason the walk reads as "not the interview".
//
// WHAT THIS IS NOT
//   Not a game mode. No movement, no timer, no fail state. SPACE turns the page; the last page clocks
//   out. The rule (which traces exist) lives in Core/Rules/Sweep.cs and the page layout in
//   Core/Rules/SweepScript.cs; both are tested headlessly. This file only draws and listens.
//
// WHY THE TEXT GETS ITS OWN BAND
//   The first draft drew the words straight onto the room art. It was unreadable wherever the
//   background had detail (seen in tools/comp/preview_sweep.py before it ever reached Unity). So the
//   words sit on a solid 'void' bar with a 1 px 'line' border, sized to the rows actually used, and the
//   band is part of the same texture as the text -- one sprite, one draw call, no second layer that can
//   fall out of sync with the words on it.
using System;
using System.Collections.Generic;
using HRZeroDay.Core.Rules;
using UnityEngine;

namespace HRZeroDay.Game.Interaction
{
    public sealed class SweepView : MonoBehaviour
    {
        public const string RootName = "SweepView";

        private const string AtlasResource = "fonts/monitor_font_5x7";
        private const float Ppu = 16f;                       // the project's pixels per unit, everywhere
        private const int MaxRows = 22;                      // fits the band inside the page with the strip

        private DeskBootstrap _game;
        private List<SweepPage> _pages;
        private int _index;

        private GameObject _root;
        private SpriteRenderer _background;
        private SpriteRenderer _textLayer;
        private Texture2D _tex;
        private IGlyphSource _glyphs;
        private int _cols;

        private Texture2D _keyBand;

        public bool IsOpen { get { return _root != null && _root.activeSelf; } }
        public int PageIndex { get { return _index; } }
        public int PageCount { get { return _pages == null ? 0 : _pages.Count; } }

        public static SweepView Attach(DeskBootstrap game)
        {
            var go = new GameObject(RootName);
            var view = go.AddComponent<SweepView>();
            view._game = game;
            return view;
        }

        // ---------------------------------------------------------------- build
        public bool Build(CanvasSpec canvas)
        {
            if (!LoadText()) return false;

            _root = new GameObject("SweepPage");
            _root.transform.SetParent(transform, false);

            _background = NewLayer("Background", 180);
            _textLayer = NewLayer("Text", 195);
            var keys = NewLayer("Keys", 199);
            keys.sprite = Resources.Load<Sprite>("Art/Kit/sweep/sweep_keys_480x22");
            keys.transform.localPosition = new Vector3(SweepScript.PageW * 0.5f / Ppu,
                                                      (SweepScript.KeyStripH * 0.5f) / Ppu, -0.4f);

            // one texture for the whole caption band, allocated at the largest page and reused
            // 1:1 with the canvas on purpose. This is a 5x7 bitmap font: it has no smaller size, and
            // downscaling the texture to fake one would smear the glyphs. What CAN be made smaller is the
            // panel around it -- the band is sized to the rows actually used, so a two-line trace is a thin
            // bar and the room stays visible. (The player's "text is too big" was the desk HUD, which used
            // the engine font at 1.7 glyph boxes; that is fixed in GameHud.)
            _tex = new Texture2D(SweepScript.BandW, SweepScript.BandHeight(MaxRows), TextureFormat.RGBA32, false);
            _tex.filterMode = FilterMode.Point;
            _tex.name = "SweepBand";
            _textLayer.sprite = Sprite.Create(_tex, new Rect(0, 0, _tex.width, _tex.height),
                                             new Vector2(0f, 1f), Ppu);       // pivot: top-left
            _textLayer.transform.localPosition = new Vector3(SweepScript.BandX / Ppu,
                (SweepScript.PageH - SweepScript.BandTop) / Ppu, -1f);

            // The prompt is painted INTO the page texture with the same atlas as everything else. It used to
            // be a TextMesh placed at (TextX+40, KeyStrip+9) px -- which in y-up canvas space is 1.9 units
            // ABOVE the page's bottom edge, i.e. off the screen. That is why the walk said nothing about
            // what to press and the player had to guess SPACE on 2026-11-26.
            _keyBand = new Texture2D(SweepScript.PageW, SweepScript.KeyBandH, TextureFormat.RGBA32, false);
            _keyBand.filterMode = FilterMode.Point;
            _keyBand.name = "SweepKeyBand";
            var keyLayer = NewLayer("KeyText", 200);
            keyLayer.sprite = Sprite.Create(_keyBand, new Rect(0, 0, SweepScript.PageW, SweepScript.KeyBandH),
                                            new Vector2(0f, 1f), Ppu);   // pivot: top-left
            keyLayer.transform.localPosition = new Vector3(0f, SweepScript.KeyBandTop / Ppu, -1f);

            _root.SetActive(false);
            return true;
        }

        private SpriteRenderer NewLayer(string name, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_root.transform, false);
            var sr = go.AddComponent<SpriteRenderer>();
            // SpriteRenderer IS a Renderer: set the order directly, never via GetComponent<MeshRenderer>().
            sr.sortingOrder = order;
            return sr;
        }

        /// <summary>Loads the glyph atlas once, from the same resources the CRT and letter use.</summary>
        private bool LoadText()
        {
            var tex = Resources.Load<Texture2D>(AtlasResource);
            var metricsAsset = Resources.Load<TextAsset>(AtlasResource);
            if (tex == null || metricsAsset == null)
            {
                Debug.LogError("SweepView: Resources/" + AtlasResource +
                               " (.png/.json) missing. Run HR//0D > Publish Art To Resources.");
                return false;
            }
            var metrics = JsonUtility.FromJson<FontMetrics>(metricsAsset.text);
            var palette = MonitorScreenView.Palette.FromResources();
            // The walk happens at night, so the ink is the CRT's, not the paper's. The atlas json carries
            // the same ink, so a texture that is not readable still draws (BUG-045's lesson).
            _glyphs = new FontAtlasGlyphSource(metrics, tex.isReadable ? tex.GetPixels32() : null,
                                               tex.width, palette);
            _cols = SweepScript.Cols;
            return true;
        }

        // ---------------------------------------------------------------- play
        /// <summary>Starts the walk. False when there is nothing to walk (clean day) or no page yet.</summary>
        public bool Open(List<SweepTrace> traces)
        {
            if (_root == null || traces == null || traces.Count == 0) return false;
            _pages = SweepScript.Build(traces);
            if (_pages == null || _pages.Count == 0) return false;
            _index = 0;
            _root.SetActive(true);
            Show();
            return true;
        }

        public void Close()
        {
            if (_root != null) _root.SetActive(false);
        }

        /// <summary>SPACE: next floor, or clock out when the last page is showing.</summary>
        public void Advance()
        {
            if (!IsOpen) return;
            if (_index + 1 < _pages.Count) { _index++; Show(); return; }
            Close();
            if (_game != null) _game.OnSweepFinished();
        }

        private void Show()
        {
            var page = _pages[_index];
            var sprite = Resources.Load<Sprite>("Art/Kit/" + page.BackgroundKey);
            if (sprite == null)
                throw new InvalidOperationException(
                    "sweep background not found at Resources/Art/Kit/" + page.BackgroundKey +
                    ". It ships in art/kit/sweep/ -- run: python tools/art/publish_to_unity.py");

            _background.sprite = sprite;
            // the page is 480x270 art px = 30 x 16.875 units; the sprite pivot is its centre
            _background.transform.localPosition = new Vector3(SweepScript.PageW * 0.5f / Ppu,
                                                              SweepScript.PageH * 0.5f / Ppu, 0f);

            RepaintKeyLine(page);
            Repaint(page);
        }

        /// <summary>
        /// Paints the prompt into its own strip under the page: "[WALK 1/3]  [SPACE] WALK ON" (or CLOCK OUT
        /// on the last page). Dark fill, 'signal' ink -- it is the only UI on the screen, so it has to be
        /// the most obvious thing on it.
        /// </summary>
        private void RepaintKeyLine(SweepPage page)
        {
            if (_keyBand == null || _glyphs == null) return;
            int w = _keyBand.width, h = _keyBand.height;
            var fill = Colour("void", new Color32(11, 14, 19, 255));
            var buf = new Color32[w * h];
            for (int i = 0; i < buf.Length; i++) buf[i] = fill;

            var lines = new List<string> { SweepScript.KeyLine(page, _index, _pages.Count) };
            var colours = new List<string> { "signal" };
            var painted = TextPanel.Paint(w, h, lines, colours, _glyphs, fill, fill);
            for (int i = 0; i < buf.Length; i++) buf[i] = painted[i];

            MonitorScreenRaster.FlipToTextureOrder(buf, w, h);
            _keyBand.SetPixels32(buf);
            _keyBand.Apply(false);
        }

        /// <summary>
        /// Rasterises the band: background, 1 px border, heading, then the traces. Same path as
        /// LetterView (wrap -> rects -> pixels) so the walk's type cannot drift from the paperwork's.
        /// </summary>
        private void Repaint(SweepPage page)
        {
            if (_tex == null || _glyphs == null) return;

            var specs = new List<MonitorScreen.LineSpec>();
            specs.Add(new MonitorScreen.LineSpec { text = page.Heading, colour = "warn" });
            specs.Add(new MonitorScreen.LineSpec { text = string.Empty, colour = "text" });
            int rows = 2;
            for (int i = 0; i < page.Lines.Count && rows < MaxRows; i++, rows++)
                specs.Add(new MonitorScreen.LineSpec { text = page.Lines[i], colour = "textdim" });

            int height = SweepScript.BandHeight(rows);
            var glass = new MonitorScreen.GlassRect
            {
                x = SweepScript.PadX,
                y = SweepScript.PadY,
                w = _tex.width - 2 * SweepScript.PadX,
                h = height - 2 * SweepScript.PadY
            };
            var rects = MonitorScreen.LinesToRects(specs, glass);

            var void_ = Colour("void", new Color32(11, 14, 19, 255));
            var line_ = Colour("line", new Color32(43, 58, 77, 255));
            var buf = new Color32[_tex.width * _tex.height];
            for (int i = 0; i < buf.Length; i++) buf[i] = new Color32(0, 0, 0, 0);   // transparent page

            // band: rows down from the top of the texture, so a two-line page is a thin bar
            int top = 0, last = height - 1;
            for (int y = top; y <= last; y++)
                for (int x = 0; x < _tex.width; x++)
                    buf[y * _tex.width + x] = void_;
            for (int x = 0; x < _tex.width; x++)
            {
                buf[top * _tex.width + x] = line_;
                buf[last * _tex.width + x] = line_;
            }
            for (int y = top; y <= last; y++)
            {
                buf[y * _tex.width] = line_;
                buf[y * _tex.width + _tex.width - 1] = line_;
            }

            // text on top of the band; the raster only writes glyph pixels, so the band survives
            MonitorScreenRaster.Render(buf, _tex.width, _tex.height, rects, _glyphs, void_, paintBackground: false);
            // the raster is PNG-ordered (row 0 = top); the texture is not
            MonitorScreenRaster.FlipToTextureOrder(buf, _tex.width, _tex.height);
            _tex.SetPixels32(buf);
            _tex.Apply(false);
        }

        private Color32 Colour(string key, Color32 fallback)
        {
            var palette = MonitorScreenView.Palette.FromResources();
            Color32 value;
            return palette.TryGetValue(key, out value) ? value : fallback;
        }
    }
}
