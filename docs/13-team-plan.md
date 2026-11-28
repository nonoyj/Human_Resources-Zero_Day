# Team Plan (revised) -- 1 frontend, 2 backend, audio by backend

**Confirmed 2026-11-25:** the human is the **frontend dev**; the other two are **backend devs**; one of
them will also **produce the audio**. This document replaces the A/B/C split in
`docs/01-roadmap-14-days.md` (same days, same gates, different owners).

---

## 1. Who owns what

| Role | Person | Owns (exclusive write rights) | Does NOT touch |
|---|---|---|---|
| **FE -- Frontend / UI** | **you** | `unity/Assets/Scenes`, `unity/Assets/Prefabs`, `unity/Assets/Scripts/UI`, animations, transitions, input, audio *playback*, settings screens, all `art/` usage | core logic in `Scripts/Core` (read-only), server |
| **BE1 -- Systems** | backend dev 1 | `unity/ProjectSettings`, `unity/Packages`, `unity/Assets/Scripts/{Core,Data,Rules,Content,Analytics}` integration, `SaveSystem`, `ContentLoader`, `GameManager`, `00_Boot`, EditMode test wrapper | scenes/prefabs (yours), visual design |
| **BE2 -- Server, content & audio** | backend dev 2 | `backend/`, `content/`, `unity/Assets/StreamingAssets/content`, `unity/Assets/Audio`, Android build + QA | `Scripts/Core`, scenes |

Rule unchanged: **one owner per file/scene.** Cross-boundary work is requested via a handoff file in
`handoff/`, never by editing someone else's files.

## 2. State of play -- are we using Unity yet?

**Correct: no Unity project exists yet.** What is in the repo is:

```
unity/Assets/Scripts/Core/**        <- pure C# gameplay core (compiled + tested outside Unity)
unity/Assets/Scripts/Tests/**       <- 15 tests
unity/Assets/Scripts/**/*.asmdef    <- assembly definitions
```

There is **no** `ProjectSettings/`, **no** `Packages/`, **no** `.unity` scene. Unity cannot open a bare
`Assets/` folder -- a project needs those two folders. That is deliberate: **BE1's first task is to create
the project**, because doing it wrong costs a day of merge pain.

### BE1's first task -- create the Unity project (SUPERSEDED: FE did it; see `docs/19`)

> **2026-11-26:** the user created the project so the week would not be blocked. This section is kept for
> reference only -- the live steps are in `docs/19-unity-project-setup.md`, and BE1's replaced task 1 is
> in `handoff/2026-11-26_fe-to-BE1_unity-project-created.md`.

```powershell
# 1. create a throwaway project with the pinned editor
#    Unity Hub > New project > Universal 2D > name: HRZeroDayTemp > create it anywhere OUTSIDE the repo
# 2. close Unity completely, then copy the two generated folders into the repo:
$src = "C:\path\to\HRZeroDayTemp"
Copy-Item "$src\ProjectSettings" "C:\users\user\HR-ZeroDay\unity\ProjectSettings" -Recurse
Copy-Item "$src\Packages"        "C:\users\user\HR-ZeroDay\unity\Packages" -Recurse
Remove-Item $src -Recurse -Force        # the temp project is disposable

# 3. open the real thing
#    Unity Hub > Add > Add project from disk > C:\users\user\HR-ZeroDay\unity
#    Editor must be 6000.3.29f1 (Hub will refuse a mismatch -- that is the protection working)
```

Then commit:
```powershell
git add unity/ProjectSettings unity/Packages
git commit -m "chore(unity): create project skeleton (URP 2D, 6000.3.29f1)"
```

**Do not commit `unity/Library/`** (already in `.gitignore`).

Verify before committing: `git status` must not list `Library/`, and `ProjectVersion.txt` must read
`m_EditorVersion: 6000.3.29f1`.

## 3. Work left for the two backend devs (so they are not idle)

### BE1 -- Systems (10 tasks, in order)

1. ~~Create the Unity project skeleton~~ -- **DONE by FE on 2026-11-26** (`docs/19-unity-project-setup.md`).
   Do **not** create a second one. Adopt the repo instead: see
   `handoff/2026-11-26_fe-to-BE1_unity-project-created.md`.
2. Drop in `UnityTestAdapter.cs` from `handoff/2026-11-26_agent-to-devA_core-handoff.md`; open Test Runner
   -> EditMode -> 15 green.
3. `SaveSystem`: atomic write (`tmp` -> `File.Replace`), autosave on every transition, corrupt-file
   recovery, `Application.persistentDataPath`, version migration via `SaveData.Migrate()`.
4. `ContentLoader`: read `StreamingAssets/content/*.json` on Android via `UnityWebRequest`, cache, verify
   `sha256`; `IBackend` interface + `FakeBackend` implementation first.
5. `AnalyticsService`: memory buffer -> `JSONL` outbox -> `IBackend`; never throws; opt-out respected;
   *"Export my play data"* share-sheet button.
6. `GameManager` (persistent singleton): owns `ShiftController`, survives scene loads, wires save/load.
7. `00_Boot` scene: load save, decide intro vs menu, then hand off to FE's scenes.
8. `RuleEngine` presentation glue: expose `tellsPresent` as inspectable hotspots + tool results;
   never reveal ground truth.
