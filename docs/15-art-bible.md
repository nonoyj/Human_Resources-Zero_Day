# ART BIBLE — HUMAN RESOURCES: ZERO DAY

**This file is the single source of truth for how our art looks.** Read it before drawing anything.
If a new piece of art disagrees with this file, the art is wrong. Anything the user approves is added
here *the same day*, so we never rediscover our own style.

Last updated: 2026-11-26 · Owner: Agent · Approver: the user (final say on every entry)

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

**Approved palette extension (user, 2026-11-26):** up to **3 additional "glow" steps** may be added so
light can fall off without blending. They must be listed in `PALETTE` (so the gate enforces them) and
recorded in this table with their hex values before first use. No other new colours.

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

**Rejected compositions (do not revisit):** monitor ≥ 0.74× while the applicant is ≤ 0.70× (studies D/E);
any prop overlapping the applicant; window frame crossing the Applicant's head (study F).

## 7. Corruption states (decided 2026-11-26)

One room, three **lighting** states — not three different rooms:

| Level | Treatment |
|---|---|
| `L0` normal | as §6 |
| `L1` warning | warm lamp off; window lights shift to `orange`; mint accents dim to `signaldim`; a `line`-coloured scan band |
| `L2` breach | `danger` rim on the bezel and desk edge; window sky goes red-tinted via a flat `dangerdim` overlay sprite; every 3rd row darkened |

Implement as **one background + overlay sprites + a runtime tint**, never as three painted rooms.

## 8. Open decisions (do not build on these yet)

- Whether each applicant's built-in backdrop panel is replaced by a room-aware chair/backdrop.
- Exact hex values of the 3 approved glow steps.
- Whether the monitor's inner glass gets its own scanline texture sprite.
