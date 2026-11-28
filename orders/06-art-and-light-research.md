# 06 -- ART, LIGHT AND INTERACTION: the research, turned into rules this repo can check

**Why this file exists.** The player asked (v130) for a deep study of texturing, the 16-bit look, colour
harmony, hue, balance, contrast, saturation, lighting dynamics, how light hits objects, HUD and UI design, touch
interaction, animation and the *naturalness* of objects on a table -- and then: "make it a instilled part of
your memory that always reminds you". A document nobody re-reads is not memory. So every rule below has, next to
it, **the file in this repo that enforces it**. If a rule has no enforcer, it says so out loud and becomes work.

Sources are listed at the end ([1]..[5]); the numbering in the rules refers to them.

---

## 1. VALUE IS THE HIERARCHY; HUE IS THE FLAVOUR

- **Value carries the read.** An image must be legible in greyscale before it is pretty in colour. [1]
- **Ramps are authored, not computed.** Every material steps along a hand-set ramp; nothing is multiplied. [2]
- **Hue shifts along the ramp.** Shadows drift cool, highlights drift warm -- because the shadow is lit by the
  ambient sky, not by the key light. [1][3]
- **Warm light casts cool shadow, and vice versa.** The temperature *contrast* is what makes shading read as
  light rather than as dirt. [1]
- **Never mix a warm and a cool at the same value** -- equal-value warm/cool is mud. Separate them in value. [1]

**In this repo:** `art/kit/palette.json` (81 colours, 13 ramps), the ramp ladder rule in
`tools/art/make_menu_room.py::_step_up`, and the gate `palette ramps + value separation` in
`scripts/Test-ArtPipeline.ps1`, which asserts *every step is at least 6 degrees of hue and separable in value*.
`LightRig.cs` never blends: it steps a pixel up its **own** ramp, which is [2] implemented literally.

## 2. LIGHT IS AN INTEGER OPERATION, NOT A GRADIENT

- Draw the scene **as if fully lit**, then work out how much light each pixel *lacks* and step it down its own
  ramp; **dither only between two adjacent steps**, in a narrow band. Never blend, never multiply. [2]
- **Several lights add their strengths, then one lookup.** [2]
- **Give every light a direction and a height.** Height sets the shadow's length: overhead = short and gathered,
  low = long and stretched. [1]
- **No glow without a named emitter.** (This project's own rule, and the menu's; the player has rejected
  unexplained light three times.)

**In this repo:** `unity/Assets/Scripts/Core/Rules/LightRig.cs` -- `LightAt()` = add, clamp to `Levels` (3);
`FractionAt()` = the ordered-dither remainder; `ShadowAt()` / `ShadowStrength()` = the cast shadow, longest at
the contact point. Measured twin: `tools/comp/preview_lightrig.py`. Gates: `LightRigTests`
(`several_lights_add_up`, `light_is_stepped_not_smooth`, `shadows_strengthen_toward_the_lit_side`).

## 3. AN OBJECT SITS ON A SURFACE; IT DOES NOT FLOAT ON IT

- **The contact point is the whole trick.** The one or two pixels where an object meets the surface must be the
  darkest thing on it (ambient occlusion), or the object reads as floating. [1][4]
- **Cast shadow and form shadow are different things.** The cast shadow leaves the object in the direction
  *opposite* the light; the form shadow is the object's own turned-away side. Both must exist. [1][4]
- **For a light on the same plane as the object (a monitor on your desk), the shadow is a flat smear at the
  object's foot, not a bar up its side.** A shadow that climbs the object means the light is above it.
- **A prop belongs in the surface's layer** so it inherits the contact shadow, the shared light and the shared
  perspective. (BUG-019, learned the hard way.)

**In this repo:** `desk_slab_sprite()` in `tools/art/make_desk_room.py` paints wood -> props -> contact shadow ->
light pool, in that order. `LightRig.ContactBand = 5` rows is the flat-shadow rule as a constant, and
`LightRigTests.shadows_strengthen_toward_the_lit_side` measures it.

## 4. THE MONITOR IS A LIGHT, AND TURNING IT ON MUST COST SOMETHING

- A screen in a dark room is the frame's **dominant source**: it lights what faces it and *deepens* what does
  not. Turning it off must give the shadows back -- if it does not, it is a decal, not a light. (BUG-018.)
- Its pool is projected **in front of the glass, onto the surface below it**, never onto the wall behind.
- It lights the sides of things: a cup's near flank, a keyboard's near keys, a mouse's near edge.

**In this repo:** `LightRig.ForDesk(tubeOn, ...)` / `ForMenu(tubeOn, lampOn, ...)`; the tube carries a **core
pool plus a wide, weak spill lobe** (`WithSpill`) because a real light has a near field and a far field -- one
ellipse cannot both floodlight the mouse and reach the coffee. Measured (v130, `preview_lightrig.py`):
wood under the tube **0.22 -> 2.52** steps; wall above it **0.00 -> 0.32**; the coffee's monitor-side flank
**0.13 -> 0.62** against its far flank **0.06 -> 0.38**; keyboard **0.20 -> 2.35**.

## 5. MOBILE: THE FINGER IS NOT A MOUSE