9. EditMode tests for save/analytics (the 6 listed in `docs/10-analytics-spec.md` #5).
10. Profile: applicant layer < 1.5 ms CPU, 60 fps on a mid-range device.

### BE2 -- Server, content, audio (10 tasks)

1. `backend/` skeleton: FastAPI + Docker Compose (api + postgres + minio) with `/healthz` returning 200.
2. Migrations: `content_packs`, `runs`, `run_events`, `decisions`, `quiz_results`, `rate_limits`.
3. `GET /v1/content/manifest` + signed immutable packs (hash-pinned).
4. `POST /v1/runs`, `/events` (partial accept), `/finish`.
5. `validate_pack.py` -- fairness invariants I1-I8 on publish, refusing bad packs.
6. Research endpoints: `/research/export` (CSV, `minCell>=5`), `/tell-matrix`, `/funnel`.
7. Rate limits, PII-key rejection, opt-out purge, log redaction.
8. Content JSON: 5 day plans, 12 email/attack templates, cover letters (human/bait/synthetic),
   Field Manual entries (5 days + 12 tells), dialogue pools, 5 ending texts, intro script.
9. **Audio** (see #4): SFX set + 2 music loops + ambience, delivered per the naming contract.
10. Android build + QA matrix: signed APK, 3 device profiles, save/quit/resume, offline, low storage.

## 4. Audio -- ownership contract (BE2 produces, FE plays)

BE2 owns the files and their format; FE owns *when they play*. This split only works if the names are
frozen up front.

**Delivery folder:** `unity/Assets/Audio/` (BE2 writes, FE reads)

| File | Type | Length | When it plays (FE wires this) |
|---|---|---|---|
| `sfx_stamp_approve.ogg` | SFX | 0.3 s | stamp press, approve |
| `sfx_stamp_reject.ogg` | SFX | 0.3 s | stamp press, reject |
| `sfx_page_turn.ogg` | SFX | 0.2 s | document tab change |
| `sfx_terminal_beep.ogg` | SFX | 0.1 s | tool use / menu confirm |
| `sfx_error.ogg` | SFX | 0.4 s | invalid action, wrong reason |
| `sfx_alarm.ogg` | SFX | 1.5 s | corruption level rise |
| `sfx_inbox.ogg` | SFX | 0.5 s | new email |
| `music_desk_calm.ogg` | music | 60 s loop | days 1-2 |
| `music_desk_tense.ogg` | music | 60 s loop | days 4-5, corruption >=1 |
| `amb_office.ogg` | ambience | 30 s loop | all desk scenes |
| `amb_crt.ogg` | ambience | 10 s loop | tool/monitor close-ups |

Rules: **OGG Vorbis**, 44.1 kHz, mono for SFX / stereo for music, SFX peak <= -3 dBFS, music <= -14 LUFS,
no leading silence, seamless loops (music/ambience). Total audio budget **<= 12 MB**.

**FE's obligation:** every sound must be reachable through one `AudioManager` with `PlaySfx(id)` /
`PlayMusic(id)`, volume groups for Music/SFX/Master, mute on app pause, and honour the accessibility
setting *"reduce audio cues"*. Missing clip = silent, never an exception.

## 5. Interface between you and BE1 (so you can build screens before systems exist)

BE1 delivers these as **interfaces/stubs first**, real implementations second. You code against the
interfaces, so no one waits:

```csharp
public interface IDeskDataSource {          // BE1 implements, FE consumes
    ApplicantRecord Current { get; }
    int Trust { get; }
    int Day { get; }
    int CorruptionLevel { get; }
    ToolState GetTool(ToolId id);           // charges left, last result
    DecisionResult Submit(DecisionInput input);
    DayResult EndDay();
    IEnumerable<ApplicantRecord> Schedule();
}

public interface ISaveService { bool HasSave { get; } void Save(); SaveData Load(); void Delete(); }
public interface IAudioService { void PlaySfx(string id); void PlayMusic(string id); void SetVolume(string group, float v); }
```

BE1 must merge stubs within the first two days (returns canned data), or FE cannot work. This is the
single most important coordination point of week 1.

## 6. Order of work (unchanged dates, new owners)

| Days | You (FE) | BE1 (systems) | BE2 (server/content/audio) |
|---|---|---|---|
| 1-2 | Menu + settings + character creation with placeholder art | Unity project, adapter, stubs, `SaveSystem` | FastAPI skeleton, content schemas started, SFX list |
| 3-6 | **Desk screen**: monitor-left / applicant-right, stamps, reason dropdown, transitions | `ContentLoader`, `GameManager`, `00_Boot`, analytics | Manifest + runs endpoints, day-1 content, first SFX |
| 7-11 | Inbox, Field Manual, day summary, endings, intro animation, corruption overlay | tool glue, EditMode tests, perf pass | remaining content, audio delivery, research endpoints |
| 12-14 | polish + device testing with BE2 | bug fixes, save hardening | Android build, QA matrix, event kit |

Day-6 gate unchanged: if the vertical slice is not solid, cut Day-5 composite content -> Reference Call
tool -> animated intro -> applicants 4->3 per day. Never cut save safety, responsiveness, debrief screens.
