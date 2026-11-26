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
        private const int LineCount = 22;
        private const float Step = 0.68f;

        private DeskBootstrap _game;
        private TextMesh[] _lines;
        private bool _bound;

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
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _lines = new TextMesh[LineCount];
            for (int i = 0; i < LineCount; i++)
            {
                var go = new GameObject("Line" + i);
                go.transform.SetParent(transform, false);
                go.transform.localPosition = new Vector3(0.45f, 16.25f - i * Step, -2f);
                var text = go.AddComponent<TextMesh>();
                text.font = font;
                text.characterSize = 0.38f;          // ~6 px at 480x270 / 16 PPU
                go.GetComponent<Renderer>().sortingOrder = 220;   // above the letter page and the CRT
                text.text = string.Empty;
                _lines[i] = text;
            }
        }

        private void Update()
        {
            if (!_bound || _game == null || _game.Session == null) return;
            var lines = BuildLines();
            for (int i = 0; i < _lines.Length; i++)
                if (_lines[i] != null) _lines[i].text = i < lines.Count ? lines[i] : string.Empty;
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
                var p = s.Paperwork;
                Add(result, p.title);
                Add(result, new string('-', 58));
                var words = WrapBlock(p.body, 58, 9);
                for (int i = 0; i < words.Count; i++) Add(result, words[i]);
                Add(result, string.IsNullOrEmpty(p.signature) ? string.Empty : p.signature.Replace("\n", " "));
                Add(result, new string('-', 58));
                Add(result, "FLAG WHAT IS WRONG (1-5)     [X] BACK TO DESK");
                var flags = p.flags;
                for (int i = 0; i < flags.Count && i < 5; i++)
                {
                    string mark = p.flagged.Contains(flags[i].id) ? "[X]" : "[ ]";
                    Add(result, (i + 1) + " " + mark + " " + flags[i].label);
                }
                return result;
            }

            var v = s.Current;
            Add(result, "HUMAN RESOURCES // ZERO DAY");
            Add(result, "DAY " + s.Day + "/5   TRUST " + s.Trust + "   CORRUPTION " + s.CorruptionLevel +
                        "   SUBJECT " + (s.Shift.CurrentIndex + 1) + "/" + s.Shift.Today.Count);
            Add(result, new string('-', 58));
            Add(result, (v.FullName ?? "APPLICANT") + "  /  " + (v.Role ?? "OPEN ROLE"));
            Add(result, "ID " + (v.IdNumber ?? "-") + "   AGE " + v.Age);
            Add(result, "UNIVERSITY " + (v.University ?? "-") + " " + v.GraduationYear);
            Add(result, "EMPLOYER " + (v.Employer ?? "-") + " EST. " + v.EmployerFounded);
            Add(result, "STARTED " + v.EmploymentStartYear + "   EXPERIENCE " + v.YearsExperience + "Y   REFERRAL " + (v.Referral ?? "-"));
            Add(result, new string('-', 58));
            Add(result, "CLICK THE PERSON: inspect body tells (magnifier + text)");
            Add(result, "[L] READ COVER LETTER   [1-5] LETTER FLAGS / REASON   [6] INSUFFICIENT");
            Add(result, "[A] APPROVE   [S] REJECT   [SPACE] CONTINUE   [H] HELP   [X] CLOSE");
            if (_game.HelpVisible)
            {
                Add(result, "");
                Add(result, "HELP: the letter is read once; the stamp locks it for good.");
                Add(result, "Right flags pay trust, wrong flags cost it. No truth is shown.");
            }
            return result;
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

        /// <summary>First `max` wrapped lines of the letter body; pagination stays in LetterView.</summary>
        private static System.Collections.Generic.List<string> WrapBlock(string body, int width, int max)
        {
            var result = new System.Collections.Generic.List<string>();
            string wrapped = Wrap(body, width);
            string[] parts = wrapped.Split('\n');
            for (int i = 0; i < parts.Length && result.Count < max; i++) result.Add(parts[i]);
            return result;
        }
    }
}
