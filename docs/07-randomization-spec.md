# Randomised Runs — Specification & Fairness Invariants

**Goal:** no two playthroughs are the same — applicants, identities, tells, distractors, emails and
attack events are all drawn per run — **while guaranteeing every AI is catchable from on-screen
evidence.** Randomness must never manufacture an unfair loss.

**Reference implementation (port this, don't reinvent):** `prototype/generator_sim.py`
**Current proof:** 1,000 runs · 23,000 applicants · **0 invariant violations** · deterministic ·
0.72 ms per run generation time (C# will be faster).

```
python prototype/generator_sim.py --runs 1000
```

---

## 1. Seeding model

- One `runSeed` (`int`, from `Guid` hash or `Environment.TickCount`) is stored in the save file.
- One `Random` stream is created from the seed and **shared across the whole run**, so day 3 depends
  on days 1–2 having been generated (this is what makes identities unique per run).
- Every bug report includes the seed. A tester with the seed reproduces the exact run.
- **Debug override:** `SeedOverride` in the editor (see `docs/guides`), plus "Replay this run" in
  the ending screen for QA.

## 2. The eight invariants (enforced in code + tested in EditMode)

| ID | Invariant | How it is enforced |
|---|---|---|
| **I1** | Every synthetic has ≥ 1 tell | `SynthArchetype` requires `minTells ≥ 1`; validator rejects |
| **I1b** | That tell's reveal surface is **unlocked on that day** | `SurfaceUnlock[day]` table; generator filters candidate tells |
| **I2** | No duplicate name or ID within a run | `NameRegistry` (HashSet) per run, shared across days |
| **I3** | Every arithmetic contradiction is really contradictory | `Applicant.RecomputeContradictions()` is the source of truth; validator compares claimed vs actual |
| **I4** | Exact synthetic count per day; human quota reachable | Synthetics are drawn **first**, then humans fill to `applicantsPerDay` |
| **I5** | Humans never carry a hard tell | `human_bait` is the only human archetype allowed compromised documents, and its tells are *soft* (coercion) |
| **I6** | Every distractor has a resolving field/tool | `Distractors` dict maps each distractor to its resolution source |
| **I7** | Determinism from seed | Same seed → identical applicant list (test asserts equality of IDs) |
| **I8** | Never colour-only | Every `Tell` carries a `textSurface` string shown in UI |

## 3. Day plan (difficulty is *categories*, not timers)

| Day | Applicants | Synthetics | Tells/synthetic | New category taught | New surface unlocked |
|---|---|---|---|---|---|
| 1 | 4 | 1 | 2 | `SYNTHETIC_MEDIA` | applicant stage, cover letter |
| 2 | 4 | 1 | 2–3 | `DOC_INCONSISTENCY` | resume rows, **ID Registry** |
| 3 | 5 | 2 | 2–3 | `COERCION` (human bait) | — |
| 4 | 5 | 2 | 2–3 | `SOCIAL_ENGINEERING` | **Reference Call**, inbox attack |
| 5 | 5 | 2 | 3 | `IMPERSONATION` (composite) | — |

Total ≈ 23 applicants per run. Difficulty rises by **overlapping tells and noisier distractors** —
never by hiding facts, and never by a hard timer.

## 4. Archetypes (weights are the tuning knobs)

| Archetype | Is synthetic | Tells | Distractors | Weight | Purpose |
|---|---|---|---|---|---|
| `human_solid` | no | 0 | 0–1 | 40 | the honest baseline; teaches you not to over-reject |
| `human_nervous` | no | 0 (behaviour only) | 0–1 | 22 | teaches "nervousness ≠ guilt" |
| `human_bait` | **no** | 1–2 soft (coercion) | 0–1 | 16 | the victim: a real person used by the AI. Reject the application, not the human |
| `synthetic_polished` | yes | 2–3 | 0–1 | 10 | portrait/voice/tidy-document tells |
| `synthetic_cloned` | yes | 1–2 | 1 | 6 | identity clone: ID belongs to a real person; referee denies knowing them |
| `synthetic_collusion` | yes | 2–3 | 0 | 6 | needs a human insider: fast-track referral, requisition number missing |

`human_bait` cannot appear before day 3. `synthetic_collusion` cannot appear before day 4.
Day 1 uses only `synthetic_polished` (no tools are unlocked yet).

## 5. Tell catalogue (v1 — extend via content, not code)

| Tell id | Category | Surface (revealed by) | Player-facing wording |
|---|---|---|---|
| `portrait_asymmetry` | SYNTHETIC_MEDIA | applicant stage (zoom) | "Mole on the left cheek in the photo, right cheek in real life" |
| `portrait_blend_seam` | SYNTHETIC_MEDIA | applicant stage (metadata/zoom) | "Hair edge cuts a straight line into the background" |
| `voice_flat_metrics` | SYNTHETIC_MEDIA | cover letter | "Every sentence is the same length; no contractions" |
| `exp_vs_grad` | DOC_INCONSISTENCY | resume row | "8 years of experience, graduated 4 years ago" |
| `employer_founded` | DOC_INCONSISTENCY | resume row | "Worked at a company 3 years before it existed" |
| `grad_too_young` | DOC_INCONSISTENCY | resume row | "Graduated at 16" |
| `id_checksum` | DOC_INCONSISTENCY | ID Registry tool | "Checksum digit does not validate" |
| `ref_unreachable` | IMPERSONATION | Reference Call tool | "Referee says they have never heard of this applicant" |
| `identity_clone` | IMPERSONATION | ID Registry tool | "This ID is registered to a different name" |
| `bait_script` | COERCION | cover letter | "Reads like dictated instructions, not a person writing about themself" |
| `bait_pressure` | COERCION | cover letter | "'I need an answer by Friday or I lose my home'" |
| `golden_fast_track` | SOCIAL_ENGINEERING | referral row | "Director fast-track with no requisition number" |

**Distractors** (look like evidence, are resolvable — this is where the real learning is):

| Distractor | Looks like | Resolved by |
|---|---|---|
| `career_gap` | 12-month employment gap | cover letter explains caring duties |
| `surname_change` | Different name at university | cover letter mentions marriage |
| `short_stints` | 3 employers in 18 months | cover letter explains a company closure |
| `odd_lighting` | Portrait lighting is uneven | metadata scan shows a real camera + timestamp |

## 6. Content surfaces that must exist for the invariants to hold

`applicant_stage` · `cover_letter` · `resume_row` · `id_tool` · `reference_tool` · `metadata_tool` ·
`inbox`

If a tell's surface doesn't exist yet on a given day, the tell **cannot** be drawn (I1b). When you
add a new tell, you must also add its surface + the day it unlocks, or the validator fails the build.

## 7. Emails & attack events (also randomised)

- Each day draws 1–2 inbox items from a pool; at least one per run must be malicious.
- Email attack types: `lookalike_domain`, `urgency`, `authority`, `unexpected_attachment`,
  `wrong_thread`, `credential_request`.
- Day 4–5 pool adds `grant_database_access` and `referral_fast_track` (the golden-opportunity trap).
- Same seed → same emails. Emails are authored, then **selected + parameterised** (names, days,
  amounts) from pools — never generated as raw text by code.

## 8. Tuning procedure (do this, don't guess)

1. Run `python prototype/generator_sim.py --runs 2000`.
2. Check: violations = 0; synthetics/human ≈ 35/65; tells/synthetic median 2.
3. Run a playtest; if the day feels unfair, lower `tells/synthetic` or raise `distractors` — **never**
   add a timer.
4. Log the change + the numbers in `docs/90-bug-log.md` or the tuning table below.

### Tuning history
| Date | Change | Before → After | Why |
|---|---|---|---|
| 2026-11-25 | initial weights | — | baseline, 0 violations @1000 runs |

## 9. Porting checklist for Dev A (C#)

- [ ] `RunGenerator` with a single `System.Random` seeded from the save
- [ ] `NameRegistry` per run; throws/excludes on collision (never silently duplicates)
- [ ] `SurfaceUnlock` table as a `static readonly Dictionary<int, HashSet<Surface>>`
- [ ] `ApplicantRecord` as a plain `[Serializable]` class — **no MonoBehaviour**
- [ ] Generator + validator in an assembly with **no** `UnityEngine` reference (asmdef with
      `noEngineReferences: true`) so EditMode tests run in milliseconds
- [ ] EditMode tests: `Invariants_AreSatisfied_For1000Seeds`, `SameSeed_SameRun`,
      `Synthetic_AlwaysHasReachableTell`, `Human_NeverHasHardTell`
- [ ] Expose `runSeed` in the save file and in the crash/bug report payload
