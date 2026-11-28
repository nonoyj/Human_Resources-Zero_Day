# 88 — EVERY DECISION I NEED, WITH MY DEFAULT

**Started 2026-11-27.** One place for the questions that block or shape the next ~16 rounds. Each one has a
**recommended default**, so you can answer in a few words ("all defaults except Q7") and I can start.

Rules for this page: an answered question moves to `docs/06-decisions.md` as an ADR and is struck here.
A question I can answer myself does not belong here — only the ones where your answer changes what gets
built.

**Status: ALL 22 ANSWERED (2026-11-27).** W-1 → ADR-011 · Q1–Q4 → ADR-012 · Q11 → ADR-013 · Q12 → ADR-014 · Q13 → ADR-015. The rest are accepted at their stated defaults and will be recorded as ADRs as each phase starts.

---

## Already answered

| # | Question | Your answer |
|---|---|---|
| **W-1** | Voiced intro cap: raise to 32 s or cut to four beats? | **Raise to 32 s.** Implemented in `IntroScriptRules` (`MaxTotalSecondsVoiced = 32`, silent stays 25), with a beat holding for its clip up to `AudioMaxHold = 12 s`. Test: *"a voiced intro gets 32 s and keeps its beats"*. |

---

## A. THE SECURITY RATING AND ALERTS (Phase 1, rounds v109–v110) — **ANSWERED, ADR-012**

**Q1. What is the rating made of, and how do you want it to feel?**
My default: **0–100**, starting at **70**, labelled `STABLE` (70+) / `SHAKY` (40–69) / `BREACHED` (below 40).
Computed **60 % trust, 30 % corruption, 10 % synthetics hired this run**. The dashboard shows the three
contributions and the last three things that moved it.
→ *Do you want it visible during a shift, or only on the alerts screen and the day summary?*

**Q2. Can the rating go UP as well as down?**
Default: **yes** — a correct call and a clean day raise it slightly. A number that only falls stops being a
score and becomes a countdown. → *Confirm, or say it only falls.*

**Q3. What exactly triggers an alert?**
Default: four kinds — **a synthetic was hired**, **two mismatches on one applicant** (a warning, not proof),
**a verified lie** (the roster check contradicts the letter), **corruption rising a level**. Each alert is
**one short line in the HUD** plus a row on the dashboard.
→ *Is there anything that should alert that is not on that list?*

**Q4. Hired counter — whose number?**
Default: **today, out of the quota, on the HUD** (`HIRES 2/3`), and **the run total on the ending**
(`HIRES 11/15`). → *Confirm.*

---

## B. EVIDENCE, MISMATCHES AND NOTES (Phase 1–2, rounds v111–v114)

**Q5. The evidence counter's exact words.** The team's own note says a mismatch may still be a human.
Default: the counter reads **`2 MISMATCHES`**, never `PROVEN`, and the stamp screen says, in place,
*"Mismatches are not proof. People lie about themselves."*
→ *Is that the tone you want? It is the difference between a fair game and one that teaches the wrong lesson.*

**Q6. What counts as a mismatch, exactly?**
Default: three kinds — **the file contradicts the letter** (dates, employer, name spelling), **the body
contradicts the file** (age, stated height), **the answer contradicts the application** (a question's reply
conflicts with a claim). → *Add or remove any?*

**Q7. How many questions per applicant, and what does asking cost?**
Default: **3 questions**, each costs **time on the card** (the observation clock keeps running) but **no
tool budget**. One of the three, per applicant, can expose a real tell; the other two are texture.
→ *Is 3 right? Should a question ever be free?*

**Q8. Verification choices — how many, and are they shared with questions?**
Default: **3 verifications per applicant** — *check the ID against the roster*, *cross-check the dates*,
*call the reference*. Each returns **a hint, not proof**, and each is **once per applicant**.
→ *Confirm, or tell me the three you want.*

**Q9. Do notes write themselves, or does the player write them?**
Default: **automatically, as a log** — "ID photo matches roster ✓", "start date on the letter is before the
company existed ✗" — one line per finding, in the order found, and the counter counts the ✗ lines.
→ *Or do you want the player to type their own notes?*

