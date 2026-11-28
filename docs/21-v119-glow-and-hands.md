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
