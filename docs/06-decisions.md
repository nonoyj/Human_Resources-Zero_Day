# Decision Record (ADR)

Locked decisions, newest first. Change requires a new entry — never edit history.

---

## ADR-006 — Detection = diegetic core + 3 limited-use verification tools
**Date:** 2026-11-25 · **Status:** Accepted

The player never sees an "AI: yes/no" flag. All identification is inference from evidence across
5 categories. On top of that, the player has a **VERITAS toolkit** — three checks, each with a
daily budget and each *imperfect on purpose*:

| Tool | Uses/day | What it does | How it can betray you |
|---|---|---|---|
| `ID REGISTRY` | 2 | Looks up an ID number in the national registry | The AI has been poisoning the registry — a synthetic may return "VALID" |
| `METADATA SCAN` | 2 | Reveals provenance marks on the submitted portrait | Clean metadata on a synthetic image that was re-captured via a real camera |
| `REFERENCE CALL` | 1 | Contacts the listed referee | The referee is a human bait or an AI-run voice line |

**Why:** tools teach that *verification is not the same as proof*. Giving the player a button that
solves the game would remove the skill; giving them a tool that lies sometimes teaches the real
lesson (defence in depth, corroborate with a second source).

**Player-facing teaching line (Field Manual):** *"A tool tells you what a record says. Only
corroboration tells you what's true."*

---

## ADR-005 — Landscape orientation
**Date:** 2026-11-25 · **Status:** Accepted

Landscape, locked. Consequences:
- Base art canvas **480 × 270** (PPU 16, integer 4× on a 1080p phone). Matches the "desk terminal" fantasy.
- Two-column desk layout: left = document stack / inbox, right = verification tools + stamps + HUD.
- All interactives inside the **safe area** (notch/camera cutout on the long edge in landscape).
- Thumb-reach: primary actions (stamps) anchored bottom-right; back/back-out bottom-left; nothing
  interactive in the top 15% of the screen.
- Event play: players will be handed a phone — landscape means two hands. Mitigated by large
  targets and a pause-anywhere save.

---

## ADR-004 — Target: "product-quality build, demo-sized content"
**Date:** 2026-11-25 · **Status:** Accepted (with a Day-6 gate)

You chose *finished product-quality*. I'll hold us to that bar for **quality**, but content volume
must stay demo-sized or the quality bar eats the deadline. Definition:

**In scope (must ship):** 5 playable days · ~21 applicants · 12 emails/events · 5 ending screens ·
full intro sequence · menu + settings + character creation · local save/resume · zero known P0/P1 ·
60 fps on a mid-range phone · sound complete.

**Not "product" in the store sense, and that's deliberate:** no app-store listing, no analytics,
no cloud, no localization beyond English, no achievements, no endless mode, no tutorial video.

**The gate — Day 6, after the vertical slice:** if the slice isn't solid, we cut in this order:
(1) Day-5 composite content → 4 days, (2) REFERENCE CALL tool, (3) animated intro → static
storybook with fades, (4) applicant count 4→3 per day. We never cut responsiveness, save safety,
or the debrief screens — those *are* the product.

---

## ADR-003 — Three hired AIs = defeat, with visible corruption
**Date:** 2026-11-25 · **Status:** Accepted · supersedes the "1 AI = loss" option

- **1 hired AI:** warning beat (+1 anomaly in the office, a colleague email hints at odd access logs)
- **2 hired AIs:** visible damage — corrupted UI elements, a data-loss event, trust drop
- **3 hired AIs:** game over → *The Perfect Employee* ending

Visible corruption is implemented as a `CorruptionLevel` (0–3) that drives background variant,
glitch overlay intensity, audio interference, and an occasional 1-line story beat. One integer,
huge narrative payoff.

---

## ADR-002 — Team profile: capable coders who need guides, and speed matters
**Date:** 2026-11-25 · **Status:** Accepted

All three can code and navigate a codebase; none needs hand-holding on syntax. What they need is
**structure and pre-made decisions**, not tutorials.