| Rule | Number | Source |
|---|---|---|
| Minimum tap target | **44pt iOS / 48dp Android** (~48 px at 480x270 is *not* enough -- see below) | [5][6] |
| Spacing between targets | **8dp minimum** | [5][6] |
| Primary actions | **bottom third / thumb arc**, not top corners | [5] |
| States that must be distinguishable | enabled, disabled, **hover (pointer only)**, focused, pressed | [6] |
| Feedback latency | **50-100 ms** immediate; 100-200 ms transitions; 300 ms max for small UI | [7][8] |
| No hover on touch | hover is a **desktop-only** state; a touch device gets press + settle instead | [5] |
| Contrast | **4.5:1** for body text, 3:1 for large text | [6] |

**The canvas conversion, done once, here:** the game is 480x270 and the phone is 16:9-ish, so **1 canvas px is
3-4 device px** on a phone. A 48dp target (~144-192 device px) is therefore **36-48 canvas px**. That is why
`HudRules`' plates are 12-24 px tall on the canvas: they are drawn small and hit-tested **padded**, which is the
"visual smaller than hit area" pattern in [5]. `HotspotHitTest` already pads; `HudTests` asserts
`hit_test_safe_area_and_overlap`.

## 6. ANIMATION IS LIFE AND FEEDBACK, NEVER DECORATION

- **Timing:** 100-150 ms for hover/immediate feedback, 200-300 ms for state changes, 300-500 ms for panels.
  Anything past 500 ms needs a reason. **Never linear** -- linear reads as broken. [7][8]
- **Interruptible and cheap:** an idle loop must not fight the thing the player is reading.
- **Anticipation, then follow-through.** A press squashes, a settle overshoots by 2% and returns.
- **The game's own rule:** an applicant's idle is a **tell channel** (synthetics never blink, `no_blink`), so
  the animation budget is a mechanic, not a polish item. `docs/08`, `motion_manifest.py`, and the gate
  `animation layers within budget` (10 layers, none above 12 fps).

## 7. THE HUD IS OBJECTS, AND SAYS WHAT IT IS

- The player's standing order: **"not displayed as text but as clickable objects."**
- Shape before number: five pips before the score; a band word before the digits. (**this repo's own rule**)
- Disabled is **grey and still drawn**, never invisible -- a vanished control teaches nothing. [6]
- One legend-free interface: every clickable thing looks clickable, so no key legend is needed.

**In this repo:** `Core/Rules/HudRules.cs` (geometry, states, hit test), `Game/Interaction/GameHudObjects.cs`
(one painted 480x270 layer, integer canvas coordinates, no per-button SpriteRenderers -- the 16-bit look cannot
survive eight fractional blits), and `HudTests` (23 labels upper-case, nothing in the middle band, grey means
dead).

## 8. TEXTURING AT 480x270

- **Dither is a tool, not a texture.** Use it to join two adjacent ramp steps; over-dithering is noise. [1][4]
- **Banding** -- several ramp pixels following the same path -- reads as an accidental line. Break it. [4]
- **A specular is a place, not a level.** One or two pixels at the edge that would really catch the light.
- **Wood is a grain, not a noise field:** long horizontal runs, a slower ramp, one front-edge specular.

**In this repo:** `make_menu_room.speculars()`, `dither()`, `floor_band()`; the floor chevrons in
`orders/05-phases.md` are an open, known banding artefact.

## 9. THE ROOM TELLS THE TIME

- A clock on the wall is **furniture**, not a readout -- it belongs in the room's light and its perspective.
- It must be **derived from the same clock the game runs on**, or it is a second truth and it will lie.

**In this repo (v130):** `Core/Rules/RoomClock.cs` (08:40 -> 00:00, clamped, monotonic, hands snapped to whole
pixels), `Game/Desk/RoomClockView.cs` (rotates the hands on the face the room painted), gate
`LightRigTests.clock_*`. `Resident Evil`-style diegetic readouts are the reason: the HUD says "how long have I
got", the wall says "how late is it".

---

## Sources

[1] Colour theory for pixel art -- value, ramps, hue shifting, warm/cool separation.
    https://www.pixel-editor.com/articles/color-theory-for-pixel-art
[2] Palette lighting -- per-colour ramps, ordered dither between steps, "several lights add, then one lookup".
    https://www.spritefusion.com/game-dev-atlas/pixel-art/palette-lighting
[3] Liberated Pixel Cup styleguide -- block out first, vary ramp hues, dither sparingly, props contrast with bg.
    https://lpc.opengameart.org/static/LPC-Style-Guide/build/styleguide.html
[4] Light and shadow with few colours -- contact dots, cast vs form shadow, hover by inverting contact.
    https://art-pia.com/guide-light-shadow-en.html
[5] Mobile touch psychology -- 44/48 minimums, 8dp spacing, occlusion, thumb arc, visual vs hit area.
    https://github.com/davila7/claude-code-templates/blob/main/cli-tool/components/skills/creative-design/mobile-design/touch-psychology.md
[6] Playables design best practices (Google) -- 48dp + 8dp, five button states, 4.5:1 / 3:1 contrast.
    https://developers.google.com/youtube/gaming/playables/certification/best_practices_design
[7] Micro-interaction animation -- 100-300 ms budgets, ease-out, anticipation and follow-through.
    https://playbooks.com/skills/dylantarre/animation-principles/micro-interactions
[8] How to design micro-interactions -- trigger/rules/feedback/loops, timing table, purpose over show.
    https://www.uxblueprints.com/guides/how-to-design-micro-interactions
