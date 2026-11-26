# Docs index — one page, no guessing

**Updated 2026-11-26.** If a file is not listed here, it does not exist. This page is the only
navigation you need; the numbering is deliberate (lower number = decided earlier = more binding).

## Read these first

| File | What it is | Binding? |
|---|---|---|
| `16-the-later-list.md` | **Everything still owed, ordered and owner-tagged.** Start every work session here | yes |
| `01-roadmap-14-days.md` | The 14-day phase plan and role table | yes |
| `06-decisions.md` | Every locked decision with its date | yes |
| `00-open-questions.md` | Decisions still open, with the default I will use | — |

## Specs (the game)

| File | What it is |
|---|---|
| `02-tech-stack.md` | Unity version, project layout, data & save formats, build settings |
| `07-randomization-spec.md` | Seeded run generation + the fairness invariants I1–I8 |
| `08-behaviour-model.md` | The applicant across the table: animation, the 9 artifacts, stress model |
| `09-backend-spec.md` | The three backend jobs, offline-first |
| `20-backend-runbook.md` | **What the backend actually does today**, how to run/test it, what is still owed |
| `21-content-pack-format.md` | Content JSON: schemas, the fairness gate, how to add a day/email/letter |
| `10-analytics-spec.md` | Research instrument: events, metrics, privacy guardrails |
| `11-core-verification.md` | How the core is proven (test plan) |
| `15-interaction-layer.md` | Hotspot -> evidence -> stamp loop, paperwork flags and the playable browser twin |

## Art

| File | What it is |
|---|---|
| `15-art-bible.md` | **Palette, composition, RAGGED rules, nothing-floats, corruption drift, applicant tells.** The only art law |
| `14-art-to-unity-pipeline.md` | Generator → manifest → publish → scene, and the gates |

## Process

| File | What it is |
|---|---|
| `04-collaboration-protocol.md` | Git workflow, handoff format, bug-log discipline |
| `REPORTING-FORMAT.md` | How the agent reports (A/B/C) |
| `13-team-plan.md` | Who owns what |
| `BUNDLE-WORKFLOW.md` | How bundles move between the agent and your PC |
| `PUSH-TO-GITHUB.md` | Pushing to the team remote |
| `90-bug-log.md` | BUG-001 … BUG-026. Read before "fixing" anything twice |
| `91-backlog.md` | Explicitly **not** in the build |

## Setup (only needed once per machine)

| File | What it is |
|---|---|
| `tooling-setup-database.md` | Verified installers D01–D17 with sizes and first-party URLs |
| `tooling-status.md` | Live status of what is installed on the Dev A machine |
| `SETUP-CHECKLIST.md` | Step-by-step Windows setup |
| `TROUBLESHOOT-SETUP.md` | "script not recognized" and every other setup failure |
| `DOWNLOADS.md` | Short download checklist |
| `19-unity-project-setup.md` | Open `unity/` in Hub, one-click setup, Play controls, project settings |
| `GIT-LFS-DECISION.md` | Why this project does **not** use LFS (BUG-010) |

## Deleted on purpose (2026-11-26) — do not recreate

`PROJECT-STATUS.md` (it pinned an old HEAD and had been false for weeks), `NEXT-STEPS.md`
(superseded by `16-the-later-list.md`), `03-title-shortlist.md` (title locked),
`05-design-suggestions.md` (superseded by `06-decisions.md`), `92-retrospective.md` (once-off).

**Rule for adding a doc:** if it duplicates a decision that already lives in `06-decisions.md` or a
task that already lives in `16-the-later-list.md`, do not add it. Update the existing file instead.
