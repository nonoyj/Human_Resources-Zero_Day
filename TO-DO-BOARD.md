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
| A1 | Run the v130.4 paste, send `gate-v130.4.log` + a Play screenshot | **BLOCKED (player)** |
| A2 | Say whether the applicant is ONE person now, or still three | **BLOCKED (player)** |
| A3 | Say whether the lamp actually reads as a lamp lit by the monitor | **BLOCKED (player)** |
| A4 | Say whether the wall clock is now readable and plausibly "9-ish" | **BLOCKED (player)** |
| A5 | Confirm F (or a tap on the glass) switches the monitor | **BLOCKED (player)** |

## B. Closed this round (v130.4) -- gated, not yet seen

| # | Item | State |
|---|---|---|
| B1 | 486 `ArgumentOutOfRangeException`s per shift | **DONE (gated)** |
| B2 | Magenta Continue / Approve pad (missing `cyan_3`) | **DONE (gated)** |
| B3 | "Texts are hardly visible" -- measured 0.30 luma gap on every label | **DONE (gated)** |
| B4 | Wall clock moved off the monitor, to the free wall's centre | **DONE (gated)** |
| B5 | Monitor now has a power switch (F / tap the glass) | **DONE (gated)** |
| B6 | Six outfits across fourteen applicants | **DONE (gated)** |
| B7 | ArtPipeline / FreshClone no longer FAIL over a missing Pillow | **DONE (gated)** |
| B8 | Apply-Bundle exits 0 on success, 2 on dirty, 1 on failure | **DONE (gated)** |

## C. Next, in order (smallest first)

| # | Item | State |
|---|---|---|
| C1 | **Game-time clock wording.** The wall clock follows the shift (08:40 -> 00:00); the player asked for "in game time" -- confirm the hands match `RoomClock` and that the HUD's own minute agrees, on screen. | **NEXT** |
| C2 | **The clock on the menu screen.** `MakeRoom()` has no clock; `menu/clock.png` does not exist. Paint it (one generator) and blit it, then a test that both rooms carry a clock at the same relative spot. | **NEXT** |
| C3 | **Applicant layer as ONE cell.** Done as documentation last round; the honest fix is a manifest field the loader must honour, plus a test that the layer's width is 192 and not 768. | **NEXT** |
| C4 | **More outfits + hair.** Six looks now; the brief has room for hats, scarves, ties and four hair colours actually visible in game. | **NEXT** |
| C5 | **The lamp, seen.** Everything about the monitor's light is measured but unviewed. If it still does not read on the player's screen, the next step is a per-prop shadow dump, not a parameter tweak. | **NEXT** |
| C6 | **P6 content:** the remaining 5 of 8 items (see Phase board in the delivery message). | **NEXT** |

## D. Standing rules this round added (orders/09)

- H10 a per-frame exception is a state you did not allow
- H11 contrast is measured, not eyeballed
- H12 "centred" is a coordinate
- H13 a key the player asked for is a feature, not a discovery
