// HR//0D -- world-space implementation of IEvidenceOverlay: the read-out you get when you inspect.
// Assembly: HRZeroDay.Game
//
// WHAT THIS USED TO BE, AND WHY IT CHANGED (2026-11-26)
//   Two TextMesh lines in the engine's own font at characterSize 0.07-0.09 -- about 3 px tall at this
//   camera, which the player reported as "a text panel with very small text". The "panel" behind it was
//   art/kit/ui/tooltip_9slice.png: a CREAM sheet with 2 px of padding, scaled to 10 x 3 units. Stretched
//   over the applicant's face it read as a blank white card.
//
//   So the callout is now painted, not assembled: TextPanel (in MonitorScreenView.cs) renders the same
//   5x7 atlas the CRT and the letter use into one texture, with its own dark fill, 1 px 'line' border and
//   8 px padding. One sprite, one draw call, readable at 480x270, and it cannot fail to load separately
//   from its text -- which is exactly how the cream box ended up with no words on it.
using System.Collections.Generic;
using UnityEngine;

namespace HRZeroDay.Game.Interaction
{
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class DeskEvidenceOverlay : MonoBehaviour, IEvidenceOverlay
    {
        public Transform magnifierRing;
        public Transform callout;
        public TextMesh calloutLabel;      // legacy fields kept so old scenes still bind
        public TextMesh calloutBody;

        public int referenceWidth = 480;
        public int referenceHeight = 270;
        public float pixelsPerUnit = 16f;
        public float zoom = 2f;

        // ANCHOR, in canvas px: the panel's TOP-RIGHT corner. The applicant's bust sits on the right of
        // the desk (head top ~ y 210, left edge ~ x 330), so the read-out hangs to the LEFT of the head
        // and never covers the face it is describing. These used to be read as a CENTRE while the panel
        // was placed by a function that forgot the canvas-centre offset, so the panel was drawn 15 units
        // right and 8 units up of where the tap router thought it was -- off the room entirely.
        public float calloutX = 316f;
        public float calloutY = 214f;

        private SpriteRenderer _panel;
        private Texture2D _tex;
        private IGlyphSource _glyphs;
        private Dictionary<string, Color32> _palette;
        private string _label = string.Empty;
        // where the panel actually ended up (canvas px), which the tap router must agree with
        private float _anchorX;
        private float _anchorY;
        private string _body = string.Empty;
        private bool _ready;

        // An INDEX CARD. The player's two reports -- "opens it too big" (2026-11-26) and "opens something
        // big at the bottom of the screen which goes off screen" (2026-11-27) -- are the same thing seen
        // twice: the panel had no size bound and no clamp to the page. Now both are constants, and
        // CalloutPlacement is a pure function with its own tests.
        private const int PaddingX = 6;
        private const int PaddingY = 6;
        private const int MaxWidthPx = 148;          // 22 columns at 5x7: a card, not a panel
        private const int MinWidthPx = 74;
        private const int MaxLines = 6;              // 6 rows of text: a label + 5 body rows

        public bool IsShowing { get { return callout != null && callout.gameObject.activeSelf; } }

        /// <summary>Wires the painter. Called once by the scene builder, after the callout exists.</summary>
        public void Initialise(GameObject panelObject)
        {
            _panel = panelObject == null ? null : panelObject.GetComponent<SpriteRenderer>();
            var tex = Resources.Load<Texture2D>("fonts/monitor_font_5x7");
            var metricsAsset = Resources.Load<TextAsset>("fonts/monitor_font_5x7");
            if (tex == null || metricsAsset == null || _panel == null)
            {
                Debug.LogError("DeskEvidenceOverlay: font atlas missing -- the read-out will stay text-free. " +
                               "Run HR//0D > Publish Art To Resources.");
                return;
            }
            var metrics = JsonUtility.FromJson<FontMetrics>(metricsAsset.text);
            _palette = MonitorScreenView.Palette.FromResources();
            _glyphs = new FontAtlasGlyphSource(metrics, tex.isReadable ? tex.GetPixels32() : null,
                                               tex.width, _palette);
            _ready = true;
        }

        public void ShowMagnifier(Rect canvasRect, string label, string wording)
        {
            if (magnifierRing != null)
            {
                magnifierRing.gameObject.SetActive(true);
                magnifierRing.position = World(canvasRect.center);
                float w = canvasRect.width * zoom / pixelsPerUnit;
                float h = canvasRect.height * zoom / pixelsPerUnit;
                magnifierRing.localScale = new Vector3(w, h, 1f);
            }
            ShowText(label, wording);
        }

        public void ShowCallout(Rect canvasRect, string label, string wording)
        {
            if (magnifierRing != null) magnifierRing.gameObject.SetActive(false);
            ShowText(label, wording);
        }

        public void Hide()
        {
            if (magnifierRing != null) magnifierRing.gameObject.SetActive(false);
            if (callout != null) callout.gameObject.SetActive(false);
        }

        /// <summary>The footprint of the panel in canvas px -- the router uses it to close on a tap inside.</summary>
        public Rect CalloutCanvasRect()
        {
            float w = _tex != null ? _tex.width : 120f;
            float h = _tex != null ? _tex.height : 40f;
            // The rect comes from the SAME function that placed the sprite, not from the anchor the
            // caller asked for. Two copies of this maths is what BUG-050 and BUG-051 both were.
            var box = CalloutPlacement.Fit(calloutX, calloutY, w, h, referenceWidth, referenceHeight);
            return new Rect(box.Left, box.Top - h, w, h);
        }

        public bool Contains(Vector2 canvasPoint)
        {
            if (!IsShowing) return false;
            var r = CalloutCanvasRect();
            return canvasPoint.x >= r.xMin && canvasPoint.x <= r.xMax &&
                   canvasPoint.y >= r.yMin && canvasPoint.y <= r.yMax;
        }

        private void ShowText(string label, string wording)
        {
            if (callout == null) return;
            callout.gameObject.SetActive(true);
            _label = label ?? string.Empty;
            _body = wording ?? string.Empty;
            Repaint();
        }

        /// <summary>
        /// Paints the label (warn) plus the wrapped body (text), sized to the content: the panel is as big
        /// as its words, so a two-word label does not carry a card-sized box.
        /// </summary>
        private void Repaint()
        {
            if (!_ready || _panel == null) return;

            int cols = (MaxWidthPx - 2 * PaddingX) / (MonitorScreen.GlyphW + MonitorScreen.LetterGap);
            var lines = new List<string>();
            var colours = new List<string>();
            lines.Add(Fit(_label, cols));
            colours.Add("warn");
            foreach (var line in Wrap(_body, cols))
            {
                lines.Add(line);
                colours.Add("text");
            }
            while (lines.Count > MaxLines) { lines.RemoveAt(lines.Count - 1); colours.RemoveAt(colours.Count - 1); }

            int step = MonitorScreen.GlyphH + MonitorScreen.LineGap;
            // width to the longest line, so a short label does not carry a card-sized box
            int widest = 0;
            foreach (var l in lines) if (l.Length > widest) widest = l.Length;
            int w = 2 * PaddingX + widest * (MonitorScreen.GlyphW + MonitorScreen.LetterGap);
            w = w < 72 ? 72 : (w > MaxWidthPx ? MaxWidthPx : w);
            int h = 2 * PaddingY + lines.Count * step - MonitorScreen.LineGap;
            if (_tex == null || _tex.width != w || _tex.height != h)
            {
                if (_tex != null) Destroy(_tex);
                _tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
                _tex.filterMode = FilterMode.Point;
                _tex.name = "CalloutPanel";
            }

            var fill = TextPanel.Ink(_palette, "panel", new Color32(19, 25, 38, 255));
            var border = TextPanel.Ink(_palette, "line", new Color32(43, 58, 77, 255));
            var buf = TextPanel.Paint(w, h, lines, colours, _glyphs, fill, border);
            MonitorScreenRaster.FlipToTextureOrder(buf, w, h);   // PNG order -> texture order
            _tex.SetPixels32(buf);
            _tex.Apply(false);

            if (_panel.sprite == null || _panel.sprite.texture != _tex)
                _panel.sprite = Sprite.Create(_tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), pixelsPerUnit);
            _panel.color = Color.white;

            // Placement is a pure function in Core-free code so it can be tested without a scene: the
            // panel keeps its anchor beside the applicant's head but is pushed back inside the page.
            var box = CalloutPlacement.Fit(calloutX, calloutY, w, h, referenceWidth, referenceHeight);
            _anchorX = box.Left;
            _anchorY = box.Top;
            callout.localScale = PageTransform.NaturalScale;
            _panel.transform.localScale = PageTransform.NaturalScale;
            callout.localPosition = PageTransform.CanvasToWorld(box.Left + w * 0.5f, box.Top - h * 0.5f,
                                                                pixelsPerUnit);
        }

        private static string Fit(string text, int cols)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;
            return text.Length <= cols ? text : text.Substring(0, cols - 1) + "~";
        }

        private static List<string> Wrap(string text, int cols)
        {
            var outp = new List<string>();
            if (string.IsNullOrEmpty(text) || cols < 2) return outp;
            var line = new System.Text.StringBuilder();
            foreach (var word in text.Split(' '))
            {
                if (word.Length == 0) continue;
                if (line.Length == 0) { line.Append(word); continue; }
                if (line.Length + 1 + word.Length <= cols) { line.Append(' ').Append(word); continue; }
                outp.Add(line.ToString());
                line.Length = 0;
                line.Append(word);
            }
            if (line.Length > 0) outp.Add(line.ToString());
            return outp;
        }

        private Vector3 World(Vector2 canvasPoint)
        {
            var p = PageTransform.CanvasToWorld(canvasPoint.x, canvasPoint.y, pixelsPerUnit);
            p.z = -1f;
            return p;
        }

        private void OnDisable()
        {
            Hide();
        }
    }
}
