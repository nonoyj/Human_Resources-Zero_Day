# 18 -- Today's plan + the animation spec we actually committed to

**Written 2026-11-26.** Answers four questions the user asked in one message: what animations we are
doing, when Unity starts, today's phases, and what the backend devs get.

---

## 1. Animations -- what is IN, what is OUT

Everything here is **sprite-and-tween**, not skeletal, not 3D, not Timeline-authored cutscenes.
The whole game is a 480x270 pixel-art office; animation is used for *life* and *feedback*, never for
spectacle we cannot afford to hand-animate.

### 1a. Applicant idle (the one that carries the game's core tell)

`docs/08-behaviour-model.md` #3 is the authority. The applicant has **2 idle frames** and everything
else is timing, which is the point: a synthetic is caught by *rhythm*, not by a special pose.

| Animation | Frames | Driven by | Notes |
|---|---|---|---|
| Breathe | 2 | cycle 5-7 s (human) / **exactly 6.0 s** (synthetic) | randomised phase per applicant, so a row of applicants never syncs |
| Blink | 2 (open / closed) | interval 2.5-6 s human, **9-14 s** synthetic, jitter high vs very low | this is the cheapest, strongest synthetic tell |
| Fidget | 0 extra frames | chance/min 12-60% human, **0%** synthetic | motion = a 1-2 px offset + a lean, tweened |
| Look-away | 0 extra frames | 20-50% human, **0%** synthetic | eye/head layer only |
| Sweat / tremor additive | 1 overlay each | nervous / bait only | `art/kit/fx/` |
| Stare reaction | -- | after ~4 s of the magnifier on them | humans only; synthetics never react |

**Cut for scope (locked, `docs/01` "deliberately cut"):** idle loops longer than 2 frames, walk-in
animation beyond a 3-frame slide, and any applicant animation on the Desk other than the above.
**Accessibility:** *reduce motion* disables breathe + fidget and keeps blinks.

### 1b. UI / transitions (FE owns -- this is your week)

