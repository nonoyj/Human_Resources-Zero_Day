// HR//0D -- readable play-mode HUD: status, applicant card, letter flags, debrief, day summary, ending.
// Assembly: HRZeroDay.Game
//
// The pixel-perfect CRT and letter render through the font atlas; the HUD uses the engine's built-in
// font so a fresh checkout with no generated frames still shows a legible, operable game. It renders
// ONLY data already in the player-facing DTO / Paperwork flags / DecisionResult -- never isReal,
// tellsPresent or ground truth.
using System.Text;
using UnityEngine;
using HRZeroDay.Core.Data;
using HRZeroDay.Core.Rules;

namespace HRZeroDay.Game.Interaction
{
    public sealed class GameHud : MonoBehaviour
    {
        private const int LineCount = 16;
        // "Text now is too big" (player, 2026-11-26). characterSize 0.26 with a 0.52 unit line step is a
        // 1.7 glyph box; 0.19 with 0.36 is a 1.2 box -- the same 5x7 feel as the CRT, and it leaves the
        // room readable behind it. These two numbers move together: the step must stay ~1.9 x the size or
        // the lines collide, and the backing panel is sized from both.
        private const float Step = 0.36f;
        // TextMesh spacing is in the font's em, NOT world units: the visible line pitch is
        // characterSize * 1.7 (Unity's textLineHeight) * Step. At 0.19 / 0.36 that is 0.116 units, i.e.
        // ~1.9 screen px on a 1080-tall Game view. The old code moved each line by a flat 0.36 units --
        // 23x too far -- so the block ran off the bottom of the screen after five lines and the next
        // screen's copy landed on top of this one. That is the "text opens way too big" report.
        private const float LinePitchEm = 1.7f;
        // One unit per character column, in em. 0.55 keeps the widest HUD line (50 chars) under 9 of the
        // 30 units the canvas is wide, while still reading as a card in the top-left.
        private const float ColumnEm = 0.55f;
        private const float WrapCharsPerUnit = 8.5f;

        private DeskBootstrap _game;
        private TextMesh _text;
        private bool _bound;
        private SpriteRenderer _panel;

        private static readonly Color Ink = new Color(42f / 255f, 33f / 255f, 24f / 255f, 1f);
        private static readonly Color PaperWhite = new Color(230f / 255f, 241f / 255f, 255f / 255f, 1f);

        public static GameHud Attach(DeskBootstrap game)
        {
            var go = new GameObject("HR_Hud");
            if (game != null) go.transform.SetParent(game.transform, false);
            var hud = go.AddComponent<GameHud>();
            hud.Bind(game);
            return hud;
        }

        public void Bind(DeskBootstrap game)
        {
            _game = game;
            _bound = true;
        }

