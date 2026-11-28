# v119 — the glow, the ground, and hands on the menu

*Round notes. Player report: a broken-looking front door, a flat room, a floating table, monitors too small and
too tidy, "one art instead of layers", and a request: make the menu interactive.*

---

## 1. The bug that outranked everything: v118 did not compile

The player's paste contained one line that mattered more than the screenshot:

```
MenuView.cs(311,35): error CS0266: Cannot implicitly convert type 'double' to 'float'
```

`(MenuLayout.DustHash(i, 7) & 0xFFFF) / 65535.0 * MenuLayout.StackCycle` is a **double** in C#; the field it
went into was a `float`. One cast. And it was never caught because *there was no compiler in the build box* —
every round since v100 was verified by a Python twin renderer and a set of gates, none of which is a C#
compiler. That is the round's real lesson, so this round installed one:

* **`dotnet` 8.0.428** is now at `/home/user/.dotnet` (see `tools/UnityStubs/README` and the re-install line in
  the command book).
* `dotnet build tools/UnityStubs/CompileCheck.csproj` → **`Build succeeded.`**, zero errors, zero warnings.
* `dotnet run --project tools/CoreTests` → **`ALL TESTS PASSED`** — the first time the project's own rule suite
  has ever run.

Running it found **six defects that the gates could not see**, all of them real:

| # | what the suite said | what it was |
|---|---|---|
| 1 | `the lamp runs into the server` | true in the data as it stood; the lamp moved 2 px and the test now measures the **pole**, not the shade's deliberate overhang |
| 2 | `the desk does not meet the wall at the floor line` | the assertion compared the slab's *far edge* to the floor line; a desk in front of a wall has its front **panel** on the floor. The test was wrong, not the art |
| 3 | `the warm practical and the cold key are not on opposite sides of the eye line` | compared `lamp.X > 480` — a light that can only be "on the right" if it is **off the canvas**. Could never pass |
| 4 | `manifest_covers_every_sprite` | the new glow sprite was not minted |
| 5 | the glow-ramp test | **`palette.json` was stale**: the published palette had 71 colours and neither of the two new lit steps. Nothing in the pipeline regenerated it |
| 6 | (found while fixing 5) | the headless `JsonUtility.FromJson` stub returned `default(T)`, so **every test that read shipped JSON was passing on an empty result**. It parses for real now |

Two of those six were tests asserting things that were never true. **A gate nobody runs is a comment.**

## 2. The stale sprite slices: your Unity, not the repository

```
monitor_stack_0 not generated because the rect lies (partially) outside of texture
```

Root cause found, and it is a pipeline hole rather than a bad rect: **the source art folder
`unity/Assets/Art/Kit/` had no committed `.meta` files at all.** Your editor had imported those PNGs once,
written its own metas with slices, and kept them when v118 resized `monitor_stack.png` (112×96 → 76×66) and
`window.png` (104×132 → 88×160). The runtime mirror's metas were fine; the folder your project actually
imports from was not.

Fixed in `tools/art/write_unity_metas.py`:

* every published PNG now gets a `.meta` **in the source folder as well** (154 written this round);
* any existing meta that disagrees — slices, non-Single `spriteMode`, wrong `textureType`, a readable sprite
  that is not readable — is **repaired in place**, keeping its guid so nothing re-imports or loses a reference;
* `--check` fails on both faults, so the gate now closes the hole instead of describing it.

Nothing in this project ships a sliced menu sprite, so a meta that declares slices is stale by definition.

## 3. The room, in layers

Your sentence — *"make use of proper layering so that everything works out properly rather than putting
everything into one art"* — is now the structure of both the renderer and the sprite set:

```
  menu/shell.png            480x270   back wall, floor, their perspective, ambient occlusion
  menu/window.png            88x160   the city, wet glazing
  menu/monitor_stack.png    116x88    the table and its six tubes
  menu/crt.png              260x186   the monitor the menu lives in
  menu/server.png            26x112   the cabinet in the corner
  menu/desk.png             480x120   slab, keyboard, mouse, forms, cup
  menu/lamp.png              56x132   the warm practical, standing on the desk
  ------------------------------------------------------------------------------
  menu/glow.png             480x270   THE GLOW LAYER -- a separate pass, drawn last
```

The glow is a **layer**, not a filter: it is blitted after the room, after the light pass and after the
vignette, so a corner can never eat a halo and one source can be tuned without touching the art. The runtime
never computes a blur; it overlays a pre-dithered sprite and reads each pixel as a *strength*.

## 4. The table is on the ground now

You were right, and the reason was geometry rather than shading. In v118 the table was a 66 px sprite that
simply stopped. In v119:

* the sprite is **116×88** and carries the slab *and* its legs;
* the legs stop at canvas **y = 152**, which is the floor plane where the desk's far edge crosses it, and the
  desk is drawn **over** them — so the legs are visibly interrupted by the thing in front of them, which is
  what "standing on a surface" looks like in a single-plane room;
* each foot has a dithered contact shadow;
* there are **two posts, both in the right half** of the sprite: the monitor is drawn after the stack and
  covers everything west of canvas x = 364, so a post at the sprite's left edge would have been eaten and the
  table would have read as one-legged. The slab may vanish behind the monitor — that is depth — the support
  may not.

The sprite also moved **left** (372 → 330) because a 116 px table at the old x ran into the server.

## 5. Bigger, jumbled, touching

| | v118 | v119 |
|---|---|---|
| cases | 6 × 22×18, perfect 3×2 grid | 6 × **30–34 × 22–28**, interlocking rows, 1–2 px tilts |
| readout | one grid painted at fixed coordinates | painted **inside each cell's own bezel** (`MenuLayout.StackCells`) |
| glass | grey (`scr_1`) | **cyan**: a bright beam at the top of the tube falling into teal |
| mounts | on a wall rail | on a table with legs, feet on the floor plane |

The readouts are painted per-cell now, which is the fix for the "grey stickers" look: the old code stamped six
identical boxes at six fixed positions regardless of where the art put the cases. Both the generator and the
runtime index the same table, so **a click on a screen and the screen that lights up cannot disagree**.

## 6. The glow, and the two failed attempts before it

You asked for glow and no extra darkness. What shipped is a **distance-transform band map** over the room's
real emitters — the glazing, the lit tubes, the mouth of the lamp — so the light hugs the objects it leaves
instead of being a circle behind them. Two attempts are worth recording because they are the traps:

1. **A radial gradient is the wrong shape.** Bell curves centred on the emitters put the light where the
   emitter already is (and is cut out) and leave a ring around it. The first attempt painted a cyan ring around
   the window's *frame* and a glowing ellipse under the lamp — the "sprayed blob" failure.
2. **There was nothing to lift into.** The back wall already sat at the top of its own ramp, so a glow made of
   the wall's own steps was arithmetically invisible. That is why the palette gained two honest **lit steps**
   (`wall_3`, `floor_3` — 0.29 and 0.10 value, both still under the 0.62 ceiling) and why the glow's top cold
   band is a cyan rather than a teal. **A glow needs somewhere to go.**

The final arithmetic, in `MenuView.Glow()`:

* a glow pixel's colour is a **strength** (0–3 cold, 4–7 warm) — it lifts the surface under it that many steps
  up that surface's *own* material ramp;
