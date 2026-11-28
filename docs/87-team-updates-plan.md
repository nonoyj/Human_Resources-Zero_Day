# 87 -- THE TEAM'S NEW UPDATES: what exists, what is new, and when

**Received 2026-11-27.** HUD additions, two new investigation mechanics, four changes to the after-hours
walk, and a narrated day-summary animation. This page checks every item against the code as it stands,
prices it in **rounds** (one round = one build + five gates + a bundle, roughly a session), and puts it in
order. It supersedes nothing: `docs/86-whats-owed.md` is still the full backlog; this is the new work folded
into it.

**No work has started.** This is the plan you asked for before later.

---

## 1. Triage: what we actually already have

| # | Team's item | Today | Verdict |
|---|---|---|---|
| A1 | **Candidate hired counter** | `RunDay.Approved`, `DayResult.SyntheticsHired`, `RunLedger` all exist and are tested; the ending screen already prints `QUOTAS MET n/n` and `BY DAY`. Nothing counts hires **on the HUD during a shift**. | **Small** -- data is there, one HUD block |
| A2 | **Day counter** | **Already shipped.** `DAY 3/5` is on the HUD today, and the day summary says `DAY n OF 5 -- N SHIFTS TO GO`. | **Done** -- I will tidy the wording, nothing more |
| A3 | **Company Security Rating** | Nothing. Trust (`0-100`) and corruption (`0-3`) exist independently, but no single combined "how safe is the company" number. | **New mechanic** -- needs a formula, tests, and a HUD slot |
| A4 | **Alerts + a dashboard showing how the rating is made** | Nothing. No alert system, no breakdown panel. | **New UI + data** -- depends on A3 |
| A5 | **Evidence counter per applicant (mismatch vs their application, with "could still be human")** | `EvidenceLedger` counts *tells* found (`FoundCount`), and de-duplicates repeats. It does **not** compare a finding against what the applicant *claimed*, and it does not distinguish "mismatch" from "proof". | **New model + UI** -- the most delicate item here |
| B1 | **Provided questions per applicant** | Nothing. The player inspects artifacts; there is no way to *ask* anything. | **New mechanic** + content |
| B2 | **Verification choices that give hints** | Nothing. Artifacts reveal tells passively. | **New mechanic** + content |
| B3 | **Player notes per applicant (what matched, what did not)** | Nothing. `EvidenceNote` records a finding for the debrief, but there is no per-applicant notes page that persists across the interview. | **New UI** on existing data |
| C1 | **Company members at the sides during the walk, conversable, to check if AI (hint, not evidence)** | Nothing. The walk is three pages of traces with no people. | **New system** + art + writing |
| C2 | **AIs act weird, names hidden, investigate next clock-in via day events** | Nothing. No NPCs, and the day-event/email screen does not exist yet (8 emails are written as JSON, never shown). | **New narrative system** -- depends on the email/inbox screen |
| C3 | **Elevators for routine walks; AIs loiter on lower floors by how far they got in** | Partly cheaper than it looks: the walk **already** has `lift` and `corridor` floors and art (`sweep_lift_480x270.png`, `sweep_corridor_480x270.png`), and traces already carry `Floor` and `CauseArchetype`. What is missing is a *place*: navigation, loitering NPCs, and an investigation verb. | **New system** -- the biggest item in this list |
| D1 | **Day-summary animation of what happened to the company + narration, with logic for different results** | The day summary exists as **text on the HUD**. There is no animation, no narration, and no "what happened to the company afterwards" model. | **New**: rules + art + voice |

## 2. The hard problem nobody has spotted yet: the spoken intro does not fit its own budget

`docs/06` ADR-009 says a beat's `hold` becomes a **minimum** once DISPATCH's voice is in. `IntroScriptRules`
caps the whole intro at **25 s**. Using 150 wpm (the rate the voice direction assumes) plus ~0.35 s of
breath per sentence:

