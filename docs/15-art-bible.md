# ART BIBLE — HUMAN RESOURCES: ZERO DAY

**This file is the single source of truth for how our art looks.** Read it before drawing anything.
If a new piece of art disagrees with this file, the art is wrong. Anything the user approves is added
here *the same day*, so we never rediscover our own style.

Last updated: 2026-11-26 (composition L + bigger city, slab removed, glow steps live) · Owner: Agent · Approver: the user (final say on every entry)

---

## 0. How to use this file

1. Before drawing: read §2 (mood), §3 (palette), §4 (shapes), §5 (what we already shipped).
2. After drawing: run the palette gate. Off-palette pixels must be **0**:
   `python tools\art\make_art_kit.py --check` · `python tools\art\make_applicants.py --check`
3. If you add a rule or a new asset, add a line here in the same commit.

---

## 1. The one-sentence look

> A dark, silent office at 3 a.m., rendered in 8 colours of navy and one shade of mint, where the only
> warm light in the room is the one you brought with you.

Reference image: `art/concepts/menu_concept_A.png` — the user approved **its mood, theme and vibe**, not
its props. It is our north star for colour and light, never for subject matter.

---

## 2. Mood & lighting (locked 2026-11-26)

| Rule | Detail |
|---|---|
| Time of day | **Always night.** Windows show a city skyline, a moon, half-drawn blinds. Never daylight. |
| Light sources | Exactly **one warm light** (a lamp, off-screen left; also the monitor's own glow) plus **cool mint** accents on hardware. Never two competing warm lights. |
| Where warm light may appear | As a **rim** on the desk's back edge, on the mug, inside the monitor bezel's indicators. Never as a soft radial blob on the floor. |
| Ceiling | **No hanging ceiling fixtures.** The camera sits low; a lamp near the table broke the illusion (user, 2026-11-26). Ceiling = a flat two-step shadow band at the top only. |
| Shadows | Flat and hard. One shadow under anything that stands on the floor (a 2 px dark line). No gradients, no drop shadows with blur. |
| Contrast | Background lives at `void` / `panel` / `panel2`. Anything the player must read (paper, tells, UI) is `paper`, `text`, `textdim`, `signal`. |

## 3. Palette (locked — `tools/art/make_art_kit.py::PALETTE`)

| Role | Colour | Hex | Used for |
|---|---|---|---|
| Room base | `void` | `#0b0e13` | behind everything, floor, ceiling shadow |
| Wall | `panel` | `#131926` | far wall, desk front face |
| Wall/hardware | `panel2` | `#1d2735` | wall seams, filing cabinet, desk surface |
| Edges | `line` / `linehi` | `#2b3a4d` / `#3f5570` | 1 px outlines, frames, keyboard keys |
| Cool mid | `muted` | `#5d7ea1` | water cooler, glass, distant detail |
| Dim text | `textdim` | `#a9c7e4` | moon, small labels |
| Readable text | `text` | `#e6f1ff` | UI copy |
| **Signal (the brand)** | `signal` | `#18e0b0` | the one accent: brand marks, "found" state, power LEDs |
| Signal shadow | `signaldim` | `#0fa88a` | plant leaves, secondary accent |
| Warm light | `warn` / `orange` | `#ffd166` / `#ff8a3d` | lamp rim, window lights, the pen |
| Alarm | `danger` / `dangerdim` | `#ff4757` / `#a11a2b` | corruption, stamps, warning pips |
| Paper | `paper` / `paperdim` / `ink` | `#f2e8d5` / `#8a6d3b` / `#2a2118` | documents, cover letters |
| Synthetic tell | `synth` / `synthdim` | `#7b61ff` / `#b39dff` | reserved: AI-only tells, manual highlights |
| **Glow 1** | `glow_teal` | `#0c3d38` | mint light falling off (monitor spill, signals in shadow) |
| **Glow 2** | `glow_amber` | `#5c4a20` | warm lamp falloff on surfaces away from the source |
| **Glow 3** | `glow_red` | `#4a1520` | breach/corruption falloff |

**Approved palette extension — DONE 2026-11-26.** The three glow steps are now in the palette and the
gate enforces them: `glow_teal #0c3d38`, `glow_amber #5c4a20`, `glow_red #4a1520`. Use them only for
light falloff; never as a base or a UI colour. No other new colours may be added without approval.

**Forbidden:** alpha blending of two palette colours to make a third; anti-aliased edges; any colour
outside `PALETTE`; semi-transparent shadows.

## 4. Shape language (locked)

- **Pixel grid:** 1 px detail minimum. Landscape 480×270, PPU 16, Point filter, no compression.
- **Outlines:** 1 px, `line`/`linehi` on hardware; `ink` on paper. Never 2 px unless it is a shadow line.
- **Corners:** square. Rounded corners only on UI 9-slices (3 px radius).
- **Feet on the ground:** every prop that stands in the room touches the floor line and gets a 2 px
  contact shadow. *A plant floating 6 px above the floor is a bug, not a style* (user, 2026-11-26).
- **Silhouette first:** the applicant must read as a dark shape against the wall. Keep the wall behind
  the applicant **clean** — no window frame, no props, no panel seams crossing the head or shoulders.
- **Faces:** flat, frontal, no soft shading. Eyes need both a pupil and a catchlight pixel (BUG-012).

## 5. What is already shipped (do not redraw without approval)

| Asset | File | Status |
|---|---|---|
| CRT monitor bezel | `art/kit/desk/monitor_bezel.png` | **approved as-is** — dark bezel, thin lit inner frame, indicator marks |
| Email sheet | `art/kit/desk/document_sheet.png` | approved |
| ID card | `art/kit/desk/id_card.png` | approved |
| Table + chair | `art/kit/desk/table_and_chair.png` | approved (the applicant's side of the desk) |
| Applicant archetypes | `art/kit/applicants/*` (14 × 4-frame strips) | approved; panel behind each applicant is part of the sprite |
| Portraits | `art/kit/portraits/*` (14) | approved |
| UI 9-slices, bars, pips, slider | `art/kit/ui/*` (18) | approved |
| Icons, stamps | `art/kit/icons/*` (18), `art/kit/stamps/*` (2) | approved |
| Backgrounds | `art/kit/backgrounds/office_L0..L2.png` | **being replaced** — see §6 |

## 6. Composition (decided 2026-11-26, composition study H)

The desk you sit at, seen from your chair:

```
+----------------------------------------------------------------+
|  ceiling shadow (2 steps)                                      |
|  +-------------------+     wall clock                          |
|  |  WINDOW: skyline, |                 +------------------+    |
|  |  moon, blinds     |                 |                  |    |
|  +-------------------+                 |    APPLICANT     |    |
|  +-------------------+   cabinet plant |  (clean wall     |    |
|  |    MONITOR        |                 |   behind him)    |    |
|  |  (email on glass) |                 |                  |    |
|  +-------------------+                 +------------------+    |
|================== desk back edge, warm rim ====================|
|  mug      [ keyboard ]        mouse      paper stack          |
|================== desk front lip (shadow) =====================|
+----------------------------------------------------------------+
```

Measured proportions (of 480×270):

| Element | Size | Notes |
|---|---|---|
| Monitor | 164×124 (0.62×) | smaller than the applicant — the *user's* note: "the computer looks too big" |
| Applicant | 180×203 (0.94×) | **must read bigger than the screen** |
| Desk band | 64 px tall (y 0–64) | surface 12 px, front lip 52 px — "the desk is missing" is fixed by this |
| Window | 172×62 at top-left | above the monitor; never behind the applicant |
| Clean wall zone | x 296–480, above the desk | reserved for the applicant silhouette |
| Keyboard / mouse / mug / papers | on the desk band | paper stack on the **right**, not centre |

**Desk-work layer (study J, 2026-11-26):** the desk must look *worked at*, not empty. Locked contents
and placement (world px, 480×270):

| Item | Where | Note |
|---|---|---|
| Keyboard | centred on the monitor's centre line, on the desk surface | "in front of the computer", not offset |
| Mouse | immediately right of the keyboard, cable running back to it | |
| Notepad + pen | far left of the desk | |
| Sticky notes | left of the folder stack | `warn` + `paperdim` only — warm accents are rationed |
| Folder stack | centre-left of the desk | `paper` / `paperdim` with `ink` outline |
| Clipboard | right of centre | |
| **Coffee mug** | **far right of the desk** | user, 2026-11-26: "put the coffee to the right" |
| Paper fold | never centre-right of the desk | it competes with the applicant's silhouette |

**Background props are drawn large** (user, 2026-11-26): window ≈ 184×62, filing cabinet 44×76, wall
clock r=11, plant 42 px tall, noticeboard 66×40. Small props read as decoration; big ones read as a room.

**The window / city (rewritten 2026-11-26, second pass).** The window is split into strict **zones**:
above `skyline_top` = sky only (stars, moon, one wisp); below = silhouettes only, and every lit window
is clipped *inside* its own building. Depth comes from **sky gradient steps + dithered boundaries
(3 rows max) + a segmented sodium-red glow on the roofline + gap-separated silhouette bands**, never
from a lighter "haze band" (that read as a grey slab across the middle of the window).
Moon is a **crescent** with a single halo step; two aviation beacons blink in the sky.

**The window / city (user, 2026-11-26):** the city must read **large** — the frame runs from just below
the applicant's shoulder line (row 145) to well above his head (row 8), 224×137 px, on the **right**.
His head sits against the sky, and the city lights act as a rim behind him. No moon at this size (it
became a second focal point).

**The applicant carries no backdrop.** The old lighter slab behind him was deleted from
`make_applicants.py` (2026-11-26); the room supplies everything behind him. Never add a slab back —
if an archetype needs separation, use a 1 px rim light, not a filled rectangle.

**PROMOTED 2026-11-26 — composition L is the shipped desk.** The window sits on the **right, behind the
applicant's shoulders** (user: "it highlights the interviewee"), its frame top below his jaw so his head
still reads against clean wall; the moon was dropped because at this size it became a second focal point.
Filing cabinet and plant sit on the floor line between monitor and applicant; noticeboard and clock are
side by side on the left wall, never overlapping.

Shipped files (all generated by `tools/art/promote_composition_L.py`, off-palette px = 0):

| File | Size | Note |
|---|---|---|
| `backgrounds/office_L0..L2.png` | 480×270 | the room, three lighting states |
| `desk/desk_foreground.png` | 480×64 | the desk you sit at + all desk work; sorts **above** the applicant |
| `desk/monitor_small.png` | 154×106 | **1-px redraw** at the approved 0.58 scale — never a downscale |
| `desk/document_small.png` | 108×63 | **1-px redraw** at 0.56 |

**Never resample a sprite to reach a scale.** Downscaling the 264×200 bezel would destroy its 1 px frame
and the Point filter would bake the blur in. Re-author at final size instead.

**Rejected compositions (do not revisit):** monitor ≥ 0.74× while the applicant is ≤ 0.70× (studies D/E);
any prop overlapping the applicant; window frame crossing the applicant's head (window therefore lives
top-left, above the monitor); props floating above the floor line.

## 6b. RAGGED — the "used and second-hand" look (locked 2026-11-26)

Everything in this game is a bit worn. The reference is `art/concepts/menu_concept_A.png`: nothing there
is a perfect rectangle. Helper: `tools/art/ragged.py`.

| Rule | Detail |
|---|---|
| Shapes are hand-drawn | `ragged_rect()` draws boxes whose edges step by 1 px. Never build a hero prop from `ImageDraw.rectangle` alone. |
| 1 px inner lines stay straight | Wobbling an inner lit frame reads as *scribble*, not age. Only the OUTER silhouette wobbles. |
| Paper is torn, never stepped | Sheets, letters, sticky notes: tear the EDGES (1–2 px, per side, ~30–50 %). A stepped fill makes a page look chewed. |
| Corners chip | `notch_corner()` on at most two corners, 1–3 px deep, with an `ink`/`line` pixel at the base. |
| Scuffs are sparse | **~1 per 1,500 opaque px** (down from 900 on 2026-11-26), low contrast (`panel` / `void` on light surfaces, `linehi` on dark). Dust, not static. |
| Big hard objects stay hard | **The monitor is drawn straight.** `ragged_rect` on a 154x106 box produces 1 px dotted edges with gaps -- it read as debris, not as a CRT. Monitors, cases and UI chrome get a *dented corner* and 3-4 scuffs; only paper and soft goods get torn silhouettes. |
| 9-slices are never warped | Unity stretches those bands. `distress(..., nine_slice=True)` scuffs the fill only. |
| Faces are never scuffed | Chips and scuffs go on clothing, hair and hardware. Tells must stay readable. |
| Machinery wears at the corners only | Filing cabinet, monitor bezel, stamps: chipped/dented OUTER silhouette, crisp inner faces. Steel dents; paper frays. |
| Icons and 9-slices get one corner knock | `_chip()` in `make_art_kit.py`: one pixel off each corner, never the interior. A 9-slice border must tile. |

**Rollout status (2026-11-26, complete):** desk props, monitor, documents, all 14 applicants, noticeboard,
filing cabinet, wall head, 18 icons (×2 states), 2 stamps, 5 UI 9-slices. Everything in `art/kit/` now
carries wear; the only perfect rectangles left are the 1 px window mullion and the room's floor seam,
which are architectural, not props.

### 6b.1 Wear level drifts with corruption (user decision 2026-11-26)

The kit is authored at **level 2 ("office surplus")**. It is never repainted per state — the drift is one
overlay sprite, generated by `tools/art/make_wear_overlays.py` (480×270, palette-only, alpha 0/255):

| `CorruptionLevel` | Overlay | Reads as |
|---|---|---|
| 0 (no synthetics hired) | none | the room as drawn |
| 1 | `fx/wear_a.png` | hairline cracks, extra scuffs, a few dead pixels |
| 2 | `fx/wear_b.png` | "salvage": long diagonal cracks from structural seams, heavy scuffing, one damp stain |
| 3 (3 hired = defeat) | `fx/wear_b.png` + existing runtime `danger` tint | failing |

Overlays are published to `Assets/Art/Kit/fx/` and appear in `unity_sprites.json` under `kind: fx`,
ppu 16, so they align with `office_L0..L2` at the same position. Draw order: background → wear overlay →
desk props → applicant.

**Failed approaches — do not retry:** deleting random edge pixels (produces static noise); shifting
whole rows/columns to wobble an edge (cuts 1 px frames into dashes); any post-process pass that nibbles a
finished 1 px sprite (our sprites are thin — every pixel of a frame has ≤ 2 neighbours, so removal either
does nothing or destroys the line). **Wear must be built in while drawing.**

## 7. Corruption states (decided 2026-11-26)

One room, three **lighting** states — not three different rooms:

| Level | Treatment |
|---|---|
| `L0` normal | as §6 |
| `L1` warning | warm lamp off; window lights shift to `orange`; mint accents dim to `signaldim`; a `line`-coloured scan band |
| `L2` breach | `danger` rim on the bezel and desk edge; window sky goes red-tinted via a flat `dangerdim` overlay sprite; every 3rd row darkened |

Implement as **one background + overlay sprites + a runtime tint**, never as three painted rooms.

### 6b.2 Applicant detail (locked 2026-11-26)

Identity is more than a colour swap. Each applicant now also varies in:

| Trait | Values |
|---|---|
| `collar` | crew / V-neck / turtleneck (ribbed) / collared shirt + tie |
| `glasses_shape` | rectangular / round (pixel-ring lenses with a glint) |
| `accessory` | none / wristwatch / earrings / breast-pocket pen / ring |
| `desk_prop` | mug / pen / folded note / phone face-down |
| `folds`, `brow`, `mole` | 1–3 creases (never crossing the badge), 2–4 px brow, one off-centre mole |
| lanyard | a real V off both shoulders converging on the card, with a 1 px drift. Synthetics keep `straight_badge` (dead vertical) as a tell. |

**Drawing rules learned here (do not re-break):** sleeves must GROW out of the shoulder over ~6 rows — a
fixed block starting below the shoulder leaves only its 1 px edge visible and the arm reads as a wire.
Finger separations are `skin_mid` on `skin_light`, never `skin_dark`: four dark bars read as a fence.

## 7b. Floors: nothing floats (added 2026-11-26)

**BUG-025.** The monitor hung 12 px above the desk. Cause was not the drawing: `monitor_small.png`
drew a 4 px "stand" inside its own sprite, but the monitor layer sorts at **−20** and the desk layer
at **+10**, so a stand baked into the monitor could never be seen below the desk edge.

| Rule | Detail |
|---|---|
| Props that stand ON the desk are two layers | body sprite + `*_stand.png` (or shadow sprite) anchored separately, with the base overlapping the desk surface by 3–4 px so the desk layer covers the overhang |
| Every prop gets a contact shadow | a dark line or 2-step band exactly where it meets the surface. No contact shadow = floating, at any resolution |
| Check at 4×, not 1× | `twin_desk.py --zoom 4` with the right crop; a 12 px gap is invisible in a 480 px thumbnail |

## 7c. The desk must read as furniture (locked 2026-11-26)

User feedback: *"the desk looks too scrambled or too undefined"*. Both were true, for two different
reasons, and the fixes are rules now:

| Problem | Cause | Rule |
|---|---|---|
| Scrambled | eight small props spread evenly across 480 px, each competing | Props are **grouped** with gaps: work area (notepad, keyboard, mouse) → paperwork (sticky notes, folder, clipboard) → comfort (mug). Never place a prop in every band. |
| Undefined | props had no contact shadow, and the front face was flat bands with tick marks | **Every prop gets a contact shadow.** The desk front is lip (3 bevelled rows) → apron → rail → kick shadow. Definition comes from value steps, never from added clutter. |
| "Everything the same" | surface tone equals prop tone | A prop may never use the surface's own tone as its body: the mouse was `panel2` on a `panel2` desk and read as a plus sign. Use one step off. |
| Fake depth | drawer drawn under the props read as a tray | No horizontal panel line directly under a prop cluster. |

**Rejected in this pass (do not retry):** a centred drawer with a bar handle (reads as a tray);
vertical panel joints at x=168/312 (they ran through the mouse and the folder); a white 3-slab folder
stack (one bright undefined block); a mouse drawn as concentric ellipses (two coins); a diagonal glare
streak on the monitor glass (reads as a scratch and competes with the email text).

## 7d. The monitor arm, and the document that overflowed the glass (2026-11-26)

**BUG-028 -- the document stuck out of the screen.** Measured: monitor glass occupied rows 84-171,
columns 20-161. The document layer was at anchor `[34, 93]`, i.e. rows 114-177 -- **6 px below the
glass** -- so the sheet hung out of the bottom of the CRT. The document is now **96x54** (was 108x63)
at anchor `[38, 100]`, which leaves 6 px of glass above it and 1 px below.

Rule: **any sprite drawn "on" the screen must be measured against the glass rect, not eyeballed.**
The glass rect for `monitor_small.png` is `x+6, y+6 .. x+w-7, y+h-13`.

**BUG-029 -- the arm was not an arm.** The old `monitor_stand.png` was a ragged column plus one
ellipse, outlined in 1 px dots (same failure as the monitor, BUG-027). Redrawn as 56x34 with real
articulation: base plate (bevelled, with a standby LED) -> thick lower column -> **joint collar** 2 px
wider -> thinner upper column offset 2 px so it leans -> mount plate with two screw heads. Hardware
edges are straight; wear is 2 scuffs on the column.

**Desk props vs the arm.** The arm occupies x 63..119. Desks props may not be placed under it: the
keyboard used to be centred at x=104 and covered the arm's lower half. Band rule now: notepad x12 |
**arm clear** | keyboard x200 / mouse x250 | sticky 268 / folder 300 | clipboard 374 | coffee 436.

## 7e. The CRT glass is a character grid (2026-11-26)

The monitor is where the player reads the game, so its interior is specified, not painted freehand:

| Value | Detail |
|---|---|
| Glass rect | **142x88 px** at monitor-local `(6, 12)`, y UP. Derived by `MonitorScreen.GlassForSprite()` and asserted against `desk_layout.json` layer `monitor.screen` |
| Font cell | 5x7 glyph + 1 px letter gap + 2 px line gap |
| Capacity | **23 columns x 10 rows** |
| Overflow | never. `LinesToRects` clips vertically and `FitToColumns` truncates with `~`; the test suite fails if a line crosses the glass edge |
| Empty fields | produce **no line at all** -- the screen never invents a fact |
| y flip | canvas is y-UP, text runs DOWN: line 1 gets the highest y. One place does this (`LinesToRects`), and `YFlipIsReal` guards it |
| Font | `art/kit/fonts/monitor_font_5x7.png` + `.json`, generated by `tools/art/make_font.py` (56 glyphs, palette-only, gated). Never a TMP asset: TMP fonts cannot be built headlessly and cannot hit a 5x7 grid |
| See it | `python tools/comp/preview_screen.py --zoom 4` renders the real layout over the real bezel. This is the fast loop -- no Unity needed |

## 8. Open decisions (do not build on these yet)

- None carried over. (The applicant backdrop-slab question was closed by removing the slab.)
- Exact hex values of the 3 approved glow steps.
- Whether the monitor's inner glass gets its own scanline texture sprite.