* if the ramp runs out, the leftover strength becomes a **dithered tint** (that fraction of pixels take the
  glow's colour) — the 16-bit way to fake a colour that is not in the palette;
* the top band is emissive and painted as-is: that is the 1–2 px hot rim;
* **warm wins** wherever the lamp is nearer, so the lamp stays the only warm thing in the frame;
* the tube is never washed — you are reading it;
* fixtures (the CRT cases, the lamp's shade, the server, the cluster) are cut out: a housing is not a surface.

## 7. Hands on the menu — what I added, and why each one

You asked for suggestions *and* for them to be applied. Applied this round (all of them reversible, none of
them blocking):

| interaction | what it does | why |
|---|---|---|
| **Pointer hover on rows** | lights the row's edge in the tube's cyan and turns its text white; **hover moves the selection** | a hover that only highlights is a lie in a keyboard menu — two selections on screen at once; here there is always exactly one |
| **Click a row** | chooses it | a mouse player never has to find the arrow keys |
| **Pointer hover on the cluster** | the tube under the cursor brightens a step and its LED goes white | six machines that do not notice being pointed at read as wallpaper |
| **Click the lamp** | **switches the warm key off and on** | the fastest possible proof that the warm side is *light*, not paint; the whole image goes cold and you can see what the lamp was doing |
| **Pointer hover on the keyboard** | the keycaps light | the desk stops being a backdrop |
| **Click the keyboard** | types a line (`OK`, `OK OK`, `OK OK OK`, `WHY`) above the footer | a joke, and the cheapest honest proof that the furniture answers |
| **Keyboard hint** | `[UP/DOWN] MOVE [ENTER] CHOOSE` replaces the footer for a few seconds once you touch a key | first-time players are told the keys where they are already looking; it fades so it never becomes clutter |
| **Boot reveal** | the rows type themselves in ~0.5 s after the tube warms | half a second of ceremony that makes the front door a machine waking up rather than a pause screen. Any keypress skips it |

Not applied, because they change the game rather than the menu — say the word and they are next round:

* **a save-slot strip** under CONTINUE that shows the run's shift number and last applicant (needs a
  save-format decision);
* **drag-to-scroll** on the cluster so you can rearrange the six screens and the menu keeps your arrangement;
* **sound on hover** (the engine has one tick; a second, softer tick would need a new cue);
* **a settings row that previews** the volume bar by playing a tone as you step it.

## 8. Gates

| gate | state |
|---|---|
| `dotnet build tools/UnityStubs/CompileCheck.csproj` | **Build succeeded**, 0 errors, 0 warnings |
| `dotnet run --project tools/CoreTests` | **ALL TESTS PASSED** (36 checks) |
| `menu_palette.py --check` | 11 ramps, 13 pairs separated, ≥6°/step, **71%** of steps dark |
| `make_menu_room.py --check` | 0 off-palette pixels, 0 structural problems |
| `make_menu_composite.py --check` | the frame and the tube |
| `make_art_kit.py --check` | 99 sprites, every pixel palette-conformant |
| `make_sprite_manifest.py --verify` | 99 sprites, 30 reference sheets |
| `write_unity_metas.py --check` | all 167 runtime images + **154 source metas**, no stale slices |

New checks this round: `CheckThePointerCanLandOnEveryRow` (it immediately found that the row hit bands
overlapped by 2 px, so the bottom of every row resolved to the row above) and
`CheckTheGlowOnlyEverAddsLight` (it found the stale `palette.json`).

## 9. What to look at first in the editor

1. **Hover a row** — the cyan edge, the white text, the selection following the pointer.
2. **Click the lamp** — the warm side goes out and the room turns cold. Click again.
3. **Look under the table** — the legs are interrupted by the desk at the floor plane.
4. **Watch the cluster** — six tubes on six different beats, one of them flooding red every 14–26 s.
5. **Click a wall screen** — nothing yet; it brightens on hover only (the readout itself is on a timer).


---

# v119.1 — the correction round (player report on v119.0)

> "I did not export it because there are things wrong with the recent update. Define the ground more. The
> computers at the back is overlapping with the main monitor. The glows look wrong. I like the monitor and
> table look just need to define the ground more. I want the last edit on window look, right now it looks
> weird. I want you to make the highlights stronger."

v119.0 was **not** exported, so it is treated as a draft. Every one of those five notes is a real defect and
each is listed below with its cause. Nothing about v118's *composition* was re-litigated: the monitor, the
desk, the rows and the type are the ones the player approved.

## 10. "The glow looks wrong" / "the window looks weird" — the same bug

The window **sprite had not changed** (byte-identical to v118 — that was checked first, with a hash). The
weirdness was the glow pass, which for v119.0 had:

* a **solid `cyan_2` rim at distance 0** around every emitter, which is literally a one-pixel outline — a cyan
  pen line around the window frame; and
* a **fallback "dithered tint"** that painted the glow's own colour onto surfaces with no headroom left. The
  wall around the window was already at the top of its ramp, so it got speckled with cyan.

Both are gone. The rules now are:

1. **No rim, ever.** The cold ramp has no emissive step; the near band is a 3-in-4 dither and the far band
   1-in-5, so the halo has a soft edge.
2. **A glow pixel adds light or adds nothing.** If the surface under it has no next step up its own ramp, the
   pixel does nothing. It never paints its own colour.
3. Emissive pixels exist **only** in a new `_highlights()` pass: fewer than twenty 1–2 px speculars, each on an
   edge that would really catch a light, each on the *inside* of a silhouette.

A gate now enforces (3): it counts the glow layer's emissive pixels and fails if any of them sits on the
window's silhouette edge. **It caught three of my own first attempts**, which is the point.

## 11. "Define the ground more" — there was no floor

The desk sprite's front panel was a flat `wood_0` band from the wall down to the bottom of the frame, so the
bottom 57 px of the picture was *furniture*, and the "floor" the eye was asked to stand the desk on did not
exist. That panel is now a **tiled floor in perspective**:

* tiles converge on the same vanishing point (268) as the wall and the desk;
* rows get taller toward the camera, and the checker climbs a step (`floor_2`/`floor_4` near, `floor_1`/`floor_3`
  far) so the floor gets lighter toward the viewer;
* black grout with a one-pixel lit bevel on the far edge of every tile — the joint is what reads as depth;
* a 3 px **shadow socket** under the desk, then a dithered falloff in front of it, so the desk sits *in* the
  floor instead of floating on it;
* the monitor's cold spill lands as a pool in front of the desk, the lamp's warm pool at the right end, and the
  window's cold pool at the near-left corner.

The palette needed one more step for this: the floor ramp spanned value 0.022–0.100, i.e. the entire floor
lived inside 2–10% luma and every surface there was the same near-black. `floor_4` (0.145) was added — still
dark, still violet, and enough separation for a checker and a grout line to be readable. Gate: 71% of steps
still dark.

## 12. "The computers at the back is overlapping with the main monitor"

Exactly true, and the cause was arithmetic: the cluster started at canvas **x = 331** and the monitor's case
ends at **x = 364**, so the case's hard vertical edge cut two screens clean in half. A face cut by a vertical
line does not read as depth; it reads as a mistake.

Two constraints had to be solved together:

* the cluster must start right of the monitor (366+); and
* the room is only ~116 px wide there, and the lamp and the server both live in it — a 3-wide row physically
  cannot coexist with them (the first v119.1 attempt put the lamp's shade on top of the top-right screen).

**Solution: two across, three high.** 70 × 108 px, which makes each screen **31–35 px wide** — bigger than
v119.0's 27 and much bigger than v118's 22 — and leaves clear air for the lamp at x 422. The table's posts stay
visible (x 380 and 420), the feet meet the floor plane, and only the table tucks behind the monitor, which is
the overlap that reads as *layering* because the table is a thin slab.

The readout painter, the hover hit test and the twin renderer all read `MenuLayout.StackCells`, so a click on a
screen and the screen that lights up still cannot disagree.

## 13. "I want the last edit on window look"

The sprite was not the problem, but two things in it were worth fixing while there:

* a **broad checker band** across the middle of the pane (the old "city glow") — at 88 px wide a 16 px checker
  is a texture fault, not haze. Replaced with a graded sky of five ≥12 px steps whose seams are dithered across
  14 px, plus a tight three-row light-pollution band right above the skyline where it belongs.
* **no light inside the glass** — the pane was three steps of near-black. It now ends on `steel_1` at the
  horizon, has a moon with a two-step halo, and two **horizontal** wet-glass reflections (a vertical one-pixel
  line reads as a scratch; that trap is already in the bug log).

Frame, bars and sill unchanged.

## 14. "Make the highlights stronger"

Interpreted as speculars, not exposure — brightening everything is what made v116 flat in the first place. A
new `speculars()` pass puts the **brightest step of each material** on the edges that would really catch light:

| surface | highlight |
|---|---|
| desk front edge | `wood_4` line + two dashed `paper_2` runs at the light's reach |
| cup rim, keyboard top, spacebar, mouse spine | 1–2 px each |
| application forms, brass handle | dashed `paper_2`, `amber_3` |
| CRT case | lit top edge, bezel chamfer, one glass corner glint |
| cluster | each case's top edge + the table slab's bevel |
| lamp | the shade's lit rim, the brass stem |
| window | the sill's front edge (inside the frame) |

All dashed rather than continuous, all palette steps, none of them inventing a colour.

## 15. Gates (all green)

`Build succeeded` · `ALL TESTS PASSED` · 99 sprites palette-conformant · menu frame fits · 11 ramps / 71% dark ·
3 structural checks incl. **the new no-rim gate** · 10 layers ≤12 fps · manifest and metas current.

One test had to change with the art, and it was the right change: `the monitor cluster is clickable` now probes
`StackX + 8` instead of a hard-coded 340, **and** asserts `StackX >= CrtX + CrtW` — i.e. the no-overlap rule the
player reported is now a test, not a drawing habit.


---

# v119.2 — the last look pass: the table on the ground, and lines on the wall

> "Table at the back looks like it's floating. Define the ground and the wall behind more, separate it with
> lines."

## 16. Why the table floated (and why the previous fix did not work)

v119.1 gave the table feet that stopped at canvas y=149 and a small shadow. The player still read it as
floating, and the arithmetic says why: **the table's feet end on the WALL band, not on the floor.** The wall's
floor line is at y=150 and the desk (drawn after the table) covers the floor from y=150 down. So the only thing
under the table's feet was 1 px of floor, then wall again — and *a shadow drawn on a wall is still a table
standing on a wall*.

What fixes it, in three parts, all of them in `monitor_stack.png`:

1. **the sprite carries its own ground**: a floor band from the foot line to the bottom of the sprite, with the
   same lit bevel (`floor_3`) the desk's floor uses, so both read as ONE plane receding behind the desk;
2. **a wide ground shadow**: one pool across the table's whole footprint, darkest at the feet, dithered out to
   the edges — a table darkens the floor *around* it, and that pool is the only thing at this scale that says
   "touching" rather than "near";
3. **the feet are redrawn on top of their own shadow** with a lit top edge (`wood_3`), so the table stands *in*
   the pool instead of being swallowed by it. Foot pixels are `wood` — a different material from the floor — and
   that contrast is what the eye actually uses.

## 17. "Define the wall behind, separate it with lines"

The back wall was one value with two faint joints, which at 480x270 reads as fog. It now has a **panel grid**:

* vertical joints on a 96 px pitch: a hard `void` line with a `wall_1` bevel beside it (the bevel is the depth);
* a horizontal joint at y=108, where a dado rail would sit;
* the joints **fade with distance** (a quarter of their pixels drop back to `wall_0`), because a perfect grid
  reads as a cage;
* a black seam where the wall meets the floor (the skirting already had it) and the corner darkening kept.

Because the wall and the floor are now both *drawn* rather than implied, the two planes separate on their own —
which is what "separate it with lines" asks for, done with architecture instead of a drawn border.
