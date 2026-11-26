# Tech Stack & Project Specification

## Engine

| Item | Choice | Notes |
|---|---|---|
| Unity | **6.3 LTS — 6000.3.29f1** | Pin exactly. `ProjectSettings/ProjectVersion.txt` is committed. All 3 machines must match |
| Modules | Android Build Support + SDK/NDK tools + OpenJDK (installed via Hub) | Verify with Dev C on Day 1 |
| Render pipeline | **URP 2D** (Universal 2D template) | 2D Renderer, no post-processing beyond a bloom-less "scanline" overlay done in-shader |
| Scripting backend | **IL2CPP**, **arm64 + armv7**, API 24 min / API 35 target | Mono only for fast local iteration if build times hurt |
| Input | **Input System** package (1.14+), one `InputActions` asset | Touch primary, mouse fallback for editor testing |
| Animation | **Animator + Timeline** (intro), **DOTween** for UI tweening | DOTween free is fine; add to `Assets/Plugins` |
| Text | **TextMeshPro** | All strings from JSON; `TMP_FontAsset` with a pixel font |
| Audio | `AudioManager` singleton + pooled `AudioSource` | No third-party audio middleware |
| Save | JSON + `JsonUtility`, written atomically to `Application.persistentDataPath` | Version field for migrations |
| DI/architecture | Lightweight: `ServiceLocator` + ScriptableObject configs. **No** heavy framework | Avoid over-engineering in 14 days |
| Testing | Unity Test Framework — EditMode tests for the rule engine only | The rule engine is where bugs hide; UI is manual-tested |

## Packages (`Packages/manifest.json`)

```
com.unity.render-pipelines.universal
com.unity.inputsystem
com.unity.textmeshpro            (built-in in U6, still referenced)
com.unity.ugui
com.unity.2d.sprite / 2d.tilemap / 2d.animation / 2d.pixel-perfect
com.unity.test-framework
com.unity.timeline
com.unity.localization         (install Day 1, use later)
```
Add via Package Manager UI or `manifest.json`; **commit `manifest.json` + `packages-lock.json`**.

## Folder layout (`Assets/`)

```
Assets/
├─ Art/
│  ├─ Sprites/{UI,Characters,Applicants,Props,Documents}
│  ├─ Backgrounds/
│  ├─ Fonts/
│  └─ Animations/
├─ Audio/{SFX,Music,Ambience}
├─ Prefabs/{Screens,Widgets,Applicants,Emails}
├─ Scenes/
│  ├─ 00_Boot.unity          (loading + save init)
│  ├─ 01_Intro.unity         (animated story, skippable)
│  ├─ 02_MainMenu.unity
│  ├─ 03_CharacterCreate.unity
│  ├─ 04_Desk.unity          (the game: review, stamps, inbox, manual)
│  └─ 05_Ending.unity
├─ Scripts/
│  ├─ Core/        (GameManager, DayManager, SaveSystem, ServiceLocator, EventBus)
│  ├─ Data/        (ScriptableObjects + DTOs that mirror the JSON)
│  ├─ Rules/       (RuleEngine, rules/, Evidence)
│  ├─ Content/     (ContentLoader, JSON schemas, validators)
│  ├─ UI/          (screen controllers, animations, transitions)
│  ├─ Audio/
│  └─ Editor/      (build scripts, content validators, art import presets)
├─ Settings/       (URP assets, InputActions, ScriptableObject configs)
├─ Resources/      (JSON content — see note)
└─ StreamingAssets/ (only if we need to hot-swap content without a rebuild)
```

**Content loading:** JSON lives in `Assets/StreamingAssets/content/` for v1 — it ships in the
APK, can be edited without touching C#, and is readable with `UnityWebRequest` on Android
(`Resources/` + `TextAsset` is the alternative; StreamingAssets is simpler for writers).
Wrap it in a `ContentLoader` so we can swap later.

## Naming conventions

- Classes: `PascalCase`; private fields `_camelCase`; constants `UPPER_SNAKE`.
- ScriptableObjects: `SO_<Name>`; prefabs `P_<Screen>_<Thing>`; scenes numbered as above.
- Sprites: `<category>_<name>_<size>.png` e.g. `ui_button_primary_48x48.png`.
- One public class per file, file name = class name.

## Art spec

