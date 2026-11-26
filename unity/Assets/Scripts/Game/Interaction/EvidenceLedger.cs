// HR//0D -- the player's notebook: only what the player actually opened counts.
// Pure C#: no UnityEngine references. The scoring service receives these lists instead of the
// hidden record, so a right answer for the wrong reason is still teachable (see ScoringService).
// Owner: Agent (interaction layer).
using System;
using System.Collections.Generic;
using HRZeroDay.Core.Data;
using HRZeroDay.Core.Rules;

namespace HRZeroDay.Game.Interaction
{
    /// <summary>What happened when the player inspected one artifact or document surface.</summary>
    public sealed class ObservationResult
    {
        public string Artifact;
        public string TellId;                 // null unless a catalog-backed tell was proven
        public RevealStyle Style;
        public RevealSurface Surface;
        public bool Proven;                   // true only for a tell present on this applicant
        public bool Changed;                  // false on a repeat inspection (no double counting)
        public bool RequiresMoreTime;         // timed tells such as no_blink need a longer look
        public string Wording;                // plain language, from TellCatalog (Dev C owns final copy)

        public bool IsEvidence { get { return Proven && TellId != null; } }
    }

    /// <summary>One notebook line. Kept for the Field Manual and the debrief.</summary>
    public sealed class EvidenceNote
    {
        public string Artifact;
        public string TellId;
        public string Wording;
        public RevealStyle Style;
        public RevealSurface Surface;

        public EvidenceNote(string artifact, string tellId, string wording,
                            RevealStyle style, RevealSurface surface)
        {
            Artifact = artifact; TellId = tellId; Wording = wording; Style = style; Surface = surface;
        }
    }

    public sealed class EvidenceLedger
    {
        private ApplicantRecord _applicant;
        private readonly List<EvidenceNote> _notes = new List<EvidenceNote>();
        private readonly HashSet<string> _tells = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> _artifacts = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<RevealSurface> _surfaces = new HashSet<RevealSurface>();

        public IEnumerable<EvidenceNote> Notes { get { return _notes; } }
        public ICollection<string> InspectedTells { get { return _tells; } }
        public ICollection<string> ObservedArtifacts { get { return _artifacts; } }
        public ICollection<RevealSurface> OpenedSurfaces { get { return _surfaces; } }

        /// <summary>Proven tells the player actually found. Never the full tellsPresent list.</summary>
        public int FoundCount { get { return _tells.Count; } }

        public void Begin(ApplicantRecord applicant)
        {
            _applicant = applicant;
            _notes.Clear();
            _tells.Clear();
            _artifacts.Clear();
            _surfaces.Clear();
        }

        /// <summary>Inspects one hotspot on the applicant. Returns what can honestly be shown.</summary>
        public ObservationResult InspectArtifact(string artifact, int day, float observedSeconds = 0f)
        {
            var result = new ObservationResult();
            result.Artifact = artifact;
            result.Style = RevealStyle.Callout;
            result.Surface = RevealSurface.ApplicantStage;

            var target = EvidenceMap.ForArtifact(artifact);
            if (target == null) return result;

            result.Style = target.Style;
            result.Surface = target.Surface;

            // Behaviour alone is not evidence: the timed artifact needs a long enough look and
            // never gets a tell id of its own.
            if (artifact == EvidenceMap.NoBlinkArtifact &&
                observedSeconds < EvidenceMap.NoBlinkObserveSeconds)
            {
                result.RequiresMoreTime = true;
                return result;
            }

            bool firstArtifact = _artifacts.Contains(artifact) == false;
            _artifacts.Add(artifact);
            _surfaces.Add(RevealSurface.ApplicantStage);

            if (target.TellId == null)
            {
                result.Changed = firstArtifact;         // art-only observation, not a tell
                return result;
            }
            if (TryRevealTell(target.TellId, day))
            {
                result.Proven = true;
                result.TellId = target.TellId;
                result.Wording = TellCatalog.Get(target.TellId).Wording;
                result.Changed = true;
            }
            else
            {
                result.Changed = firstArtifact;
            }
            return result;
        }

        /// <summary>Opens a document row or a verification tool and reveals what is there.</summary>
        public List<ObservationResult> OpenSurface(RevealSurface surface, int day)
        {
            var results = new List<ObservationResult>();
            if (_applicant == null || TellCatalog.IsUnlocked(day, surface) == false) return results;
            _surfaces.Add(surface);

            foreach (var id in _applicant.tellsPresent)
            {
                if (TellCatalog.Has(id) == false) continue;
                if (TellCatalog.Get(id).Surface != surface) continue;
                bool first = TryRevealTell(id, day);
                var r = new ObservationResult();
                r.Artifact = EvidenceMap.ForTell(id).Artifact;
                r.TellId = id;
                r.Style = TellCatalog.Get(id).Surface == RevealSurface.IdTool ||
                          TellCatalog.Get(id).Surface == RevealSurface.ReferenceTool
                              ? RevealStyle.ToolResult : RevealStyle.DocumentRow;
                r.Surface = surface;
                r.Proven = true;
                r.Wording = TellCatalog.Get(id).Wording;
                r.Changed = first;
                results.Add(r);
            }
            return results;
        }

        /// <summary>Builds the exact input the pure scoring service needs. No ground truth here.</summary>
        public DecisionInput BuildDecision(Decision decision, EvidenceCategory? reason, int secondsOnCard)
        {
            var input = new DecisionInput();
            input.Applicant = _applicant;
            input.Decision = decision;
            input.Reason = decision == Decision.Approve ? null : reason;
            input.TellsInspected = new List<string>(_tells);
            input.SurfacesOpened = new List<RevealSurface>(_surfaces);
            input.SecondsOnCard = secondsOnCard;
            return input;
        }

        public bool HasInspected(string tellId)
        {
            return tellId != null && _tells.Contains(tellId);
        }

        /// <summary>Records a tell only if it is present on this applicant and reachable today.</summary>
        private bool TryRevealTell(string tellId, int day)
        {
            if (_applicant == null || TellCatalog.Has(tellId) == false) return false;
            var tell = TellCatalog.Get(tellId);
            if (TellCatalog.IsUnlocked(day, tell.Surface) == false) return false;
            if (_applicant.tellsPresent.Contains(tellId) == false) return false;
            if (_tells.Contains(tellId)) return false;

            _tells.Add(tellId);
            _notes.Add(new EvidenceNote(EvidenceMap.ForTell(tellId).Artifact, tellId,
                                        tell.Wording, EvidenceMap.ForTell(tellId).Style, tell.Surface));
            return true;
        }
    }
}
