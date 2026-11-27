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
        private const int MaxRows = SweepScript.MaxRows;     // fits the band inside the page with the strip

        private DeskBootstrap _game;
        private List<SweepPage> _pages;
        private int _index;

        private GameObject _root;
        private SpriteRenderer _background;
        private SpriteRenderer _textLayer;
        private Texture2D _tex;           // the caption band on its own (kept for the size checks)
        private SpriteRenderer _fade;
        private IGlyphSource _glyphs;
        private int _cols;

        private Texture2D _stripTex;      // the strip, painted once, composited into the page texture
        private Texture2D _pageTex;       // the whole page: room art + caption band + strip

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

            // ONE layer draws the whole walk: the room art, the caption band and the strip, composited
            // into a single 480x270 texture per page. Before this, three sprites had to agree with each
            // other about position and sort order, and on 2026-11-27 they did not: the strip sorted over
            // the room like a dropped shutter and the caption sat in the wrong half of its bar. A page is
            // a page: one texture, one draw, nothing to misalign.
            _background = NewLayer("Page", 180);
            _fade = NewLayer("Fade", 210);          // a black card over everything while a page changes
            _fade.sprite = SolidSprite(new Color32(255, 255, 255, 255));
            _fade.color = new Color(0f, 0f, 0f, 0f);
            _fade.enabled = false;
            _fade.transform.localScale = new Vector3(SweepScript.PageW / Ppu, SweepScript.PageH / Ppu, 1f);
            _fade.transform.localPosition = PageTransform.CanvasToWorld(SweepScript.PageW * 0.5f,
                                                                       SweepScript.PageH * 0.5f, Ppu);

            // the strip (prompt + the player's key line) is painted once per page into its own small
            // texture, then composited -- it never gets its own sprite, so it cannot float over the room.
            _stripTex = new Texture2D(SweepScript.PageW, SweepScript.StripH, TextureFormat.RGBA32, false);
            _stripTex.filterMode = FilterMode.Point;
            _stripTex.name = "SweepStrip";

            // the whole page, painted per floor
            _pageTex = new Texture2D(SweepScript.PageW, SweepScript.PageH, TextureFormat.RGBA32, false);
            _pageTex.filterMode = FilterMode.Point;
            _pageTex.name = "SweepPageCard";
            _background.sprite = Sprite.Create(_pageTex, new Rect(0, 0, SweepScript.PageW, SweepScript.PageH),
                                               new Vector2(0.5f, 0.5f), Ppu);
            _background.transform.localPosition = PageTransform.CanvasToWorld(SweepScript.PageW * 0.5f,
                                                                             SweepScript.PageH * 0.5f, Ppu);

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
            var palette = MonitorScreenView.PaletteCached;
            // the walk happens at night, so the ink is the CRT's, not the paper's
            _glyphs = MonitorScreenView.Load(AtlasResource, palette);
            if (_glyphs == null) return false;
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

        private float _fadeT = -1f;   // <0 = no fade running

        private void Update()
        {
            if (_fade == null) return;
            if (_fadeT >= 0f)
            {
                _fadeT += Time.unscaledDeltaTime / SweepFadeSeconds;
                float a = Mathf.Clamp01(_fadeT);
                // black in, then out: 0 -> 1 over the first half, 1 -> 0 over the second
                float level = a < 0.5f ? a * 2f : (1f - a) * 2f;
                _fade.color = new Color(0f, 0f, 0f, level * 0.85f);
                _fade.enabled = level > 0.001f;
                if (a >= 1f) { _fadeT = -1f; _fade.enabled = false; }
            }
        }

        /// <summary>Half a second of black between floors -- the walk's only animation.</summary>
        private const float SweepFadeSeconds = 0.45f;

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

            // WHERE THE PAGE DRAWS (v97). The sprite and the position are set in ONE place, Repaint(),
            // which composes the page texture and binds it with the centre pivot it was created with.
            // This method used to set both for the floor ART, and Repaint then replaced the sprite with
            // the composed page -- so the art decided the placement and the page inherited it by luck.
            // The art sprite is what Repaint reads PIXELS from; it is not what gets drawn.
            RepaintKeyLine(page);
            Repaint(page, sprite);
            _fadeT = 0f;                 // black card in, then out: the page change reads as a change
        }

        /// <summary>
        /// Paints the prompt into its own strip under the page: "[WALK 1/3]  [SPACE] WALK ON" (or CLOCK OUT
        /// on the last page). Dark fill, 'signal' ink -- it is the only UI on the screen, so it has to be
        /// the most obvious thing on it.
        /// </summary>
        private void RepaintKeyLine(SweepPage page)
        {
            if (_stripTex == null || _glyphs == null) return;
            int w = _stripTex.width, h = _stripTex.height;
            var fill = Colour("void", new Color32(11, 14, 19, 255));
            var border = Colour("line", new Color32(43, 58, 77, 255));
            var buf = new Color32[w * h];
            for (int i = 0; i < buf.Length; i++) buf[i] = fill;
            for (int x = 0; x < w; x++) buf[x] = border;      // strip-local row 0 = the strip's top edge

            // Line 1: the page prompt (signal ink -- it is the only UI on the page).
            // Line 2: the player's key line (dim ink -- present on every page so they never have to
            // remember it, quiet enough not to compete with the prompt).
            //
            // TOP-DOWN, FROM CORE'S ROWS (v97). This used TextPanel.Paint, whose rows start 6 px down and
            // whose pitch is hard-coded for a callout card; it then went through the CRT raster, which lays
            // a line against the BOTTOM of a box. The strip now paints the two rows SweepScript.StripRows
            // publishes, in strip-local coordinates, so the view, the tests and the Python twin all read
            // the same two numbers.
            var rows = SweepScript.StripRows(SweepScript.KeyLine(page, _index, _pages.Count),
                                             SweepScript.HintLine());
            var local = new List<MonitorScreenRaster.RasterRow>();
            for (int i = 0; i < rows.Count; i++)
                local.Add(new MonitorScreenRaster.RasterRow
                {
                    text = rows[i].Text,
                    x = rows[i].X,
                    top = rows[i].Top - SweepScript.StripTop,
                    colour = rows[i].Colour
                });

            int painted = MonitorScreenRaster.RenderRows(buf, w, h, local, _glyphs,
                                                         MonitorScreenRaster.RasterClip.Box(1, 1, w - 2, h - 1));
            if (painted == 0)
                Debug.LogError("SweepView: the key strip painted no glyphs for '" +
                               rows[0].Text + "' / '" + rows[1].Text + "'. The walk's only UI would be a " +
                               "blank bar -- check Resources/fonts/monitor_font_5x7 (json + png).");

            MonitorScreenRaster.FlipToTextureOrder(buf, w, h);
            _stripTex.SetPixels32(buf);
            _stripTex.Apply(false);
        }

        /// <summary>
        /// Paints the whole page into one texture: floor art, caption band, strip -- composed by
        /// SweepPageComposer (a pure function with its own tests), then flipped once for the GPU.
        ///
        /// THIS METHOD MUST NOT BE ABLE TO LEAVE A BLANK PAGE. The first compositing attempt did exactly
        /// that: the art blit threw, the catch swallowed it, and the walk showed an empty card. Every step
        /// is now either total (compose always fills w*h) or reported in the Console.
        /// </summary>
        private void Repaint(SweepPage page, Sprite art)
        {
            if (_pageTex == null) return;

            int W = SweepScript.PageW, H = SweepScript.PageH;
            var void_ = Colour("void", new Color32(11, 14, 19, 255));
            var line_ = Colour("line", new Color32(43, 58, 77, 255));

            if (_glyphs == null)
                Debug.LogError("SweepView: no glyph source -- the walk will show its art but no copy. " +
                               "MonitorScreenView.Load failed; check Resources/fonts/monitor_font_5x7.");

            var bandLines = new List<string> { page.Heading, string.Empty };
            var bandColours = new List<string> { "warn", "text" };
            for (int i = 0; i < page.Lines.Count && bandLines.Count < MaxRows; i++)
            {
                bandLines.Add(page.Lines[i]);
                bandColours.Add("textdim");
            }

            var artPixels = ArtToRaster(art, W, H, void_);
            var stripPixels = ReadTexture(_stripTex);

            var buf = SweepPageComposer.Compose(artPixels, W, H, bandLines, bandColours,
                                                stripPixels, SweepScript.StripTop, _glyphs, void_, line_);
            MonitorScreenRaster.FlipToTextureOrder(buf, W, H);
            _pageTex.SetPixels32(buf);
            _pageTex.Apply(false);

            // The composed page is what draws: bind it to the ONE page layer, with the centre pivot it
            // was created with. (Build already made this sprite; re-creating it here keeps a reload of
            // the art from leaving a stale sprite on screen.)
            _background.sprite = Sprite.Create(_pageTex, new Rect(0, 0, W, H), new Vector2(0.5f, 0.5f), Ppu);
            _background.transform.localPosition = PageTransform.CanvasToWorld(W * 0.5f, H * 0.5f, Ppu);

            // A blank walk page was the player's report on 2026-11-27 and it had no exception behind it.
            // If the composed page is one flat colour, say so loudly rather than showing a black card.
            var first = buf[0];
            bool allSame = true;
            for (int i = 1; i < buf.Length && allSame; i += 97)
                if (buf[i].r != first.r || buf[i].g != first.g || buf[i].b != first.b) allSame = false;
            if (allSame)
                Debug.LogError("SweepView: the composed page for '" + page.Floor + "' is a single flat colour " +
                               "(" + first.r + "," + first.g + "," + first.b + "). Art=" + (art != null) +
                               " glyphs=" + (_glyphs != null) + " strip=" + (_stripTex != null));
        }

        /// <summary>The floor art as a top-down raster. Null on failure -- the composer fills the page.</summary>
        private static Color32[] ArtToRaster(Sprite art, int W, int H, Color32 fill)
        {
            if (art == null || art.texture == null) return null;
            var tex = art.texture;
            if (!tex.isReadable)
            {
                Debug.LogError("SweepView: floor art is not Read/Write Enabled, so the walk cannot use it. " +
                               "Run python tools/art/write_unity_metas.py (then let Unity reimport).");
                return null;
            }
            try
            {
                int rw = Mathf.RoundToInt(art.rect.width), rh = Mathf.RoundToInt(art.rect.height);
                int rx = Mathf.RoundToInt(art.rect.x), ry = Mathf.RoundToInt(art.rect.y);
                var px = tex.GetPixels32();            // whole texture, bottom-up
                int tw = tex.width;
                var buf = new Color32[W * H];
                for (int i = 0; i < buf.Length; i++) buf[i] = fill;
                int copy = Mathf.Min(rh, H);
                for (int y = 0; y < copy; y++)
                {
                    int srcRow = (ry + rh - 1 - y) * tw + rx;   // flip: PNG row y comes from the TOP
                    int dst = y * W;
                    for (int x = 0; x < Mathf.Min(rw, W); x++) buf[dst + x] = px[srcRow + x];
                }
                return buf;
            }
            catch (System.Exception e)
            {
                Debug.LogError("SweepView: could not read the floor art (" + e.GetType().Name + ": " +
                               e.Message + "). The page will draw without its room.");
                return null;
            }
        }

        private static Color32[] ReadTexture(Texture2D tex)
        {
            if (tex == null) return null;
            try { return tex.GetPixels32(); }
            catch (System.Exception) { return null; }
        }

        private void OnDestroy()
        {
            // These two are created here, not imported: an un-destroyed Texture2D leaks for the session
            // and, in the editor, keeps the play-mode object alive after Stop.
            if (_tex != null) Destroy(_tex);
            if (_stripTex != null) Destroy(_stripTex);
            if (_pageTex != null) Destroy(_pageTex);
        }

        private static Sprite SolidSprite(Color32 c)
        {
            var t = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            t.SetPixels32(new[] { c });
            t.Apply(false, false);
            return Sprite.Create(t, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
        }

        private Color32 Colour(string key, Color32 fallback)
        {
            var palette = MonitorScreenView.Palette.FromResources();
            Color32 value;
            return palette.TryGetValue(key, out value) ? value : fallback;
        }
    }
}
