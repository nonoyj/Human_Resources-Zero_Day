# 86 -- WHAT'S OWED: your list, checked against the repo

**Written 2026-11-27 at the rest stop.** You asked, in one message, whether we are still doing eleven
things, then asked what else you had mentioned that we have not done. This page answers each one against
the files, not against memory. Nothing here is new scope -- every row already exists somewhere in
`docs/16-the-later-list.md`, `docs/82-replenish-phase.md`, `docs/91-backlog.md` or `docs/06-decisions.md`.

Legend: **BUILT** = in the game today - **PARTLY** = the logic exists, the screen does not - **PLANNED** =
specified, scheduled, not started - **NOT DECIDED** = needs a choice from you before it can be scheduled.

---

## 1. Your list

| # | Your question | Answer | Where it lives |
|---|---|---|---|
| 1 | **Intro animation telling the story** | **PARTLY.** A skippable five-beat story card ships (v102), but it is *text on a card*, not animation. The frames do not exist (`16-later-list` 2.3, `82` A-7) and the spoken version waits on BE2 (ADR-009). | `content/intro/`, `IntroView.cs`, `82` A-7 |
| 2 | **A menu screen with all the features** | **PLANNED, NOT DESIGNED.** No scene exists (`unity/Assets/Scenes/` is empty; the desk builds itself on Play). Nothing has ever listed the features. This one needs a decision from you first -- see #3. | `16-later-list` 2.3 |
| 3 | **Proper UI + overall look/feel/vibes** | **PLANNED (P5).** This is the replenish phase: 8 art items + 6 animation items + a consistency pass, all scheduled together on purpose so the build was never stopped for each note. | `82-replenish-phase.md` all |
| 4 | **Stamping with accept/reject animation** | **PARTLY.** The stamp works and now *sounds* right (v105), but it lands instantly: no press, no shake, no ink burst, no hire flash. That is `fx set 2` + the transition list. | `82` A-6, `82` M-3 |
| 5 | **A better clipboard holding applicant info** | **PARTLY, and not as a clipboard.** The applicant's file is read through the inspect panel and the letter; there is no clipboard object with pages. The 48 dp inspect list was owed since the interaction branch. | `16-later-list` 0a, `15-interaction-layer.md` |
| 6 | **Emails feature** | **CONTENT EXISTS, NO SCREEN.** `content/emails/*.json` (8 emails, both legit and attack) are written and validated; nothing shows them. The inbox is one of the 12 day-events. | `content/emails/`, `16-later-list` 3.5 |
| 7 | **Short events: legit vs attack emails, decide if it is an AI** | **PLANNED.** This is the day-event system: 12 events spec'd (identity checks, access requests, "urgent" attacks), none built. The rules for telling them apart are not written yet. | `16-later-list` 4.1, `docs/07` |
| 8 | **Applicant applications with links to judge real or suspicious** | **PLANNED, and not yet spec'd as its own mechanic.** Today's tells are behavioural (blink/breathe rhythm) and paperwork flags. "This link is wrong" needs a *new* tell family and a spec line before it is anything else. | new -- see #3 |
| 9 | **Review of applicant information** | **PARTLY.** The letter, the flags logic and the inspect panel work; the **flag panel that draws the five flags and lets you toggle them** is still not drawn (`1.2a`, owed since 2026-11-26). | `16-later-list` 1.2a, `17-paperwork-mechanic.md` |
| 10 | **Overall art, more aesthetic** | **YES -- that is P5**, and it is the single biggest visual win left: faces at 3x (decided: option A, a dedicated portrait), hair silhouettes, body/suit shapes, room lighting balance. | `82` A-1..A-5, `ART-DIRECTION-STATE.md` |
| 11 | **Clickables, "more free" art, more possibility** | **PARTLY, with a constraint.** 9 hotspots are authored and gated by safe-area geometry + a hit-test that is tested; adding more is easy *as long as* each one has a hit area that respects the 48 dp rule. "Free" is the right instinct; it costs art, not code. | `desk_layout.json`, `HotspotHitTest.cs` |

