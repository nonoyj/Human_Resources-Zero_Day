# 14-Day Roadmap -> Playable Demo (Event Ready)

**Assumptions:** 3 people, part-time (assume ~4-6 focused hours/day each; if it's more, pull
the stretch goals in). Unity 6.3 LTS (6000.3.29f1) with Android Build Support, Windows/PowerShell.

## Roles

| Role | Owner | Primary responsibilities | Never touches |
|---|---|---|---|
| **A -- Systems / Gameplay** | Dev A | Day loop state machine, applicant generator, validation rules, save/load, scoring & suspicion, defeat/win conditions | Art files, final copy |
| **B -- UI / Frontend** | Dev B | All screens & prefabs, animations & transitions, input/touch UX, responsive mobile layout, audio hooks | Gameplay logic internals |
| **C -- Content / Systems support** | Dev C | JSON content (applicants, emails, warnings, manual), narrative & intro sequence script, Android builds, QA matrix, localization-safe strings | Core code in `Scripts/Core` |

**Me (Arena agent):** architecture, art generation, review, debugging, docs, handoffs,
integration -- and the "tie-breaker" on design calls. I can write complete C#/JSON/content
here in the workspace; you copy or pull it into the Unity project.

> **Hard rule:** Day 1 = contracts. Nobody writes gameplay or UI until `docs/05-data-contracts.md`
> is agreed, because A and B will otherwise build incompatible screens.

---

## Phase 1 -- Pre-production & contracts (Days 1-2)

**Goal:** one shared skeleton that A, B and C can build on without merge pain.

- [ ] Ideas locked: title, art direction, input model, save model (**decisions in `00-open-questions.md`**)
- [ ] Unity project created, `ProjectSettings` committed, `.gitignore` verified (Library/ must NOT be committed)
- [ ] Git LFS enabled **before** first art import (`.gitattributes`)
- [ ] Branch protection + PR template + `handoff/` folder convention
- [ ] Data contracts frozen: `ApplicantRecord`, `EmailRecord`, `WarningRecord`, `ShiftResult`, `SaveFile`
- [ ] Art spec frozen: 320x180 base canvas, PPU 16, palette (see `02-tech-stack.md`), 9-slice UI rules
- [ ] Grey-box: one screen that shows an applicant card with text only + Approve/Reject buttons

**Exit test:** a fresh clone opens in Unity 6.3 with zero console errors, and a teammate can
launch the grey-box scene on their machine following only the README.

---

## Phase 2 -- Full vertical slice (Days 3-6)

**Goal:** Days 1-2 of the game are *actually playable on an Android phone*, end-to-end, with
placeholder art. This is the single most important milestone: a slice you can hand to a judge.

- [ ] Core loop: `DayManager` -> N applicants -> stamp approve/reject -> day summary -> next day
- [ ] Applicant review screen: resume with inspectable fields (name, age, ID number, photo,
      employment history, references, referral source)
- [ ] **Detection layer v1**: 2 rule types (photo/voice naturalness flag, and one text-based
      contradiction like "graduated 2019" + "12 years experience")
- [ ] Side task v1: **Inbox** -- 1 email per day with a link/sender to assess (phish or legit)
- [ ] Field Manual screen (the teaching device: each new tell is explained once, then tested)
- [ ] Suspicion/pressure meter, mistakes counter, wrong-firing consequences
- [ ] Save/resume (JSON in `Application.persistentDataPath`), autosave at each transition
- [ ] Real art pass on the 3 core screens; main menu functional (no polish yet)
- [ ] Android internal build: debug APK, landscape **and** portrait decided, touch input verified

**Exit test:** a stranger plays 10 minutes on a phone without verbal instructions and reaches
the day-2 summary. Anything they fumble becomes a UX bug ticket.

---

## Phase 3 -- Content, narrative, polish (Days 7-11)

- [ ] Intro animated story (skippable) + logo -> menu transition
- [ ] Character creation: name entry + appearance selection (badge, portrait, pronouns), no face morph
- [ ] All 5 shifts authored in JSON, escalating: each day introduces exactly **one new attack vector**
  1. Obvious synthetic applicant (learn the basic tells)
  2. Document forgery / mismatched ID (teach cross-referencing)
  3. Human *bait* -- a real person with a real resume applying on behalf of the AI (social pressure)
  4. Email attack -- impersonation of a colleague, urgent request, lookalike domain
  5. Composite: cloned identity + collusion + "golden opportunity" trap (fast-track hire request)
- [ ] End-of-day summary screen with hints (never a direct "you hired an AI" statement)
- [ ] Fire/remove flow via inbox (the AI you hired can be removed -- at a cost)
- [ ] Win + 4 distinct defeat endings, each with a "what you could have done" hint
- [ ] Settings: audio sliders, text speed, language stub, haptics, color-blind safety, reduce motion, reset save
- [ ] Accessibility: min touch target 48 dp, no colour-only signalling, skip/auto-advance on cutscenes
- [ ] Audio: SFX set + one loop per mood
- [ ] Performance pass: 60 fps on a mid-range Android (target: 2 GB RAM, 2020-era phone)

---

## Phase 4 -- QA, build, ship the demo (Days 12-14)

- [ ] Test matrix (`docs/06-qa-matrix.md`): 3 device profiles, portrait/landscape, low storage,
      background/foreground interruptions, offline, save corruption, rapid double-tap on stamps
- [ ] Bug bash: only P0/P1 fixed; everything else documented as known issues
- [ ] Release build, signed, versioned `0.1.0-demo`, arm64 + armv7
- [ ] Sideload instructions (APK, no store listing needed) + QR/hosted link
- [ ] Event kit: 3-min pitch script, "what to say while they play", feedback form, known-issues card
- [ ] Post-build retrospective -> `docs/92-retrospective.md`

---

## What we deliberately cut (and can add back after the event)

- Voice/audio-video interviews (capture is expensive on mobile)
- Languages beyond EN (but all strings externalised from Day 1 so PH/CEB/TL can be added)
- Cloud save, leaderboards, achievements
- Animated applicant "idle" loops beyond 2-frame breathe
- Anything requiring a server -- **the AI runs on top of generation templates, not an API call**

## Risk register

| Risk | Likelihood | Impact | Mitigation |
|---|---|---|---|
| Unity version mismatch between machines | High | High | Commit `ProjectSettings/ProjectVersion.txt`; pin exact 6000.3.29f1; document install steps |
| Merge conflicts in scenes/prefabs | High | High | One owner per scene; prefab-per-screen; force text serialization (YAML) + LFS |
| Art volume larger than expected | Medium | Medium | Placeholder-first pipeline; agent generates art in batches; no sprite is a blocker |
| Content authoring becomes the bottleneck | Medium | High | Content schema + authoring checklist ready Day 1; C owns it from Day 2 |
| Scope creep from "wouldn't it be cool if" | Very high | High | Anything not in this doc goes to `docs/91-backlog.md`, not into the code |
| Android build machine not set up | Medium | High | Dev C sets up the toolchain on Day 1 in parallel with the rest |

---

## Cadence

- **Daily 10-min standup** (async text is fine): yesterday / today / blocked.
- **End of each phase = an integration window** where only A + the agent merge and test.
- Never start polish while the slice is broken. **Playable > pretty.**
