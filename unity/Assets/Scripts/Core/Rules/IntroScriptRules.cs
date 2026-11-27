// HR//0D -- the intro: the slot the content team writes into, and the rules that keep it skippable.
// Assembly: HRZeroDay.Core  (pure C#, no UnityEngine)
//
// DECISION (user, 2026-11-27): "one of the backends will be doing the intro."
//   The five beats of story are BE2's to write (handoff/2026-11-26_agent-to-BE2 ... section B:
//   `content/intro/` -- "Skippable intro story script (5 beats)"). What the game needs from this side is
//   everything AROUND the words: where they come from, what happens if the file is missing, what "skip"
//   means, that it is shown once per profile, and that it can never block the desk.
//
// THE CONTRACT (docs/21-content-pack-format.md gets this table too)
//   content/intro/intro_01.json
//   {
//     "id": "intro_01",
//     "version": 1,
//     "beats": [
//       { "speaker": "DISPATCH", "text": "One line. Upper case. Max 90 characters.", "hold": 2.4 },
//       ...
//     ]
//   }
//   * 1..8 beats. Fewer than five is fine; more than eight is not an intro, it is a cutscene.
//   * `text` <= TextChars (90) per beat, ASCII only -- it is painted with the game's 5x7 atlas, which has
//     no lowercase descenders and no glyphs outside the atlas. A beat that breaks this is DROPPED with a
//     reason, never silently truncated in the middle of a word.
//   * `hold` seconds, 0.5..8. Omitted -> DefaultHold. The whole intro is capped at MaxTotalSeconds.
//   * `speaker` optional, <= 12 chars, same atlas.
//
//   Missing file, empty beats, or every beat dropped => the intro is SKIPPED and the desk starts. The
//   player must never see an error for a missing story file, and the game must never depend on the server
//   (docs/09-backend-spec.md "Fallback").
using System;
using System.Collections.Generic;

namespace HRZeroDay.Core.Rules
{
    /// <summary>One line of the intro: who is talking, what they say, how long it holds.</summary>
    public sealed class IntroBeat
    {
        public string Speaker;
        public string Text;
        public float Hold;
    }

    /// <summary>A parsed intro script, or a reason why there is nothing to show.</summary>
    public sealed class IntroScript
    {
        public string Id = "";
        public int Version;
        public List<IntroBeat> Beats = new List<IntroBeat>();
        /// <summary>Non-null when the script could not be used. Shown in the Console, never to the player.</summary>
        public string Problem;

        public bool HasBeats { get { return Beats != null && Beats.Count > 0; } }
        public float TotalSeconds
        {
            get { float t = 0f; foreach (var b in Beats) t += b.Hold; return t; }
        }

        /// <summary>An intro that cannot be shown: missing, empty, or over the cap after filtering.</summary>
        public static IntroScript None(string why)
        {
            return new IntroScript { Problem = why };
        }
    }

    public static class IntroScriptRules
    {
        public const int TextChars = 90;        // 90 chars at 7 px per column = 630 px; the panel wraps at 56
        public const int SpeakerChars = 12;
        public const int MaxBeats = 8;
        public const int MinBeats = 1;
        public const float MinHold = 0.5f;
        public const float MaxHold = 8f;
        public const float DefaultHold = 2.4f;
        /// <summary>The whole intro, enforced. The brief's budget is "intro <= 25 s" (master brief, booth time).</summary>
        public const float MaxTotalSeconds = 25f;

