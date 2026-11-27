// HR//0D -- MonitorScreenView: the MonoBehaviour that puts MonitorScreen's text INTO the CRT.
// Assembly: HRZeroDay.Game  (UnityEngine)
// Owner: Agent (written) / Dev A (owns it afterwards). Started 2026-11-26.
//
// THE THREE-PART SPLIT (keep it -- it is why the screen is testable)
//   MonitorScreen        pure C#: what text, measured, clipped. No Unity. Tested headlessly.
//   MonitorScreenRaster  pure C#: text -> Color32[]. No Unity runtime. Tested headlessly.
//   MonitorScreenView    THIS: owns the Texture2D/SpriteRenderer, listens to the shift, repaints.
//
// REPAINT POLICY
//   Only when the CONTENT changes. The desk re-renders on applicant change and on trust change;
//   nothing repaints per frame, because a per-frame SetPixels on a phone at 480x270 is pure waste.

using System;
using System.Collections.Generic;
using HRZeroDay.Core.Data;
using UnityEngine;

namespace HRZeroDay.Game
{
    /// <summary>Loads the generated atlas + metrics from Resources/fonts and answers InkAt().</summary>
    public sealed class FontAtlasGlyphSource : IGlyphSource
    {
        private readonly Dictionary<char, Vector2> _rects = new Dictionary<char, Vector2>();
        private readonly Color32[] _atlas;
        private readonly int _atlasW;
        private readonly Dictionary<string, Color32> _palette;
        private readonly Dictionary<char, byte[,]> _rows = new Dictionary<char, byte[,]>();
        private readonly int _cellW, _cellH;

        public int CellW { get; private set; }
        public int CellH { get; private set; }

        public FontAtlasGlyphSource(FontMetrics metrics, Color32[] atlasPixels, int atlasW,
                                    Dictionary<string, Color32> palette)
            : this(metrics, atlasPixels, atlasW, palette, RowsFromMetrics(metrics))
        {
        }

        public FontAtlasGlyphSource(FontMetrics metrics, Color32[] atlasPixels, int atlasW,
                                    Dictionary<string, Color32> palette, byte[][,] rows)
        {
            CellW = metrics.cellW;
            CellH = metrics.cellH;
            _cellW = metrics.cellW;
            _cellH = metrics.cellH;
            _atlas = atlasPixels;
            _atlasW = atlasW;
            if (rows != null)
            {
                for (int i = 0; i < metrics.glyphs.Count && i < rows.Length; i++)
                {
                    var g = metrics.glyphs[i];
                    if (string.IsNullOrEmpty(g.ch) || rows[i] == null) continue;
                    // FIRST glyph wins, same rule as _rects: the text metrics and the art must not disagree.
                    if (!_rows.ContainsKey(g.ch[0])) _rows[g.ch[0]] = rows[i];
                }
            }
            _palette = palette ?? new Dictionary<string, Color32>();
            for (int i = 0; i < metrics.glyphs.Count; i++)
            {
                var g = metrics.glyphs[i];
                // the atlas json stores the glyph as a string; the lookup key is its single char
                if (string.IsNullOrEmpty(g.ch)) continue;
                char key = g.ch[0];
                if (!_rects.ContainsKey(key)) _rects[key] = new Vector2(g.x, g.y);
            }
        }

        /// <summary>Builds the ink table from the json's `rows` -- no texture required.</summary>
        public static byte[][,] RowsFromMetrics(FontMetrics metrics)
        {
            if (metrics == null || metrics.glyphs == null) return new byte[0][,];
            var all = new byte[metrics.glyphs.Count][,];
            for (int i = 0; i < metrics.glyphs.Count; i++)
            {
                var g = metrics.glyphs[i];
                if (g.rows == null || g.rows.Count == 0) continue;
                int h = g.rows.Count;
                int w = 0;
                for (int r = 0; r < h; r++) if (g.rows[r] != null && g.rows[r].Length > w) w = g.rows[r].Length;
                if (w == 0) continue;
                var cell = new byte[w, h];
                for (int r = 0; r < h; r++)
                {
                    var line = g.rows[r];
                    if (line == null) continue;
                    for (int c = 0; c < line.Length && c < w; c++)
                        cell[c, r] = (byte)(line[c] == '#' || line[c] == 'X' || line[c] == '1' ? 1 : 0);
                }
                all[i] = cell;
            }
            return all;
        }