Therefore:
- I write the core systems as **complete drop-in files** with XML doc comments (no "TODO: implement").
- Each module has **exactly one owner**; no shared files except `GameEvents`.
- Guides are **click-path docs** (`docs/guides/*.md`), short and literal.
- Prefer fewer, dumber systems over elegant abstractions. No DI framework, no ECS, no addressables.
- Every day ends with `main` runnable.

---

## ADR-001 — Local git repo here; remote is the team's
**Date:** 2026-11-25 · **Status:** Accepted, remote URL pending

The GitHub connector isn't available in this session, so I keep a real git repo in this workspace
with reviewable commits. When you paste the remote URL I'll produce the exact PowerShell to
attach and push (including LFS init). Until then nothing is blocked — the repo works standalone.

---

## Still open (needs you)

1. **GitHub remote URL** (you said you'd paste it).
2. **Event date + how long each playtester gets at the booth** — drives session length and intro budget.
3. **Is there actually a backend/server in this project** ("communicate with the backend group") —
   or is "backend" your team's internal name for the systems team? This determines whether I write
   a network layer at all. My assumption: **no server**.
4. **Do you have a name for the studio/team?** Needed for the icon, splash, and credits.
5. **Do you want a 5-slide pitch deck** for the event?

---

## ADR-007 — Desk composition: monitor left, applicant right
**Date:** 2026-11-25 · **Status:** Accepted

The interview is a physical scene, not a paperwork metaphor: **the monitor occupies the left half**
of the screen (resume, ID, cover letter, metadata, tools, stamps, reason dropdown) and **the
applicant sits across the table on the right half**, live and animated. Both are always visible.

Consequences:
- Behaviour becomes evidence-adjacent: nervousness invites investigation, never rejects alone
  (`docs/08-behaviour-model.md`).
- Art budget shifts to the right half (applicant sprites: 192x216, 3 corruption background variants).
- Any inspect/zoom overlay anchors left so the applicant is never hidden.
- "Reduce motion" and colour-blind settings must not remove any tell (they only change presentation).

## ADR-008 — Randomised runs with proven fairness
**Date:** 2026-11-25 · **Status:** Accepted

Applicants, identities, tells, distractors, emails and attack events are generated per run from a
single seed. No campaign is authored by hand, though content (names, wording pools, emails) is.
Fairness invariants I1–I8 (`docs/07-randomization-spec.md`) are enforced in code and EditMode tests,
and are already validated by `prototype/generator_sim.py` (0 violations / 1,000 runs).

Consequences:
- Every bug report must include the run seed.
- Determinism is a feature: same seed = same run (QA, debugging, replay).
- Content tuning happens through weights/pools, never by hardcoding a fixed applicant.

## ADR-009 — The intro is spoken by DISPATCH, and the slot is silent until the voice exists
**Date:** 2026-11-27 · **Status:** Accepted (player decision)

DISPATCH narrates the intro beats as a **radio voice**, not silent text. The voice is produced by the
backend/content side (BE2), one clip per beat.

Consequences:
- `content/intro/intro_01.json` grows an optional `audio` field per beat (path + sha256), and the client
  plays it if present. **The client does not require it**: with no audio the card shows text and holds for
  `hold` seconds exactly as it does today. That keeps the intro shippable while the voice is recorded, and
  it keeps a missing/mis-signed clip a silent degrade rather than a crash.
- A beat's `hold` becomes a MINIMUM when audio is present: the card waits for the clip to finish (capped).
  Radio pacing is the writer's job, so the file must not cut a line off mid-sentence.
- The intro no longer needs a separate "skip" affordance to be bearable — but skip stays (X/ESC), because
  a voice on first launch is exactly what returning players want to skip.
- Audio pass is therefore two things, in this order: (1) the spoken intro, (2) the room bed + cues.

## ADR-010 — The game starts itself; R is replay, not the way in
**Date:** 2026-11-27 · **Status:** Accepted (observed behaviour, confirmed by the player)

Earlier builds required pressing **R** to begin a run. That is gone: `GameBoot` builds the desk and the
first shift starts on its own (`DeskBootstrap.Start`), and **R** now means "start a new run" (it re-shows
the intro, because a new run is a new first minute). Nothing in the shipping path waits for a keypress, and
no doc should imply otherwise.

## ADR-011 — A voiced intro gets 32 seconds; a silent one keeps 25
**Date:** 2026-11-27 · **Status:** Accepted (player decision, docs/87 W-1)

At 150 wpm plus breath the five shipped intro beats come to **~30.5 s**. The intro's own budget was 25 s, so
the first time DISPATCH's voice was recorded, the last line would have been cut by a rule nobody would have
noticed until a playtest. The player chose to raise the cap rather than cut a beat.

Consequences:
- `IntroScriptRules.MaxTotalSecondsVoiced = 32`, `MaxTotalSecondsSilent = 25`. **The cap is chosen from the
  beats**: any beat carrying an `audio` clip switches the script to the voiced cap, so the same JSON can be
  written silent today and voiced tomorrow without losing a line.
- A voiced beat's `hold` is a *minimum*: the card waits for the clip, capped at `AudioMaxHold = 12 s`.
- Covered by a test (*"a voiced intro gets 32 s and keeps its beats; a silent one is still cut at 25 s"*),
  because this is exactly the kind of number that gets quietly changed later.
- The intro stays skippable (X/ESC) and once per profile, which is why 32 s is not a burden.

## ADR-012 — The Security Rating: one number, visible during a shift, explained by a popup
**Date:** 2026-11-27 · **Status:** Accepted (player decision, docs/88 Q1–Q4)

The company has **one number, 0–100, starting at 70**, banded `STABLE` (70+) / `SHAKY` (40–69) /
`BREACHED` (<40), built from **trust 60 % + corruption 30 % + synthetics hired 10 %**. It is visible **during
a shift**, and clicking ALERTS opens a popup that shows how the number is made. It can rise as well as fall.

Consequences:
- **The scale is anchored.** A healthy company (full trust, corruption 0, no synthetics) reads exactly 70;
  catastrophic reads 0. Without the anchor, "start at 70" and "60/30/10" contradict each other — a fresh run
  computes 100, the HUD opens on a perfect score, and there is nowhere to go but down. The first test written
  for this caught exactly that.
- `Breakdown()` scales and anchors identically to `Compute()`, so the three contributions on the popup always
  add up to the number on the HUD (asserted across 200+ input combinations).
- Four alert kinds only: **a synthetic was hired** (loudest), **two mismatches on one file**, **a verification
  contradicted the application**, **corruption rose a level**. Fact outranks inference: a double mismatch is
  severity 1, a verified lie is severity 2.
- Alert lines are **upper case**, because they are painted with the 5×7 atlas. The atlas gate caught sentence
  case on the first pass.

## ADR-013 — The inbox arrives as an event, not as a screen you open
**Date:** 2026-11-27 · **Status:** Accepted (player decision, docs/88 Q11)

Mail does not sit in a menu waiting to be read. **The system triggers an inbox event randomly**, and the
player reads it when it happens. This changes the shape of the email feature from "a screen" to "an
interruption with a decision in it", which is a better fit for a game about a desk nobody leaves.

Consequences:
- An email event **interrupts** the desk (never mid-stamp: the trigger is checked between applicants).
- Because it interrupts, it must be **skippable/deferrable** — no player should lose a shift's rhythm to a
  random mail. Deferring is itself a choice (see ADR-014: ignoring is a decision with a cost).
- The 8 written emails stay valid; what changes is *when* they arrive.

## ADR-014 — An email decision changes the company, not a counter
**Date:** 2026-11-27 · **Status:** Accepted (player decision, docs/88 Q12)

Refusing / granting / ignoring an email must **not** merely nudge tomorrow's corruption. Each choice has a
**consequence on the company**: refusing a genuine employee's access request loses you a colleague (a named
person, who then appears in the walk with a problem); granting a real attack raises corruption; *ignoring* is
never free. The wrong choice must be felt as a story beat, not as a number.

