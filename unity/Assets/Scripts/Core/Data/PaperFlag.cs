// HR//0D -- PaperFlag: one thing a player can say is wrong with a letter.
// Assembly: HRZeroDay.Core  (pure C#, no UnityEngine)  -- moved here from Game/Desk/LetterView.cs on
// 2026-11-26: the flag set is a RULE, not a view. Scoring, the debrief and the tests all need it, and
// none of them may reference UnityEngine.
using System;
using System.Collections.Generic;

namespace HRZeroDay.Core.Data
{
    [Serializable]
    public sealed class FlagOption
    {
        public string id;             // stable id, safe to store in a save/analytics event
        public string label;          // what the player sees on the button
        public bool isReal;           // hidden ground truth: flagging this is CORRECT
        public int trustDelta;        // trust paid when right, taken when wrong
    }

    /// <summary>
    /// The shipping flag set. Every flag is a question about the LETTER, phrased so the player has to
    /// commit before the answer is revealed (at the debrief, never during the shift).
    /// </summary>
    public static class LetterFlags
    {
        public const string FlagMoneyDemand = "paper_money_demand";
        public const string FlagUrgency = "paper_urgency";
        public const string FlagMassTemplate = "paper_mass_template";
        public const string FlagWrongReference = "paper_wrong_reference";
        public const string FlagMissingSignature = "paper_missing_signature";

        public static List<FlagOption> Build(ApplicantRecord a, string letterBody)
        {
            var opts = new List<FlagOption>();
            var body = letterBody ?? string.Empty;

            opts.Add(new FlagOption
            {
                id = FlagMoneyDemand, label = "ASKING FOR MONEY / FEES",
                isReal = Has(body, "fee") || Has(body, "deposit") || Has(body, "processing cost"),
                trustDelta = 6
            });
            opts.Add(new FlagOption
            {
                id = FlagUrgency, label = "PRESSURE / DEADLINE THREAT",
                isReal = Has(body, "immediately") || Has(body, "today") || Has(body, "urgent"),
                trustDelta = 4
            });
            opts.Add(new FlagOption
            {
                id = FlagMassTemplate, label = "TEMPLATE / SOMEONE ELSE'S LETTER",
                isReal = Has(body, "[name]") || Has(body, "dear sir/madam"),
                trustDelta = 5
            });
            opts.Add(new FlagOption
            {
                id = FlagWrongReference, label = "REFERENCE DOES NOT MATCH THE RECORD",
                isReal = a != null && string.IsNullOrEmpty(a.referral),
                trustDelta = 5
            });
            opts.Add(new FlagOption
            {
                id = FlagMissingSignature, label = "NO SIGNATURE / NO CONTACT DETAILS",
                isReal = !Has(body, "regards"),
                trustDelta = 3
            });
            return opts;
        }

        private static bool Has(string body, string needle)
            => body.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0;
    }
}
