# 20 — The cinematic pass: what changed in v118, and what each change is for (ADR-021)

The player's note, verbatim, and what it became:

> "This looks too clean, the coffee is on top of keyboard, the screen looks too retro, the color is too
> bright. We've lost the cinematic dark vibes look. Maybe turn it to 16 bit rather than 32 bit style. But
> keep the cinematic cyan glows."

> "The screen is bugged, the game is playing while the menu is displaying and there is no text in the menu
> items."

---

## 1. The reference still, measured

The player supplied one frame as the target. It was measured rather than eyeballed, because "make it
cinematic" is not a specification but a set of numbers is:

| Measurement | The still | What it means for us |
|---|---|---|
| Pixels under 12% luma | **75.5%** | the frame is mostly near-black; the room is a pool of light in a dark box |
| Median luma | 9% | the mid-grey is very low; there is no "grey" anywhere |
| Pixels above 60% luma | < 1% | only two things are bright, and neither is a surface |
| Brightest object | the window (cyan, hue ~195) | the COLD key is the brightest thing in the frame |
| Second bright object | the lamp (amber, hue ~45) | the WARM practical is small, hard, and on the other side |
| Darkest regions | the near-left foreground, the ceiling | the foreground goes to black; the frame has a near edge |
| Saturation of shadows | high (0.7-0.9) but at v<0.16 | dark AND coloured, never grey |
| Number of specular hits | three: the desk's front edge, the cup, the sill | gloss is rationed |

Three of those numbers are now gates (`menu_palette.py --check`), because they are the ones the previous
round got wrong: **69% of the material steps are dark**, **nothing non-emissive is above 0.62 value**, and
**every material ramp rotates at least 6° per step**.

## 2. "16-bit, not 32-bit"

The player asked to step back from the 32-bit direction of v117. The difference that matters is not
resolution, it is **how many decisions per pixel**:

- **16-bit:** 11 material ramps of 3-5 steps, ~70 colours total, dithered seams, **no per-pixel noise, no
  smoothing, no gradients that are not made of two adjacent palette steps**. Shading is cluster-based.
- **32-bit (what v117 was drifting toward):** dozens of colours per object, alpha everywhere, soft
  gradients. At 480×270 that reads as *mud*, and it is exactly what the player called "too clean" and "too
  bright" — too many mid values, no hard decisions.

So: fewer, harder steps. The floor is three values. The wall is three. The desk is five, and three of them
are under 0.15 value.

## 3. The composition (the angle the player asked for)

The eye line is at y=150 and the vanishing point at x=268 — right of centre, so the desk is seen from the
left. The floor's grout, the desk's front edge and the window's sill all converge on it. That is the most
perspective a 480×270 frame carries before the pixel grid starts to shear.

Left to right, and why:

| | object | role |
|---|---|---|
| far left, tall | **window**, 88×160 | the cold key; the brightest thing in the frame; the only cold source |
| centre, largest | **monitor**, 260×186 | the subject. 54% of the frame's width, and the menu lives in it |
| right of it, small | **six wall screens** | secondary light, cyber readouts, one breach screen |
| right, on the desk | **lamp**, warm | the practical. Small, hard-edged, and the reason cyan reads as cold |
| far right | **server**, three-quarter | the object that says "database"; 7 px of side face is all it needs |
| bottom third | **desk** with coffee, keyboard, mouse, forms | the foreground. Near-left corner goes to black |

## 4. The two bugs, and their causes

**BUG-071 — "the game is playing while the menu is displaying".**
`DeskBootstrap.Update()` ticked `Session.Tick()` every frame, menu or no menu. The shift clock, the
observation timer and the applicant's patience all ran behind the door. Fixed at the source: `Update()`
returns early while `ShowingMenu` or `ShowingIntro`; the desk's applicant actors are disabled on open and
re-enabled on close. A menu is a pause with furniture in it.

**BUG-072 — "there is no text in the menu items".**
`MenuView.Paint()` called `FlipToTextureOrder(buf)` and **then** `MonitorScreenRaster.RenderRows(buf)`.
`RenderRows` converts canvas y-up into a top-down buffer — inverting the buffer a second time. Every glyph
row landed above the glass and was clipped away by the raster's own clip box. The fix is an ordering rule,
now stated in the file's header: **the flip is the last thing that happens to the buffer**, and no raster
call follows it. The rewrite paints its text directly, so the trap cannot be re-entered by accident.

**BUG-074 (found while fixing the above) — `palette.json` had no writer.**
`MonitorScreenView.Palette` reads `Resources/palette.json` for every colour in the game. Nothing in the
repo generated it: the file was committed once and went stale. `menu_palette.py --write-json` now MERGES
the front door's material keys into it (never replaces — the legacy keys `text`, `signal`, `danger`,
`paper`, `ink`, `panel`, `void` are what the HUD, the letter and the sweep read), and the art gate runs the
writer so a stale file fails the build instead of quietly tinting the HUD wrong.

## 5. Colour: what "each object has its own colour" became

