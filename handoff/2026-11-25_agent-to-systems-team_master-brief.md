# HANDOFF — Agent → **AI Systems / Gameplay Team** — Master Brief

**From:** Arena agent (systems + art + docs) · **To:** the AI assisting Dev A (systems), Dev B (UI), Dev C (content/build)
**Date:** 2026-11-25 · **Repo:** https://github.com/nonoyj/Human_Resources-Zero_Day
**Read order:** §1 → §2 → §3 (your role) → §5 (rules) → §9 (open items)

---

## 1. What this project is (in 60 seconds)

`HUMAN RESOURCES: ZERO DAY` (`HR//0D`) is a mobile cybersecurity-awareness game for Android,
Unity 6.3 LTS (`6000.3.29f1`), landscape, offline, local save only. The player is a hiring
officer at the corporation that holds the world's security. **Some applicants are AI.**
The player reviews documents, observes the applicant across the table, uses three limited
verification tools, and stamps APPROVE / REJECT with a reason. Five shifts to survive.
Three AIs hired = defeat.

**The one-line design law:** *the player must never be told who is an AI — they must be able
to prove it from what is on screen.* Everything else follows from that.

Full context: `README.md`, `docs/01-roadmap-14-days.md`, `docs/05-design-suggestions.md`,
`docs/06-decisions.md` (ADR-001…006, all locked).

## 2. Locked decisions — do not re-litigate without asking a human

| # | Decision | Consequence for you |
|---|---|---|
| ADR-001 | Local git repo, remote is the team's | Never push to `main` directly; branch + PR |
| ADR-002 | Team = capable coders who need structure | Ship complete files, one owner per module, no frameworks |
| ADR-003 | 3 hired AIs = defeat; visible corruption at 1 and 2 | `CorruptionLevel` 0–3 drives bg variant + glitch overlay |
| ADR-004 | "Product-quality build, demo-sized content" | Polish is mandatory, content volume is not. Day-6 scope gate |
| ADR-005 | **Landscape**, art base **480×270**, PPU 16, integer scale | Never design portrait. Safe-area insets mandatory |
| ADR-006 | **Diegetic detection + imperfect tools** (ID Registry 2/day, Metadata Scan 2/day, Reference Call 1/day) | Tools can still say "VALID" for an AI. Never build a tool that solves the game |

**New since the last handoff — the desk composition (this is the fiction now):**
The screen is split into two halves. **Left half = the monitor** on the player's desk:
documents, resume, ID card, cover letter, metadata, tools, stamps. **Right half = the table
across from you**: the applicant, seen live, reacting to what you do (fidgeting, blinking,
leaning in, looking away, a nervous laugh). The player watches *both*. Behaviour is evidence,
but it is **never sufficient on its own** — a nervous human is not an AI.

**New since the last handoff — randomised runs (no two shifts are the same):**
Applicants, names, ages, IDs, employers, tells, distractors, emails and attack events are all
generated from a per-run seed. Same seed = same run (for debugging and bug reports). The
fairness invariants below are **non-negotiable** and are already proven by a reference
implementation: `prototype/generator_sim.py` (0 violations / 1,000 runs / 23,000 applicants,
0.72 ms per run). Port it to C# 1:1 — do not invent a different generator.

Spec: `docs/07-randomization-spec.md` · Model: `docs/08-behaviour-model.md`

| Invariant | Meaning |
|---|---|
| I1 / I1b | Every synthetic has ≥1 tell **whose reveal surface is unlocked on that day** |
| I2 | No duplicate name or ID within a run |
| I3 | Every arithmetic contradiction is actually contradictory |
| I4 | Exact synthetic count per day; human quota always reachable |
| I5 | Humans never carry a hard tell |
| I6 | Every distractor has a resolvable explanation (a field/tool that clears it) |
| I7 | Deterministic from seed |
| I8 | No tell is conveyed by colour alone |

## 3. Role split — and what each of you must NOT do

