# Retrospective — the first 5 exchanges (written by the agent, about the agent)

You asked me to note our mistakes so the next round is better. Being honest here is more useful to
you than being diplomatic, so this grades **me** first, then **the process**, then what I'd change.
The bug log (`docs/90-bug-log.md`) has the mechanical entries; this file is about *judgement*.

---

## 1. What I got wrong, ranked by cost to you

| # | Mistake | Real cost to you | Root cause (behavioural, not technical) |
|---|---|---|---|
| 1 | Shipped PowerShell I never executed | Two failed setup attempts, ~40 min of your time, and a real hit to trust | I validated by *reading* code I'd written, which is the weakest possible test. I had a sandbox the whole time and didn't install `pwsh` until you hit an error |
| 2 | Non-ASCII characters in `.ps1` | Same as above | I wrote for my environment (UTF-8, PS7) instead of yours (Windows PS 5.1, ANSI). Classic "works on my machine" |
| 3 | Told you the bundle contained git history when it didn't | You downloaded 5.5 MB for nothing and I had to correct myself in the next message | I asserted a property of an artefact I never inspected. I even *had* the exclusion in my own code (`.git` filtered out) and didn't reconcile it with what I was telling you |
| 4 | Verify reported `adb not on PATH` immediately after adding it | A false alarm you had to reason about | I checked the environment inside the same process that mutated it. Correct testing means re-reading the source of truth (registry), not the cached copy |
| 5 | Nested/ambiguous extract instructions | Your first error | I gave a path (`C:\users\user\HR-ZeroDay`) without telling you *how* to make the folder look like that. Archive tools create a folder named after the zip |
| 6 | Too much prose per turn, not enough click-path | You had to ask me to "be specific with steps" | I optimised for completeness over usability. Docs should be dense; instructions to a tired human at a terminal should be dumb-simple and copy-pasteable |
| 7 | Asserted environment details I couldn't see | Minor, but it produced the false bundle claim | I inferred instead of marking "unverified" |

## 2. What worked (keep doing these)

1. **Writing the fairness simulator before the spec** — the invariants are proven, not asserted, and
   the simulator caught three generator bugs that would have shipped as "unfair loss" complaints.
2. **The bug log with Prevention rules** — your PS 5.1 failure is now impossible to repeat because
   `scripts/Test-Scripts.ps1` refuses non-ASCII bytes. Every one of my four bugs now has a mechanical gate.
3. **ADRs** — decisions are visible and reversible instead of buried in chat.
4. **Handoff files with an explicit feedback format** — the three devs' AIs will produce comparable
   reports, which is the only way you can manage three parallel efforts.
5. **Asking six questions in one batch** instead of dribbling questions across turns.

## 3. Process problems on your side (said with respect — these are structural, not personal)

| Observation | Why it matters | Suggested habit |
|---|---|---|
| Half the questions went unanswered (event date, studio name, booth duration) | I defaulted, which is fine — but defaults become silent decisions | Answer with "you decide" explicitly; then it's a documented decision, not an accident |
| "Product-quality" was chosen for a 14-day, 3-person scope | The plan now carries a Day-6 scope gate to protect us | Keep the gate. Re-read ADR-004 on Day 6 rather than Day 13 |
| GitHub URL arrived after two rounds of local git work | Mild rework, plus I built a bundle path that didn't need to exist | Paste accounts/URLs in the first message of a project |
| No one has run Unity yet | Everything is still paper. Day 1 risk is concentrated here | Get one empty scene on one real phone before writing any game code |

## 4. What I will do differently from this turn onward

1. **Nothing executable ships unexecuted.** Scripts get installed, parsed, and run in the sandbox.
   If I can't run it, I say so in the same sentence I hand it over: *"reviewed, not executed."*
2. **Every claim about an artefact gets a check.** Bundle contents → extract and `git log`. Download
   URL → HEAD request and real byte size. Nothing described from memory.
3. **Windows-first hygiene by default**: ASCII-only code, UTF-8 BOM, CRLF, PS 5.1 compatibility,
   `Test-Scripts.ps1` gate, and no assumptions about PATH/registry state.
4. **Instructions become numbered click-paths**, each line copy-pasteable, with an expected-output
   line after the ones that can fail, and a "if you see X, do Y" fallback.
5. **I state the verification status of every deliverable** in one short line: *executed / dry-run /
   reviewed-only*. You should never have to guess how much I actually tested.
6. **Less narration, more signal.** Long explanations only for decisions; steps stay terse.

## 5. Scorecard (self-assessed, 1–5)

| Dimension | Score | Comment |
|---|---|---|
| Design quality | 5 | The diegetic detection + imperfect tools + fairness invariants hold together |
| Documentation | 4 | Thorough, but written for me rather than for someone at a terminal; fixed by the SETUP checklist |
| Code correctness | 2 | Two P0 setup bugs and one false verification — all avoidable with a single execution test |
| Honesty / self-correction | 4 | I corrected the bundle claim unprompted; should have caught it before saying it |
| Managing the human | 3 | Six good questions, then too much density; you had to ask for specificity |
| Scope judgement | 4 | I flagged the "product-quality vs 14 days" tension and built a gate instead of pretending |

## 6. The one sentence to remember

> **Verify by executing, not by reading — then say exactly how much I verified.**

That single habit would have prevented 4 of my 7 mistakes. It is now encoded as prevention rules
2 and 6 in the bug log, and as `scripts/Test-Scripts.ps1` in the repo, so it survives even if I forget.
