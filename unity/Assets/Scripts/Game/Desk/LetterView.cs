// HR//0D -- LetterView: the full-screen paperwork. This is where the player actually READS.
// Assembly: HRZeroDay.Game  (UnityEngine)
// Owner: Agent (written) / Dev A (owns it afterwards). Started 2026-11-26.
//
// WHY A SEPARATE VIEW (user decision 2026-11-26, option c)
//   The 142x88 CRT glass is 20 columns x 9 rows. That is enough for a card, not for prose. The brief
//   says the screen hosts the email/letter -- so the letter gets a full 480x270 page that slides over
//   the desk, and the CRT keeps the card: day, trust, who is sitting in front of you, "> READ LETTER".
//
// WHAT THE PLAYER DOES HERE
//   Read the letter. Spot what is wrong with it. Either FLAG it (money demand, pressure, wrong
//   reference, template language) or RETURN to the desk and stamp the applicant. Flagging does not
//   decide the hire -- it records EVIDENCE, which is what the debrief is built from.
//
// LINE BUDGET (this is the whole design constraint)
//   480x270, 5x7 font, letterGap 2, lineGap 3 -> 68 columns x 27 rows.
//   Title 1 + rule 1 + body 16 + signature 2 + flag prompt 3 = 23 rows. Fits with air to spare.

using System;
using System.Collections.Generic;
using HRZeroDay.Core.Data;
using UnityEngine;

namespace HRZeroDay.Game
{
    public static class LetterLayout
    {
        // Everything is derived from the page, never guessed:
        //   paper  456x246 at canvas (12,12)
        //   inset  6 px            -> text area 444x234 at canvas (18,18)
        //   cols   (444 + 2) / 7 = 63      rows (234 + 3) / 10 = 23
        // Two earlier versions were wrong and the tests caught both: a 68-column rule truncated to
        // '-----~', and a 0-inset page clipped the title's first glyph against the paper edge.
        public const int Inset = 6;
        public const int Cols = 63;
        public const int BodyRows = 16;
        public const int TotalRows = 23;

        /// <summary>The paper in canvas px (y up). The editor places the paper sprite on this rect.</summary>
        public static MonitorScreen.GlassRect PaperRect()
            => new MonitorScreen.GlassRect { x = 12, y = 12, w = 456, h = 246 };

        /// <summary>The inset text area inside the paper: this is the raster the text composites onto.</summary>
        public static MonitorScreen.GlassRect TextRect()
            => new MonitorScreen.GlassRect
            {
                x = 12 + Inset, y = 12 + Inset,
                w = 456 - 2 * Inset, h = 246 - 2 * Inset
            };

        public static int GlyphW => MonitorScreen.GlyphW;
        public static int GlyphH => MonitorScreen.GlyphH;
        public static int LetterGap => MonitorScreen.LetterGap;
        public static int LineGap => MonitorScreen.LineGap;

        /// <summary>Wrap on words. Never splits a word unless it is longer than the whole line.</summary>
        public static List<string> Wrap(string text, int cols)
        {
            var outp = new List<string>();
            if (string.IsNullOrEmpty(text)) return outp;
            foreach (var paragraph in text.Replace("\r\n", "\n").Split('\n'))
            {
                if (paragraph.Length == 0) { outp.Add(string.Empty); continue; }
                var line = string.Empty;
                foreach (var word in paragraph.Split(' '))
                {
                    if (word.Length == 0) continue;
                    if (line.Length == 0) line = word;
                    else if (line.Length + 1 + word.Length <= cols) line += " " + word;
                    else { outp.Add(line); line = word; }
                    while (line.Length > cols)                 // a URL or a pasted token
                    {
                        outp.Add(line.Substring(0, cols - 1) + "~");
                        line = line.Substring(cols - 1);
                    }
                }
                if (line.Length > 0) outp.Add(line);
            }
            return outp;
        }
    }

    /// <summary>One page of text, already wrapped and paginated. Pure data -- no Unity.</summary>
    public sealed class LetterPage
    {
        public string title;
        public List<string> body = new List<string>();
        public string signature;
        public string flagPrompt;
        public int page, pageCount;