        /// <summary>
        /// Turns raw beats into a showable script. Never throws, never returns null. Every beat that cannot
        /// be painted with the game's atlas is dropped with a reason, and if nothing survives the caller
        /// gets a script with no beats and a Problem string to log.
        /// </summary>
        public static IntroScript Validate(string id, int version, IList<IntroBeat> raw)
        {
            var script = new IntroScript { Id = id ?? "", Version = version };
            if (raw == null || raw.Count == 0)
            {
                script.Problem = "the intro has no beats";
                return script;
            }

            var reasons = new List<string>();
            foreach (var b in raw)
            {
                if (script.Beats.Count >= MaxBeats)
                {
                    reasons.Add("beat " + (script.Beats.Count + 1) + ": more than " + MaxBeats + " beats -- the rest were dropped");
                    break;
                }
                if (b == null) continue;
                string text = Normalise(b.Text);
                if (text.Length == 0) { reasons.Add("beat " + (raw.IndexOf(b) + 1) + ": empty text"); continue; }
                if (text.Length > TextChars)
                {
                    reasons.Add("beat " + (raw.IndexOf(b) + 1) + ": " + text.Length + " chars, the panel holds " +
                                TextChars + " -- dropped, not cut");
                    continue;
                }
                if (!IsPaintable(text))
                {
                    reasons.Add("beat " + (raw.IndexOf(b) + 1) + ": has characters the 5x7 atlas cannot draw -- dropped");
                    continue;
                }
                string speaker = Normalise(b.Speaker);
                if (speaker.Length > SpeakerChars) speaker = speaker.Substring(0, SpeakerChars);
                if (!IsPaintable(speaker)) speaker = "";

                float hold = b.Hold <= 0f ? DefaultHold : b.Hold;
                if (hold < MinHold) hold = MinHold;
                if (hold > MaxHold) hold = MaxHold;

                script.Beats.Add(new IntroBeat { Speaker = speaker, Text = text, Hold = hold });
            }

            // the total cap: drop from the END, because the intro's first beat is the one that sets the room
            while (script.TotalSeconds > MaxTotalSeconds && script.Beats.Count > MinBeats)
            {
                script.Beats.RemoveAt(script.Beats.Count - 1);
                reasons.Add("the intro ran past " + MaxTotalSeconds + " s -- the last beat was dropped");
            }

            if (script.Beats.Count == 0)
                script.Problem = reasons.Count > 0 ? string.Join("; ", reasons.ToArray()) : "no usable beats";
            else if (reasons.Count > 0)
                script.Problem = "usable, with problems: " + string.Join("; ", reasons.ToArray());
            return script;
        }

        /// <summary>Trimmed, single-spaced, upper case: the atlas has capitals, digits and ASCII punctuation.</summary>
        public static string Normalise(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            var sb = new System.Text.StringBuilder(s.Length);
            bool space = true;
            foreach (var ch in s)
            {
                char c = char.ToUpperInvariant(ch);
                if (c == ' ' || c == '\t' || c == '\n' || c == '\r')
                {
                    if (!space) { sb.Append(' '); space = true; }
                    continue;
                }
                sb.Append(c);
                space = false;
            }
            return sb.ToString().Trim();
        }

        /// <summary>
        /// True when every character is one the 5x7 atlas has a glyph for. The atlas is capitals, digits and
        /// a short punctuation set (art/kit/fonts/monitor_font_5x7.json); anything else would paint as a hole.
        /// </summary>
        public static bool IsPaintable(string s)
        {
            for (int i = 0; i < s.Length; i++)
                if (!IsPaintableChar(s[i])) return false;
            return true;
        }

        private const string Punctuation = " !\"#%&'()*+,-./0123456789:;<=>?_[]~";
        private static bool IsPaintableChar(char c)
        {
            if (c >= 'A' && c <= 'Z') return true;
            return Punctuation.IndexOf(c) >= 0;
        }

        /// <summary>
        /// The line a beat shows, painted with the game's atlas. The speaker is a prefix, not a second panel:
        /// one block of text on a dark card, the same way the walk's caption works.
        /// </summary>
        public static string LineFor(IntroBeat beat)
        {
            if (beat == null) return "";
            return string.IsNullOrEmpty(beat.Speaker) ? beat.Text : beat.Speaker + ": " + beat.Text;
        }

        /// <summary>Wrap for the intro card: `cols` characters, same greedy rule as every other text panel.</summary>
        public static List<string> Wrap(string text, int cols)
        {
            var outp = new List<string>();
            if (string.IsNullOrEmpty(text)) return outp;
            if (cols < 2) cols = 2;
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
    }
}
