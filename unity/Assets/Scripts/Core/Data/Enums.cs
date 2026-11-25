// HR//0D -- Core data enums.
// Assembly: HRZeroDay.Core  (pure C#: MUST NOT reference UnityEngine)
// Owner: Dev A. See docs/07-randomization-spec.md for the rules these back.
using System;

namespace HRZeroDay.Core.Data
{
    /// <summary>The five evidence categories the player can pick as a rejection reason.</summary>
    public enum EvidenceCategory
    {
        SyntheticMedia,      // portrait / voice artifacts
        DocInconsistency,    // arithmetic or record contradictions
        Impersonation,       // identity clone, unreachable referee
        Coercion,            // a real human used as bait
        SocialEngineering,   // urgency, authority, fast-track
        Insufficient         // "not enough evidence" -- allowed, costs trust
    }

    /// <summary>A place in the UI that can reveal evidence. Day-gated (invariant I1b).</summary>
    public enum RevealSurface
    {
        ApplicantStage,   // the person across the table
        CoverLetter,
        ResumeRow,
        IdTool,
        ReferenceTool,
        MetadataTool,
        Inbox
    }

    /// <summary>What the player actually did.</summary>
    public enum Decision { None = 0, Approve = 1, Reject = 2, Fire = 3 }

    /// <summary>Hidden ground truth. Never shown to the player during play.</summary>
    public enum GroundTruth { Human = 0, HumanBait = 1, Synthetic = 2 }

    /// <summary>How a player's choice is scored after the fact.</summary>
    public enum DecisionQuality
    {
        CorrectRightReason,   // right call, right evidence
        CorrectWrongReason,   // right call, wrong evidence -- still a teaching moment
        WrongCall             // misclassified
    }

    public enum RunOutcome
    {
        InProgress, Survived, DefeatedPerfectEmployee, DefeatedGhostQuota,
        DefeatedBrokenLink, DefeatedOpenDoor, DefeatedGoldenHandcuffs
    }

    [Serializable]
    public class Violation
    {
        public string invariant;   // "I1".."I8"
        public string detail;
        public string applicantId;

        public Violation() { }
        public Violation(string invariant, string detail, string applicantId = null)
        {
            this.invariant = invariant;
            this.detail = detail;
            this.applicantId = applicantId;
        }

        public override string ToString()
        {
            return applicantId == null
                ? invariant + ": " + detail
                : invariant + " [" + applicantId + "]: " + detail;
        }
    }
}
