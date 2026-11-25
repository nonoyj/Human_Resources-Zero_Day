# HR//0D — Project Status, Plan and Full Task List

**Updated:** 2026-11-25 · **Repo HEAD:** `dfea20f` · 23 commits · 53 tracked files · 9.7 MB content
**Engine:** Unity 6.3 LTS `6000.3.29f1` · Android · landscape · offline-first

---

## Part 1 — What we have been doing (chronological truth)

| # | Exchange | What was decided / built |
|---|---|---|
| 1 | Brief → plan | Repository skeleton, 14-day roadmap, tech stack spec, title shortlist, collaboration protocol, bug log, backlog, first handoff |
| 2 | Six decisions | Landscape (ADR-005), diegetic detection + imperfect tools (ADR-006), "product-quality, demo-sized content" with a Day-6 scope gate (ADR-004), 3 hired AIs = defeat (ADR-003), team profile (ADR-002), local git (ADR-001) |
| 3 | Design push | Rejection-reason dropdown, Field Manual as curriculum, human-bait as emotional core, fair-play rule (every AI must be provable on screen), escalating corruption, menu/settings spec, 5 ending archetypes |
| 4 | Layout + randomisation | Monitor-left / applicant-right desk (ADR-007), randomised runs proven fair (ADR-008) — `prototype/generator_sim.py`, 8 invariants, 0 violations / 46k applicants |
| 5 | Handoffs + tooling | Master brief for the three dev AIs, backend spec handoff, download database (D01–D17, live sizes), setup + workspace scripts |
| 6 | Analytics | Research instrument: 6 research questions, event taxonomy, 14 derived metrics, consent + privacy guardrails, backend addendum, "Export my play data" fallback |
| 7–11 | **Setup war** (agent's bugs) | BUG-001 non-ASCII `.ps1` under Windows PowerShell 5.1 · BUG-002 null `$PSScriptRoot`/env vars · BUG-003 bundle without `.git` · BUG-004 PATH not refreshed before verify · BUG-005 wrong `PlaybackEngines` path · BUG-006 "not on PATH" ≠ "not installed" → **your machine now verifies 10/10 PASS** |
| 12 | Core built | Pure-C# generator, scoring, shift loop, save schema, analytics models, 15 tests — **compiled and executed**, 2,000-seed soak, 0 violations, C# matches Python |
| 13 | Budget incident | 636 MB SDK in the workspace (BUG-008), then committed (BUG-009) → workspace back to 9.7 MB, rules codified |
| 14 | Update workflow | In-place updates instead of reinstall; LFS removed (BUG-010); copy/nesting defects fixed and proven against a damaged copy (BUG-011) |

**Net position:** design is locked, environment is green, gameplay core is **written and tested**, distribution tooling has cost us most of the friction and is now bootstrapped out of the loop.

---

## Part 2 — What we are about to do (next 3 moves)

1. **Unblock distribution once** — run the bootstrap `Apply-Bundle.ps1` (no dependencies, cannot be defeated by an old script), then **push to GitHub** so bundles stop existing.
2. **Pixel-art kit** — UI 9-slices, 3 corruption-level backgrounds, applicant sprite sheets (192×216), stamps, icons, logo. Gives Dev B something real to build screens against.
3. **Unity glue layer** — `SaveSystem` (atomic), `ContentLoader` (StreamingAssets + hash), `AnalyticsService` + `FakeBackend`, `GameManager`, `00_Boot` scene, plus `UnityTestAdapter.cs` so the 15 core tests appear in Unity's Test Runner.

Then Phase 2 of the roadmap: the vertical slice (Day 1–2 playable on a phone).

---

## Part 3 — Full task list

Legend: **[x]** done · **[~]** in progress · **[ ]** to do · Owner: **A** systems, **B** UI, **C** content/build, **G** agent

### 3.1 Foundations & docs
- [x] Repository, `.gitignore`, `.gitattributes`, README (G)
- [x] 14-day roadmap with roles, risks, cadence (G)
- [x] Tech stack, folder layout, data contracts, art spec, palette (G)
- [x] 8 ADRs — every locked decision (G)
- [x] Collaboration protocol: git rules, handoff template, Definition of Done (G)
- [x] Bug log with 11 entries + 10 standing prevention rules (G)
- [x] Retrospective + self-assessment (G)
- [x] Analytics/research spec (G)
- [x] Backend spec + research addendum (G)
- [x] Behaviour model (applicant across the table) (G)
- [x] Randomisation spec + fairness invariants (G)
- [x] Title locked: **HUMAN RESOURCES: ZERO DAY** (you)

### 3.2 Environment (your machine)
- [x] Unity Hub, Editor 6000.3.29f1, Android SDK/NDK/OpenJDK, VS 2022 + Unity workload, Git/LFS client, gh, adb, scrcpy, Python — **10/10 PASS** (you)
- [x] Scripts hardened + parse/encoding gate (G)
- [ ] Long paths enabled (admin, one line) (you)
- [ ] `gh auth login` + first push (you)
- [ ] Dev B and Dev C run `Setup-DevEnvironment.ps1 -Verify` (B, C)

### 3.3 Gameplay core (verified)
- [x] `RunGenerator` — seeded, validated 1:1 against the Python reference (A/G)
- [x] `RunValidator` — invariants I1–I8 (G)
- [x] `TellCatalog` — 12 tells + surface-unlock table (G)
- [x] `ApplicantRecord` — computed contradictions (G)
- [x] `ScoringService` — right/wrong call, right/wrong reason, trust economy (G)
- [x] `ShiftController` — day loop, defeat/survive, save/restore (G)
- [x] `SaveData` v1 + migrations (G)
- [x] Analytics event models (G)
- [x] 15 EditMode tests + headless runner (G)
- [ ] `UnityTestAdapter.cs` wrapper (A)
- [ ] `SaveSystem` — atomic write, autosave, corruption recovery (A)
- [ ] `ContentLoader` — StreamingAssets JSON on Android, hash verify (A)
- [ ] `AnalyticsService` + `FakeBackend` + local outbox (A)
- [ ] `GameManager` + `00_Boot` scene (A)
- [ ] RuleEngine presentation glue: tells → inspectable hotspots (A+B)

### 3.4 Content (data, not code)
- [x] Applicant/email/save schemas in the tech-stack doc (G)
- [ ] `content/schema/*.json` formal schemas (C)
- [ ] 5 day-plans authored: tell pools, distractor pools, weight tuning (C)
- [ ] 12+ email/attack templates with refusal/legit variants (C)
- [ ] Cover letters: human / bait / synthetic voice variants (C)
- [ ] Field Manual: 5 day entries + 12 tell entries (C)
- [ ] Dialogue pools per archetype (2 lines each) (C)
- [ ] Endings: 1 win + 4 defeat texts, each with a "what you could have done" hint (C)
- [ ] Intro story script (skippable) (C)

### 3.5 Art (agent generates, FE integrates)
- [x] Palette + swatch (G) — fonts still to choose
- [x] **UI 9-slice kit v1: panels, 6 button states, inset, tooltip, bars, pips, slider** (G)
- [x] **Office backgrounds x3 (clean / glitch / red alert)** (G)
- [x] **Desk furniture, monitor bezel, document sheet, ID card, table** (G)
- [x] Stamps x2, 9 icons x2 states, contact sheet, palette checker (G)
- [ ] Applicant sprites: 192×216, 4 idle frames, 3 reaction frames ×6 archetypes (G)
- [ ] Portrait crops 96×96 with synthetic artifacts (G)
- [ ] Stamps: APPROVE / REJECT + animation (G)
- [ ] Icons: 3 tools, inbox, manual, pause, settings (G)
- [ ] Logo / wordmark `HR//0D` + app icon (G)
- [ ] Player avatar: 4 bodies, 6 hair, 6 skin, 4 outfits (G)

### 3.6 Screens (FE = you)
- [ ] Boot / loading (FE)
- [ ] Intro sequence + skip (B)
- [ ] Main menu: Continue / New Shift / Field Manual / Settings / Credits (B)
- [ ] Character creation: name + appearance (B)
- [ ] **Desk** (the game): monitor-left / applicant-right (B)
- [ ] Applicant review + zoom inspect + reason dropdown (B)
- [ ] Tool row with daily use pips (B)
- [ ] Inbox + email assessment (B)
- [ ] Field Manual reader (B)
- [ ] End-of-day summary (facts / hint / taught concept) (B)
- [ ] Ending screens ×5 (B)
- [ ] Settings: audio, text speed, reduce motion, colour-blind, font scale, handedness, reset (B)
- [ ] Pause + resume-where-you-were (B)
- [ ] Transitions + corruption overlay driven by `CorruptionLevel` (B)

### 3.7 Audio
- [ ] SFX set: stamp, page turn, terminal beep, alarm, error (C)
- [ ] Music: 2 loops (calm desk / escalating) (C)
- [ ] Ambience: office hum, CRT (C)
- [ ] AudioManager persisting across scenes (B)

### 3.8 Backend (separate AI team)
- [ ] FastAPI skeleton + Docker Compose + `/healthz` (backend)
- [ ] Content pack manifest + signed immutable packs (backend)
- [ ] `runs` / `run_events` / `decisions` / `quiz_results` tables (backend)
- [ ] Pack fairness validator (I1–I8) on publish (backend)
- [ ] Research export CSV + tell-matrix + funnel endpoints (backend)
- [ ] Rate limits, PII-key rejection, opt-out purge (backend)
- [ ] Staging deploy + OpenAPI + fixtures (backend)

### 3.9 QA, build, event
- [ ] Device matrix: 720×1280, 1080×2400, tablet (C)
- [ ] Save/quit/resume, background-kill, offline, low-storage tests (C)
- [ ] 60 fps on mid-range Android (A)
- [ ] Signed APK `0.1.0-demo`, arm64+armv7 (C)
- [ ] Sideload instructions + scrcpy booth setup (C)
- [ ] Event kit: 3-min pitch, known-issues card, feedback form (you + G)
- [ ] Research export dry-run at the event (C)

### 3.10 Deliberately not doing (see `docs/91-backlog.md`)
Cloud saves · accounts · leaderboards in v1 · localisation beyond English · endless mode ·
voice/video interview rounds · store listing · analytics dashboards beyond the four required

---

## Part 4 — Critical path (what actually decides whether we ship)

```
environment ✔  →  core ✔  →  art kit  →  desk screen  →  save/resume  →  Android build  →  content pass  →  QA
                                    ↘  Unity glue layer ↗
```

Day-6 scope gate: if the vertical slice is not solid, cut in this order — Day-5 composite content,
REFERENCE CALL tool, animated intro → static storybook, applicants 4→3/day. **Never cut** save safety,
responsiveness, or the debrief screens.
