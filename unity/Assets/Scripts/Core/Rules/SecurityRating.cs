// HR//0D -- SecurityRating: the company's one number, and the alerts that explain it.
// Assembly: HRZeroDay.Core  (pure C#, no UnityEngine)
//
// DECIDED by the player 2026-11-27 (docs/88 Q1-Q4, ADR-012):
//   * 0..100, starting at 70, labelled STABLE (70+) / SHAKY (40-69) / BREACHED (below 40).
//   * Made of THREE inputs, weighted 60 / 30 / 10: trust, corruption, synthetics hired this run.
//     The scale is ANCHORED: a healthy company (full trust, corruption 0, no synthetics) reads exactly 70,
//     and the three weighted parts are stretched from there to 0..100. Without the anchor, "start at 70"
//     and "60/30/10" contradict each other -- a fresh run computes 100 -- and the HUD would have opened on a
//     perfect score with nowhere to go but down.
//   * It can go UP as well as down -- "a number that only falls stops being a score and becomes a countdown".
//   * Visible during a shift: the HUD shows the number, and ALERTS opens a popup that explains it.
//
// WHAT THIS FILE IS NOT: it is not the dashboard. The panel that draws "60 % trust, 30 % corruption, 10 %
// hires" is a view (v111). This file is the arithmetic behind it, plus the alert vocabulary, so that every
// number on that panel is DERIVED and none of it can be quietly hardcoded in a MonoBehaviour.
using System;

namespace HRZeroDay.Core.Rules
{
    /// <summary>The three words the player ever sees about their own company.</summary>
    public enum SecurityBand
    {
        Breached,
        Shaky,
        Stable
    }

    /// <summary>Every reason the HUD can raise an alert. Q3: four kinds, and only these four.</summary>
    public enum AlertKind
    {
        /// <summary>A synthetic was stamped through. The one that actually costs you.</summary>
        SyntheticHired,
        /// <summary>Two mismatches on one applicant. A warning, never proof (Q5/Q10).</summary>
        DoubleMismatch,
        /// <summary>A verification contradicted the application -- a fact, not an inference.</summary>
        VerifiedLie,
        /// <summary>The building got worse: corruption rose a level.</summary>
        CorruptionRose
    }

    /// <summary>One alert: what happened, how much it cost the rating, and how loudly to say it.</summary>
    public sealed class SecurityAlert
    {
        public AlertKind Kind;
        public int Day;
        /// <summary>
        /// Short, plain, no jargon. Shown on the HUD line and as the popup's title. UPPER CASE because it is
        /// painted with the game's 5x7 atlas -- the first draft said "A synthetic was hired." in sentence case
        /// and the atlas gate refused it, which is the correct outcome: there is no lowercase in that font.
        /// </summary>
        public string Line;
        /// <summary>Points this event took off the rating (negative) or gave back (positive).</summary>
        public int Delta;
        /// <summary>1 = a note, 2 = a warning, 3 = it happened.</summary>
        public int Severity;
        /// <summary>Optional: the applicant this concerns, so the dashboard can name them.</summary>
        public string ApplicantId;
    }

    public static class SecurityRules
    {
        // ---- the scale (Q1) -----------------------------------------------------------------------
        public const int Min = 0;
        public const int Max = 100;
        public const int Start = 70;

        /// <summary>Q1 thresholds: 70+ STABLE, 40-69 SHAKY, below 40 BREACHED.</summary>
        public const int StableAt = 70;
        public const int ShakyAt = 40;

        // ---- the weights (Q1): 60 / 30 / 10, and they must sum to 1 --------------------------------
        public const float TrustWeight = 0.6f;
        public const float CorruptionWeight = 0.3f;
        public const float HiresWeight = 0.1f;

        /// <summary>Each synthetic hired through the desk takes this much off the hires component (0..100).</summary>
        public const int HiresPenaltyEach = 20;