## 2. What else you have mentioned that we have not done

These are the ones I would not want to lose, ordered by how much they matter.

1. **The consent screen.** `profile.consentGiven` is in the save file and **nothing ever sets it**. The
   research mandate (anonymous telemetry, opt-out) is a commitment we made in the docs, and a field that
   nobody asks about is not a consent. **This is the most important missing screen in the game.**
2. **Analytics actually emitting.** `AnalyticsEvents.cs` exists; nothing calls it. Without it the
   "research mandate" is a class file.
3. **Five endings, not three.** The spec says 5 (`16-later-list` 3.9); the game has 3. The other two need
   copy and a trigger.
4. **The Field Manual** -- the in-game legend that teaches the 9 artifacts. Logic and layout exist; the
   pages and legend text do not.
5. **The after-hours sweep's 7 trace strings** are still placeholders (BE2's to write).
6. **The reason list is short** -- a player can be right and not be told *why* clearly enough. That is P2's
   last point, and it is writing, not code.
7. **Android**: no APK has ever been built, no QA matrix, no icon/splash set. P6 is 0/8 -- and it holds the
   only unknowns that can still change the design (letter and key band at 20:9).
8. **"Fire an employee"** -- decided *in* for v1 (`7.4`), never built.
9. **Player cosmetics** -- name + 4 bodies / 4 outfits / 6 hair / 6 skin, no faces. Small, unlocked by the
   save schema, never drawn.
10. **Strings externalised for translation** (EN only at launch, but the strings must be ready for FIL/CEB).
11. **Accessibility**: reduce-motion path (spec'd, M-5) and a colour-blind check that cannot remove a tell.
12. **D-5**: the save `IOException` printed on quitting Play mode. Parked since v87.
13. **Backend's 7 owed items** (storage, rate limits, ops console, purge, deploy, soak, OpenAPI) -- theirs,
    queued in `backend/README.md`.

> **2026-11-27, later the same evening:** the team sent a second list (HUD additions, questions/verification,
> evidence counter, NPCs and elevators in the walk, a narrated day summary). That list is triaged, priced in
> rounds and ordered in **`docs/87-team-updates-plan.md`**, which is now the active plan. This page remains
> the full backlog of what is owed.

## 3. The three decisions only you can make

1. **Does the menu exist, and what is in it?** My proposal: *Continue / New run / Settings (audio: the two
   sliders we already have) / Consent & data / Credits*, with the room already audible behind it. A menu
   also gives the consent screen a home, which is why I would build them in one round.
2. **Is "the application has links" a new tell family?** It is a good idea and it is a *different* skill
   from the rhythm tell -- reading bad URLs rather than watching behaviour. Worth doing, but it changes the
   fairness invariants (a link tell must be provable on the day it appears), so it needs its own spec
   paragraph in `docs/07` before code.
3. **Is Android still the target this cycle?** P6 is the only untouched phase and the only one with real
   unknowns. If yes, the phone smoke test comes before the art pass. If no, P5 first and P6 later.

## 4. The order I would actually recommend

1. **Consent + menu + settings** (one round) -- closes an ethics commitment and gives the game a front door.
2. **Phone smoke test** -- before any art pass, because it can still change the layout.
3. **P5 art**: faces (A-1/A-2/A-3), then room lighting (A-4), then bodies (A-5).
4. **Flag panel + clipboard page** -- makes the paperwork the mechanic it was designed to be.
5. **Stamp animation + fx set 2** -- the single biggest "feel" upgrade per hour of work.
6. **Day events** (emails, attacks, links) -- the content team's 12 events, once the spec line exists.
7. **Intro animation + menu frames + ending cards** -- art that needs the final copy.
8. **P6**: Android build, QA matrix, store assets.
