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
        public IntroView Intro { get; private set; }
        public HRZeroDay.Game.Audio.GameAudio Sound { get; private set; }
        public MenuView Menu { get; private set; }
        /// <summary>True while the front door is up. Input goes to it and nothing behind it moves.</summary>
        public bool ShowingMenu { get { return Menu != null && Menu.IsOpen; } }

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

        /// <summary>
        /// The five days as one run (see Core/Rules/RunLedger.cs). The HUD reads it for the summary's arc
        /// line and for the ending, so those two screens can never disagree with the days just played.
        /// Rebuilt from the save on resume, so a quit on day 3 comes back knowing days 1 and 2.
        /// </summary>
        public RunLedger Ledger { get; private set; }

        /// <summary>The intro, when one is configured and not yet seen (see IntroView).</summary>
        public bool ShowingIntro { get { return Intro != null && Intro.IsOpen; } }

        /// <summary>The roster of the day that is closing. Captured before the shift advances.</summary>
        private System.Collections.Generic.List<HRZeroDay.Core.Data.ApplicantRecord> _closedRoster;

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
            // The arc's memory. On a resumed run it is rebuilt from the save (days already closed keep
            // their numbers); on a fresh run it starts empty and fills as days close.
            Ledger = Save != null && Save.days != null
                ? RunLedger.FromSave(Save.days, 0)
                : new RunLedger();

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

            // THE ROOM IS ALREADY THERE (v105). Built before the intro, because the intro card plays over a
            // room that is already running -- that is the difference between "a scene" and "a menu".
            EnsureAudio();

            // THE FRONT DOOR, THEN THE INTRO, THEN THE DESK (v110). The menu comes first because it is where
            // consent and the audio settings live -- the two things the save file had no home for. The intro
            // is shown after it, because the player has now said "start".
            bool atDoor = TryStartMenu();

            // THE INTRO, BEFORE THE FIRST SHIFT (v102). The words are the content team's
            // (content/intro/intro_01.json); this side only shows them, once per profile, and steps out of
            // the way if there is nothing to show. It runs AFTER the desk is built and AFTER the room is
            // running, so the first thing behind the card is the room the player is about to work in --
            // not an empty screen, and not a silent one.
            if (!atDoor) TryStartIntro();
            if (ResumedFromSave)
                watchCopy = "SHIFT RESUMED - DAY " + Session.Day + " / " + watchCopy;
            PersistSave();                  // a run that is quit on day 1 must still persist
        }

        // --------------------------------------------------------------- player actions
        public void OnHotspot(string artifact)
        {
            if (_showingDebrief || _showingDaySummary || ShowEnding || LetterOpen) { Cue(Sfx.Deny); return; }
            Presenter.Inspect(artifact);
            Cue(Sfx.Drawer);            // the player just opened something on the desk
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
            if (_showingDebrief || _showingDaySummary || ShowEnding) { Cue(Sfx.Deny); return; }
            CloseLetter();
            if (Decision != null) Decision.Approve();
        }

        public void Reject()
        {
            if (_showingDebrief || _showingDaySummary || ShowEnding) { Cue(Sfx.Deny); return; }
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
                    // captured BEFORE CloseDay advances the shift: this is the roster of the day being
                    // closed, and the only one the walk and the summary may talk about
                    _closedRoster = new System.Collections.Generic.List<HRZeroDay.Core.Data.ApplicantRecord>(
                        Session.Shift.Today);
                    var closedDay = Session.LastClosedDay;
                    LastDay = Session.CloseDay();
                    RecordDay(LastDay, closedDay);
                    RecordArc(LastDay, closedDay);
                    CloseLetter();
                    Presenter.Hide();

                    // DECIDED 2026-11-26: the walk happens only on a day when something got through.
                    // A quiet office is not a scene; that day goes straight to the summary.
                    //
                    // FROM THE CLOSED DAY'S ROSTER (v101). CloseDay() has already advanced the shift, so
                    // Session.Shift.Today is the NEXT roster -- building the walk from it could name
                    // somebody the player had not met. The walk is a receipt; it may only name people who
                    // were actually at the desk today.
                    _sweepTraces = Sweep.ForClosedDay(closedDay, _closedRoster,
                                                      LastDay == null ? 0 : LastDay.SyntheticsHired);
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
            // a new RUN is a new first minute, so the intro is shown again (it is once per profile, and R
            // starts a fresh profile). Deliberately after Save is recreated.
            TryStartIntro();
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
            Ledger = new RunLedger();
            _sweepTraces = null;
            _sweepLine = null;
            _showingDebrief = false;
            _showingDaySummary = false;
            CloseLetter();
            var stamps = FindStamp();
            if (stamps != null) stamps.Cancel();
        }

        // --------------------------------------------------------------- the front door
        /// <summary>
        /// Shows the menu on boot. Failure is silent: no atlas, no menu, and the desk starts as before -- a
        /// front door that can stop the game opening is worse than no front door.
        /// </summary>
        private bool TryStartMenu()
        {
            try
            {
                if (Menu == null)
                {
                    Menu = MenuView.Attach(this);
                    if (!Menu.Build()) { Menu = null; return false; }
                }
                bool hasSave = Save != null;
                bool inProgress = Session != null && !Session.Finished;
                var p = Save == null ? null : Save.profile;
                bool decided = p != null && p.consentDecided;
                bool granted = p != null && p.consentGranted;
                float room = p == null ? 0.8f : p.bedVolume;
                float sfx = p == null ? 0.9f : p.sfxVolume;
                bool muted = p != null && p.muted;

                if (Menu.Open(hasSave, inProgress, decided, granted, room, sfx, muted))
                {
                    Presenter.Hide();
                    if (Hud != null) Hud.RefreshNow();
                    return true;            // the intro waits: two full-screen cards must never stack
                }
                return false;
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[HR//0D] menu unavailable: " + ex.Message + " -- starting at the desk.");
                if (Menu != null) Menu.Close();
                return false;
            }
        }

        /// <summary>
        /// What happens when the door closes. If the player has never seen the intro, this is when they see
        /// it -- after the menu, never underneath it. Anything that goes wrong leaves the desk running.
        /// </summary>
        private void AfterMenuClosed()
        {
            TryStartIntro();
            if (Hud != null) Hud.RefreshNow();
            PersistSave();
        }

        /// <summary>
        /// The player's data decision, written to the save IMMEDIATELY: a consent that only exists in memory is
        /// not consent. Declining records a decision too -- "no" is an answer, and the game must not ask again.
        /// </summary>
        public void ApplyConsent(bool granted)
        {
            if (Save == null) Save = new SaveData();
            if (Save.profile == null) Save.profile = new PlayerProfile();
            Save.profile.consentDecided = true;
            Save.profile.consentGranted = granted;
            Save.profile.consentVersion = ConsentRules.Version;
            PersistSave();
        }

        /// <summary>UP/DOWN while the menu is up.</summary>
        public bool MenuMove(int delta)
        {
            if (!ShowingMenu) return false;
            Menu.Move(delta);
            return true;
        }

        /// <summary>LEFT/RIGHT (and A/D) while the menu is up: step the selected row down/up.</summary>
        public bool MenuStep(int delta)
        {
            if (!ShowingMenu) return false;
            if (Menu.Step(delta)) Cue(Sfx.Key);
            return true;
        }

        /// <summary>SPACE/ENTER while the menu is up. Returns true when the press was consumed.</summary>
        public bool MenuChoose()
        {
            if (!ShowingMenu) return false;
            if (!Menu.Choose())
            {
                Cue(Sfx.Key);
                return true;
            }

            // a row that leaves the menu: the bootstrap owns what happens next
            var action = Menu.SelectedAction();
            Menu.Close();
            switch (action)
            {
                case MenuAction.NewRun:
                    Restart();                  // a new run is a new first minute: the intro shows again
                    break;
                case MenuAction.Continue:
                    // already resumed in Awake: the desk is behind the menu, so closing it IS continuing
                    AfterMenuClosed();
                    break;
            }
            return true;
        }

        /// <summary>ESC while the menu is up: back a screen, or close the menu and start playing.</summary>
        public bool MenuBack()
        {
            if (!ShowingMenu) return false;
            if (Menu.Back())
            {
                Menu.Close();
                AfterMenuClosed();
            }
            return true;
        }

        /// <summary>S while the menu is up: jump straight to settings.</summary>
        public bool MenuSettings()
        {
            if (!ShowingMenu) return false;
            if (Menu.Screen != MenuScreen.Settings) Menu.OpenSettings();
            return true;
        }

        // --------------------------------------------------------------- sound
        /// <summary>
        /// Builds the audio rig once and hands it the player's settings. Failure is silent by design: a
        /// missing audio kit must degrade to a quiet game, never to a broken one (see GameAudio).
        /// </summary>
        private void EnsureAudio()
        {
            try
            {
                if (Sound == null) Sound = HRZeroDay.Game.Audio.GameAudio.Attach(gameObject);
                if (Save != null && Save.profile != null)
                    Sound.Configure(Save.profile.bedVolume, Save.profile.sfxVolume, Save.profile.muted);
                Sound.SetCorruption(Save == null ? 0 : Save.corruptionLevel);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[HR//0D] audio disabled: " + ex.Message);
                Sound = null;
            }
        }

        /// <summary>Every cue in one place, so "what does the desk sound like" is answerable by reading.</summary>
        private void Cue(Sfx sfx)
        {
            if (Sound != null) Sound.Play(sfx);
        }

        /// <summary>A footstep. Called by SweepView per floor, so the walk has a body.</summary>
        public void CueStep() { Cue(Sfx.Step); }

        /// <summary>A page. Called by the walk's opening and by the desk's own page turns.</summary>
        public void CuePage() { Cue(Sfx.Page); }

        /// <summary>The player's audio settings changed (options screen, or a test). Applies immediately.</summary>
        public void ApplyAudioSettings(float bedVolume01, float sfxVolume01, bool muted)
        {
            if (Save == null) return;
            if (Save.profile == null) Save.profile = new HRZeroDay.Core.Data.PlayerProfile();
            Save.profile.bedVolume = bedVolume01;
            Save.profile.sfxVolume = sfxVolume01;
            Save.profile.muted = muted;
            if (Sound != null) Sound.Configure(bedVolume01, sfxVolume01, muted);
            PersistSave();
        }

        // --------------------------------------------------------------- the intro
        /// <summary>
        /// Shows the intro when there is one. Failure is silent by design: a missing or unusable story file
        /// must never stop the player from playing, so every path here ends with the desk running.
        /// </summary>
        private void TryStartIntro()
        {
            try
            {
                bool seen = Save != null && Save.profile != null && Save.profile.introSeen;
                var script = IntroLoader.Load();
                if (script == null || !script.HasBeats || seen) return;

                if (Intro == null)
                {
                    Intro = IntroView.Attach(this);
                    if (!Intro.Build()) { Intro = null; return; }
                }
                if (!Intro.Open(script, seen)) return;
                Presenter.Hide();
                if (Hud != null) Hud.RefreshNow();
            }
            catch (Exception ex)
            {
                // Never let the story layer be the reason the game does not start.
                Debug.LogWarning("[HR//0D] intro unavailable: " + ex.Message + " -- starting at the desk.");
                if (Intro != null) Intro.Close();
            }
        }

        /// <summary>Called by IntroView when the last beat ends or the player skips. Returns true if consumed.</summary>
        public void OnIntroFinished()
        {
            if (Save != null)
            {
                if (Save.profile == null) Save.profile = new PlayerProfile();
                Save.profile.introSeen = true;
                PersistSave();                  // a player who quits after the intro must not see it twice
            }
            if (Hud != null) Hud.RefreshNow();
        }

        /// <summary>SPACE/ENTER while the intro is up. Returns true when the press was consumed.</summary>
        public bool IntroAdvance()
        {
            if (!ShowingIntro) return false;
            Intro.Advance();
            return true;
        }

        /// <summary>X/ESC while the intro is up.</summary>
        public bool IntroSkip()
        {
            if (!ShowingIntro) return false;
            Intro.Skip();
            return true;
        }

        // --------------------------------------------------------------- events
        /// <summary>Keeps the bed in step with the run: corruption detunes the room (AudioRules.BedPitch).</summary>
        private void SyncAudioToRun()
        {
            if (Sound != null && Save != null) Sound.SetCorruption(Save.corruptionLevel);
        }

        private void OnCommitted(DecisionResult result)
        {
            LastDecision = result;
            PersistSave();
            _showingDebrief = true;
            // THE VERDICT LANDS (v105). Wrong calls get the low thud FIRST, under the stamp, so the player
            // feels the mistake in the same instant they see it. Both sounds fire here, not in the view:
            // the view draws what happened, this is what it sounded like.
            if (result != null && !result.IsCorrect) Cue(Sfx.Thud);
            Cue(Sfx.Stamp);
            SyncAudioToRun();           // a run that got worse should sound worse from this moment
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
            // the file is slapped down on the desk
            Cue(Sfx.Page);
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

        /// <summary>
        /// Adds the closed day to the run ledger. Separate from RecordDay because the ledger is what the
        /// PLAYER reads (summary arc line, ending verdict) and RecordDay is what the SAVE keeps; both get
        /// the same numbers, from the same DayResult, in the same place, so they cannot drift.
        /// </summary>
        private void RecordArc(DayResult day, int dayNumber)
        {
            if (day == null || Ledger == null) return;
            // Correct calls cannot be read off DayResult directly: it counts approvals and rejections but
            // not which were right. The shift knows. Rejection of a synthetic is a catch; approval of a
            // synthetic is a hire; a wrongful rejection is neither. Everything else was a correct call.
            var r = new RunDay
            {
                Day = dayNumber,
                Approved = day.Approved,
                Rejected = day.Rejected,
                WrongfulRejections = day.WrongfulRejections,
                SyntheticsHired = day.SyntheticsHired,
                SyntheticsCaught = day.SyntheticsCaught,
                QuotaMet = day.QuotaMet,
                TrustAtEnd = day.TrustAfter
            };
            r.CorrectCalls = System.Math.Max(0, (r.Approved + r.Rejected) - r.WrongfulRejections - r.SyntheticsHired);
            Ledger.Record(r);
            Ledger.WriteTo(Save.days);
            PersistSave();
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
