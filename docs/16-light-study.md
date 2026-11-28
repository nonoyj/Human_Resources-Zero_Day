# 16 -- Lighting the front door room (v115, ADR-019)

What the room's light actually is, why the first passes looked wrong, and the rules the code now enforces.
Written after BUG-068, which cost four rejected renders.

---

## 1. What a pixel-art light is *not*

A light is **not** a coloured fog laid over the frame, and it is **not** a halo sprite pasted around an
emitter. Both of those replace a surface with a colour, which reads as paint. In a 25-colour palette the
difference is obvious and ugly: every surface the light touches becomes the *same* colour, so a lit wall, a
lit desk and a lit floor stop being a wall, a desk and a floor.

The v115 first pass did exactly this. `Blend()` picked the brighter of `surface` and `band` and `break`-ed
out of the source loop, so one 250x210 source stamped a flat disc across the CRT, the desk and half the
floor, and the other two sources contributed nothing.

## 2. What a pixel-art light is

A light **raises the value of the surface it lands on**, in a small number of hard steps.

- Give every surface colour a position on a **ramp**.
- A light adds steps to that position. Nothing else.
- The boundaries between steps are the only place dithering belongs.
- The top of the step is solid and clean -- bright cores stay bright, they do not get speckled.
- The most a light may add is **one step**. Two lamps in a room do not make it twice as bright; a second
  step is the whole distance between "lit" and "washed out".

Our ramp:

```
void  ->  panel  ->  panel2  ->  line
```

`line` is a cool grey-blue. **The ramp deliberately stops there.** The emitting colours -- `text`, `white`,
`signal`, `warn`, `paper` -- are not on the ramp at all, so the light pass cannot touch a screen, a piece of
paper or an LED.

## 3. Where cyan is allowed

The brief asks for a cyan fade coming out of the monitor. In a 25-colour palette "cyan everywhere" is one
bad render away, so cyan is rationed to three places and nothing else:

| Where | Colour | Size |
|---|---|---|
| Beside the glass | `glow_teal` (dark teal) | 14 px (`CyanNear`) |
| The case's bounce | `glow_teal`, ring 2 dithered 1-in-4 | 2 px (`Halo()`) |
| The fade | `glow_teal` on a 4x4 ordered threshold | 84 px (`FadeReach`) |

The fade is measured from the **case**, not from an ellipse around it, and it only lands on a dark surface
(`void`/`panel`/`panel2`). A bright surface lifted into teal is a turquoise panel, not light.

`signaldim` is a *lit LED*. It is never used for ambience. The first ramp pass used it for every lit
surface and the room looked like a fish tank.

## 4. The glass is a source, not a surface

The screen is the brightest thing in the frame, and the light pass must skip it entirely -- every pass:
`Light()`, `Halo()`, `Vignette()`, and the composite twin. Lighting the screen you are reading speckles the
title and lets the fade cross the text.

The tube is painted separately, before the light pass, by `GlassMaterial()`:

- phosphor black (`void`, one step darker than the case)
- a scanline every third row, cyan (`glow_teal`) in the top third where the beam is brightest, `panel`
  below it
- one emissive line (`signaldim`) just inside the bezel, **top and left only** -- a tube's edge is not a
  frame

## 5. The three sources

| Source | Centre | Reach | Falloff | Steps |
|---|---|---|---|---|
| Monitor (dominant) | glass centre | 232x176 | 1.55 | 2, clamped to 1 |
| Window (cold, far) | wall | 92x106 | 1.90 | 1 |
| Table of screens | stack | 96x88 | 2.10 | 1 |

One dominant light. The other two are subordinate, and they are *smaller and further* -- that is what makes
the desk the bright place and the corner the dark one. A test asserts the monitor is the strongest.

## 6. Ambient

- Room ambient is `void`: the shadows are lifted, never crushed. Legibility is what makes the room feel
  wrong rather than merely dark.
- The vignette darkens beyond r 1.18 and half-dithers between 0.92 and 1.18. Nothing harder.
- **Warmth is rationed.** The coffee's steam, one amber LED on the case's control strip, one amber window in
  the city. That is all of it. Five warm sources is a kitchen; one is a night shift.

## 7. Silhouettes need a lighter sky

The window first came out empty. The sky and the buildings were both `void`, so the city had nothing to be
a silhouette *against*. Sky is `panel`, buildings are `void`, and the moon takes the only cold white in the
frame (`textdim`). Seven stars. Restraint, not noise.

