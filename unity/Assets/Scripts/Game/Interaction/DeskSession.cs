// HR//0D -- the playable minute: one session owns the shift, the notebook and the tool budget.
// Pure C#: no UnityEngine references. Unity views subscribe to the events; they never receive
// groundTruth, so the only way to win is to prove something from what is on screen.
// Owner: Agent (interaction layer).
using System;
using HRZeroDay.Core.Data;
using HRZeroDay.Core.Rules;

namespace HRZeroDay.Game.Interaction
{
    /// <summary>Only the fields the player may see. This DTO deliberately has no truth/tells.</summary>
    public sealed class ApplicantViewData
    {
        public string Id;
        public string FullName;
        public int Age;
        public string IdNumber;
        public string University;
        public int GraduationYear;
        public string Employer;
        public int EmployerFounded;
        public int EmploymentStartYear;
        public int YearsExperience;
        public string Role;
        public string Referral;
        public string CoverLetterId;
        public string PortraitId;
        /// <summary>Art file key for the sprite; never render or speak this as text.</summary>
        public string SpriteKey;

        public static ApplicantViewData From(ApplicantRecord a)
        {
            var v = new ApplicantViewData();
            v.Id = a.id;
            v.FullName = a.fullName;
            v.Age = a.age;
            v.IdNumber = a.idNumber;
            v.University = a.university;
            v.GraduationYear = a.graduationYear;
            v.Employer = a.employer;
            v.EmployerFounded = a.employerFounded;
            v.EmploymentStartYear = a.employmentStartYear;
            v.YearsExperience = a.yearsExperience;
            v.Role = a.role;
            v.Referral = a.referral;
            v.CoverLetterId = a.coverLetterId;
            v.PortraitId = a.portraitId;
            v.SpriteKey = ArtKey(a.Archetype);
            return v;
        }

        private static string ArtKey(Archetype archetype)
        {
            switch (archetype)
            {
                case Archetype.HumanSolid: return "human_solid_a";
                case Archetype.HumanNervous: return "human_nervous_a";
                case Archetype.HumanBait: return "human_bait_a";
                case Archetype.SyntheticPolished: return "synthetic_polished_a";
                case Archetype.SyntheticCloned: return "synthetic_cloned_a";
                case Archetype.SyntheticCollusion: return "synthetic_collusion_a";
                default: return "human_solid_a";
            }
        }
    }

    public sealed class DeskSession
    {
        private readonly ShiftController _shift;
        private readonly EvidenceLedger _ledger = new EvidenceLedger();
        private readonly ToolBudget _tools = new ToolBudget();
        private float _observationSeconds;

        public event Action<ApplicantViewData> ApplicantChanged;
        public event Action<DecisionResult, ApplicantViewData> DecisionCommitted;
        public event Action<DayResult> DayClosed;

        public DeskSession(int seed)
        {
            _shift = new ShiftController(seed);
            BeginCurrent();
        }

        public ShiftController Shift { get { return _shift; } }
        public EvidenceLedger Ledger { get { return _ledger; } }
        public ToolBudget Tools { get { return _tools; } }
        public int Seed { get { return _shift.Seed; } }
        public int Day { get { return _shift.Day; } }
        public int Trust { get { return _shift.Trust; } }
        public int CorruptionLevel { get { return _shift.CorruptionLevel; } }
        public bool Finished { get { return _shift.RunFinished; } }
        public bool DayFinished { get { return _shift.DayFinished; } }
        public float ObservationSeconds { get { return _observationSeconds; } }

        public ApplicantViewData Current { get { return ApplicantViewData.From(_shift.Current); } }

        public void Tick(float unscaledDeltaTime)
        {
            if (unscaledDeltaTime > 0f) _observationSeconds += unscaledDeltaTime;
        }

        public ObservationResult Inspect(string artifact)
        {
            return _ledger.InspectArtifact(artifact, _shift.Day, _observationSeconds);
        }

        public System.Collections.Generic.List<ObservationResult> Open(RevealSurface surface)
        {
            return _ledger.OpenSurface(surface, _shift.Day);
        }

        public bool UseTool(ToolKind tool)
        {
            return _tools.TryUse(tool, _shift.Day);
        }

        public DecisionResult Stamp(Decision decision, EvidenceCategory? reason, int secondsOnCard)
        {
            if (Finished || DayFinished) return null;
            var input = _ledger.BuildDecision(decision, reason, secondsOnCard);
            var result = _shift.Submit(input);
            if (DecisionCommitted != null) DecisionCommitted(result, ApplicantViewData.From(input.Applicant));
            return result;
        }

        public DayResult CloseDay()
        {
            if (Finished) return null;
            var day = _shift.EndDay();
            if (DayClosed != null) DayClosed(day);
            return day;
        }

        /// <summary>Call after a stamp (or after a day summary) to put the next person in the chair.</summary>
        public bool BeginCurrent()
        {
            if (Finished || DayFinished) return false;
            _observationSeconds = 0f;
            _ledger.Begin(_shift.Current);
            _tools.AdvanceToDay(_shift.Day);
            if (ApplicantChanged != null) ApplicantChanged(Current);
            return true;
        }
    }
}
