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

namespace HRZeroDay.Game.Interaction
{
    public sealed class GameHud : MonoBehaviour
    {
        private const int LineCount = 16;
        private const float Step = 0.52f;

        private DeskBootstrap _game;
        private TextMesh[] _lines;
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
            _lines = new TextMesh[LineCount];
            for (int i = 0; i < LineCount; i++)
            {
                var go = new GameObject("Line" + i);
                go.transform.SetParent(transform, false);
                go.transform.localPosition = new Vector3(0.45f, 16.25f - i * Step, -2f);
                var text = go.AddComponent<TextMesh>();
                text.font = font;
                text.characterSize = 0.26f;          // compact: leaves the desk art readable
                // TextMesh is a Component, not a Renderer -- the order goes on its MeshRenderer.
                DeskRuntimeScene.SetTextMeshSorting(text, 220);   // above the letter page and the CRT
                text.text = string.Empty;
                _lines[i] = text;
            }
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
            bool letter = _game.LetterOpen;
            // The walk draws its own page and its own key strip. The desk HUD stays out of it.
            bool walking = _game.ShowingSweep;
            // Letter mode: LetterView already shows the letter itself, so the HUD shows ONLY the
            // flags, in dark ink, in the blank middle of the page (below the body, above the
            // prompt) instead of duplicating the letter in white over cream paper.
            float x = 0.5f;
            float topY = letter ? 8.6f : 16.25f;
            Color ink = letter ? Ink : PaperWhite;
            int maxLen = 0;
            for (int i = 0; i < _lines.Length; i++)
            {
                if (_lines[i] == null) continue;
                _lines[i].transform.localPosition = new Vector3(x, topY - i * Step, -2f);
                if (walking) { _lines[i].text = string.Empty; continue; }
                _lines[i].color = ink;
                _lines[i].text = i < lines.Count ? lines[i] : string.Empty;
                if (i < lines.Count && lines[i] != null && lines[i].Length > maxLen) maxLen = lines[i].Length;
            }
            if (_panel != null)
            {
                _panel.gameObject.SetActive(!letter && !walking && lines.Count > 0);
                if (!letter && !walking && lines.Count > 0)
                {
                    float w = Mathf.Clamp(1f + maxLen * 0.24f, 4f, 14.5f);
                    float h = lines.Count * Step + 0.6f;
                    _panel.transform.localPosition = new Vector3(0.25f + w * 0.5f, 16.45f - h * 0.5f, -2.1f);
                    _panel.transform.localScale = new Vector3(w, h, 1f);
                }
            }
        }

        private System.Collections.Generic.List<string> BuildLines()
        {
            var result = new System.Collections.Generic.List<string>();
            var s = _game.Session;

            if (_game.ShowEnding)
            {
                Add(result, "HUMAN RESOURCES // ZERO DAY");
                Add(result, "SHIFT COMPLETE - " + OutcomeCopy(s.Shift.Outcome));
                Add(result, "TRUST " + s.Trust + "   SYNTHETICS HIRED " + s.Shift.SyntheticsHired);
                Add(result, "");
                Add(result, "The building keeps your file. It does not forget.");
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
                Add(result, "[SPACE] NEXT DAY");
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
