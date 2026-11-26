// HR//0D -- maps an on-screen artifact or catalog tell to the way it is shown to the player.
// Pure C#: no UnityEngine references, so the same rules run in the headless tests.
// Owner: Agent (interaction layer). The tell ids come from HRZeroDay.Core.Data.TellCatalog;
// the artifact ids come from unity/Assets/Content/desk_layout.json.
//
// Locked presentation decision (2026-11-26):
//   body tells (hands, badge, shadow, ear, seam) -> magnifier ring (2x region zoom)
//   face tells (glasses_merge, skin_band, no_blink, mirror) -> callout text
// Behaviour is never sufficient on its own: art-only artifacts without a catalog tell id
// produce an observation, never a proven piece of evidence.
using System;
using HRZeroDay.Core.Data;

namespace HRZeroDay.Game.Interaction
{
    public enum RevealStyle
    {
        MagnifierRing,
        Callout,
        DocumentRow,
        ToolResult
    }

    public enum ToolKind
    {
        IdRegistry,
        MetadataScan,
        ReferenceCall
    }

    /// <summary>One resolvable place on the desk.</summary>
    public sealed class RevealTarget
    {
        public readonly string Artifact;      // hotspot id / document row / tool id
        public readonly string TellId;        // null = art-only observation, not evidence
        public readonly RevealStyle Style;
        public readonly RevealSurface Surface;

        public RevealTarget(string artifact, string tellId, RevealStyle style, RevealSurface surface)
        {
            Artifact = artifact;
            TellId = tellId;
            Style = style;
            Surface = surface;
        }
    }

    public static class EvidenceMap
    {
        public const string NoBlinkArtifact = "no_blink";
        public const float NoBlinkObserveSeconds = 6f;

        /// <summary>Art-only or catalog-backed artifact -> how the desk should reveal it.</summary>
        public static RevealTarget ForArtifact(string artifact)
        {
            if (string.IsNullOrEmpty(artifact)) return null;
            switch (artifact)
            {
                // Magnifier: shoulders, hands, badge, cast shadow, hair silhouette.
                case "seam":          return new RevealTarget(artifact, TellCatalog.PortraitBlendSeam,
                                          RevealStyle.MagnifierRing, RevealSurface.ApplicantStage);
                case "ear":           return Magnifier(artifact);
                case "hands":         return Magnifier(artifact);
                case "straight_badge": return Magnifier(artifact);
                case "shadow":        return Magnifier(artifact);

                // Callout: the face band is text-anchored so it never relies on colour alone (I8).
                case "mirror":        return new RevealTarget(artifact, TellCatalog.PortraitAsymmetry,
                                          RevealStyle.Callout, RevealSurface.ApplicantStage);
                case "glasses_merge": return Callout(artifact);
                case "skin_band":     return Callout(artifact);
                case NoBlinkArtifact: return Callout(artifact);

                default:              return null;   // unknown hotspot: never invent evidence
            }
        }

        /// <summary>Catalog tell -> the exact surface that can prove it (I1b / I8).</summary>
        public static RevealTarget ForTell(string tellId)
        {
            if (TellCatalog.Has(tellId) == false) return null;
            var surface = TellCatalog.Get(tellId).Surface;
            switch (tellId)
            {
                case TellCatalog.PortraitAsymmetry: return new RevealTarget("mirror", tellId,
                                                        RevealStyle.Callout, surface);
                case TellCatalog.PortraitBlendSeam: return new RevealTarget("seam", tellId,
                                                        RevealStyle.MagnifierRing, surface);
                case TellCatalog.VoiceFlatMetrics:
                case TellCatalog.BaitScript:
                case TellCatalog.BaitPressure:
                    return new RevealTarget("cover_letter", tellId, RevealStyle.Callout, surface);

                case TellCatalog.ExpVsGrad:
                    return new RevealTarget("resume_experience", tellId, RevealStyle.DocumentRow, surface);
                case TellCatalog.EmployerFounded:
                    return new RevealTarget("resume_employment", tellId, RevealStyle.DocumentRow, surface);
                case TellCatalog.GradTooYoung:
                    return new RevealTarget("resume_education", tellId, RevealStyle.DocumentRow, surface);
                case TellCatalog.GoldenFastTrack:
                    return new RevealTarget("resume_referral", tellId, RevealStyle.DocumentRow, surface);

                case TellCatalog.IdChecksum:
                case TellCatalog.IdentityClone:
                    return new RevealTarget("id_tool", tellId, RevealStyle.ToolResult, surface);
                case TellCatalog.RefUnreachable:
                    return new RevealTarget("reference_tool", tellId, RevealStyle.ToolResult, surface);
                default: return null;
            }
        }

        public static bool IsBodyArtifact(string artifact)
        {
            var t = ForArtifact(artifact);
            return t != null && t.Style == RevealStyle.MagnifierRing;
        }

        public static bool IsFaceArtifact(string artifact)
        {
            var t = ForArtifact(artifact);
            return t != null && t.Style == RevealStyle.Callout;
        }

        public static int MaxUsesPerDay(ToolKind tool)
        {
            switch (tool)
            {
                case ToolKind.IdRegistry: return 2;
                case ToolKind.MetadataScan: return 2;
                case ToolKind.ReferenceCall: return 1;
                default: return 0;
            }
        }

        private static RevealTarget Magnifier(string artifact)
        {
            return new RevealTarget(artifact, null, RevealStyle.MagnifierRing, RevealSurface.ApplicantStage);
        }

        private static RevealTarget Callout(string artifact)
        {
            return new RevealTarget(artifact, null, RevealStyle.Callout, RevealSurface.ApplicantStage);
        }
    }
}