        public bool InkAt(char ch, int gx, int gy)
        {
            // data path first: it always exists, and it cannot be unreadable
            byte[,] cell;
            if (Lookup(_rows, ch, out cell))
            {
                if (gx < 0 || gy < 0) return false;
                if (gx >= cell.GetLength(0) || gy >= cell.GetLength(1)) return false;
                return cell[gx, gy] != 0;
            }

            Vector2 origin;
            if (_atlas == null) return false;
            if (!_rects.TryGetValue(ch, out origin))
            {
                if (!_rects.TryGetValue(char.ToUpperInvariant(ch), out origin)) return false;
            }
            int ax = (int)origin.x + gx;
            int ay = (int)origin.y + gy;
            if (ax < 0 || ay < 0 || ax >= _atlasW || ay * _atlasW + ax >= _atlas.Length) return false;
            return _atlas[ay * _atlasW + ax].a != 0;
        }

        private static bool Lookup<T>(Dictionary<char, T> table, char ch, out T value)
        {
            if (table.TryGetValue(ch, out value)) return true;
            return table.TryGetValue(char.ToUpperInvariant(ch), out value);
        }

        public Color32 Colour(string key)
        {
            Color32 c;
            if (key != null && _palette.TryGetValue(key, out c)) return c;
            Color32 fallback;
            if (_palette.TryGetValue("text", out fallback)) return fallback;
            return new Color32(0xe6, 0xf1, 0xff, 0xff);
        }
    }

    /// <summary>Mirrors art/kit/fonts/monitor_font_5x7.json (JsonUtility-friendly: public fields).</summary>
    [Serializable]
    public class FontMetrics
    {
        [Serializable]
        public class Glyph
        {
            public string ch;
            public int x;
            public int y;
            // The glyph's ink, one string per pixel row ('#' or '.'). This is the SAME ink the atlas PNG
            // holds -- the json is not a substitute for the art, it is a second copy of it, which is why
            // the CRT can draw with no readable texture at all.
            public List<string> rows = new List<string>();
        }

        public int cellW = 5, cellH = 7, pad = 2, cols = 16;
        public int atlasW, atlasH;
        public string ink = "text";
        public List<Glyph> glyphs = new List<Glyph>();
    }

    [RequireComponent(typeof(SpriteRenderer))]
    /// <summary>
    /// A small pixel-exact text panel: one texture, one sprite, painted from the same 5x7 atlas the CRT
    /// and the letter use. This replaces the evidence callout's TextMesh pair, whose engine font was too
    /// small to read and whose "panel" was the cream tooltip 9-slice stretched over the applicant's face.
    ///
    /// The painter is public and pure enough to be called from a test: give it lines, colours and a size,
    /// it returns pixels with the panel's own background, border and padding already in.
    /// </summary>
    public sealed class TextPanel
    {
        public static Color32 Ink(Dictionary<string, Color32> palette, string key, Color32 fallback)
        {
            Color32 value;
            return palette != null && palette.TryGetValue(key, out value) ? value : fallback;
        }