| Beat | Words | Spoken |
|---|---|---|
| "NIGHT SHIFT. FOURTH FLOOR. THE PREVIOUS OFFICER DID NOT CLOCK OUT." | 13 | ~ 6.3 s |
| "YOUR JOB IS THE INTERVIEWS. THREE HIRES A SHIFT. NOTHING ELSE." | 11 | ~ 5.5 s |
| "SOME OF THEM ARE NOT PEOPLE. THEY ARE VERY GOOD AT THIS." | 11 | ~ 5.5 s |
| "READ THE FILE. LOOK AT THE ONE IN THE CHAIR. THEN STAMP." | 12 | ~ 5.9 s |
| "AND IF YOU GET ONE WRONG, THE OFFICE WILL TELL YOU IN THE MORNING." | 14 | ~ 6.3 s |
| **Total incl. 4 gaps** | | **~ 30.5 s** |

**So the placeholder intro is 5.5 s over the cap the moment it is voiced.** Two honest options:

- **(a) Raise the cap to 32 s for a *voiced* intro, keep 25 s for the silent text version.** The intro is
  skippable (X/ESC) and shown once per profile, so 30 s is not a burden.
- **(b) Cut to four beats** and let the story carry the rest.

**DECIDED 2026-11-27: option (a).** `IntroScriptRules` now enforces *silent <= 25 s, voiced <= 32 s*, with a
voiced beat holding for the length of its clip (cap 12 s). It is implemented and tested (153 checks), and
recorded as ADR-011. BE2 can record to 32 s.

## 3. Narration budgets -- the seconds you asked for

Rule of thumb used everywhere below: **150 wpm ~ 2.5 words/second, +0.35 s per sentence** for breath, +0.3 s
between lines. Record to the **target**, never above the **max**.

| Recording | Lines | Words | Target | Max | Why that number |
|---|---|---|---|---|---|
| **Day summary narration** (per day, one of ~4 variants) | 3 | ~ 30-34 | **14 s** | **18 s** | It plays *after* a 5-day game day; anything over 18 s gets skipped on day 3+ |
| **Ending narration** | 5-6 | ~ 70-80 | **30 s** | **35 s** | One per run, the emotional payoff, still under 35 s |
| **DISPATCH intro** (5 beats) | 5 | ~ 61 | **28 s** total | **32 s** | see W-1; ~5.5-6.3 s per beat |
| **Walk: an employee's line** | 1 | ~ 8-12 | **4 s** | **6 s** | Conversational ping, not a monologue |
| **An alert sting** (no voice) | -- | -- | **0.8 s** | **1.2 s** | UI, not narration |

Per-day narration should be **three short sentences**, in this order -- this is the shape the logic will
generate, so it can be recorded once and cut:

1. *What the company got* -- "Three new hires started on the fourth floor." (~ 7 words)
2. *What it cost or bought* -- "The breach counter went up by one. Security held at 62." (~ 12 words)
3. *What tomorrow is* -- "Four more applicants tomorrow. Trust is thin." (~ 7 words)

Variants needed per day: **quota met / quota missed / synthetic hired / clean day** -> 4 x 5 days = up to 20
clips, but most days reuse a variant, so realistically **8-10 recordings**. Write them last, record them in
one session, and record **one extra "neutral" variant** as the fallback for any missing case.

## 4. The order, and when "when"

Rounds are units of work, not calendar promises. I can deliver **2-3 rounds per working session**.

