// HR//0D -- wires the desk scene to a seeded session. Drop-in for BE1's GameManager.
// Everything below is wiring: rules live in DeskSession/EvidenceLedger, scoring in Core.
// No truth flag or tell list is passed to any view.
// Owner: Agent (interaction layer).
using System;
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

        /// <summary>When true, Play resumes the newest save instead of starting a fresh shift.</summary>
        public bool resumeSavedRun = true;

        public SaveData Save { get; private set; }
        public bool ResumedFromSave { get; private set; }

        public DeskSession Session { get; private set; }
        public EvidencePresenter Presenter { get; private set; }
        public DecisionInputController Decision { get; private set; }
        public LetterView Letter { get; private set; }
        public GameHud Hud { get; private set; }
        public SweepView Walk { get; private set; }

        public bool LetterOpen { get { return Letter != null && Letter.gameObject.activeSelf; } }
        public bool ShowDebrief { get { return _showingDebrief; } }
        public bool ShowDaySummary { get { return _showingDaySummary; } }
        /// <summary>The after-hours walk is on screen. Input goes to the walk while this is true.</summary>
        public bool ShowingSweep { get { return Walk != null && Walk.IsOpen; } }
        public bool ShowEnding { get { return Session != null && Session.Finished && !_showingDebrief; } }
        public DecisionResult LastDecision { get; private set; }
        public DayResult LastDay { get; private set; }
        public bool HelpVisible { get { return _helpVisible; } }
        public float DebriefLeft { get { return _debriefLeft; } }
        /// <summary>The body part under the mouse, for the HUD's pointer line. Null when nothing is hovered.</summary>
        public string HoveredArtifact { get; private set; }

        /// <summary>Called by the router on hover. Names the target so the player knows what they found.</summary>
        public void SetHoveredArtifact(string artifact)
        {
            HoveredArtifact = artifact;
            if (Hud != null) Hud.RefreshNow();
        }

        /// <summary>The walk's one-line receipt for the summary screen; null when the walk was skipped.</summary>
        public string SweepLine { get { return _sweepLine; } }

        private float _debriefLeft;
        private bool _showingDebrief;
        private bool _showingDaySummary;
        private bool _helpVisible;
        private System.Collections.Generic.List<SweepTrace> _sweepTraces;
        private string _sweepLine;                 // the summary's receipt, null on a clean day

        private void Awake()
        {
            if (string.IsNullOrEmpty(watchCopy)) watchCopy = "HOLD STILL. THE TELL IS SMALL.";
            if (string.IsNullOrEmpty(correctHeading)) correctHeading = "DECISION RECORDED";
            if (string.IsNullOrEmpty(wrongHeading)) wrongHeading = "DECISION RECORDED";
            var layout = DeskLayout.Load();
            var overlay = GetComponentInChildren<DeskEvidenceOverlay>(true);
            var stamps = FindStamp();
            Letter = GetComponentInChildren<LetterView>(true);
            Decision = GetComponentInChildren<DecisionInputController>();
            Hud = GetComponentInChildren<GameHud>(true);

            Save = resumeSavedRun ? SaveSystem.Read() : null;
            if (Save != null)
            {
                ResumedFromSave = true;
                seed = Save.runSeed;                 // the seed in the save wins, or the run changes
                Session = new DeskSession(Save);
            }
            else
            {
                Save = new SaveData();
                Save.runSeed = seed;
                Session = new DeskSession(seed);
            }
            Presenter = new EvidencePresenter(Session, layout, overlay, watchCopy);

            if (Decision != null)
            {
                Decision.session = Session;
                Decision.presenter = Presenter;
                Decision.stamps = stamps;
                Decision.Committed += OnCommitted;
            }

            var hotspots = GetComponentsInChildren<HotspotInteractor>();
            for (int i = 0; i < hotspots.Length; i++) hotspots[i].Tapped += OnHotspot;

            Session.ApplicantChanged += OnApplicantChanged;
            Session.BeginCurrent();            // the constructor fired before we subscribed
        }

        private void Start()
        {
            if (Hud == null) Hud = GameHud.Attach(this);
            else Hud.Bind(this);
            if (string.IsNullOrEmpty(watchCopy)) watchCopy = "HOLD STILL. THE TELL IS SMALL.";
            if (string.IsNullOrEmpty(correctHeading)) correctHeading = "DECISION RECORDED";
            if (string.IsNullOrEmpty(wrongHeading)) wrongHeading = "DECISION RECORDED";
            if (Letter != null) Letter.Close();
            EnsureSweep();
            if (ResumedFromSave)
                watchCopy = "SHIFT RESUMED - DAY " + Session.Day + " / " + watchCopy;
            PersistSave();                  // a run that is quit on day 1 must still persist
        }

        // --------------------------------------------------------------- player actions
        public void OnHotspot(string artifact)
        {
            if (_showingDebrief || _showingDaySummary || ShowEnding || LetterOpen) return;
            Presenter.Inspect(artifact);
        }

        public void ToggleHelp() { _helpVisible = !_helpVisible; }

        // --------------------------------------------------------------- after-hours sweep
        /// <summary>
        /// Builds the walk's page lazily, once. Lazy because most Play sessions never reach a summary,
        /// and a failed build must not stop the desk from working: it is caught and logged, and the day
        /// simply skips the walk.
        /// </summary>
        private void EnsureSweep()
        {
            if (Walk != null) return;
            try
            {
                var view = SweepView.Attach(this);
                if (view.Build(DeskLayout.Load().canvas)) Walk = view;
                else Destroy(view.gameObject);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[HR//0D] sweep unavailable: " + ex.Message + " -- the day summary continues without it.");
                Walk = null;
            }
        }

        /// <summary>Called by SweepView when the last page clocks out.</summary>
        public void OnSweepFinished()
        {
            _showingDaySummary = true;
            Hud.RefreshNow();          // the summary must appear with the walk's line on the first frame
        }

        private void StartSweep()
        {
            EnsureSweep();
            if (Walk == null) { _showingDaySummary = true; return; }
            if (!Walk.Open(_sweepTraces)) { _showingDaySummary = true; return; }
            CloseLetter();
            Presenter.Hide();
        }

        /// <summary>SPACE while the walk is on screen. Returns true when the press was consumed.</summary>
        public bool SweepAdvance()
        {
            if (!ShowingSweep) return false;
            Walk.Advance();
            return true;
        }

        public void ToggleLetter()
        {
            if (_showingDebrief || _showingDaySummary || ShowEnding) return;
            if (LetterOpen) { CloseLetter(); return; }
            if (Letter == null || Session == null || Session.Paperwork == null) return;
            if (!Session.Paperwork.CanOpen) return;      // one read, then commit: no reopen after stamp
            if (!Letter.OpenPaperwork(Session.Paperwork)) return;
            Presenter.Hide();
        }

        public void CloseLetter()
        {
            if (Letter != null) Letter.Close();
        }

        /// <summary>1..5 toggle a letter flag; 6 picks "insufficient" on the desk.</summary>
        public void NumberKey(int n)
        {
            if (n < 1 || n > 6) return;
            if (LetterOpen)
            {
                if (n > 5 || Session == null || Session.Paperwork == null) return;
                var flags = Session.Paperwork.flags;
                if (flags == null || n > flags.Count) return;
                Session.TogglePaperFlag(flags[n - 1].id);
                return;
            }
            if (Decision != null) Decision.ChooseReason(n - 1);
        }

        public void Approve()
        {
            if (_showingDebrief || _showingDaySummary || ShowEnding) return;
            CloseLetter();
            if (Decision != null) Decision.Approve();
        }

        public void Reject()
        {
            if (_showingDebrief || _showingDaySummary || ShowEnding) return;
            CloseLetter();
            if (Decision != null) Decision.Reject();
        }

        /// <summary>Space/Enter: advance the debrief, then the day summary, then the next applicant.</summary>
        public void Continue()
        {
            if (_showingDebrief)
            {
                _showingDebrief = false;
                if (Session.DayFinished || Session.Finished)
                {
                    var closedDay = Session.LastClosedDay;
                    LastDay = Session.CloseDay();
                    RecordDay(LastDay, closedDay);
                    CloseLetter();
                    Presenter.Hide();

                    // DECIDED 2026-11-26: the walk happens only on a day when something got through.
                    // A quiet office is not a scene; that day goes straight to the summary.
                    _sweepTraces = Sweep.AfterShift(closedDay, Session.Shift.Today);
                    _sweepLine = SweepScript.SummaryLine(closedDay, _sweepTraces);
                    if (Sweep.HasSweep(closedDay)) StartSweep();
                    else _showingDaySummary = true;
                }
                else if (Decision != null) Decision.NextApplicant();
                return;
            }
            if (_showingDaySummary)
            {
                _showingDaySummary = false;
                if (Session.Finished) return;                // ending screen; R restarts
                if (Decision != null) Decision.NextApplicant();
            }
        }

        public void Restart()
        {
            if (Session == null) return;
            Session.ApplicantChanged -= OnApplicantChanged;
            if (Decision != null)
            {
                Decision.Committed -= OnCommitted;
                Decision.Reset();
            }
            Save = new SaveData();
            Save.runSeed = seed;
            Session = new DeskSession(seed);
            // A restart is a new run: the old save must not resurrect itself on next launch.
            SaveSystem.Delete();
            Presenter = new EvidencePresenter(Session, DeskLayout.Load(),
                                              GetComponentInChildren<DeskEvidenceOverlay>(), watchCopy);
            if (Decision != null)
            {
                Decision.session = Session;
                Decision.presenter = Presenter;
                Decision.Committed += OnCommitted;
            }
            Session.ApplicantChanged += OnApplicantChanged;
            Session.BeginCurrent();
            LastDecision = null;
            LastDay = null;
            _sweepTraces = null;
            _sweepLine = null;
            _showingDebrief = false;
            _showingDaySummary = false;
            CloseLetter();
            var stamps = FindStamp();
            if (stamps != null) stamps.Cancel();
        }

        // --------------------------------------------------------------- events
        private void OnCommitted(DecisionResult result)
        {
            LastDecision = result;
            PersistSave();
            _showingDebrief = true;
            _debriefLeft = debriefSeconds;
            CloseLetter();
            var overlay = GetComponentInChildren<DeskEvidenceOverlay>(true);
            if (overlay != null)
            {
                overlay.ShowCallout(Presenter.DocumentRect(),
                                    result != null && result.IsCorrect ? correctHeading : wrongHeading,
                                    result == null ? string.Empty : result.DebriefLine);
            }
        }

        private void Update()
        {
            if (Session != null && !_showingDebrief && !_showingDaySummary && !ShowEnding)
            {
                Session.Tick(Time.unscaledDeltaTime);
                if (Decision != null) Decision.Tick(Mathf.RoundToInt(Session.ObservationSeconds));
            }
            if (!_showingDebrief || autoAdvance == false) return;
            _debriefLeft -= Time.unscaledDeltaTime;
            if (_debriefLeft <= 0f) Continue();
        }

        private void OnApplicantChanged(ApplicantViewData view)
        {
            _showingDebrief = false;
            CloseLetter();
            var applicant = transform.Find("L_applicant");
            if (applicant == null) return;
            var actor = applicant.GetComponent<ApplicantView>();
            if (actor == null) return;
            var manifest = SpriteManifest.Load();
            var entry = manifest.Find("art/kit/applicants/app_" + view.SpriteKey + "_192x216.png");
            actor.SetApplicant(view.SpriteKey, DeskSceneBuilder.LoadFrames(entry), null);
        }

        private StampAnimator FindStamp()
        {
            return GetComponentInChildren<StampAnimator>(true);
        }

        // --------------------------------------------------------------- persistence
        /// <summary>Snapshots the run. Cheap enough to call after every stamp (a few KB).</summary>
        public void PersistSave()
        {
            if (Session == null || Save == null) return;
            Session.WriteTo(Save);
            // The ending is stored so a resumed finished run shows the ending, not day 1.
            if (Session.Finished && Save.Outcome == RunOutcome.InProgress)
                Save.Outcome = Session.Shift.Outcome;
            SaveSystem.Write(Save);
        }

        private void RecordDay(DayResult day, int dayNumber)
        {
            if (day == null || Save == null) return;
            Save.days.Add(new DayRecord
            {
                day = dayNumber,
                completed = true,
                approved = day.Approved,
                rejected = day.Rejected,
                wrongfulRejections = day.WrongfulRejections,
                syntheticsHired = day.SyntheticsHired,
                trustAtEnd = day.TrustAfter,
            });
            PersistSave();
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused) PersistSave();          // Android: the app is about to be backgrounded
        }

        private void OnApplicationQuit()
        {
            // The run was persisted after every committed decision, so nothing is lost by skipping the
            // one write that cannot succeed: the file handle may already be closed under us.
            SaveSystem.MarkQuitting();
        }

        private void OnDisable()
        {
            // OnDisable also fires on Stop in the editor and on destruction -- both while the previous
            // write may still be in flight. Persist only if we are not shutting down.
            if (!SaveSystem.ApplicationIsQuitting) PersistSave();
            if (Session != null) Session.ApplicantChanged -= OnApplicantChanged;
            if (Decision != null) Decision.Committed -= OnCommitted;
            var hotspots = GetComponentsInChildren<HotspotInteractor>();
            for (int i = 0; i < hotspots.Length; i++) hotspots[i].Tapped -= OnHotspot;
        }
    }
}