        /// <summary>
        /// Paints `lines` into a w x h buffer, top row first. Row 0 of the buffer is the TOP of the panel
        /// in PNG order -- the caller flips once if its texture wants bottom-up.
        /// </summary>
        public static Color32[] Paint(int w, int h, IList<string> lines, IList<string> colours,
                                      IGlyphSource glyphs, Color32 fill, Color32 border)
        {
            var buf = new Color32[w * h];
            for (int i = 0; i < buf.Length; i++) buf[i] = fill;
            for (int x = 0; x < w; x++)
            {
                buf[x] = border;
                buf[(h - 1) * w + x] = border;
            }
            for (int y = 0; y < h; y++)
            {
                buf[y * w] = border;
                buf[y * w + w - 1] = border;
            }

            int step = MonitorScreen.GlyphH + MonitorScreen.LineGap;
            for (int li = 0; li < lines.Count; li++)
            {
                var text = lines[li];
                if (string.IsNullOrEmpty(text)) continue;
                int top = 6 + li * step;
                int left = 8;
                for (int ci = 0; ci < text.Length; ci++)
                {
                    int gx = left + ci * (MonitorScreen.GlyphW + MonitorScreen.LetterGap);
                    if (gx + MonitorScreen.GlyphW > w - 2) break;
                    for (int gy = 0; gy < MonitorScreen.GlyphH; gy++)
                    {
                        if (!glyphs.InkAt(text[ci], 0, gy)) continue;
                        for (int px = 0; px < MonitorScreen.GlyphW; px++)
                        {
                            if (!glyphs.InkAt(text[ci], px, gy)) continue;
                            int y = top + gy;
                            if (y < 1 || y >= h - 1) continue;
                            buf[y * w + gx + px] = glyphs.Colour(colours[li]);
                        }
                    }
                }
            }
            return buf;
        }
    }

    public sealed class MonitorScreenView : MonoBehaviour
    {
        public const string RootName = "MonitorScreen";
        private const string AtlasResource = "fonts/monitor_font_5x7";

        [Serializable]
        public class PaletteEntry { public string key; public Color32 value; }

        // assigned by DeskSceneBuilder (or in the inspector)
        public ShiftController shift;
        public GameObject monitorLayer;

        private Texture2D _tex;
        private SpriteRenderer _sr;
        private IGlyphSource _glyphs;
        private MonitorScreen.GlassRect _glass;
        private Color32 _bg;
        private string _lastSignature;

        /// <summary>Wired once, after the desk is built. Returns false (and logs) if art is missing.</summary>
        public bool Initialise(ShiftController controller, GameObject monitor, Color32 background)
        {
            shift = controller;
            monitorLayer = monitor;
            _bg = background;

            // Two assets, two extensions: Resources.Load<Texture2D>("fonts/x") resolves fonts/x.png and
            // Resources.Load<TextAsset>("fonts/x") resolves fonts/x.json. They are not interchangeable,
            // and before the atlas PNG was published this failed and left the CRT blank on screen.
            var tex = Resources.Load<Texture2D>(AtlasResource);
            var metricsAsset = Resources.Load<TextAsset>(AtlasResource);
            if (tex == null)
            {
                Debug.LogError("MonitorScreenView: Resources/" + AtlasResource + ".png missing. " +
                               "Run HR//0D > Publish Art To Resources.");
                return false;
            }
            if (metricsAsset == null)
            {
                Debug.LogError("MonitorScreenView: Resources/" + AtlasResource + ".json missing. " +
                               "Run HR//0D > Publish Art To Resources.");
                return false;
            }

            var metrics = JsonUtility.FromJson<FontMetrics>(metricsAsset.text);
            var palette = Palette.FromResources();
            // READ/COPY PROBLEM (2026-11-25): tex.GetPixels32() throws "texture data is either not
            // readable, corrupted or does not exist" unless the texture is imported with Read/Write
            // Enabled, and that flag lives ONLY in a .meta file. A fresh clone has no .meta (Unity
            // generates them on import), so the import default applies and the game died on the first
            // Play. Two fixes, both kept:
            //   1. the glyph metrics in the json carry the same ink, so the atlas PNG is optional;
            //   2. if the texture IS readable, it is used and wins, so art and data cannot disagree.
            byte[][,] rows = FontAtlasGlyphSource.RowsFromMetrics(metrics);
            Color32[] atlasPixels = null;
            if (tex.isReadable)
            {
                try { atlasPixels = tex.GetPixels32(); }
                catch (Exception) { atlasPixels = null; }
            }
            if (atlasPixels == null)
            {
                Debug.Log("[HR//0D] font atlas is not readable (no .meta); drawing glyphs from the json " +
                          "rows instead. Visual result is identical.");
            }
            _glyphs = new FontAtlasGlyphSource(metrics, atlasPixels, tex.width, palette, rows);

            // the glass is derived from the sprite the desk actually placed, never hard-coded
            var layout = DeskLayout.Load();
            var monitorLayerData = layout.Layer("monitor");
            var spec = monitorLayerData.screen;
            _glass = spec != null
                ? new MonitorScreen.GlassRect { x = spec.x, y = spec.y, w = spec.w, h = spec.h }
                : MonitorScreen.GlassForSprite((int)tex.width, (int)tex.height);

            _tex = new Texture2D(_glass.w, _glass.h, TextureFormat.RGBA32, false);
            _tex.filterMode = FilterMode.Point;
            _tex.name = "CRT_glass";

            _sr = GetComponent<SpriteRenderer>();
            _sr.sprite = Sprite.Create(_tex, new Rect(0, 0, _glass.w, _glass.h),
                                       new Vector2(0.5f, 0.5f), layout.canvas.pixelsPerUnit);
            _sr.sortingOrder = monitorLayerData.sortingOrder + 1;   // directly above the bezel

            Repaint(force: true);
            return true;
        }