Consequences:
- Every email needs **three outcomes**, each with: a company effect, a security-rating effect, and **one line
  of consequence** the next morning. That is a content cost (8 emails × 3 outcomes) and a rules table.
- The consequence must be **visible**: a person who no longer appears, a name in the day summary, an alert.
- This makes the email system a *source* of day events rather than a parallel feature.

## ADR-015 — "From an AI" is a real third category
**Date:** 2026-11-27 · **Status:** Accepted (player decision, docs/88 Q13)

Emails are **legit / attack / from an AI**. The AI category is not a writing style — it is mechanically
different, and it is what ties the inbox to the game's core question ("is this a person?").

Consequences:
- A third rule set and a third tell family, so the inbox needs a **spec paragraph in `docs/07`** before code:
  what makes an AI-authored email provable on the day it arrives (the fairness invariant applies here too).
- The intro, the interview and the inbox then all ask one question in three registers, which is the reason
  to do it at all.

## ADR-016 -- The front door: a menu, a consent that is a decision, and the two settings that matter (2026-11-27)

**Context.** Q20: "the menu comes first." The save file has carried `profile.consentGiven` since v090 but
nothing ever set it, and the audio settings added in v105 lived in memory only -- they were lost on quit. The
game also had no way back to a menu once the desk was up.