| Item | Spec |
|---|---|
| Base canvas | **480 × 270**, landscape, integer scaling only (Pixel Perfect Camera). See ADR-005 |
| PPU | 16 |
| Portraits | 96 × 96, 1px outline, 4-shade shading |
| Applicant full-card | 384 × 216 (resume sheet) |
| Filtering | Point (no filter), compression **None**, Mip Maps **off** |
| Colour space | **Gamma** for crisp pixel art (not Linear) |
| Format | PNG, transparency, no alpha bleeding |

### Palette (`HR//0D` 20 colours)

```
#0b0e13  void        #131926  panel       #1d2735  panel-2
#2b3a4d  line        #3f5570  line-hi     #5d7ea1  muted
#a9c7e4  text-dim    #e6f1ff  text        #ffffff  white
#18e0b0  signal      #0fa88a  signal-dim  #ffd166  warn
#ff8a3d  orange      #ff4757  danger      #a11a2b  danger-dim
#7b61ff  synth       #b39dff  synth-dim   #6ee7c8  ok
#8a6d3b  paper-dim   #f2e8d5  paper       #2a2118  ink
```
- **Rule:** AI tells must never be conveyed by colour alone (accessibility). Use shape/motion/text too.
- Fonts: one monospace pixel font for the terminal UI (`Departure Mono` / `Pixel Operator Mono`
  and one humanist pixel font for documents. Both must be licensed for commercial use — I'll
  shortlist OFL fonts and generate fallback bitmap glyphs if needed.

## Data contracts (freeze these before coding)

```jsonc
// content/applicants/day_01.json
{
  "day": 1,
  "quota": 3,
  "applicants": [{
    "id": "d1_a01",
    "isSynthetic": false,
    "groundTruth": "human",              // human | synthetic | human_bait
    "portrait": "app_marisol_d1",
    "fields": [
      { "key": "name",        "label": "NAME",       "value": "MARISOL V. TAN", "editable": false },
      { "key": "age",         "label": "AGE",        "value": "27" },
      { "key": "id_no",       "label": "ID NO.",     "value": "PH-4402-8841" },
      { "key": "exp_years",   "label": "EXP. YRS",   "value": "8" },
      { "key": "grad_year",   "label": "GRAD YEAR",  "value": "2019" },
      { "key": "referral",    "label": "REFERRED BY","value": "PUBLIC POSTING" }
    ],
    "evidence": [
      { "id": "ev1", "type": "contradiction", "target": "exp_years",
        "note": "8 years exp vs graduated 2019", "severity": 1 }
    ],
    "voiceSample": "text used for the writing-voice tell (optional)",
    "manualEntry": "manual_doc_contradiction"   // unlocks/points at the Field Manual entry
  }]
}
```

```jsonc
// content/emails/day_02.json
{ "day": 2, "emails": [{
  "id": "d2_m1", "from": "HR.DEPT@NEXUS-SEC.gov", "displayName": "HR Department",
  "subject": "URGENT: Database access request",
  "body": "...", "attachments": [{ "name": "roster.pdf", "risk": "macro" }],
  "trueSender": "hr.dept@nexus-sec.gov.co",   // lookalike TLD
  "isMalicious": true,
  "evidence": [{ "type": "lookalike_domain", "note": ".gov.co vs .gov" }],
  "actions": ["open_link", "reply", "report", "ignore", "grant_access"]
}]}
```

```jsonc
// SaveFile (Application.persistentDataPath/save_01.json)
{
  "saveVersion": 1,
  "profile": { "name": "ACE", "avatar": { "body": 2, "hair": 4, "skin": 3, "outfit": 1, "accent": "#18e0b0" } },
  "day": 3, "trust": 72, "wrongfulRejections": 1, "syntheticsHired": 1,
  "approvedIds": ["d1_a02"], "rejectedIds": ["d1_a01"],
  "ruleStats": { "contradiction": { "hits": 4, "misses": 1 } },
  "manualSeen": ["manual_doc_contradiction", "manual_lookalike_domain"],
  "flags": { "introSeen": true, "tutorialDone": true },
  "seed": 882731,
  "timestamp": "2026-11-25T13:00:00Z"
}
```

## The rule engine (the heart — get this right)

```
RuleEngine.Evaluate(ApplicantRecord a, RuleContext ctx) -> List<Episode> 
```
- Rules are `IRule` implementations, registered in a list, **data-driven where possible**
  (`ContradictionRule` reads `evidence[]`; `PortraitArtifactRule` reads sprite metadata JSON;
  `VoiceRule` scores the text sample against a human/AI writing profile).
- Output is a list of **episodes** (what a player could legitimately notice), each with
  `category`, `severity`, `manualEntry`. The UI surfaces episodes as *inspectable hotspots*
  (a magnifier over a field), never auto-flags.
- `ScoringService` compares the player's submitted reason(s) against the true evidence set:
  - right call + right reason = full credit (teaches)
  - right call + wrong reason = credit, but the debrief says "for the wrong reason" (still learning-relevant)
  - wrong call = penalty, with the true evidence revealed at end of day
- **Keep this pure C#, no UnityEngine dependencies** → EditMode-testable, and portable to a web
  build if you want a playable browser prototype for playtests in week 1.

## Determinism & content generation

- Every shift is seeded (`seed` in save) so levels replay identically for debugging.
- Template pools per tell, so Day 4 isn't identical to Day 2 with different names.
- Difficulty = number of concurrent tells, clarity of tells, and distractor noise — **not**
  faster timers or unfair ambiguity.

## Orientation & desk composition (ADR-005, ADR-007)

World layout on the 480x270 canvas:

```
+------------------------------------------------------------------+
|  HUD: DAY 1   SHIFT CLOCK   TRUST   [pause]                      |
+---------------------------------+--------------------------------+
|  LEFT HALF  = YOUR MONITOR      |  RIGHT HALF = THE APPLICANT     |
|  - resume sheet (384x216)       |  - waist-up seated sprite       |
|  - cover letter / ID card       |    (192x216)                    |
|  - tabs: RESUME | LETTER | ID   |  - chair + table edge           |
|  - tool row (3 tools + pips)    |  - behaviour loop (breathe,     |
|  - stamp tray (approve/reject)  |    blink, fidget, react)        |
|  - reason dropdown              |  - 0-2 short dialogue lines     |
+---------------------------------+--------------------------------+
|  [back]  FIELD MANUAL   INBOX(1) |  CORRUPTION/RISK INDICATOR     |
+------------------------------------------------------------------+
```

Design consequences:
- Both halves are always visible at once — the player can watch the applicant while reading the
  document. This is the core of the fantasy and of the "observation" lesson.
- Any zoom/inspect layer must be anchored to the **left** half so the applicant stays visible.
- Safe-area insets: keep 24 px (at 480x270) clear on all sides for notch/cutout handling.
- The right half is the most expensive art asset (animated applicant); budget it accordingly.

## Build settings checklist (Android)

- [ ] **Landscape** (auto-rotate locked), Resizable window off, Render outside safe area off
- [ ] Min API 24, Target API 35, IL2CPP, arm64+armv7
- [ ] Scripting: .NET Standard 2.1, IL2CPP code generation: Faster (smaller builds) — switch to Faster runtime for release if perf needs it
- [ ] Managed stripping: Low (avoid reflection surprises in JsonUtility)
- [ ] Keystore created and stored in a password manager (never in the repo)
- [ ] `bundleVersion 0.1.0`, `AndroidBundleVersionCode` incremented per build
- [ ] Test on: 1 low-end (2 GB RAM), 1 mid (4 GB), 1 recent flagship, tablet if possible

## Project layout (corrected 2026-11-26)

**`unity/` IS the Unity project.** Open *that* folder in Unity Hub, never the repo root.

```
HR-ZeroDay/                 <- git repo. Not a Unity project. No Assets/ here, so Hub refuses it.
  art/                      <- authoring source for the sprites (python generates it)
  docs/  handoff/  tools/   <- everything the game is not
  unity/                    <- OPEN THIS IN UNITY
    Assets/                 <- Art, Content, Resources, Scripts (Core, Game, Editor, Tests)
    Packages/manifest.json  <- URP 2D, Input System, pixel-perfect, uGUI
    ProjectSettings/        <- 480x270, Android 24/35, IL2CPP, Force Text serialization
```

Why this changed: `ProjectSettings/` was being generated at the repo root (whatever folder the operator
happened to be standing in), while `Assets/` was always in `unity/`. Hub rejects a folder with settings
but no `Assets/` — "Please add a valid project" — so the project could never be opened at all.
`scripts/Promote-UnityProject.ps1` makes `unity/` self-contained; `scripts/Adopt-UnityProject.ps1` is
superseded by it except as the `-Source` copier.