### AI assisting **Dev A — Systems / Gameplay**
Owns: `Assets/Scripts/Core`, `Data`, `Rules`, `Content`, the run generator port.
**Must do:** keep `Rules/` and `Generator/` **pure C#** (no `UnityEngine` references) so they are
EditMode-testable; write EditMode tests for every invariant; make every shift reproducible from a seed.
**Must NOT:** touch UI prefabs, art files, or final copy.

### AI assisting **Dev B — UI / Frontend**
Owns: `Assets/Scenes`, `Prefabs/Screens`, `Scripts/UI`, animations, transitions, input.
**Must do:** portrait-of-layout per ADR-005; 48 dp minimum touch targets; every screen has a back
affordance + safe-area anchor; transitions cancellable in `OnDisable`; respect `CorruptionLevel`
visually; reduce-motion setting respected by every animation.
**Must NOT:** write gameplay rules, scoring, or content.

### AI assisting **Dev C — Content / Build / QA**
Owns: `Assets/StreamingAssets/content`, `docs/` content schemas, Android builds, QA matrix, event kit.
**Must do:** author content as JSON matching the schemas; never hardcode strings in prefabs; run
`python prototype/generator_sim.py` after any content/schema change; keep the build green; own
`docs/tooling-setup-database.md` status table.
**Must NOT:** edit core scripts without a handoff to Dev A.

### Agent (me)
Architecture, art generation, reviews, debugging, docs, integration, tie-breaker on design calls.

## 4. The 14-day plan (mirrors `docs/01-roadmap-14-days.md`)

| Phase | Days | Exit test |
|---|---|---|
| 1 Contracts & scaffold | 1–2 | fresh clone opens with zero console errors; data contracts frozen |
| 2 Vertical slice | 3–6 | Day 1–2 playable on a real Android phone, save/resume works. **Scope gate** |
| 3 Content & polish | 7–11 | all 5 days authored, intro + character creation + endings + settings |
| 4 QA & ship | 12–14 | signed APK `0.1.0-demo`, 3 device profiles, no P0/P1 open |

Order of work inside Phase 2 (do not reorder — each step unblocks the next):
1. `SaveSystem` + `ContentLoader` + JSON schemas 2. `ApplicantRecord` + generator port 3. `RuleEngine` + 3 rules
4. Desk screen layout (left monitor / right table) 5. Stamps + reason dropdown 6. Inbox task 7. Day summary
8. Save/resume integration 9. Android build.

## 5. How you must work (the methodology — follow it literally)

1. **Ask, don't guess.** If a decision changes design, costs > 30 min, or is not in the ADRs — stop and
   ask the human. Then record the answer in `docs/06-decisions.md` as a new ADR (never edit history).
2. **Log before you fix.** Every non-trivial bug gets an entry in `docs/90-bug-log.md` *before* the fix,
   including a **Prevention rule** line. Re-read the standing rules first; if your fix repeats a logged
   mistake, say so out loud.
3. **Handoff, don't hope.** Anything crossing a boundary gets `handoff/YYYY-MM-DD_<from>-to-<to>_<topic>.md`
   using the template in `docs/04-collaboration-protocol.md` §2. If it isn't written down, it didn't happen.
4. **Data over code.** Content lives in JSON. If you find yourself writing a name, an applicant, or an
   email body inside a C# file or a prefab, stop.
5. **Prove it, don't claim it.** "It works" means: ran in editor, ran on device, survives save→quit→
   relaunch, zero new warnings. Paste the evidence in the handoff.
6. **One owner per file.** Never edit a scene/prefab owned by someone else without announcing it.
7. **Small commits.** Conventional prefixes (`feat:`, `fix:`, `art:`, `content:`, `docs:`, `chore:`),
   ≤ 72-char subjects, one logical change each. `main` must always open without errors.