### Phase 1 -- CHROME (where the game already is) -- 4 rounds
| Round | Delivers | Exits when |
|---|---|---|
| **v108** | **Consent + menu + settings** (`docs/86` #2 -- the field nobody sets) | Menu lists Continue/New/Settings/Consent/Credits; consent writes to the save; audio sliders work |
| **v109** | **HUD pass**: hires counter, day counter wording, **Security Rating** (formula + tests + HUD) | The rating changes visibly when a synthetic is hired; 6 new tests |
| **v110** | **Alerts + rating dashboard** (a panel: what moved the rating, by how much) | Tapping ALERTS shows the breakdown; every number on it is derived, none hardcoded |
| **v111** | **Evidence counter + player notes page** (mismatch vs claim, explicitly "not proof") | Notes list what matched and what did not, and a mismatch *never* reads as guilt |

### Phase 2 -- INVESTIGATION (the new mechanics) -- 4 rounds
| Round | Delivers | Exits when |
|---|---|---|
| **v112** | Question bank: 3 questions per applicant, answers can expose a tell | Asking costs time; an answer can be the evidence you needed |
| **v113** | Verification choices (check the ID against the roster, cross-check dates, call the reference) returning **hints** | Each verification is a real choice with a cost and a result |
| **v114** | Wire both into the evidence counter + notes, with the fairness invariant | A human can be mismatched and still hired; tests prove it |
| **v115** | **Inbox / day events** (the 8 emails that already exist finally appear, legit vs attack) | An email can be inspected and refused; the event shapes the next day |

### Phase 3 -- THE WALK BECOMES A PLACE -- 5 rounds
| Round | Delivers | Exits when |
|---|---|---|
| **v116** | NPC cast at the sides of the walk (art + sprites + idle tell rhythms reused) | You can see people; they blink on the same rhythms the applicants do |
| **v117** | Conversations: talk to an employee, get a **hint** (never evidence) | One conversation can raise suspicion without proving it |
| **v118** | Elevators + floor navigation (lift/corridor art already exists) | You choose the floor instead of paging through a receipt |
| **v119** | Loitering AIs: how many appear on lower floors scales with corruption | A bad run visibly fills the building |
| **v120** | Tie the walk to the next clock-in: names hidden, investigate during day events | A weird NPC from last night can be identified the next morning |

### Phase 4 -- THE DAY SUMMARY FILM -- 3 rounds
| Round | Delivers | Exits when |
|---|---|---|
| **v121** | Rules: "what happened to the company" model (deterministic, tested, 4 outcomes x corruption) | The same run always tells the same story |
| **v122** | Animation: the summary becomes a staged scene (2-3 art frames + text beats) | It reads as an ending-of-day moment, not a text block |
| **v123** | Narration: voice clips per outcome, `hold` = clip length, silent-safe if a clip is missing | A missing clip leaves the card readable, never stuck |

**Total: 16 rounds.** Then the two things that were already owed: **P5 art** (faces, room, cast -- 4-6 rounds)
and **P6 Android** (build, QA matrix, store -- 5-6 rounds).

### What this does to the roadmap

| Phase | Before today | Now |
|---|---|---|
| P2 loops | 9/10 | the evidence counter and notes move it to **10/10** in Phase 1-2 |
| P3 walk | 10/10 | **reopens**: it was "done" as a receipt; the team wants it as a space -> Phase 3 rebuilds it |
| P4 audio/intro/save | 8/10 | unchanged; the intro voice now has a timing decision (W-1) |
| New **P7 investigation** | -- | Phases 1-4 above, ~16 rounds |

## 5. What I need from the team before each phase

| Phase | Needs | Who |
|---|---|---|
| 1 | Decisions **D-1..D-3** below | you |
| 2 | **Question bank text** (3 questions x 14 archetypes) and **~40 verification result lines** | content (C) |
| 3 | **NPC cast art** (6-8 employees, busts + idle pairs) and **~60 short lines** | art + content |
| 4 | **Narration clips** (8-10, <=18 s each) + **2-3 still frames** | you + art |
| all | keep `docs/90-bug-log.md` current; the gates stay green or the round does not ship | everyone |

## 6. The decisions that block Phase 1

- **D-1 -- Security Rating scale.** My proposal: **0-100**, starting at 70, labelled `STABLE / SHAKY /
  BREACHED`, computed from trust (60 %), corruption (30 %), and synthetics hired this run (10 %). Alerts
  are the *history*, not another input, so the dashboard can always explain itself.
- **D-2 -- What "candidate hired" counts.** Proposal: **today** on the HUD (`HIRES 2/3`) and **run total** on
  the ending (`HIRES 11/15`), because a shift is a quota and a run is a record.
- **D-3 -- The evidence counter's honesty rule.** The team's own note says a mismatch may still be a human.
  So the counter shows **`2 MISMATCHES`** and never `2 PROVEN`, and the stamp screen must say so in words.
  I want that confirmed, because it is the difference between a fair game and a game that teaches the wrong
  lesson.
- **W-1 -- Voiced intro cap** (#2): raise to 32 s, or cut to four beats?

## 7. What I would cut if you want it sooner

In order of least loss: the alerts **dashboard** (keep the alerts, drop the breakdown to a later round) ->
**elevator navigation** (keep the walk as pages, add NPCs only) -> the **animation** in the day summary
(keep narration over the existing text). Everything else is load-bearing.
