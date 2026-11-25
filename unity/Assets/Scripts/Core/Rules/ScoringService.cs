// HR//0D -- ScoringService: judges one decision against the hidden truth.
// Assembly: HRZeroDay.Core  (pure C#, no UnityEngine) -> unit-testable, no scene needed.
// Design: "right call for the wrong reason" still counts as correct, but is flagged so the
// debrief can teach. See docs/05-design-suggestions.md (reason dropdown) + docs/10-analytics-spec.md.
using System.Collections.Generic;
using HRZeroDay.Core.Data;

namespace HRZeroDay.Core.Rules
{
    public sealed class DecisionInput
    {
        public ApplicantRecord Applicant;
        public Decision Decision = Decision.None;
        /// <summary>Null when the player approved.</summary>
        public EvidenceCategory? Reason;
        /// <summary>Tells the player actually opened the surface for (not merely present).</summary>
        public List<string> TellsInspected = new List<string>();
        public List<RevealSurface> SurfacesOpened = new List<RevealSurface>();
        public List<string> ToolsUsed = new List<string>();
        public int SecondsOnCard;
        public int StampsUndone;
    }

    public sealed class DecisionResult
    {
        public bool IsCorrect;
        public DecisionQuality Quality;
        /// <summary>Tells that were present but the player never looked at.</summary>
        public List<string> MissedEvidence = new List<string>();
        /// <summary>The single most important thing the debrief should say.</summary>
        public string DebriefLine;
        public int TrustDelta;
        public int WrongfulRejection;   // +1 when a human was rejected
        public int SyntheticHired;      // +1 when a synthetic was approved
    }

    public static class ScoringService
    {
        /// <summary>Trust economy: tuned so one bad day does not end a run, three do.</summary>
        public const int TrustCorrectHuman = 2;      // approving an honest human
        public const int TrustCorrectReject = 4;     // rejecting a synthetic
        public const int TrustWrongfulReject = -8;   // rejecting a human
        public const int TrustSyntheticHired = -12;  // approving a synthetic
        public const int TrustInsufficientReason = -2;

        public static DecisionResult Evaluate(DecisionInput input)
        {
            var a = input.Applicant;
            var r = new DecisionResult();
            bool playerHired = input.Decision == Decision.Approve;
            bool playerRejected = input.Decision == Decision.Reject || input.Decision == Decision.Fire;

            // What the evidence actually supports.
            bool shouldHire = a.Truth == GroundTruth.Human && a.tellsPresent.Count == 0;
            bool shouldReject = a.Truth == GroundTruth.Synthetic || a.Truth == GroundTruth.HumanBait;

            // Missed evidence: present, but never inspected.
            foreach (var t in a.tellsPresent)
                if (!input.TellsInspected.Contains(t)) r.MissedEvidence.Add(t);

            if (playerHired)
            {
                r.IsCorrect = shouldHire;
                r.Quality = r.IsCorrect ? DecisionQuality.CorrectRightReason : DecisionQuality.WrongCall;
                r.TrustDelta = r.IsCorrect ? TrustCorrectHuman : TrustSyntheticHired;
                if (!r.IsCorrect)
                {
                    r.SyntheticHired = 1;
                    r.DebriefLine = "You approved a record that could not be verified. Evidence you never opened: " +
                                    Describe(r.MissedEvidence);
                }
                else
                {
                    r.DebriefLine = "Approved an honest applicant. No tell was present.";
                }
                return r;
            }

            if (playerRejected)
            {
                r.IsCorrect = shouldReject;
                if (!r.IsCorrect)
                {
                    r.Quality = DecisionQuality.WrongCall;
                    r.TrustDelta = TrustWrongfulReject;
                    r.WrongfulRejection = 1;
                    r.DebriefLine = "You rejected a real person. Nothing in this record was contradictory" +
                                    (a.distractorsPresent.Count > 0
                                        ? "; the flags you saw were explained in the cover letter."
                                        : ".");
                    return r;
                }

                // Right call. Was the stated reason actually supported?
                bool reasonSupported = input.Reason.HasValue && IsReasonSupported(a, input.Reason.Value);
                r.Quality = reasonSupported ? DecisionQuality.CorrectRightReason
                                            : DecisionQuality.CorrectWrongReason;
                r.TrustDelta = TrustCorrectReject;
                if (input.Reason.HasValue && input.Reason.Value == EvidenceCategory.Insufficient)
                {
                    r.TrustDelta += TrustInsufficientReason;
                    r.DebriefLine = "Rejected correctly, but 'insufficient evidence' is not a reason. " +
                                    "The evidence was: " + Describe(a.tellsPresent);
                }
                else if (!reasonSupported)
                {
                    r.DebriefLine = "Right decision, wrong reason. The evidence was: " + Describe(a.tellsPresent);
                }
                else
                {
                    r.DebriefLine = "Correct. You caught: " + Describe(a.tellsPresent);
                }
                return r;
            }

            // No decision was made (should not happen in play).
            r.IsCorrect = false;
            r.Quality = DecisionQuality.WrongCall;
            r.DebriefLine = "No decision recorded.";
            return r;
        }

        /// <summary>True when at least one tell of the claimed category is actually present.</summary>
        public static bool IsReasonSupported(ApplicantRecord a, EvidenceCategory reason)
        {
            foreach (var t in a.tellsPresent)
            {
                if (!TellCatalog.Has(t)) continue;
                if (TellCatalog.Get(t).Category == reason) return true;
            }
            return false;
        }

        public static string Describe(List<string> tellIds)
        {
            if (tellIds == null || tellIds.Count == 0) return "nothing";
            var parts = new List<string>();
            foreach (var t in tellIds)
                parts.Add(TellCatalog.Has(t) ? TellCatalog.Get(t).Wording : t);
            return string.Join(" / ", parts.ToArray());
        }
    }
}
