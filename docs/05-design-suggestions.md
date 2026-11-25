# Design Suggestions (beyond the brief)

These are the ideas I'd fight for. Each one is here because it either **teaches better** or
**costs nothing extra to build**.

## 1. The player must submit a *reason*, not just a stamp
A bare Approve/Reject is a coin flip. A reason dropdown (5–6 categories, see below) turns the
game into an assessment and gives the end-of-day debrief something concrete to teach from. It
also generates your best stat: *"You rejected 4 people for document forgery. In the real world,
that's the rarest of the 5 categories."*

Rejection reasons (mirroring the 5 evidence categories):
`SYNTHETIC MEDIA` · `DOCUMENT INCONSISTENCY` · `IMPERSONATION / IDENTITY CLONE` ·
`COERCION / BAIT` · `SOCIAL-ENGINEERING PRESSURE` · `INSUFFICIENT REQUIREMENTS`

## 2. The Field Manual is the curriculum, and it's diegetic
Each new tell enters the game through an in-fiction channel: a security bulletin, a "training
memo", a colleague's email. The Manual *entry unlocks* when the player encounters it. The player
can pause and read it mid-shift (no penalty). This is what makes the game usable by a
non-cybersecurity audience **without** a tutorial popup that breaks the fiction.

Suggested curriculum order (one concept per day):
1. **What a synthetic applicant looks like** (perfect-but-flat resume, artifacts in the portrait)
2. **Documents talk** (cross-reference: grad year vs experience, ID format, referral mismatch)
3. **Humans as bait** (a real, desperate person applying on someone else's behalf — treat them as a victim, not the enemy)
4. **The inbox attacks** (lookalike domain, urgency, authority, unexpected attachment, "just click this")
5. **Golden opportunity** (fast-track hire request from a "director" — the composite trap)

## 3. "Human bait" should be the emotional core
Your brief mentions humans used as bait. Make one of them the best-written character in the game:
a laid-off parent applying with an AI-generated resume because they can't compete. The *correct*
action is nuanced — reject the application, but do not treat the person as the threat. This is
where the game earns its message about job displacement instead of just stating it.

## 4. Never punish a decision the player had no evidence for
Fair-play rule: **every AI applicant must be detectable from information on screen.** No
"gotcha, the AI was perfect." If a player loses, the debrief must be able to show exactly the
evidence they overlooked. Unfair losses teach nothing and make an event demo feel broken.

Corollary: difficulty scales by **how many tells overlap and how noisy the distractors are**,
never by hiding facts or adding timer panic.

## 5. Escalating corruption you can *see* (cheap, high impact)
The office background degrades across the 5 days: clean on Day 1, subtle glitch artifacts on
Day 3, red emergency lighting and flickering monitors on Day 5. One set of background variants
communicates the stakes with no dialogue. It also gives the player a felt sense of "I am losing
control" without an explicit meter.

## 6. End-of-day summary: triple structure
1. **Facts** — who you approved/rejected, quota met/missed, trust delta.
2. **Hint** — one diegetic line, never a confession, e.g. *"Overtime request log shows a new
   employee accessing the archive at 03:14. No one was rostered."*
3. **One taught concept** — the Manual entry they used (or should have used), summarised in a
   sentence.

## 7. Defeat endings should teach a rule, not scold
Each ending names the *specific* mistake and its real-world equivalent:
- *The Perfect Employee* (hired 3 synthetics) → "Consistent perfection is a red flag; humans have gaps."
- *The Ghost Quota* (rejected too many humans) → "Over-suspicion is also a failure mode; verify, don't guess."
- *The Broken Link* (clicked a phishing link) → "Check the domain character by character; hover/expand before you tap."
- *The Open Door* (granted database access) → "Least privilege: nobody needs full access for a routine task."
- *Golden Handcuffs* (accepting the fast-track offer) → "Urgency is the oldest lever in social engineering."

Always end on the hint + "in the real world you would have…" line, per your brief.

## 8. Menus: exactly what a mobile player needs
**Boot** (logo, save init, offline check) → **Intro** (skippable, auto-skippable after 6 s of no touch) →
**Main Menu**: `CONTINUE` / `NEW SHIFT` / `FIELD MANUAL` / `SETTINGS` / `CREDITS`.
- Continue is the big button when a save exists; New Shift warns before overwriting.
- Settings: Master/Music/SFX sliders · text speed · auto-advance cutscenes · haptics ·
  reduce motion · high-contrast / colour-blind safe mode · font scale · left/right-handed
  layout · language (stub) · reset save (double-confirm).
- No dead ends: every screen has back, and back never loses progress.
- Pause anytime from the desk HUD; the game state is serialisable so pause = save.

## 9. Free "portability" wins
- Autosave at every screen transition; the shift is short enough that quitting mid-day is safe.
- Resume exactly where you were, including which applicant you were on.
- "Quick Shift" (1 day) mode for event queues and for players who want a 3-minute session.
- Everything works offline. No accounts, no permissions beyond storage.

## 10. Two things that make or break the demo at an event
- **First 40 seconds.** The intro must reach the first applicant fast, or people walk away.
  Budget: ≤ 25 s intro (skippable), ≤ 15 s to the first inspectable document.
- **One visible "aha" moment.** At least one applicant on Day 1 should be catchable by anyone
  with zero training (open a suspicious email, look at the portrait) so the booth demo pays off
  immediately and the deeper lessons can land later.