        /// <summary>The rule is drawn from the same budget the body wraps to, never a magic number.</summary>
        public static string Rule => new string('-', LetterLayout.Cols);

        public bool HasNext => page < pageCount;
        public bool HasPrev => page > 1;

        /// <summary>Total rows this page needs, so the caller can assert it fits before drawing.</summary>
        public int RequiredRows => 1 + 1 + body.Count + 2 + 3;

        public List<MonitorScreen.LineSpec> ToLines()
        {
            var lines = new List<MonitorScreen.LineSpec>
            {
                new MonitorScreen.LineSpec(title ?? string.Empty, "warn"),
                new MonitorScreen.LineSpec(Rule, "line")
            };
            for (int i = 0; i < body.Count; i++)
                lines.Add(new MonitorScreen.LineSpec(body[i], "text"));

            // signature sits directly under the body, where a sign-off belongs
            if (!string.IsNullOrEmpty(signature))
                lines.Add(new MonitorScreen.LineSpec(signature, "textdim"));

            int wanted = LetterLayout.TotalRows - 2;                    // reserve prompt (+ pager)
            while (lines.Count < wanted) lines.Add(new MonitorScreen.LineSpec(string.Empty, "text"));

            lines.Add(new MonitorScreen.LineSpec(
                flagPrompt ?? "> FLAG WHAT IS WRONG        X BACK TO DESK", "warn"));

            if (pageCount > 1)
                lines.Add(new MonitorScreen.LineSpec("< PREV    > NEXT    PAGE " + page + "/" + pageCount, "muted"));
            return lines;
        }
    }

    /// <summary>Splits a letter into pages of at most BodyRows wrapped lines.</summary>
    public static class LetterPaginator
    {
        public static List<LetterPage> Paginate(string title, string body, string signature,
                                                string flagPrompt, int bodyRows = LetterLayout.BodyRows,
                                                int cols = LetterLayout.Cols)
        {
            var wrapped = LetterLayout.Wrap(body, cols);
            var pages = new List<LetterPage>();
            if (wrapped.Count == 0) wrapped.Add(string.Empty);
            for (int i = 0; i < wrapped.Count; i += bodyRows)
            {
                var page = new LetterPage { title = title, signature = signature, flagPrompt = flagPrompt };
                for (int j = i; j < wrapped.Count && j < i + bodyRows; j++) page.body.Add(wrapped[j]);
                pages.Add(page);
            }
            for (int i = 0; i < pages.Count; i++)
            {
                pages[i].page = i + 1;
                pages[i].pageCount = pages.Count;
            }
            return pages;
        }
    }

    // (FlagOption / LetterFlags moved to HRZeroDay.Core.Data on 2026-11-26: a flag is a RULE.)

    /// <summary>What the flag panel is doing right now.</summary>
    public sealed class LetterSession
    {
        public ApplicantRecord applicant;
        public List<LetterPage> pages = new List<LetterPage>();
        public List<FlagOption> flags = new List<FlagOption>();
        public HashSet<string> flagged = new HashSet<string>();
        public int pageIndex;

        public LetterPage Current => pages.Count == 0 ? null : pages[Math.Min(pageIndex, pages.Count - 1)];
        public bool HasOpenFlags => flagged.Count > 0;

        /// <summary>Score of the flags against the hidden truth: +1 right, -1 wrong. Shown in debrief.</summary>
        public int Score
        {
            get
            {
                int s = 0;
                foreach (var f in flags)
                {
                    bool picked = flagged.Contains(f.id);
                    if (picked && f.isReal) s += 1;
                    else if (picked && !f.isReal) s -= 1;
                }
                return s;
            }
        }

        public void Toggle(string flagId)
        {
            if (flagged.Contains(flagId)) flagged.Remove(flagId);
            else flagged.Add(flagId);
        }

        public void Next() { if (pageIndex + 1 < pages.Count) pageIndex++; }
        public void Prev() { if (pageIndex > 0) pageIndex--; }
    }

