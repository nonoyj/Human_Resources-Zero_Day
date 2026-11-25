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