**Q10. Can a mismatch be WRONG?**
This is the fairness question behind Q5. Default: **no, a mismatch is always true; the inference may be
wrong.** Something that "mismatches" is a fact about the applicant; whether that fact means synthetic is the
player's judgement — and sometimes it does not. → *Confirm this reading of the mechanic.*

---

## C. THE INBOX AND DAY EVENTS (Phase 2, round v115)

**Q11. When does the player see the inbox?**
Default: **at the start of a shift**, before the first applicant, for as long as they want; unread mail does
not block the desk. → *Or after the last applicant, or both?*

**Q12. Do emails ever carry consequences into the shift?**
Default: **yes, one consequence each** — a refused "urgent access" email lowers tomorrow's corruption a
little; granting it raises it; ignoring it keeps it level. Eight emails are already written.
→ *Confirm, and tell me if an email should ever be able to end a run.*

**Q13. Is an email ever "from an AI" as a separate category?**
Your note lists *legit / attack / from an AI*. Default: **two mechanical categories — legit or attack** — with
**the writing doing the AI nuance** (some attacks are clumsy and some are perfect), because a third
mechanical category would need a third rule set and a third tell family.
→ *Do you want "from an AI" as a real third category? That is a bigger job and I would schedule it separately.*

---

## D. THE WALK: PEOPLE, ELEVATORS, LOITERING (Phase 3, rounds v116–v120)

**Q14. How many people on a walk page, and do they talk with a keypress or by walking up to them?**
Default: **2–3 people per floor**, talked to by **tapping them** (the same hotspot rule as the desk), one line
each, each usable **once per night**. → *Confirm.*

**Q15. How many floors, and how does the elevator work?**
Default: **3 floors** (desk / corridor / lift, which is what the art already is), and the elevator **replaces
paging**: you choose the floor, and you can leave a floor unfinished. Time in the building is unlimited
unless you want a limit. → *Do you want a time limit on the walk?*

**Q16. How bad does the building get?**
Default: **loitering AIs = one per two synthetics hired** (0, 1, 2, 3 at the end of a bad run), and they
appear on **lower floors first**. → *Confirm the rate.*

**Q17. Do the people you talk to have names, and are any of them the applicants you hired?**
Default: **yes to both** — they are the people you hired, by name, which is what makes a weird one land. Their
names are hidden only in the *night* scene when they are acting strange, so the next morning's investigation
has something to resolve. → *Confirm.*

---

## E. THE DAY SUMMARY FILM (Phase 4, rounds v121–v123)

**Q18. What does "what happened to the company" actually show?**
Default: **three beats on a still** — *who started* (names, from the roster you hired), *what it cost*
(a security line, a trust line), *tomorrow* (applicants and quota). Two or three frames, text fading over the
room art, narration on top. → *Do you want the animation to show the office changing (a new person at a
desk, a chair empty), or stay with text over a still?*

**Q19. Narration: one voice or several?**
Default: **one narrator** (the same DISPATCH voice as the intro, so the game has one voice), **14 s target,
18 s max** per day, 4 variants × 5 days but only **8–10 recordings** in practice.
→ *Confirm one narrator, or do you want a second "company" voice?*

---

## F. SCOPE AND ORDER (the questions that decide the schedule)

**Q20. Do you still want the menu before the HUD pass?**
Default: **yes** — the menu is where **consent** and **audio settings** live, and consent is the one field in
the save that nothing in the game ever sets. It is also the cheapest round in the plan.
→ *Confirm, or say "skip the menu for now and go straight at the HUD".*

**Q21. Is Android still a target this cycle?**
This has been open three times now. Default: **yes, but after Phase 1–2**, and before the art pass, because
the 20:9 layout is the only remaining thing that can change the design.
→ *Yes / no / not this cycle.*

**Q22. What may I cut if you want this sooner?**
Default, in the order I would cut: the alerts **dashboard** (keep alerts, defer the breakdown) → **elevator
navigation** (keep the walk as pages, add only the people) → the day-summary **animation** (keep narration
over the existing text). → *Confirm the cut order, or protect one of them.*

---

## The five answers I actually need to start Phase 1

If you only answer five things, answer these: **Q1, Q5, Q4, Q20, Q21.**
Everything else can wait until its phase, and every one has a default you can accept with "yes".