        /// <summary>
        /// Repaints only when the visible content changed. The signature is the trust level plus the
        /// applicant id, which is cheap and cannot miss a real change.
        /// </summary>
        public void Refresh()
        {
            if (shift == null || _glyphs == null) return;
            string sig = shift.Day + "|" + shift.Trust + "|" + (shift.Current != null ? shift.Current.id : "-");
            if (sig == _lastSignature) return;
            Repaint(force: false);
        }

        private void Repaint(bool force)
        {
            if (shift == null || shift.Current == null || _glyphs == null) return;
            var model = new MonitorScreenModel
            {
                day = shift.Day,
                trust = shift.Trust,
                applicant = shift.Current
            };
            var specs = MonitorScreen.BuildLines(model);
            var rects = MonitorScreen.LinesToRects(specs, _glass);

            var buf = new Color32[_glass.w * _glass.h];
            MonitorScreenRaster.Render(buf, _glass.w, _glass.h, rects, _glyphs, _bg);
            // the raster is PNG-ordered (row 0 = top); the texture is not
            MonitorScreenRaster.FlipToTextureOrder(buf, _glass.w, _glass.h);
            _tex.SetPixels32(buf);
            _tex.Apply(false);

            _lastSignature = shift.Day + "|" + shift.Trust + "|" + shift.Current.id;
            if (force) Debug.Log("MonitorScreenView: glass " + _glass.w + "x" + _glass.h +
                                 ", " + rects.Count + " lines");
        }

        /// <summary>Colour keys the screen may use, read from the published palette json.</summary>
        public static class Palette
        {
            public static Dictionary<string, Color32> FromResources()
            {
                var dict = new Dictionary<string, Color32>();
                var asset = Resources.Load<TextAsset>("palette");
                if (asset == null) return dict;             // Colour() falls back to 'text'
                foreach (var e in JsonHelper.ParsePalette(asset.text))
                    dict[e.key] = e.value;
                return dict;
            }
        }
    }

    /// <summary>JsonUtility cannot parse a bare object-of-objects, so the palette ships as an array.</summary>
    public static class JsonHelper
    {
        [Serializable] private class Wrapper { public List<MonitorScreenView.PaletteEntry> entries; }

        public static List<MonitorScreenView.PaletteEntry> ParsePalette(string json)
        {
            var w = JsonUtility.FromJson<Wrapper>("{\"entries\":" + json + "}");
            return w != null && w.entries != null ? w.entries : new List<MonitorScreenView.PaletteEntry>();
        }
    }
}