**Decision.**
1. **The menu is the first thing after the room loads, and the intro plays behind it, never under it.** A
   full-screen card over a full-screen card is a bug the player would report as "the game froze".
2. **Consent is a decision, not a default.** `profile.consentDecided` + `consentGranted` + `consentVersion`
   are written the instant the player toggles. Declining is recorded as an answer, so the game stops asking;
   `consentVersion` means a changed promise is re-asked rather than silently inherited. The notice is ten
   lines, in the atlas, saying what is recorded, what is never recorded, why, that it is off by default, and
   that saying no changes nothing.
3. **`consentGiven` (pre-v110) is kept as a load-only field** so old saves do not lose their answer.
4. **The menu paints in the game's own pixels**: one 480x270 RGBA32 texture, 5x7 atlas, sorting order 240,
   above the intro card's 230. A settings menu in Unity's default UI font would be the only thing in the game
   that looks like a different game.
5. **Every label is a paintability constraint, not a style choice** -- upper case, atlas characters only, and
   short enough for one line of the card. `tools/comp/preview_menu.py --check` fails the build if a label
   grows past what the atlas can draw or the card can hold.
6. **CONTINUE exists only with a save** (and says "CONTINUE THE RUN" when a run is in progress); a fresh
   profile gets START. No dead rows, ever.
7. **Failure is silent**: no atlas, an exception, a missing save -- the desk starts exactly as it did in v109.
   A front door that can stop the game opening is worse than no front door.

**Consequences.** Returning to the menu is `ESC` at the root screen; the run is not lost because the desk
never stopped running. New Run restarts the first minute. Android gets the same screen later (after Phase 2),
which is why nothing here is keyboard-only in shape: the router treats a tap as CHOOSE.

### ADR-016a -- Corrections after the first play of v110 (2026-11-28, v111)

The player installed v110 and played the door. Three findings, two of them real bugs:

1. **The consent checkbox was decoration.** v110 painted the checkbox under the notice and put the actual
   decision on a second row called CHOOSE, so pressing SPACE on the thing that looked like a checkbox did
   nothing. The row the player sees must be the row that does the thing: the consent screen is now
   `[ ] SHARE DATA` (row 0, toggles and saves on SPACE) and `BACK`. BUG-065c.
2. **The volume only went up.** One key was wired to each volume and it was "increase", so a player who
   turned a sound down had no way back. LEFT/RIGHT (and A/D) now step the selected row down and up; SPACE
   steps it up so a single key still works. BUG-065a.