| Animation | Where | How |
|---|---|---|
| Stamp press + impact shake | HIRE / REJECT stamp | tween down 3 px + a 1-frame screen nudge + ink burst (`fx/` set 2, still to draw) |
| Letter page slide-in / out | the paperwork (option c) | 240 px slide + 120 ms fade; the CRT stays visible behind |
| Card repaint flicker | CRT when the day/trust/applicant changes | 2-frame brightness pulse, then the new buffer |
| Day->day wipe | transition | horizontal CRT-style wipe, ~350 ms |
| Menu -> desk | scene transition | fade + subtle scanline sweep |
| Corruption drift | `CorruptionLevel` 1..3 | one overlay sprite cross-fading over 1.5 s (already drawn: `fx/wear_a`, `fx/wear_b`) |
| Intro | opening | **animated**, skippable -- but it is first on the Day-6 cut list (# `docs/06` ADR) |

Tooling, already decided: **Unity Animator** for the applicant frame loops, **DOTween free** for all UI
tweening (`Assets/Plugins`), **Timeline only if** the intro survives the Day-6 gate.

### 1c. What we are NOT doing

No skeletal/2D rigging, no Timeline cutscenes outside the intro, no particle systems, no parallax
camera, no animated backgrounds, no voiced dialogue.

---

## 2. When Unity starts -- the honest answer

**Unity starts on backend dev 1's first task, not on yours, and that is why it has not started yet.**
A Unity project needs `ProjectSettings/` + `Packages/`; the repo deliberately has neither, because a
wrong `ProjectSettings` is a day of merge pain. It is a 20-minute job **once someone sits at a PC with
Unity Hub installed**.

The ordering that actually works:

| Step | Who | Unblocks |
|---|---|---|
| 1. Create the empty project from the pinned editor, copy `ProjectSettings/` + `Packages/` into the repo, commit | **BE1** | everything in Unity |
| 2. Drop in `UnityTestAdapter.cs`, EditMode tests green | **BE1** | confidence the core survives Unity |
| 3. `GameManager` + `IDeskDataSource` **stub** returning canned data | **BE1** | **your** scenes: desk, stamps, letter |
| 4. Desk scene, monitor + applicant, stamps, letter page, transitions | **you (FE)** | the vertical slice |
| 5. Save, boot scene, content loader | **BE1** | playable session |

So: **the moment BE1 commits step 1, you can open the project.** Everything you and I have built so far
(monitor card, raster, letter page, flags, clock) is code under `Assets/Scripts/**` -- it compiles
outside Unity today and will compile inside it unchanged.

! **If BE1 is not at a PC with Unity yet,** there is a shortcut worth taking: you create the project
yourself (the steps are in `docs/13-team-plan.md` #2) and commit it, then BE1 owns it from there. That
is a 20-minute job and it removes the only hard block on your week. Say the word and I'll write the
exact click-by-click.

---

## 3. Phases left, and the steps in each

Day numbering is from the 14-day roadmap; we are at **end of Day 2**.

### Phase 1 -- Foundation (Days 1-2) - **95% done**
- [x] Locked scope, 5 days, 12 events, 5 endings, 3-AI defeat
- [x] Core C# gameplay (generator, scoring, save, invariants) -- **56 tests**
- [x] Art kit v2: 80 sprites, palette-gated, ragged style, desk composition L
- [x] Monitor: font atlas, raster, card view, letter page + flags
- [x] Bundle/apply pipeline (this is what kept breaking, now rehearsed end-to-end)
- [ ] **Unity project exists** <- BE1 step 1, the last item

### Phase 2 -- Vertical slice (Days 3-6) - **0% -- starts when the project exists**
1. Desk scene: monitor-left / applicant-right, 480x270, Point filtering
2. `IDeskDataSource` wired to the real `ShiftController`
3. Stamps (HIRE / REJECT) + reason dropdown + submit
4. Letter page open/close by touch, flags toggle, clock at 25%
5. Card repaint on applicant change; day header; trust bar
6. Save/resume + autosave at every transition
7. Debug APK on one phone, landscape, touch verified
- **Gate (Day 6):** a stranger plays 10 minutes on a phone, unassisted, and reaches the day-2 summary.

### Phase 3 -- Content, narrative, polish (Days 7-11)
1. Intro (animated if the gate allows, else storybook+fades) -> menu
2. Character creation: name + cosmetics, **no face morph** (locked)
3. 5 shifts authored as JSON, one new attack vector per day
4. Inbox (1 email/day) tied to the paper-flag mechanic
5. Field Manual: each tell taught once, then tested
6. Day summary + 5 endings with "what you could have done"
7. Settings + accessibility (48 dp targets, no colour-only signalling, reduce motion)
8. Audio: SFX + one loop per mood (BE2)
9. Perf: 60 fps on a 2020-era mid-range phone

### Phase 4 -- QA and ship (Days 12-14)
1. Device matrix, portrait/landscape, interruption, offline, corrupt save, double-tap stamps
2. P0/P1 only; everything else becomes a known issue
3. Signed release APK `0.1.0-demo`, arm64 + armv7
4. Sideload instructions + QR
5. Event kit: 3-min pitch, what to say while they play, feedback form
6. Retrospective -> `docs/92-retrospective.md`

---

## 4. What the backend devs do -- and what you send them

**Nothing. That is the design, and it is already the rule in `docs/04-collaboration-protocol.md`: the
repo is the handoff.** They clone once; after that they `git pull`.

| Who | Their 10 tasks (in order) | Full text |
|---|---|---|
| **BE1 -- Systems** | project skeleton -> UnityTestAdapter -> SaveSystem -> ContentLoader -> AnalyticsService -> GameManager -> 00_Boot -> rule-engine glue -> EditMode tests -> profiling | `docs/13-team-plan.md` #3 |
| **BE2 -- Server, content, audio** | FastAPI skeleton -> migrations -> content manifest -> runs/events -> `validate_pack.py` (I1-I8) -> research endpoints -> rate limits/PII -> content JSON (5 days, 12 events, letters, Field Manual, endings, intro) -> audio -> Android build + QA | `docs/13-team-plan.md` #3, #4 |

### What you actually send them (three things, once)

1. **The repo URL** -- `https://github.com/nonoyj/Human_Resources-Zero_Day` (you push; they pull).
2. **The current bundle zip** -- only if they cannot reach GitHub. It contains the whole repo plus
   `history.bundle`, so one apply gets them to the same commit you are on.
3. **Their handoff file** -- already written, in the repo:
   - BE1 -> `handoff/2026-11-26_agent-to-BE1_systems-tasks.md`
   - BE2 -> `handoff/2026-11-26_agent-to-BE2_content-server-audio-tasks.md`

### Why they never need a manual upload from you

- Art, code, docs, handoffs and the bundle manifest all live in git; `git pull` is the whole protocol.
- Their AI reads the repo like any other folder -- the guides are `docs/13-team-plan.md` (what to build),
  `docs/02-tech-stack.md` (how), `docs/04-collaboration-protocol.md` (who may edit what), and the two
  handoff files (today's task).
- **One rule to tell them explicitly:** pull *before* they start, and never edit `Scripts/Core/**`
  (that is frozen, tested code -- a change there needs a handoff note, not a direct edit).

### What they must NOT wait for

BE1's stubs (`IDeskDataSource`, `ISaveService`, `IAudioService`) returning canned data are enough for
you to build every screen. If BE1 has not merged stubs by Day 4, that is the escalation, not the art.
