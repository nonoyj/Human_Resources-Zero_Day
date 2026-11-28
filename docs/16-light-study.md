# 16 — Lighting the front door room (v115, ADR-019)

What the room's light actually is, why the first passes looked wrong, and the rules the code now enforces.
Written after BUG-068, which cost four rejected renders.

---

## 1. What a pixel-art light is *not*

A light is **not** a coloured fog laid over the frame, and it is **not** a halo sprite pasted around an
emitter. Both of those replace a surface with a colour, which reads as paint. In a 25-colour palette the
difference is obvious and ugly: every surface the light touches becomes the *same* colour, so a lit wall, a
lit desk and a lit floor stop being a wall, a desk and a floor.

The v115 first pass did exactly this. `Blend()` picked the brighter of `surface` and `band` and `break`-ed
out of the source loop, so one 250×210 source stamped a flat disc across the CRT, the desk and half the
floor, and the other two sources contributed nothing.

## 2. What a pixel-art light is

A light **raises the value of the surface it lands on**, in a small number of hard steps.

- Give every surface colour a position on a **ramp**.
- A light adds steps to that position. Nothing else.
- The boundaries between steps are the only place dithering belongs.
- The top of the step is solid and clean — bright cores stay bright, they do not get speckled.
- The most a light may add is **one step**. Two lamps in a room do not make it twice as bright; a second
  step is the whole distance between "lit" and "washed out".

Our ramp:

```
void  ->  panel  ->  panel2  ->  line
```

`line` is a cool grey-blue. **The ramp deliberately stops there.** The emitting colours — `text`, `white`,
`signal`, `warn`, `paper` — are not on the ramp at all, so the light pass cannot touch a screen, a piece of
paper or an LED.

## 3. Where cyan is allowed

The brief asks for a cyan fade coming out of the monitor. In a 25-colour palette "cyan everywhere" is one
bad render away, so cyan is rationed to three places and nothing else:

| Where | Colour | Size |
|---|---|---|
| Beside the glass | `glow_teal` (dark teal) | 14 px (`CyanNear`) |
| The case's bounce | `glow_teal`, ring 2 dithered 1-in-4 | 2 px (`Halo()`) |
| The fade | `glow_teal` on a 4×4 ordered threshold | 84 px (`FadeReach`) |

The fade is measured from the **case**, not from an ellipse around it, and it only lands on a dark surface
(`void`/`panel`/`panel2`). A bright surface lifted into teal is a turquoise panel, not light.

`signaldim` is a *lit LED*. It is never used for ambience. The first ramp pass used it for every lit
surface and the room looked like a fish tank.

## 4. The glass is a source, not a surface

The screen is the brightest thing in the frame, and the light pass must skip it entirely — every pass:
`Light()`, `Halo()`, `Vignette()`, and the composite twin. Lighting the screen you are reading speckles the
title and lets the fade cross the text.

The tube is painted separately, before the light pass, by `GlassMaterial()`:

- phosphor black (`void`, one step darker than the case)
- a scanline every third row, cyan (`glow_teal`) in the top third where the beam is brightest, `panel`
  below it
- one emissive line (`signaldim`) just inside the bezel, **top and left only** — a tube's edge is not a
  frame

## 5. The three sources

| Source | Centre | Reach | Falloff | Steps |
|---|---|---|---|---|
| Monitor (dominant) | glass centre | 232×176 | 1.55 | 2, clamped to 1 |
| Window (cold, far) | wall | 92×106 | 1.90 | 1 |
| Table of screens | stack | 96×88 | 2.10 | 1 |

One dominant light. The other two are subordinate, and they are *smaller and further* — that is what makes
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

- `python tools/art/make_menu_room.py --check` — six sprites, palette-only, alpha 0/255.
- `python tools/art/make_menu_composite.py --check` — geometry (on frame, no overlap between back-wall
  pieces), menu fit (title ≤ glass, pills clear the footer), and the light pass mirrored from the C#.
- `CheckTheRoomHasEveryPieceFromTheBrief` — the layout half: monitor on the desk, table and server on the
  floor, window clear of the monitor, table → window → server left to right, one dominant light, `MaxLift`
  is 1.

The geometry gate exists because the window shipped *behind* the monitor once. Anything this easy to get
wrong belongs in a gate, not in a screenshot review.