3. **ESC skipping the intro is correct, and the last reply promised otherwise.** The intro is once per
   profile (`profile.introSeen`), written the moment it finishes. A fresh run is `R` (ending) or NEW RUN
   from the door, and both clear the profile, so both show the intro. Doing that on CONTINUE would put a
   five-beat story in front of a player who is mid-shift. **Promise corrected, behaviour kept.**

Also fixed: the consent notice was 48 characters wide (over the card) and the layout ran its last line
onto the border. The notice is re-wrapped at 46, and `tools/comp/preview_menu.py --check` now fails the
build if any card's text can outgrow the card -- the geometry is a gate, not an eyeball.

## ADR-017 -- The front door is the room, not a card (2026-11-28, v112)

**Context.** The player supplied the menu concept they are asking for: the menu type inside a large
monitor, the night city behind it, a server on the right, coffee and applicant folders on the desk, and a
cyan glow from the screen and the window. v110/v111 shipped a 480x270 text card floating over the room --
legible, but nothing to do with the room the game is set in. The player's words: *"the menu items are on
the pixel computer, then a coffee underneath with applicant files on the table, then monitors in the
background with an aesthetic view of the city in the night with a server on the side."*

**Decision.**
1. **The front door composes its own frame** from kit sprites (background, `monitor_big`, `server_rack`,
   `desk_foreground`) at the coordinates in `desk_layout.json.menuFrame`, and paints its text into that
   monitor's glass. Gameplay keeps `monitor_small` and **the desk layer list is unchanged** -- adding a
   second monitor to it would have drawn two on the same desk, since the desk scene is built for every
   shift.
2. **A second, larger monitor exists as its own sprite**: `desk/monitor_big.png`, 260x170 with 236x144 of
   glass = **31 characters on 14 lines**. The desk monitor holds 17 x 8, which cannot carry a title, five
   rows and a hint. Same drawing language (1 px frame, vent strip, power LED), so it does not read as a
   different game.
3. **The server is drawn**, `desk/server_rack.png` 34x92: eight vented bays, three live LEDs, one amber
   bay pulled out. Kit art through the same palette gate and manifest as everything else.
4. **The cyan glow is flat palette steps**, never a blended alpha: a two-step ring outside the bezel plus
   a scan-row pool on the desk. Same rule as the corruption states -- light falls off in steps.
5. **No background animation in v112.** The menu is still, because a room that moves behind a menu that
   does not is worse than a still room. Flicker and city movement are a later pass (P5 art), and the
   concept's motion is recorded in `docs/87` rather than half-built.
6. **The two sub-screens use the same glass**, with cut-down text: the full 10-line consent notice lives
   on the big card (it does not fit 14 rows with a decision under it), while the glass carries the short
   honest version -- records / never / off by default / the decision.

**Consequences.** `tools/art/make_menu_composite.py --check` fails the build if any menu line cannot fit
the glass, so the composition is a gate and not an eyeball. MenuView renders into a 236x144 texture placed
at the glass rect; the game keeps one code path for painting (MonitorScreenRaster + the 5x7 atlas).

## ADR-019 — The front door is a specific room the player described (2026-11-28, v115)

**Context.** ADR-017 put the door in a room but composed it from gameplay sprites, and v114 replaced that
with a concept frame built from wall labels and an ID-card widget. The player rejected both and reset the
look with a description of the room itself: an old monitor on the left with the title on its screen and the
menu under it; on the desk a coffee with smoke, a keyboard, a mouse and a pile of application forms; a cyan
fade coming *out of the monitor*; behind it, in the furthest part of the room, a table of six white
screens (three under, three above) and two more glowing under the table; beside the table a window with a
city silhouette, lit windows, night sky and moon; to the right of the window, in the corner, a database
server; tiled floor with black outlines. Dark and eerie, cyan, pixel, in the register of *That's Not My
Neighbor*.