    /// <summary>
    /// The MonoBehaviour. It deliberately knows nothing about layout beyond "fill this page".
    /// Presentation (paper sprite, panel 9-slice) is assigned in the editor; the text is composited by
    /// the same raster the CRT uses, so the whole game has exactly one font path.
    /// </summary>
    public sealed class LetterView : MonoBehaviour
    {
        public const string RootName = "LetterView";
        private const string AtlasResource = "fonts/monitor_font_5x7";
        // The atlas is a PNG; the glyph metrics are a SEPARATE TextAsset. Loading both from
        // AtlasResource made every fresh letter fail with "font atlas missing" (the json is never
        // found because the name has no extension in Resources.Load).
        private const string MetricsResource = "fonts/monitor_font_5x7.json";

        public GameObject root;              // the panel that gets shown/hidden
        public SpriteRenderer paper;         // the page background (UI/panel 9-slice, stretched)
        public SpriteRenderer textLayer;     // the composited text

        public LetterSession session { get; private set; }
        private Texture2D _tex;
        private IGlyphSource _glyphs;
        private Color32 _bg = new Color32(0xf2, 0xe8, 0xd5, 0xff);   // paper
        private MonitorScreen.GlassRect _page;
        private bool _ready;

        public bool Open(ApplicantRecord applicant, string letterBody, string signature)
        {
            if (!_ready && !Initialise()) return false;

            string title = applicant != null
                ? "COVER LETTER  " + (applicant.fullName ?? "").ToUpperInvariant()
                : "COVER LETTER";
            session = new LetterSession
            {
                applicant = applicant,
                pages = LetterPaginator.Paginate(title, letterBody, signature, null),
                flags = LetterFlags.Build(applicant, letterBody + "\n" + signature)
            };
            if (root != null) root.SetActive(true);
            Repaint();
            return true;
        }

        /// <summary>
        /// Opens the letter the DeskSession already generated. This is the path the game uses: the
        /// flags (and their hidden isReal) are decided once by Paperwork.For, never re-derived here.
        /// The flagged set is shared with Paperwork so the HUD and the scoring see the same choices.
        /// </summary>
        public bool OpenPaperwork(Paperwork paper)
        {
            if (paper == null) return false;
            if (!_ready && !Initialise()) return false;
            session = new LetterSession
            {
                applicant = paper.applicant,
                pages = LetterPaginator.Paginate(paper.title, paper.body, paper.signature,
                                                 "> FLAG WHAT IS WRONG        X BACK TO DESK"),
                flags = paper.flags,
                flagged = paper.flagged
            };
            if (root != null) root.SetActive(true);
            Repaint();
            return true;
        }

        public void Close()
        {
            if (root != null) root.SetActive(false);
        }

        public void ToggleFlag(string id) { if (session != null) { session.Toggle(id); Repaint(); } }
        public void NextPage() { if (session != null) { session.Next(); Repaint(); } }
        public void PrevPage() { if (session != null) { session.Prev(); Repaint(); } }

        private bool Initialise()
        {
            var tex = Resources.Load<Texture2D>(AtlasResource);
            var metrics = Resources.Load<TextAsset>(MetricsResource);
            if (tex == null || metrics == null)
            {
                Debug.LogError("LetterView: font atlas missing from Resources -- run make_font.py");
                return false;
            }
            var fm = JsonUtility.FromJson<FontMetrics>(metrics.text);
            _glyphs = new FontAtlasGlyphSource(fm, tex.GetPixels32(), tex.width,
                                               MonitorScreenView.Palette.FromResources());

            // the TEXT area inside the paper: the paper sprite itself is placed by the editor
            _page = LetterLayout.TextRect();

            _tex = new Texture2D(_page.w, _page.h, TextureFormat.RGBA32, false);
            _tex.filterMode = FilterMode.Point;
            textLayer.sprite = Sprite.Create(_tex, new Rect(0, 0, _page.w, _page.h),
                                             new Vector2(0.5f, 0.5f), 16f);
            textLayer.sortingOrder = 200;
            _ready = true;
            return true;
        }

        private void Repaint()
        {
            if (session == null || session.Current == null) return;
            var lines = MonitorScreen.LinesToRects(session.Current.ToLines(), _page);
            var buf = new Color32[_page.w * _page.h];
            MonitorScreenRaster.Render(buf, _page.w, _page.h, lines, _glyphs, _bg);
            _tex.SetPixels32(buf);
            _tex.Apply(false);
        }
    }
}
