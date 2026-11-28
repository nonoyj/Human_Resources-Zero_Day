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
        // ---- WHAT THIS CLASS IS NOW (v126)
        // It used to draw the play HUD as sixteen lines of TextMesh prose in the corner, including the key
        // legend "[L] LETTER  [1-5] FLAGS  [A] APPROVE  [S] REJECT  [TAB] ALERTS". The player's report was
        // exactly right: "It should not be displayed as text but rather as clickable objects."
        //
        // So the prose is gone. The objects live in Core/Rules/HudRules (geometry, states, hit tests, all
        // tested headlessly) and are painted by GameHudObjects (one 480x270 layer, palette-gated glyphs).
        // What is left here are the two things that DO have to be text, because they are prose and not
        // affordances:
        //
        //   * the AFTER-HOURS WALK's receipt line  -- it is a sentence the shift writes about the player;
        //   * the ENDING's verdict                -- likewise, and it is the last thing the game says.
        //
        // ...plus the binding/refresh plumbing DeskBootstrap already calls. Nothing else.
        private DeskBootstrap _game;
        private TextMesh _text;
        private bool _bound;
        private SpriteRenderer _panel;

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
            var panelGo = new GameObject("HudPanel");
            panelGo.transform.SetParent(transform, false);
            _panel = panelGo.AddComponent<SpriteRenderer>();
            var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            tex.SetPixels(new[] { new Color(1f, 1f, 1f, 1f) });
            tex.Apply(false, true);
            _panel.sprite = Sprite.Create(tex, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
            _panel.color = new Color(0.02f, 0.03f, 0.05f, 0.78f);
            _panel.sortingOrder = 219;
            panelGo.SetActive(false);

            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var go = new GameObject("Text");
            go.transform.SetParent(transform, false);
            _text = go.AddComponent<TextMesh>();
            _text.font = font;
            _text.characterSize = 0.19f;
            _text.anchor = TextAnchor.UpperLeft;
            _text.alignment = TextAlignment.Left;
            DeskRuntimeScene.SetTextMeshSorting(_text, 220);
            _text.text = string.Empty;
        }

        /// <summary>
        /// Forces the very next frame to show the current state. The object layer repaints itself from the
        /// live run every frame, so this only matters for the prose lines below -- but it stays because
        /// DeskBootstrap calls it at every screen change and a no-op is cheaper than an audit.
        /// </summary>
        public void RefreshNow()
        {
            if (!_bound || _game == null || _game.Session == null) return;
            Push(ProseLines());
        }

        private void Update()
        {
            if (!_bound || _game == null || _game.Session == null) return;
            Push(ProseLines());
        }

        /// <summary>
        /// The ONLY lines the HUD still prints, and both are prose about the run: the walk's receipt (shown
        /// on the day summary) and the ending's verdict. Everything else on this screen is an object now.
        /// </summary>
        private System.Collections.Generic.List<string> ProseLines()
        {
            var result = new System.Collections.Generic.List<string>();
            if (_game == null || _game.Session == null) return result;
            if (_game.ShowingMenu || _game.ShowingIntro || _game.ShowingSweep) return result;

            if (_game.ShowDaySummary && !string.IsNullOrEmpty(_game.SweepLine))
            {
                var d = _game.LastDay;
                if (d != null) result.Add("DAY " + d.Day + " CLOSED   " + d.Approved + " IN   " + d.Rejected + " OUT");
                result.Add(_game.SweepLine);
                return result;
            }

            if (_game.ShowEnding)
            {
                var ledger = _game.Ledger;
                if (ledger != null && ledger.Count > 0)
                {
                    result.Add(OutcomeCopy(_game.Session.Shift.Outcome));
                    result.Add("DAYS " + ledger.Count + "/5   QUOTAS MET " + ledger.QuotasMet + "/" + ledger.Count);
                    result.Add("CALLS " + ledger.Correct + "/" + ledger.Decisions + "   SYNTHETICS IN " + ledger.Hired);
                    foreach (var line in Wrap(ledger.Verdict(), 58).Split('\n')) result.Add(line);
                }
            }
            return result;
        }

        private void Push(System.Collections.Generic.List<string> lines)
        {
            if (_text == null) return;
            var text = new System.Text.StringBuilder();
            int maxLen = 0;
            foreach (var line in lines)
            {
                var l = line ?? string.Empty;
                text.Append(l).Append('\n');
                if (l.Length > maxLen) maxLen = l.Length;
            }
            _text.text = text.ToString();
            // prose sits in the middle band, where the objects deliberately do not go
            _text.transform.localPosition = new Vector3(0.5f, 8.6f, -2f);

            if (_panel != null)
            {
                bool show = maxLen > 0;
                _panel.gameObject.SetActive(show);
                if (!show) return;
                float lineHeight = _text.characterSize * 1.7f;
                float h = 0.3f + lines.Count * lineHeight;
                float w = 0.5f + maxLen * _text.characterSize * 0.55f;
                _panel.transform.localPosition = new Vector3(0.5f + w * 0.5f, 8.6f - h * 0.5f, -2.1f);
                _panel.transform.localScale = new Vector3(w, h, 1f);
            }
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