        // ---- what a correct or wrong call is worth to the narrative (Q2: it can go up) -------------
        public const int DeltaCorrectHuman = 1;
        public const int DeltaCorrectSynthetic = 2;
        public const int DeltaWrongfulReject = -3;

        public static int Clamp(int value)
        {
            return value < Min ? Min : (value > Max ? Max : value);
        }

        public static SecurityBand BandOf(int rating)
        {
            if (rating >= StableAt) return SecurityBand.Stable;
            if (rating >= ShakyAt) return SecurityBand.Shaky;
            return SecurityBand.Breached;
        }

        /// <summary>The word shown next to the number. Upper case: it is painted with the game's atlas.</summary>
        public static string BandWord(int rating)
        {
            switch (BandOf(rating))
            {
                case SecurityBand.Stable: return "STABLE";
                case SecurityBand.Shaky: return "SHAKY";
                default: return "BREACHED";
            }
        }

        /// <summary>
        /// The rating from the state of the run (Q1).
        ///
        /// Each input becomes a 0..100 component (trust as-is, corruption inverted so level 3 is zero, hires
        /// penalised per synthetic until they bottom out), the components are weighted 60/30/10 into a 0..100
        /// total, and that total is ANCHORED so a healthy company reads exactly Start (70) and a catastrophic
        /// one reads 0 -- a straight line from (100, 70) to (0, 0). Anything better than healthy is capped.
        /// </summary>
        public static int Compute(int trust, int corruptionLevel, int syntheticsHired)
        {
            Raw total = RawTotal(trust, corruptionLevel, syntheticsHired);
            if (total.Value >= 100f) return Start;              // healthy or better: the anchor
            return Clamp((int)Math.Round(Start * (total.Value / 100f)));
        }

        /// <summary>The unanchored 0..100 total, kept for the dashboard's own arithmetic.</summary>
        private struct Raw { public float Value; }

        private static Raw RawTotal(int trust, int corruptionLevel, int syntheticsHired)
        {
            int t = Clamp(trust);
            int c = corruptionLevel < 0 ? 0 : (corruptionLevel > 3 ? 3 : corruptionLevel);
            int s = syntheticsHired < 0 ? 0 : syntheticsHired;

            float trustPart = t * TrustWeight;
            float corruptionPart = (100f - (c / 3f) * 100f) * CorruptionWeight;
            int hiresScore = 100 - Math.Min(s * HiresPenaltyEach, 100);
            float hiresPart = hiresScore * HiresWeight;
            return new Raw { Value = trustPart + corruptionPart + hiresPart };
        }

        /// <summary>The rating for a run that has not started: exactly Start, not "roughly 70".</summary>
        public static int Fresh()
        {
            return Compute(100, 0, 0);
        }

        /// <summary>
        /// How much each of the three inputs contributes, for the dashboard (v111). Scaled and anchored exactly
        /// like Compute(), so the three numbers on the popup always add up to the number on the HUD -- a
        /// breakdown that does not add up is worse than no breakdown.
        /// </summary>
        public static void Breakdown(int trust, int corruptionLevel, int syntheticsHired,
                                     out int trustPoints, out int corruptionPoints, out int hiresPoints)
        {
            int t = Clamp(trust);
            int c = corruptionLevel < 0 ? 0 : (corruptionLevel > 3 ? 3 : corruptionLevel);
            int s = syntheticsHired < 0 ? 0 : syntheticsHired;

            float rawTrust = t * TrustWeight;
            float rawCorruption = (100f - (c / 3f) * 100f) * CorruptionWeight;
            float rawHires = (100 - Math.Min(s * HiresPenaltyEach, 100)) * HiresWeight;
            float raw = rawTrust + rawCorruption + rawHires;

            // anchored scale: raw 100 -> Start, raw 0 -> 0. Above healthy, everything is Start.
            float scale = raw >= 100f ? 0f : (Start / 100f);
            trustPoints = raw >= 100f ? 0 : (int)Math.Round(rawTrust * scale);
            corruptionPoints = raw >= 100f ? 0 : (int)Math.Round(rawCorruption * scale);
            hiresPoints = raw >= 100f ? 0 : (int)Math.Round(rawHires * scale);
            if (raw >= 100f) trustPoints = Start;               // a perfect company is all trust
        }

