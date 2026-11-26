// HR//0D -- wires the desk scene to a seeded session. Drop-in for BE1's GameManager.
// Everything below is wiring: rules live in DeskSession/EvidenceLedger, scoring in Core.
// No truth flag or tell list is passed to any view.
// Owner: Agent (interaction layer).
using UnityEngine;
using HRZeroDay.Core.Data;
using HRZeroDay.Core.Rules;

namespace HRZeroDay.Game.Interaction
{
    public sealed class DeskBootstrap : MonoBehaviour
    {
        public int seed = 1000;
        public string watchCopy;             // supplied by content, never hardcoded here
        public string correctHeading;
        public string wrongHeading;
        public bool autoAdvance = true;
        [Range(0.5f, 10f)] public float debriefSeconds = 3f;

        public DeskSession Session { get; private set; }
        public EvidencePresenter Presenter { get; private set; }
        public DecisionInputController Decision { get; private set; }

        private float _debriefLeft;
        private bool _showingDebrief;

        private void Awake()
        {
            var layout = DeskLayout.Load();
            var overlay = GetComponentInChildren<DeskEvidenceOverlay>();
            var stamps = GetComponentInChildren<StampAnimator>();
            Decision = GetComponentInChildren<DecisionInputController>();

            Session = new DeskSession(seed);
            Presenter = new EvidencePresenter(Session, layout, overlay, watchCopy);

            if (Decision != null)
            {
                Decision.session = Session;
                Decision.presenter = Presenter;
                Decision.stamps = stamps;
                Decision.Committed += OnCommitted;
            }

            var hotspots = GetComponentsInChildren<HotspotInteractor>();
            for (int i = 0; i < hotspots.Length; i++)
            {
                var tap = hotspots[i];
                tap.Tapped += OnHotspot;
            }

            Session.ApplicantChanged += OnApplicantChanged;
            Session.BeginCurrent();            // the constructor fired before we subscribed
        }

        private void OnHotspot(string artifact)
        {
            if (_showingDebrief) return;
            Presenter.Inspect(artifact);
        }

        private void OnCommitted(DecisionResult result)
        {
            _showingDebrief = true;
            _debriefLeft = debriefSeconds;
            var overlay = GetComponentInChildren<DeskEvidenceOverlay>();
            if (overlay != null)
            {
                overlay.ShowCallout(Presenter.DocumentRect(),
                                    result.IsCorrect ? correctHeading : wrongHeading,
                                    result.DebriefLine);
            }
            if (autoAdvance == false) return;
        }

        /// <summary>FE calls this from the real day-summary button when autoAdvance is off.</summary>
        public void Continue()
        {
            if (!_showingDebrief) return;
            _showingDebrief = false;
            if (Session.DayFinished || Session.Finished)
            {
                Session.CloseDay();
                Presenter.Hide();
            }
            if (Decision != null) Decision.NextApplicant();
        }

        private void Update()
        {
            if (!_showingDebrief || autoAdvance == false) return;
            _debriefLeft -= Time.unscaledDeltaTime;
            if (_debriefLeft <= 0f) Continue();
        }

        private void OnApplicantChanged(ApplicantViewData view)
        {
            _showingDebrief = false;
            var applicant = transform.Find("L_applicant");
            if (applicant == null) return;
            var actor = applicant.GetComponent<ApplicantView>();
            if (actor == null) return;
            var manifest = SpriteManifest.Load();
            var entry = manifest.Find("art/kit/applicants/app_" + view.SpriteKey + "_192x216.png");
            actor.SetApplicant(view.SpriteKey, DeskSceneBuilder.LoadFrames(entry), null);
        }

        private void OnDisable()
        {
            if (Session != null) Session.ApplicantChanged -= OnApplicantChanged;
            if (Decision != null) Decision.Committed -= OnCommitted;
            var hotspots = GetComponentsInChildren<HotspotInteractor>();
            for (int i = 0; i < hotspots.Length; i++) hotspots[i].Tapped -= OnHotspot;
        }
    }
}