8. **Definition of Done** (`docs/04-collaboration-protocol.md` §4) is the merge gate. No exceptions.
9. **Escalate early.** Stuck > 30 min → report: expected / actual / console text / what you tried.
10. **Teach the player, always.** Before shipping any mechanic ask: *what real-world security lesson
    does this create?* If there's no answer, cut it. This is an awareness game, not a puzzle box.

## 6. Feedback format the human wants from you

When you report back to your human, use exactly this shape — it keeps the three devs' streams
comparable and lets me reconcile them:

```
STATUS: <on track | at risk | blocked>
DONE:   <bullets, each naming the file(s)>
NEXT:   <the single next action>
BLOCKED ON: <decision needed / dependency> -> suggested default: <...>
RISK:   <anything that could cost a day>
EVIDENCE: <test run, screenshot, build log excerpt>
```

And when you disagree with me or with an ADR, say so explicitly with the trade-off in one line —
*"ADR-005 says landscape; portrait halves the UI work but hurts booth play; recommend staying
landscape"*. Do not silently deviate.

## 7. Ground truth files (link these, don't paraphrase them)

| Topic | File |
|---|---|
| Open questions & my defaults | `docs/00-open-questions.md` |
| Roadmap / roles / risks | `docs/01-roadmap-14-days.md` |
| Engine, folders, data contracts, art spec, palette | `docs/02-tech-stack.md` |
| Titles & branding | `docs/03-title-shortlist.md` |
| Git, handoffs, DoD, escalation | `docs/04-collaboration-protocol.md` |
| Design suggestions / curriculum / endings / menu | `docs/05-design-suggestions.md` |
| ADRs (locked decisions) | `docs/06-decisions.md` |
| Randomised runs + fairness invariants | `docs/07-randomization-spec.md` |
| Applicant presence & behaviour model | `docs/08-behaviour-model.md` |
| Tooling database & install order | `docs/tooling-setup-database.md` |
| Real backend spec | `docs/09-backend-spec.md` |
| Bugs & prevention rules | `docs/90-bug-log.md` |
| Deferred ideas | `docs/91-backlog.md` |
| Generator reference implementation | `prototype/generator_sim.py` |

## 8. Definition of "demo-ready" (what we are all aiming at)

- Fresh install → intro (skippable) → character creation → menu → 5 shifts → ending, **no dead ends**.
- Runs offline; save/resume at any point; autosave on every transition.
- Two consecutive runs produce **different** applicants, emails and events (randomised), and every
  AI in both runs was catchable from on-screen evidence.
- 60 fps on a mid-range 2020-era Android phone; no P0/P1 open; APK sideloads on a clean device.
- A player who has never been taught cybersecurity can, after one run, name three tells.

## 9. Open items that need a human answer (do not block on them — use the default)

| Item | Default we proceed with |
|---|---|
| GitHub remote URL | `https://github.com/nonoyj/Human_Resources-Zero_Day` (confirmed by user) |
| Event date / booth time per player | 10–15 min per run; intro ≤ 25 s, first document ≤ 15 s |
| Is there a real server? | **Yes** — see `docs/09-backend-spec.md`; client works fully offline and syncs when reachable |
| Studio/team name | `HR//0D` wordmark only, no studio name until told |
| Pitch deck | Not requested yet |

## 10. First three tasks for each role (start here, today)

- **Dev A:** `SaveSystem` (atomic write) → `ContentLoader` (StreamingAssets JSON) → port
  `generator_sim.py` to `Assets/Scripts/Generator/` with EditMode tests asserting I1–I8.
- **Dev B:** grey-box the desk screen with the left-monitor / right-table split at 480×270, safe-area
  insets, and a single working transition (menu → desk). No art required yet.
- **Dev C:** run `scripts/Setup-DevEnvironment.ps1`, get the Android build green with an empty scene,
  and write `content/schema/applicant.schema.json` from `docs/02-tech-stack.md` §Data contracts.

---
*If any instruction here conflicts with the repo, the repo wins and you tell me about the conflict.*