        /// <summary>A one-line explanation for the HUD, in the game's voice. Never says "score".</summary>
        public static string HudLine(int rating)
        {
            return "SECURITY " + rating + " " + BandWord(rating);
        }

        /// <summary>
        /// THE SHAPE, BEFORE THE NUMBER (v120, docs/89 §7 rule 3). Five cells, filed from the rating, then the
        /// digits, then the word: `SECURITY ▮▮▮▯▯ 62 SHAKY`. A player reads a short bar long before they read
        /// "62", and the five cells are the same 0..100 scale the digits are, so the two can never disagree --
        /// `SecurityBar` is the only place the division happens, and LightingTests walks it at every boundary.
        ///
        /// The cells are `▮` (U+25AE) and `▯` (U+25AF): both are in the legacy font Unity ships, they are the
        /// same width, and they are NOT colour tells -- the blind-mode rule says a state may never be carried
        /// by colour alone, and a count of filled cells survives being printed in one ink.
        /// </summary>
        public static int BarCells(int rating)
        {
            int cells = (Clamp(rating) * BarCellCount + 50) / 100;   // rounded, integer only
            return cells < 0 ? 0 : (cells > BarCellCount ? BarCellCount : cells);
        }

        public const int BarCellCount = 5;
        public const char BarFull = '\u25AE';
        public const char BarEmpty = '\u25AF';

        /// <summary>The corner line: shape, then number, then the word.</summary>
        public static string CornerLine(int rating)
        {
            var bar = new System.Text.StringBuilder(BarCellCount);
            int cells = BarCells(rating);
            for (int i = 0; i < BarCellCount; i++) bar.Append(i < cells ? BarFull : BarEmpty);
            return "SECURITY " + bar + " " + Clamp(rating) + " " + BandWord(rating);
        }

        // ---- alerts (Q3) --------------------------------------------------------------------------
        public static SecurityAlert Alert(AlertKind kind, int day, string applicantId)
        {
            var a = new SecurityAlert { Kind = kind, Day = day, ApplicantId = applicantId };
            switch (kind)
            {
                case AlertKind.SyntheticHired:
                    a.Line = "A SYNTHETIC WAS HIRED.";
                    a.Delta = -8; a.Severity = 3;
                    break;
                case AlertKind.DoubleMismatch:
                    // severity 1 on purpose: a mismatch is TRUE but the inference may be wrong (Q10), so the
                    // alert must be quieter than a verified fact -- the test enforces "fact above inference".
                    a.Line = "TWO MISMATCHES ON ONE FILE.";
                    a.Delta = -3; a.Severity = 1;
                    break;
                case AlertKind.VerifiedLie:
                    a.Line = "THE PAPERWORK CONTRADICTED THE PERSON.";
                    a.Delta = -5; a.Severity = 2;
                    break;
                case AlertKind.CorruptionRose:
                    a.Line = "THE BUILDING CHANGED OVERNIGHT.";
                    a.Delta = -6; a.Severity = 3;
                    break;
            }
            return a;
        }

        /// <summary>What a finished decision is worth to the rating story (Q2: it can go up).</summary>
        public static int DeltaForDecision(bool applicantWasSynthetic, bool playerCorrect)
        {
            // Not correct = you got the person wrong. If it WAS a synthetic, you hired it (TrustRules already
            // docks -12 for that, so this only has to point the right way). If it was a human, you rejected
            // them -- the costliest mistake in the game.
            if (!playerCorrect) return applicantWasSynthetic ? -1 : DeltaWrongfulReject;
            return applicantWasSynthetic ? DeltaCorrectSynthetic : DeltaCorrectHuman;
        }
    }
}