## 8. The gates

- `python tools/art/make_menu_room.py --check` -- six sprites, palette-only, alpha 0/255.
- `python tools/art/make_menu_composite.py --check` -- geometry (on frame, no overlap between back-wall
  pieces), menu fit (title <= glass, pills clear the footer), and the light pass mirrored from the C#.
- `CheckTheRoomHasEveryPieceFromTheBrief` -- the layout half: monitor on the desk, table and server on the
  floor, window clear of the monitor, table -> window -> server left to right, one dominant light, `MaxLift`
  is 1.

The geometry gate exists because the window shipped *behind* the monitor once. Anything this easy to get
wrong belongs in a gate, not in a screenshot review.

---

## 9. v116: what the second look changed

The player's notes after installing v115, in order, with what each one changed.

### 9.1 "The text is offscreen"

It was not offscreen -- it was on the **curve**. A CRT's tube is a rounded box: every corner is cut, and the
picture inside it is smaller than the tube. v115 measured every line against the flat glass rect, so a line
that "fitted" could still sit on the cut corner. That is what a player sees as offscreen text.

- `MenuLayout.TubeInset` (7 px) defines the **picture area**: `TextX/TextY/TextW/TextH`.
- `Centre()` centres on the tube, `InGlass()` clips to the tube, and the composite's `check()` fails the
  build if any line is wider than the tube.
- The pills are anchored to the **footer** (`PillTop = FooterTop - (4 * PillStep + PillH) - 4`), so the row
  block can never drift into the footer no matter how the tube is re-cut.

### 9.2 "Make the monitor more dominant, and it should look old/retro"

Research (datagubbe.se on CRTs, and how vintage monitors are actually drawn):

- a CRT is a **deep case** -- thick plastic, heavier at the bottom, standing on feet;
- a **thick front bezel**, wider at the bottom because that is where the controls live;
- **cut corners** on both case and tube. A rectangle reads as a flat panel, not a tube;
- a **shadow side**: one face of the case is a value darker, because the light is on one side;
- the tube is darker at its own rim than in the middle (phosphor falls off);
- real furniture: moulded seam, vent grille, badge, two knobs, power LED, status dots, four screws.

The desk monitor went 240x172 -> **252x186** with 224x148 of tube, and the six back-wall screens are now
little retro cases too (cut-corner plastic, bottom bezel with an LED, inset white tube) instead of white
rectangles with a border.

### 9.3 "A dusty vignette over the menu with grains"

- The grain is **clumped**: 2x2 blocks, never single pixels. A one-pixel speckle at 480x270 reads as cable
  noise; a two-pixel clump reads as dust on glass.
- It lands only on **dark** surfaces (`void`/`panel`/`panel2`), never on a screen and never inside the glass
  -- dust on a white screen is a scratch, and the screens are the light in this room.
- Placement is a **Wang hash per block**, not a radius threshold and not `(x*A ^ y*B) & 15`. Both of those
  are periodic, and periodic dust draws a visible ring or a screen-door mesh over the frame. Both were tried
  and rejected in this round.
- Density: ~0.4% of blocks in the middle of the frame, ~3% at the corners (`DustMin`/`DustMax`,
  `DustDensity`).
- The grain is **deterministic**, so the frame does not crawl between runs.

### 9.4 The cyan fade became directional

An equal reach on every side drew a dome over the wall. A screen lights the desk it stands on and spills only
a little round its own edges, so the fade now reaches **58 px sideways and 74 px down** (`FadeReach`,
`FadeDrop`), still landing only on dark surfaces through a Bayer 4x4 threshold.

### 9.5 Three things that had to be undone mid-round

Worth recording because they are the failure modes of this style:

| Tried | Why it failed |
|---|---|
| Dust keyed to a radius from the centre | drew a **ring** of dust around a clean middle |
| Dust keyed to `(x*A ^ y*B) & 15` | periodic at 16 px: a **screen-door mesh** over the whole room |
| A half-checker vignette between r 0.92 and 1.18 | drew a visible **dome** across the frame |
| The tube's emissive edge in `signaldim`, all four sides | read as a **neon frame** pasted on the monitor |

### 9.6 Atmospheric perspective in the window

The city is two layers, which is the pixel-art rule for depth: a **far** skyline one value off the sky with no
detail, and a **near** skyline in `void` with lit windows. One flat row of buildings reads as wallpaper. The
window was also widened 82 -> 96 px, as asked.
