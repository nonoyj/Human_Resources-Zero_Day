# THE LATER LIST — everything still owed on HUMAN RESOURCES: ZERO DAY

Status date: **2026-11-26** (end of the art-heavy phase). Owner key: **You** = frontend/Unity,
**A/B/C** = the three devs (see `01-roadmap-14-days.md`), **Agent** = me (Arena).

Rules for this file: nothing moves out of here without either (a) a roadmap line being ticked, or
(b) a `06-decisions.md` entry replacing it. If a task is not on this list, it is not in the product.

---

## 0. Where we actually are (one paragraph)

Art is ~90 % done and gated (`Test-ArtPipeline.ps1`, 9 gates). The full 480×270 composition exists,
the RAGGED style is applied kit-wide, the night window is final, applicants have real identity
traits, and corruption wear overlays exist as sprites. **Nothing of this is running in Unity yet
beyond the stub-compiled scene builder.** The game has no loop, no UI screens, no content data, no
audio, no save round-trip, no Android build. That gap — not more art — is what the next phase is.

---

## 1. Blocking before anything else (do these in order)

| # | Task | Owner | Why it blocks | Done when |
|---|---|---|---|---|
| 1.1 | `WearOverlayBinder.cs` — spawn `fx/wear_a` / `fx/wear_b` over the background from `ShiftController.CorruptionLevel` | Agent → You copy | The drift the user approved exists only as files until something reads it | Toggling `SyntheticsHired` 0→1→2 changes the room in Play mode |
| 1.2 | `MonitorScreenRenderer` — composite email/letter + day header + trust bar into the monitor's inner glass | Agent | The screen is the only place the player *reads* the game. Right now it's a blank rectangle | Text renders inside the bezel at 480×270, no overflow, palette-only |
| 1.3 | Data contracts frozen (`05-data-contracts.md` §applicant/event/save) | Agent + A + B review | A and B cannot build screens against a moving schema this late | Two devs sign off in one PR |
| 1.4 | Unity project created from the bundle, `ProjectSettings` committed, IL2CPP/ARM64 verified once | You | Every device bug found after this point is cheaper to fix | A debug APK installs on one phone and shows the desk scene |

## 2. Art still owed (small, and mostly deliberate)

| # | Task | Owner | Notes |
|---|---|---|---|
| 2.1 | Desk-screen **content** art: email paper stack, letter, day header plate, trust-bar segments | Agent | Blocked by 1.3 (needs the real text budget) |
| 2.2 | `fx/` set 2: hire-flash, stamp-press, rejection ink burst, ID-card slide-in | Agent | Must respect the 3 glow steps (bible §3) |
| 2.3 | Menu + intro frames (there is one concept, `menu_concept_A.png`, no kit asset) | Agent | Skiappable intro is a locked requirement — art for it doesn't exist yet |
| 2.4 | Ending cards (5 endings × 1 still each) | Agent | Needs the ending copy from C |
| 2.5 | Field Manual pages (the in-game evidence legend) | Agent + C | Legend text is behavioural, art is the frame |
| 2.6 | Player-cosmetic sprite atlas (name + cosmetics only, **no faces** — locked) | Agent | Small: 4 body presets, 4 outfits, 6 hair, 6 skin, accent |
| 2.7 | Android icon / splash set (adaptive icon, 5 densities) | Agent | Not started; trivial but mandatory for a store build |

## 3. Engineering owed (the bulk of the remaining work)

| # | Task | Owner | Notes |
|---|---|---|---|
| 3.1 | Day loop state machine (arrival → interview → stamp → next; 5 days) | A | `ShiftController` is a stub |
| 3.2 | Applicant generator from data (14 archetypes × facts × artifacts) | A | Art exists; the data binding does not |
| 3.3 | Validation + scoring + suspicion model | A | Rules table not written |
| 3.4 | Save/load round-trip proven (offline-first, resume mid-shift) | A | `SaveData` exists, no round-trip test |
| 3.5 | Inbox / between-shift task screen | B + C | 1–2 emails per shift |
| 3.6 | Stamp + rejection-reason flow (the teaching mechanism) | B | 9 hotspots already authored in `desk_layout.json` |
| 3.7 | Trust meter, day header, corruption feedback | B | Data from A, art from 2.1 |
| 3.8 | All UI screens + transitions + touch input (Input System) | B | Zero screens exist |
| 3.9 | Endings: 5 outcomes, one screen each, correct trigger matrix | A + C | Copy from C, art from 2.4 |
| 3.10 | Analytics events wired (research mandate — non-negotiable) | C | `AnalyticsEvents.cs` exists; nothing emits |
| 3.11 | Android build pipeline: IL2CPP, ARM64+ARMv7, API 24/35, keystore | C + You | One green build, then it stays green |
| 3.12 | QA matrix: 3 screen ratios, low-RAM device, offline, rotation-locked | C | Devices not yet chosen |

## 4. Content owed

| # | Task | Owner |
|---|---|---|
| 4.1 | 12 events written (JSON) | C |
| 4.2 | Applicant fact sets + writing-voice variations | C |
| 4.3 | Intro sequence script (skippable) | C |
| 4.4 | 5 ending texts, tonally distinct | C |
| 4.5 | Field Manual legend text (9 artifacts) | C |
| 4.6 | All strings externalised for later FIL/CEB translation | C |

## 5. Audio owed (none exists)

| # | Task | Owner |
|---|---|---|
| 5.1 | Ambience bed: room tone, distant city, CRT hum | B (audio duty) |
| 5.2 | UI SFX: stamp press, paper, click, rejection sting | B |
| 5.3 | Corruption layer: degrades with `CorruptionLevel` 0→3 | B |
| 5.4 | One short intro/ menu theme — **must be licence-clean** | B + You |

## 6. Deliberately NOT doing (recorded so nobody re-opens it)

- 3 painted corruption rooms (runtime tint + overlay only) — locked
- Face customization for the player — locked out
- Git LFS — rejected with reasons in `.gitattributes` (BUG-010)
- Google Docs for handoffs (markdown in `handoff/` only)
- Portrait orientation (BUG-adjacent: it was the old `3.3` recommendation and is now dead)
- Server backend for v1 — offline-first, local save only
- Real-time URP 2D shadow casters on the desk — shadows are painted; revisit only if a device test shows it is cheap

## 7. Known open questions still unanswered (blocking, but not blocking art)

| # | Question | Default I will use if you don't answer |
|---|---|---|
| 7.1 | Session length target for the shipped product (10–15 min full run vs shorter shifts) | 10–15 min, save-anywhere |
| 7.2 | Soft vs no shift timer | Soft, never a hard fail |
| 7.3 | Language at launch: English only? | English only, all strings externalised |
| 7.4 | Is "fire an employee" in v1? | Yes, 1 per day, costs trust |