| Material | Ramp | Why it is that colour |
|---|---|---|
| wall | violet-blue, 3 steps | the room is cold; the wall is the largest cold surface and must not be grey |
| floor | violet-black, 3 steps | darker than the wall so the skirting line reads without an outline |
| wood (desk) | warm amber, 5 steps | the one large WARM surface. Without it the frame is monochrome cyan |
| steel | cool green-leaning, 3 steps | it reflects the screens; green-leaning keeps it off the wall's value |
| key / mouse | steel's own family, two steps darker | plastics on a desk are never the desk's colour |
| cup | cream, 3 steps, hue 34-50 | second brightest thing on the desk, and 20° off the paper |
| paper | warm off-white, 3 steps | dimmer than the cup: a pile of forms is not a lamp |
| scr (wall tubes) | pale cyan-white, 3 steps | emissive. The darkest step is a DEAD screen and is dark glass |
| teal (phosphor) | cyan, 5 steps | the key light's own colour, used only by the tube and what it lights |
| amber (lamp) | amber, 5 steps | the warm practical, and the brightest non-emissive material |
| red | the AI's colour, 3 steps | rationed: the stamp, one server LED, and the breach |
| cyan (selection) | 3 steps | the selected row's fill; brighter than teal so it reads as UI, not as screen |

The value audit (`menu_palette.py` with no arguments prints it) lists all 13 touching pairs and the value
and hue gap of each. Two materials may separate by **value** (≥ 0.055) or by **hue** (≥ 28° with a smaller
value floor) — requiring value alone forces every material into the same ramp length and flattens the
picture.

## 6. Light

Three sources, one step each, and the rules are in `MenuLayout.cs`:

1. **Two temperatures.** Cold key (the tube, hue ~185) and warm practical (the lamp, hue ~40). One
   temperature is a filter, not lighting.
2. **A light moves a surface along ITS OWN ramp.** `MenuLayout.Ramps` is a table of from→to pairs; there is
   no code path that paints a surface in the light's colour. v116 did that and made an aquarium.
3. **One step.** `MaxLift = 1`. A second step is the difference between "lit" and "washed out".
4. **The dither is on the seam.** Falloff is continuous; the step boundary is ordered-dithered on a 2×2
   pattern. Channels are not fog.
5. **The tube is skipped** by the light pass entirely: it is a source, not a surface.
6. **The vignette only touches the outer corners** (r > 1.12). A ring across the whole frame draws a dome.

## 7. Motion

Ten declared layers, all in `art/kit/menu/motion.json`, gated by `motion_manifest.py --check`:

| layer | rate | what it is |
|---|---|---|
| wall_screens | 2 fps | six cybersecurity readouts (`AUTH 0X4F2`, `ICE BREACH`, `NODE 07/12`…) on six beats |
| wall_flicker | 8 fps | a 3-step brightness flicker; the oldest thing in the room |
| breach | random 14-26 s, 1.4 s | one screen floods red: `SYS.ROOT / ?? INTRUDER` |
| main_screen | 4 fps | the glass's scanline roll |
| smoke | 6 fps | three strands, thickest at the rim |
| dust_motes | continuous | **30 motes falling at 5.5 px/s with a 1.7 px/s draught** — the player's "grains must fall and move" |
| room_glow | 2 fps | the tube's light breathing. The slowest room layer, by rule |
| lamp_glow, window_life, menu_idle | 1-2 fps | the practical, the city, the selection's pulse |

The gate enforces: nothing above 12 fps, the tube's glow is the slowest *room* layer, the breach cannot fire
more often than every 14 s, and every layer declares a frame count.

## 8. Type

The player: *"make the full title thicker and letters closer together; the sub-header closer to the title."*

The atlas is a 5×7 bitmap, so thickness and tracking are the only two dials:

- **Tracking:** the title's cell gap is **1 px** instead of the atlas's 2. The title is 15 glyphs at 2× —
  162 px instead of 178 — and it is centred with `MenuLayout.LineWidth()`, the same function the tests measure
  with, so a change to tracking cannot leave the gate behind.
- **Thickness:** each glyph is **bled 1 output pixel** to the right (the repeat of its last ink column). At
  2× that is a half-cell of extra weight: the title reads as a heavy face next to the 1× body text.
- **The sub-header** sits **3 px** under the title's cap instead of 11, and the tagline moved down with it.

## 9. UI

The player: *"research the overall UI look — it looks too cramped."*

- one alignment per block: title, sub-header and tagline are centred on the **tube**; rows are a centred
  block; the footer is centred on its own line with 12 px of air above it;
- 5 rows at a 13 px pitch on a 13 px pill, i.e. **touching** — the air went into the margins around the
  block rather than between the rows, which is what made v116 look cramped: it had a 3 px gap between rows
  and 4 px of margin at the sides;
- a **fading rule** between the header and the rows, so the header cannot read as a fourth row;
- the selected row is a filled cyan bar with **dark text**; unselected rows are a dark bar with a hairline
  edge and pale text. Two states, one accent.

## 10. Gates added or changed this round

| gate | what it now proves |
|---|---|
| `menu_palette.py --check` | 11 ramps, ≥6°/step, saturation mid-ramp, 13 touching pairs separated, 69% of steps dark, nothing non-emissive above 0.62 |
| `menu_palette.py --write-json` | `art/kit/palette.json` is generated (merged, legacy keys preserved) |
| `make_menu_room.py --check` | palette-only pixels + structure: bezel inside the case, cup on the slab, forms against the frame edge |
| `make_menu_composite.py --check` | the reference order, the tube fit, the row block, six props on the frame |
| `motion_manifest.py --check` | 10 layers, ≤12 fps, the tube breathes slowest, the breach is rationed |
| `CoreTests/MenuTests.cs` | + `CheckTheMaterialsAreRampsNotColours`, + `CheckTheMenuStopsTheClock`; the tube test now measures with `LineWidth()` and asserts the tighter tracking, the bleed and the closer sub-header |

## 11. What the player still has to judge

The still is a frame, not a game. What this build cannot show them in a PNG: the six screens actually
cycling, the breach actually firing, the motes actually falling, and smoke that moves. Those are the four
things to look at first in the editor.
