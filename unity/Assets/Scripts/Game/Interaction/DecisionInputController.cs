// HR//0D -- reason dropdown + APPROVE / REJECT stamping, with a double-tap guard.
// The reason list is supplied by content (BE2); index order matches EvidenceCategory.
// Gameplay always goes through DeskSession.Stamp -> the pure ScoringService.
// Owner: Agent (interaction layer).
using System;
using UnityEngine;
using HRZeroDay.Core.Data;
using HRZeroDay.Core.Rules;

namespace HRZeroDay.Game.Interaction
{
    public sealed class DecisionInputController : MonoBehaviour
    {
        public DeskSession session;
        public StampAnimator stamps;
        public EvidencePresenter presenter;

        /// <summary>Event raised after the stamp animation finishes, with the scoring result.</summary>
        public event Action<DecisionResult> Committed;

        public EvidenceCategory? Reason { get; private set; }
        public bool Locked { get { return _locked; } }

        private bool _locked;
        private int _secondsOnCard;

        private void Awake()
        {
            _locked = false;
            Reason = null;
        }

        /// <summary>0..4 select a real category; 5 = Insufficient. -1 clears.</summary>
        public void ChooseReason(int index)
        {
            if (_locked) return;
            if (index < 0 || index > 5) { Reason = null; return; }
            Reason = (EvidenceCategory)index;
        }

        public void Tick(int seconds)
        {
            _secondsOnCard = Mathf.Max(0, seconds);
        }

        public void Approve()
        {
            Commit(Decision.Approve, null);
        }

        public void Reject()
        {
            Commit(Decision.Reject, Reason.HasValue ? Reason.Value : EvidenceCategory.Insufficient);
        }

        public void NextApplicant()
        {
            _locked = false;
            Reason = null;
            _secondsOnCard = 0;
            if (presenter != null) presenter.Hide();
            if (session != null) session.BeginCurrent();
        }

        /// <summary>Used by DeskBootstrap.Restart; no scene reload, no stale decision state.</summary>
        public void Reset()
        {
            _locked = false;
            Reason = null;
            _secondsOnCard = 0;
            if (presenter != null) presenter.Hide();
        }

        private void Commit(Decision decision, EvidenceCategory? reason)
        {
            if (_locked || session == null || session.Finished || session.DayFinished) return;
            _locked = true;                       // standing rule 9: irreversible action, one shot
            if (presenter != null) presenter.Hide();

            var result = session.Stamp(decision, reason, _secondsOnCard);
            Action done = delegate
            {
                if (Committed != null) Committed(result);
            };
            if (stamps != null && stamps.gameObject.activeInHierarchy)
                stamps.Play(decision, done);
            else done();
        }
    }
}
