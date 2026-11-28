# 05 — PHASES: where we are, what is left, and what must not be forgotten

Always answer this, in this shape, in every reply.

## The board

| # | Phase | Bar | State |
|---|---|---|---|
| P1 | **Vertical slice** — desk, applicant, CRT, letter, stamp, day summary | `[########..]` 8/10 | playable end to end |
| P2 | **The loops** — 5-day run, scoring, reasons, trust, corruption | `[#########.]` 9/10 | arc + 3 endings tested (v101); reason list still short |
| P3 | **The after-hours sweep** — walk, traces, pages, key strip | `[##########]` 10/10 | **done v97** |
| P4 | **Save, resume, audio, endings, intro** | `[########..]` 8/10 | save/resume/endings/room/7 cues; spoken intro waits on BE2 |
| P5 | **Replenish** — art sweep, look, animation feel | `[##########]` 8/8 | **COMPLETE v119.7** |
| P6 | **Ship** — Android build, QA matrix, store assets | `[###.......]` 3/8 | started v119.6 |

`~55 %` behind us. The rest is breadth (audio, intro, ship), not new mechanics.

## P5 is CLOSED, so what is this round?

**v129 is a P1/P2 round re-opened on purpose.** P5's art sweep approved the *menu*; the game room never
inherited it. The player has now ruled that the game room must be the menu's room. So:

- **v129 belongs to P1** (the desk is P1's subject), and it does not close P1.
- P1's two remaining items are **audio hooks** and **the Android build handoff** — both untouched by v129.

## P1 — steps left

1. ✅ desk boots and plays
2. ✅ 9 hotspots that look clickable and respond where drawn
3. ✅ read-out, letter, stamp, day summary
4. ✅ after-hours walk on its own band
5. ✅ walk page placement (v97)
6. ⬜ stamp + reason loop teaches (the reason list is short)
7. ✅ 5-day run with an ending (v101)
8. ⬜ audio hooks
9. ⬜ Android build
10. ⬜ **the room is the menu's room** ← v129, this round

## P6 — steps left (the player asks about these too)

1. ✅ live hires + security in the HUD
2. ✅ ALERTS popup explains the rating
3. ⬜ QA matrix on three screen shapes
4. ⬜ Android build + signing
5. ⬜ store assets (icon, 3 screenshots, short description)
6. ⬜ the consent screen (owed; see below)
7. ⬜ the Field Manual screen
8. ⬜ the endings set finished (3 built, 5 specified)

## PENDING, PLANNED, AND NOT DROPPED

| Item | Where it lives | When | What it needs |
|---|---|---|---|
| **Day/night cycle** | `RoomLighting` + `office_L0/L1/L2` | next art round after v129 | `office_M0` repainted in three shift stages, `ScreenLight` reading the clock instead of one fixed state, a gate that measures all three |
| **Staff photo on the CRT** | `MonitorScreen` (text only today) | this round, as a spec item; the art follows the photo pass | 96×96 ID portrait sprite per applicant, cropped from the same sprite (`docs/08` §5), drawn on the glass beside the card text |
| **Applicant portraits, 96×96, with artifacts** | `docs/00` 3.6, `docs/07` | with the photo pass | the synthetic tells must be *in* the portrait (asymmetry, blend seam, odd light) |
| **Consent screen** | owed | before the ship build | the most important missing screen |
| **Analytics** | owed, never emitted | ship | one event per decision |
| **5 endings vs 3 built** | content | content owner | copy |
| **Field Manual** | owed | P6 | teaches the tells |
| **Flag panel 1.2a, stamp animation, emails screen, 12 day-events** | `docs/86` | staggered through P2/P6 | — |
| **The floor chevrons** | cosmetic, BUG-021 | next art round | the `spacing = max(16, TILE_W - ri)` dither |
| **The 1 ERROR in the player's Unity status bar** | unknown | **now** | the player must paste it; nothing in the repo can guess it |
| **The floor chevrons** (pale diagonal lines on the floor dither) | cosmetic | next art round | `spacing = max(16, TILE_W - ri)` in `floor_band` |

## Cut, deliberately — do not re-open

Google Docs handoffs · Git LFS · portrait orientation · server backend for v1 · real-time shadows on the desk ·
player face customisation.
