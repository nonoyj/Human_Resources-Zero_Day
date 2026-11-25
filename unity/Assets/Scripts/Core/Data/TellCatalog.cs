// HR//0D -- The tell catalogue: every piece of evidence a synthetic can leave behind.
// Assembly: HRZeroDay.Core  (pure C#, no UnityEngine)
// Curated content: copy is owned by Dev C; ids are frozen (used in analytics + save files).
using System.Collections.Generic;

namespace HRZeroDay.Core.Data
{
    /// <summary>One observable tell. <see cref="Surface"/> is where the player can find it.</summary>
    public sealed class Tell
    {
        public readonly string Id;
        public readonly EvidenceCategory Category;
        public readonly RevealSurface Surface;
        /// <summary>Plain-language description shown in the Field Manual and the debrief.</summary>
        public readonly string Wording;
        public readonly int Severity;      // 1..3
        public readonly bool IsFactual;    // true if it is provable from record values (arithmetic / checksum)

        public Tell(string id, EvidenceCategory category, RevealSurface surface,
                    string wording, int severity, bool isFactual)
        {
            Id = id; Category = category; Surface = surface;
            Wording = wording; Severity = severity; IsFactual = isFactual;
        }
    }

    public static class TellCatalog
    {
        public const string PortraitAsymmetry = "portrait_asymmetry";
        public const string PortraitBlendSeam = "portrait_blend_seam";
        public const string VoiceFlatMetrics = "voice_flat_metrics";
        public const string ExpVsGrad        = "exp_vs_grad";
        public const string EmployerFounded  = "employer_founded";
        public const string GradTooYoung     = "grad_too_young";
        public const string IdChecksum       = "id_checksum";
        public const string RefUnreachable   = "ref_unreachable";
        public const string IdentityClone    = "identity_clone";
        public const string BaitScript       = "bait_script";
        public const string BaitPressure     = "bait_pressure";
        public const string GoldenFastTrack  = "golden_fast_track";

        private static readonly Dictionary<string, Tell> Map = new Dictionary<string, Tell>
        {
            { PortraitAsymmetry, new Tell(PortraitAsymmetry, EvidenceCategory.SyntheticMedia, RevealSurface.ApplicantStage,
                "A mole sits on the left cheek in the photo and the right cheek in real life.", 2, false) },
            { PortraitBlendSeam, new Tell(PortraitBlendSeam, EvidenceCategory.SyntheticMedia, RevealSurface.ApplicantStage,
                "The hair edge cuts a dead-straight line into the background.", 2, false) },
            { VoiceFlatMetrics, new Tell(VoiceFlatMetrics, EvidenceCategory.SyntheticMedia, RevealSurface.CoverLetter,
                "Every sentence is the same length and there are no contractions.", 1, false) },
            { ExpVsGrad, new Tell(ExpVsGrad, EvidenceCategory.DocInconsistency, RevealSurface.ResumeRow,
                "The claimed years of experience are more than the years since graduation.", 3, true) },
            { EmployerFounded, new Tell(EmployerFounded, EvidenceCategory.DocInconsistency, RevealSurface.ResumeRow,
                "The applicant started at a company before that company existed.", 3, true) },
            { GradTooYoung, new Tell(GradTooYoung, EvidenceCategory.DocInconsistency, RevealSurface.ResumeRow,
                "The applicant graduated at an age that is not possible.", 3, true) },
            { IdChecksum, new Tell(IdChecksum, EvidenceCategory.DocInconsistency, RevealSurface.IdTool,
                "The ID number fails its checksum digit.", 3, true) },
            { RefUnreachable, new Tell(RefUnreachable, EvidenceCategory.Impersonation, RevealSurface.ReferenceTool,
                "The referee says they have never heard of this applicant.", 3, false) },
            { IdentityClone, new Tell(IdentityClone, EvidenceCategory.Impersonation, RevealSurface.IdTool,
                "The ID number is registered to a different name.", 3, false) },
            { BaitScript, new Tell(BaitScript, EvidenceCategory.Coercion, RevealSurface.CoverLetter,
                "The letter reads like dictated instructions rather than someone writing about themselves.", 2, false) },
            { BaitPressure, new Tell(BaitPressure, EvidenceCategory.Coercion, RevealSurface.CoverLetter,
                "The letter pleads for an answer by a deadline that is not the company's.", 2, false) },
            { GoldenFastTrack, new Tell(GoldenFastTrack, EvidenceCategory.SocialEngineering, RevealSurface.ResumeRow,
                "A director fast-track referral with no requisition number.", 2, false) }
        };

        public static Tell Get(string id) { return Map[id]; }
        public static bool Has(string id) { return Map.ContainsKey(id); }
        public static IEnumerable<Tell> All { get { return Map.Values; } }

        /// <summary>Categories that force a rejection: the applicant cannot be a real applicant.</summary>
        public static bool IsHardTell(string id)
        {
            Tell t;
            return Map.TryGetValue(id, out t) && t.Severity >= 2 && t.Category != EvidenceCategory.Coercion;
        }

        /// <summary>Surfaces the player can open on a given day (invariant I1b).</summary>
        public static readonly Dictionary<int, HashSet<RevealSurface>> SurfaceUnlock =
            new Dictionary<int, HashSet<RevealSurface>>
        {
            { 1, new HashSet<RevealSurface> { RevealSurface.ApplicantStage, RevealSurface.CoverLetter } },
            { 2, new HashSet<RevealSurface> { RevealSurface.ApplicantStage, RevealSurface.CoverLetter,
                                             RevealSurface.ResumeRow, RevealSurface.IdTool } },
            { 3, new HashSet<RevealSurface> { RevealSurface.ApplicantStage, RevealSurface.CoverLetter,
                                             RevealSurface.ResumeRow, RevealSurface.IdTool } },
            { 4, new HashSet<RevealSurface> { RevealSurface.ApplicantStage, RevealSurface.CoverLetter,
                                             RevealSurface.ResumeRow, RevealSurface.IdTool, RevealSurface.ReferenceTool } },
            { 5, new HashSet<RevealSurface> { RevealSurface.ApplicantStage, RevealSurface.CoverLetter,
                                             RevealSurface.ResumeRow, RevealSurface.IdTool, RevealSurface.ReferenceTool } }
        };

        public static bool IsUnlocked(int day, RevealSurface surface)
        {
            HashSet<RevealSurface> set;
            return SurfaceUnlock.TryGetValue(day, out set) && set.Contains(surface);
        }
    }
}
