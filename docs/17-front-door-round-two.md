# 17 -- The front door, round two: colour, camera, motion and the UI (v117, ADR-021)

Research notes for the second art reset. Everything here is a *rule with a gate*, because the v116 lesson was
that anything I only decide in my head drifts back within two rounds.

---

## 1. "32-bit style" means what, exactly

Colour depth in pixel art is about **how many ramps and steps you spend**, not about resolution or a filter:

| Era | Colours | Shading | Outlines |
|---|---|---|---|
| 8-bit | 3-4 per sprite | flat, 2 tones | thick black |
| 16-bit | 8-16 per sprite, 3-5 shading steps | gradients, first hue shifting | selective, coloured |
| **32-bit** | **dozens, 4-5 steps per material, alpha allowed** | **complex ramps, specular, glow, ambient** | **minimal; separation by value** |

So "32-bit" for us means:

- **more ramps, not more mud** -- 16 ramps of 3-5 steps each (#2), instead of one blue for everything;
- **alpha for glow, smoke and light haze** -- the 32-bit era could finally do transparency, and that is exactly
  where it shows;
- **separation by value, not by outlines** -- 32-bit art barely outlines anything. Objects read because their
  value bands differ, which is what makes the room look painted instead of cut out;
- the **pixel grid stays** the shape language. It is not a photograph and it never becomes one.

## 2. Colour: the five things that were wrong, and the fix

The v116 room passed the palette gate on every pixel and still read as one object. The rules that fix that:

1. **Value carries the image.** Squint and the shape must still read. Every material now owns a value band,
   and two materials that *touch* must be >= 0.055 apart on the HSV value axis. `menu_palette.py` checks 15
   touching pairs; the first version of the palette failed six of them at 0.000-0.047.
2. **Hue shifting, >= 6deg per step.** Highlights rotate toward the light, shadows toward blue-violet. The first
   palette I wrote had **0.2deg** steps -- literally grey mud -- and the audit caught it 44 times.
3. **Saturation peaks mid-ramp.** Highlights wash out toward the light; shadows desaturate. A ramp that only
   walks value reads as a colour filter over a photo.
4. **One light, and it decides everything.** The key is the screen: cold cyan, low and left. So wood highlights
   go **amber/gold** and wood shadows go **blue-violet**. The classic "warm light, cool shadow" rule is the
   same rule with a warm key -- ours flips, and that is why the room finally reads as *lit by the monitor*
   rather than tinted by it.
5. **Temperature builds depth.** Warm advances, cool recedes. The desk (warm wood) is the closest thing in the
   frame; the city (blue-violet, lowest saturation) is the furthest.

Materials, and why each is a different colour: **wall** cold blue-grey -> **floor** darker + violet-shifted ->
**desk warm wood** -> **table steel** (cool, mid) -> **server deep navy** (the only violet-blue solid in the
room) -> **CRT case desaturated blue-grey** -> **keyboard darker plastic** -> **coffee cream** (second brightest
thing in the frame) -> **paper warm off-white, dimmer than the cup**.

## 3. Camera and composition

- **Three layers, always**: foreground (desk props, closest to camera), midground (the CRT and what is on the
  desk), background (table of monitors, window, server). Value does the depth work: foreground has the darkest
  darks and the brightest highlight, background is lower contrast and cooler.
- **Focal point**: the monitor is *the* object -- closer to camera, larger, and the only thing allowed a
  specular highlight. Everything else is subordinate.
- **The server reads as a real object only in three-quarter view**: a visible side face (one value darker), the
  front face turned toward the room. A flat rectangle on the wall is a decal; two faces is a cabinet.
- **Negative space**: the wall above the monitors and to the right of the screen stays quiet. That quiet area is
  what makes the dust, the flicker and the smoke visible.

## 4. Animation: what moves, and how slow

| Thing | Frames | Rate | Notes |
|---|---|---|---|
| Wall monitors (6) | 4 content states + flicker | 6 fps content, flicker at 8 fps | cybersecurity text, cycles per screen |
| AI breach | 3-state overlay | random 6-14 s, 1.2 s long | one screen, never all of them |
| Main screen | 6 frames of panel activity | 4 fps | cursor blink, status tick, scanline roll |
| Coffee smoke | 6 frames | 8 fps | three strands, phase-shifted, thickens then breaks |
| Dust motes | 1 particle field | 12 fps drift | 24 motes, 2x2 px, alpha |
| Screen glow | 3 steps | 3 fps | the *room's* light pulses with the tube |

Rules: nothing animates faster than 12 fps (it looks like noise at 480x270), and the **glow is the slowest
thing in the frame** -- the room should feel like it is breathing, not blinking.

## 5. UI: why it looked cramped

- **Two type sizes minimum, and room between the blocks.** Title at 2x tight tracking (1 px gap instead of 2),
  subhead 4 px under it (it is a *sub* head, not a second title), tagline 1x below that, then a thin rule and
  the rows.
- **Rows need vertical air**: 12 px step on a 9 px pill means 3 px of gap. At 13-15 px step the block ran into
  the footer, which is the "cramped" the player saw.
- **Margins are part of the design**: 12 px inside the tube edge, 20 px inside the bezel. Nothing touches a
  border.
- **One accent does the work.** Cyan = selection. Amber = a warning colour, one line only. Red = breach, and
  red is rationed to the breach state and the stamp, or it stops meaning anything.
- **Alignment**: title, subhead and tagline are centred on the tube's centre line; the rows are a centred
  block; the footer is left-aligned. Three alignments, each with a job.
- **Footer gets its own rule above it**, so the hint is never mistaken for a sixth row.

## 6. The gates this round adds

- `tools/art/menu_palette.py --check` -- 16 ramps, >= 6deg hue rotation per step, saturation peaking mid-ramp,
  15 touching pairs >= 0.055 value apart.
- The composite's geometry gate grows **foreground props** (coffee, keyboard, mouse, papers) so a prop can no
  longer ship hidden behind the monitor -- that exact defect shipped in v116.
- An **animation manifest** gate: every animated layer must declare frames + fps, and no layer may exceed
  12 fps.
