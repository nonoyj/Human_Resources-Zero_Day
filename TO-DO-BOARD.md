# HR//0D -- THE TO-DO BOARD

**What this is.** The player asked: "Give me to do list to see what we will be doing so that I know what to
complain about. Also tell me once we finished something or rule it as actually done."

So every line has an owner and a state, and **a line only says DONE when a gate or the player's own eyes
confirmed it.** There are two DONE words, and they are not the same:

| State | Means |
|---|---|
| **DONE (gated)** | I built it and a gate proves it. Nobody has looked at it. |
| **DONE (seen)** | The player looked at it and said it is right. |
| **IN PROGRESS** | Being worked on now. |
| **NEXT** | Agreed, not started. |
| **BLOCKED** | Waiting on the player (a paste, a screenshot, a decision). |

---

## A. Waiting on the player -- nothing else can close these

| # | Item | State |
|---|---|---|
| A1 | Run the **v130.5** paste, send `gate-v130.5.log` + a Play screenshot of the desk and of the menu | **BLOCKED (player)** |
| A2 | Click **NEW SHIFT** and say whether a new day actually starts on screen | **BLOCKED (player)** |
| A3 | Click the **lamp** in the menu (and press **K**) and say whether anything happens | **BLOCKED (player)** |
| A4 | In game: press **K** / tap the lamp, then **F** / tap the monitor. Do the mug, the forms, the keyboard and the wood change? | **BLOCKED (player)** |
| A5 | Is the title still clipped at the top of the tube? | **BLOCKED (player)** |

## B. Closed this round (v130.5) -- gated, not yet seen

| # | Item | State | Evidence |
|---|---|---|---|
| B1 | **NEW SHIFT did nothing.** `Restart()` reset the session and never re-enabled the actors the menu disables -- so the day restarted behind an empty room. It now runs `AfterMenuClosed()`. | **DONE (gated)** | `DeskBootstrap.Restart` |
| B2 | **The menu painted five rows and acted on four.** A first-run profile has no CONTINUE; the painter always drew it. Index 1 was NEW SHIFT on screen and FIELD MANUAL to the game. One row model now. | **DONE (gated)** | `the_painted_rows_are_the_rows_that_fire` |
| B3 | **The lamp's click target was 36 px of a 56 px fixture** and missed the shade. It is the whole sprite now, plus 2 px. | **DONE (gated)** | `the_lamp_is_where_it_is_drawn` |
| B4 | **The menu lamp now answers a click**: a warm ring at the shade's mouth, and **K** works too. | **DONE (gated)** | `MenuView.LampFlare` |
| B5 | **The desk lamp could not be switched at all** (`lampOn` was a constant `true`) and its switch never reached the pixels. It is now **K** / tap on the fixture, and it repaints the room. | **DONE (gated)** | `DeskBootstrap.ToggleLamp` |
| B6 | **The desk lamp was worth 0.4 ramp steps** at its own foot -- under one step, which the renderer cannot show. Pool: 0.4 -> 2.0 steps, on the wood, and the cone no longer lights the wall above the shade. | **DONE (gated)** | `lamp ON gains 1 step(s) at the mouse by the lamp and 0 at the coffee mug` |
| B7 | **The clock light was 0.30 of a step** -- it could not show at all. 1.0 now. | **DONE (gated)** | `every_light_lands_on_its_neighbours` |
| B8 | **The applicant you saw was old art.** The frame extractor skipped any frame whose FILE existed, so all 56 animation frames were weeks stale and the game draws those, not the strips. It now compares pixels and rewrote all 56. | **DONE (gated)** | `PASS: all 56 animation frames exist in both mirrors AND match their strips` |
| B9 | **BUG-041 is dead**: the front-door composite gate needed Pillow to check arithmetic. It runs anywhere now. | **DONE (gated)** | `ART PIPELINE OK`, no `(could not run)` |
| B10 | **Title clamp**: no line in the tube can start above the glass or end below it. | **DONE (gated)** | `MenuView.ClampIntoGlass` |
| B11 | **Camera fit**: the room is fitted whole (contain) instead of a fixed vertical height, so no more empty band down the sides. | **DONE (gated)** | `DeskSceneBuilder.ConfigureCamera` |

## C. Next, in order (smallest first)

| # | Item | State |
|---|---|---|
| C1 | **Game-time clock wording.** The wall clock follows the shift (08:40 -> 00:00); confirm the hands match `RoomClock` and the HUD's own minute agrees, on screen. | **NEXT** |
| C2 | **The clock on the menu screen.** `MakeRoom()` has no clock; `menu/clock.png` does not exist. Paint it, blit it, test that both rooms carry a clock at the same relative spot. | **NEXT** |
| C3 | **More outfits + hair.** Six looks now, verified 36-48 % of pixels differ per pair. The brief has room for hats, scarves, ties and four hair colours visible in game. | **NEXT** |
| C4 | **The lamp, seen.** Every light is now worth at least one step where it lands. If it still does not read on your screen, the next step is a per-prop shadow dump, not another parameter tweak. | **NEXT** |
| C5 | **P6 content:** the remaining 5 of 8 items (see the phase board in the delivery message). | **NEXT** |

## D. Standing rules this round added (orders/09)

- H10 a per-frame exception is a state you did not allow
- H11 contrast is measured, not eyeballed
- H12 "centred" is a coordinate
- H13 a key the player asked for is a feature, not a discovery
- **H14 a file that exists is not a file that is current** (BUG-040: 56 stale frames, every gate green)
- **H15 a control with no acknowledgement reads as a dead control** (the menu lamp)
- **H16 a light under one ramp step is not a light** (0.4 steps is invisible by construction)