**Decision.**
1. **The menu is drawn inside the monitor**, on its glass, in the 5×7 atlas. No overlay ever prints on the
   room: a second layer over the door is a defect (v113).
2. **The room is six generated sprites**, not a hand-drawn scene: `menu/shell`, `menu/window`,
   `menu/monitor_stack`, `menu/server`, `menu/crt`, `menu/desk`. They are palette-gated like the rest of the
   kit and composed back-to-front by `MenuView.BuildRoom()`.
3. **Lighting is a value ramp, not paint** (BUG-068). A light adds at most one step to a surface's own
   position on `MenuLayout.Ramp`; it never substitutes a colour, and all sources accumulate before the step
   is taken. Cyan is rationed to the glass edge, two halo rings, and an 84 px fade that only lands on dark
   surfaces. `docs/16-light-study.md` is the reasoning.
4. **The glass is a light source and is never lit.** Every pass skips the monitor's rect; the tube is painted
   by `GlassMaterial()` before the light pass and the menu is painted after it.
5. **The composition is gate-checked, not eyeballed.** `make_menu_composite.py --check` fails on any sprite
   off the frame, any overlap between the back-wall pieces, or any menu line that does not fit the glass;
   `CheckTheRoomHasEveryPieceFromTheBrief` asserts the furniture relationships (monitor on the desk, table
   and server on the floor, window clear of the monitor, table → window → server left to right).
6. **The room is still.** No flicker, no city movement, no steam animation yet: a room that moves behind a
   menu that does not is worse than a still room. Motion is recorded for the P5 art pass.

**Consequences.** The v114 concept elements (wall monitor bank, ID card widget, wall labels) are gone by
order and must not come back. The title is 2× (206 px) because a 3× title is 309 px and no glass that fits
a 480×270 room holds it; the brief's "old monitor" is answered with a chunky case, not a small one. The
`menu/` folder is in the sprite manifest and the publish prune list, so the sprites reach Unity through the
same pipeline as everything else.

## ADR-020 — The monitor is an object, and the frame has a film of dust on it (2026-11-28, v116)

**Context.** After installing v115 the player reported one defect and gave three directions: *"the text is
offscreen"*, *"make the monitor more dominant and what I asked is an old/retro monitor look, the same with the
background monitors"*, and *"a dusty vignette over the menu with grains; make it more game-like; make the
window a little wider"*.

**Decision.**
1. **A CRT is drawn as the object, not as a "retro" filter**: deep case with feet, thick bottom bezel, cut
   corners on case and tube, a shadow side, a rim darker than the middle, moulded seam, vents, badge, knobs,
   LED, screws. The desk monitor grows to 252×186 with 224×148 of tube; the six back-wall screens are drawn
   in the same language at a third of the size.
2. **Type is measured against the TUBE (`TubeInset`), never the glass rect.** A CRT's picture is smaller
   than its tube; a line that fits the rect can sit on the curve, which is what "offscreen" meant. Every
   line, row and footer is inside `TextX/TextY/TextW/TextH`, and a test asserts it.
3. **The dust is clumped, dark-only, hash-placed and deterministic.** 2×2 blocks; only on
   `void`/`panel`/`panel2`; a Wang hash per block (never a radius threshold, never `(x*A ^ y*B) & 15` — both
   are periodic and both were tried and rejected this round); 0.4% of blocks in the middle of the frame,
   3% at the corners. It never touches a screen or the glass.
4. **The cyan fade is directional**: 58 px sideways, 74 px down onto the desk. An equal reach on every side
   drew a dome over the wall.
5. **The window is 96 px wide and its city is two layers** (far = one value off the sky with no detail, near
   = `void` with lit windows) — atmospheric perspective, which is what makes it read as a city behind glass
   instead of wallpaper.

**Consequences.** `make_menu_composite.py --check` still gates geometry, but the geometry gate only covers the
*back wall*; props on the desk (coffee, keyboard, mouse, forms) are checked by eye, and one of them shipped
completely hidden behind the enlarged monitor now. That check moves into the composite gate next round.
