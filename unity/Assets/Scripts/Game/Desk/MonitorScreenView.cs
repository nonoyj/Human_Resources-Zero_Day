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

        public int CellW { get; private set; }
        public int CellH { get; private set; }

        public FontAtlasGlyphSource(FontMetrics metrics, Color32[] atlasPixels, int atlasW,
                                    Dictionary<string, Color32> palette)
        {
            CellW = metrics.cellW;
            CellH = metrics.cellH;
            _atlas = atlasPixels;
            _atlasW = atlasW;
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

        public bool InkAt(char ch, int gx, int gy)
        {
            Vector2 origin;
            if (!_rects.TryGetValue(ch, out origin))
            {
                if (!_rects.TryGetValue(char.ToUpperInvariant(ch), out origin)) return false;
            }
            int ax = (int)origin.x + gx;
            int ay = (int)origin.y + gy;
            if (ax < 0 || ay < 0 || ax >= _atlasW || ay * _atlasW + ax >= _atlas.Length) return false;
            return _atlas[ay * _atlasW + ax].a != 0;
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
        public class Glyph { public string ch; public int x; public int y; }

        public int cellW = 5, cellH = 7, pad = 2, cols = 16;
        public int atlasW, atlasH;
        public string ink = "text";
        public List<Glyph> glyphs = new List<Glyph>();
    }

    [RequireComponent(typeof(SpriteRenderer))]
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

            var tex = Resources.Load<Texture2D>(AtlasResource);
            var metricsAsset = Resources.Load<TextAsset>(AtlasResource);
            if (tex == null || metricsAsset == null)
            {
                Debug.LogError("MonitorScreenView: Resources/" + AtlasResource +
                               " missing. Run: python tools/art/make_font.py && " +
                               "python tools/art/publish_to_unity.py");
                return false;
            }

            var metrics = JsonUtility.FromJson<FontMetrics>(metricsAsset.text);
            var palette = Palette.FromResources();
            _glyphs = new FontAtlasGlyphSource(metrics, tex.GetPixels32(), tex.width, palette);

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