        private void Awake()
        {
            // Dark backing so white HUD text stays readable where it crosses the light monitor
            // glass (day summary / debrief). Sized to the visible lines each frame; hidden while
            // the letter is open, where the paper is light and the HUD switches to dark ink.
            var panelGo = new GameObject("HudPanel");
            panelGo.transform.SetParent(transform, false);
            _panel = panelGo.AddComponent<SpriteRenderer>();
            var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            tex.SetPixels(new[] { new Color(1f, 1f, 1f, 1f) });
            tex.Apply(false, true);
            _panel.sprite = Sprite.Create(tex, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
            _panel.color = new Color(0.02f, 0.03f, 0.05f, 0.78f);
            _panel.sortingOrder = 219;   // just below the text (220), above the desk art
            panelGo.SetActive(false);

            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            // ONE TextMesh for the whole block. Sixteen TextMeshes positioned by hand is how the block
            // ended up 23x too tall; a single mesh wraps its own lines at the same pitch, so the layout is
            // whatever Unity decided it is, and the panel behind it can be measured from the reported size.
            var go = new GameObject("Text");
            go.transform.SetParent(transform, false);
            _text = go.AddComponent<TextMesh>();
            _text.font = font;
            _text.characterSize = 0.19f;
            _text.anchor = TextAnchor.UpperLeft;
            _text.alignment = TextAlignment.Left;
            // TextMesh is a Component, not a Renderer -- the order goes on its MeshRenderer.
            DeskRuntimeScene.SetTextMeshSorting(_text, 220);   // above the letter page and the CRT
            _text.text = string.Empty;
        }

        /// <summary>
        /// Forces the very next frame to show the current state. Needed when a screen change happens
        /// OUTSIDE Update -- ending the after-hours walk is the case: without this the summary would
        /// appear one frame late, after the walk's page has already gone.
        /// </summary>
        public void RefreshNow()
        {
            if (!_bound || _game == null || _game.Session == null) return;
            Push(BuildLines());
        }

        private void Update()
        {
            if (!_bound || _game == null || _game.Session == null) return;
            Push(BuildLines());
        }

        /// <summary>
        /// Draws a set of lines: position, ink, backing panel. Split out of Update so a caller that
        /// changes screens between frames (the walk ending) can repaint immediately instead of waiting
        /// for the next Update and showing the wrong thing for one frame.
        /// </summary>
        private void Push(System.Collections.Generic.List<string> lines)
        {
            if (_text == null) return;
            bool letter = _game.LetterOpen;
            // The walk draws its own page and its own key strip. The desk HUD stays out of it.
            bool walking = _game.ShowingSweep;

            // Letter mode: LetterView already shows the letter itself, so the HUD shows ONLY the
            // flags, in dark ink, in the blank middle of the page (below the body, above the
            // prompt) instead of duplicating the letter in white over cream paper.
            Color ink = letter ? Ink : PaperWhite;
            _text.color = ink;

            var text = new StringBuilder();
            int maxLen = 0;
            if (!walking)
            {
                int shown = 0;
                for (int i = 0; i < lines.Count && shown < LineCount; i++, shown++)
                {
                    var line = lines[i] ?? string.Empty;
                    text.Append(line).Append('\n');
                    if (line.Length > maxLen) maxLen = line.Length;
                }
            }
            _text.text = text.ToString();

            // The block is pinned to the top-left of the canvas and grows DOWN, in canvas px.
            if (letter) _text.transform.localPosition = new Vector3(0.5f, 8.6f, -2f);
            else _text.transform.localPosition = new Vector3(0.45f, 16.25f, -2f);

            if (_panel != null)
            {
                bool show = !letter && !walking && maxLen > 0;
                _panel.gameObject.SetActive(show);
                if (!show) return;

                // Size the backing from the block's real proportions. The characterSize is per glyph, so
                // the whole block is (longest line x ColumnEm) em wide and (lines x 1.7) em tall.
                float lineHeight = _text.characterSize * LinePitchEm;
                float w = 0.5f + maxLen * _text.characterSize * ColumnEm;
                float h = 0.3f + text.ToString().Split('\n').Length * lineHeight;
                w = Mathf.Clamp(w, 4f, 14f);
                h = Mathf.Clamp(h, 0.6f, 8f);
                // panel pivot is the centre: place its centre at (left + w/2, top - h/2) of the block
                float left = _text.transform.localPosition.x - 0.15f;
                float top = _text.transform.localPosition.y + 0.05f;
                _panel.transform.localPosition = new Vector3(left + w * 0.5f, top - h * 0.5f, -2.1f);
                _panel.transform.localScale = new Vector3(w, h, 1f);
            }
        }

        private System.Collections.Generic.List<string> BuildLines()
        {
            var result = new System.Collections.Generic.List<string>();
            var s = _game.Session;

            if (_game.ShowEnding)
            {
                // THE RUN, NOT THE LAST DAY (v101). This screen used to show the shift's live counters,
                // which after a defeat read as if the run had barely started. The ledger knows the days
                // that actually closed, so the ending says what the player did -- and the verdict is a
                // sentence built from those numbers, not a mood.
                var ledger = _game.Ledger;
                Add(result, "HUMAN RESOURCES // ZERO DAY");
                Add(result, OutcomeCopy(s.Shift.Outcome));
                Add(result, "");
                if (ledger != null && ledger.Count > 0)
                {
                    Add(result, "DAYS " + ledger.Count + "/5   QUOTAS MET " + ledger.QuotasMet + "/" + ledger.Count);
                    Add(result, "HIRED " + ledger.Approved + "  REJECTED " + ledger.Rejected +
                                "  SYNTHETICS IN " + ledger.Hired + "  CAUGHT " + ledger.Caught);
                    Add(result, "TRUST " + ledger.TrustAtStart + " -> " + ledger.TrustAtEnd +
                                "   CORRECT CALLS " + ledger.Correct + "/" + ledger.Decisions);
                    Add(result, "BY DAY  " + ledger.CorrectByDay());
                    Add(result, "");
                    Add(result, Wrap(ledger.Verdict(), 62));
                }
                else
                {
                    Add(result, "TRUST " + s.Trust + "   SYNTHETICS HIRED " + s.Shift.SyntheticsHired);
                    Add(result, "");
                    Add(result, "The building keeps your file. It does not forget.");
                }
                Add(result, "");
                Add(result, "[R] START A NEW RUN");
                return result;
            }

            if (_game.ShowDaySummary)
            {
                var d = _game.LastDay;
                Add(result, "DAY " + (d == null ? s.Day : d.Day) + " CLOSED");
                Add(result, "");
                Add(result, d == null ? string.Empty :
                    "APPROVED " + d.Approved + "   REJECTED " + d.Rejected + "   QUOTA " + (d.QuotaMet ? "MET" : "MISSED"));
                Add(result, d == null ? string.Empty :
                    "WRONGFUL REJECTIONS " + d.WrongfulRejections + "   SYNTHETICS CAUGHT " + d.SyntheticsCaught);
                Add(result, "TRUST " + s.Trust + "   CORRUPTION " + s.CorruptionLevel);
                Add(result, "");
                // The walk's receipt, when one happened. It is the only place the day's own damage is
                // stated in plain words -- the counters above are numbers, this is the office.
                if (!string.IsNullOrEmpty(_game.SweepLine)) { Add(result, _game.SweepLine); Add(result, ""); }
                if (d != null) Add(result, Wrap(d.HintLine, 62));
                Add(result, "");
                // WHERE THIS DAY SITS (v101). "DAY 3 OF 5" plus one honest line about the state of the
                // run, so the player always knows whether they are near the end or near the edge.
                if (_game.Ledger != null)
                    Add(result, _game.Ledger.ArcLine(d == null ? s.Day : d.Day));
                Add(result, "");
                bool last = s.Finished || (d != null && d.Day >= RunLedger.ArcLength);
                Add(result, last ? "[SPACE] CLOSE THE FILE" : "[SPACE] NEXT DAY");
                return result;
            }

            if (_game.ShowDebrief)
            {
                var r = _game.LastDecision;
                Add(result, "DECISION RECORDED");
                Add(result, r == null ? string.Empty : (r.IsCorrect ? "The record checks out." : "The record does not check out."));
                Add(result, "");
                if (r != null) Add(result, Wrap(r.DebriefLine, 62));
                Add(result, "");
                Add(result, "TRUST " + s.Trust + (r == null ? string.Empty : "   (" + Signed(r.TrustDelta) + ")"));
                Add(result, "");
                Add(result, _game.autoAdvance ? "NEXT APPLICANT IN " + Mathf.CeilToInt(Mathf.Max(0f, _game.DebriefLeft)) : "[SPACE] CONTINUE");
                return result;
            }

            if (_game.LetterOpen && s.Paperwork != null)
            {
                // Flags only: LetterView renders title/body/signature/prompt in dark ink on paper.
                // Duplicating the letter here in white text over cream paper made both unreadable.
                var p = s.Paperwork;
                Add(result, "FLAG WHAT IS WRONG (1-5):");
                var flags = p.flags;
                for (int i = 0; i < flags.Count && i < 5; i++)
                {
                    string mark = p.flagged.Contains(flags[i].id) ? "[X]" : "[ ]";
                    Add(result, (i + 1) + " " + mark + " " + flags[i].label);
                }
                return result;
            }

            var v = s.Current;
            // Compact by design: the full dossier lives in the letter. Keeping only the identity
            // line here stops the HUD covering the monitor and the applicant across the table.
            Add(result, "HUMAN RESOURCES // ZERO DAY");
            Add(result, "DAY " + s.Day + "/5  TRUST " + s.Trust + "  CORRUPT " + s.CorruptionLevel +
                        "  SUBJECT " + (s.Shift.CurrentIndex + 1) + "/" + s.Shift.Today.Count);
            Add(result, (v.FullName ?? "APPLICANT") + " / " + (v.Role ?? "OPEN ROLE") + " / ID " + (v.IdNumber ?? "-"));
            Add(result, "CLICK THE PERSON TO INSPECT");
            Add(result, "[L] LETTER  [1-5] FLAGS/REASON  [6] INSUFFICIENT");
            Add(result, "[A] APPROVE  [S] REJECT  [SPACE] NEXT  [H] HELP");
            if (_game.HelpVisible)
            {
                Add(result, "");
                Add(result, "HELP: the letter is read once; the stamp locks it for good.");
                Add(result, "Right flags pay trust, wrong flags cost it. No truth is shown.");
            }
            return result;
        }

        /// <summary>Plain names for the artifact ids, so the HUD never shows a key like 'straight_badge'.</summary>
        private static string ArtifactName(string artifact)
        {
            switch (artifact)
            {
                case "mirror": return "eyes and cheeks";
                case "no_blink": return "the eyes (hold still)";
                case "ear": return "the near ear";
                case "seam": return "the hairline";
                case "glasses_merge": return "the glasses arms";
                case "skin_band": return "jaw and neck";
                case "hands": return "the hands";
                case "straight_badge": return "lanyard and badge";
                case "shadow": return "the shadow";
                default: return artifact.Replace('_', ' ');
            }
        }

        private static void Add(System.Collections.Generic.List<string> lines, string text)
        {
            lines.Add(text ?? string.Empty);
        }

        private static string Signed(int value)
        {
            return value >= 0 ? "+" + value : value.ToString();
        }

        private static string OutcomeCopy(RunOutcome outcome)
        {
            switch (outcome)
            {
                case RunOutcome.Survived: return "SURVIVED - YOU KEPT THE LINE";
                case RunOutcome.DefeatedPerfectEmployee: return "DISMISSED - TOO MANY SYNTHETICS HIRED";
                case RunOutcome.DefeatedGhostQuota: return "DISMISSED - TRUST COLLAPSED";
                default: return "FILE CLOSED";
            }
        }

        private static string Wrap(string text, int width)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;
            if (text.Length <= width) return text;
            var sb = new StringBuilder();
            int line = 0;
            foreach (var word in text.Split(' '))
            {
                if (line == 0) { sb.Append(word); line = word.Length; continue; }
                if (line + 1 + word.Length <= width) { sb.Append(' ').Append(word); line += 1 + word.Length; }
                else { sb.Append('\n').Append(word); line = word.Length; }
            }
            return sb.ToString();
        }

    }
}
