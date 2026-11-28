# 89 -- THE ROOM PASS: what you asked for, and how each one will be built

**Written 2026-11-25, from the player's list.** Every item is checked against the code as it stands, so the
spec is a plan and not a wish. **Nothing here is art that has been approved yet** -- the art goes out, you look
at it, you say yes.

The one-line version: *the menu's room is the best-looking thing in the game, and it is currently a separate
app from the desk.* This round makes them one room, gives the desk a real light rig, and makes the applicant a
person you are looking at rather than a sprite on a wall.

---

## 1. Your list, item by item

| # | You asked | What it means in code | Verdict |
|---|---|---|---|
| 1 | menu look / art style / light effects for the whole game | one **shared palette + one light pass**, extracted out of `MenuView` so the desk renders through the same code | do it -- it is the biggest single look win |
| 2 | update the applicant with what we learned + research human features | rebuild the face from measured proportions (eye line at 1/2 head height, etc.), same 192x216 slot | do it |
| 3 | stack the monitor panel in front of the window to give the applicant room | re-lay `desk_layout.json`: monitor left (in front of the window), applicant centred-right | do it |
| 4 | the lamp lights **upward**, the desk does not catch it | the lamp's light is currently a circle above the desk's front face; it must pool **on** the desk, and the paper under it must catch it | **you are right** -- see #3 |
| 5 | the main screen looks off; turn it on and off; when on, the keyboard / cup / mouse / table / monitor's sides light up, and shadows strengthen away from it | the screen is a light source in the menu and **not** in the desk. It needs a real on/off state and a directional light pass | do it -- see #4 |
| 6 | selected text should glow, contrasting with the selection glow; on mobile, **two taps to confirm** | selection glow + a confirm step (touch only) | do it |
| 7 | lamp hover lights only the bottom; light the whole lamp and its surroundings | the hover currently paints 3 rows under the shade; it should warm the fixture and its pool | do it |
| 8 | merge, push to GitHub, new instructions, update the backend's AI, check the backend | see #6 | do it |
| 9 | deep research on game HUD | see #7 | done |

---

## 2. The one structural decision: ONE palette, TWO renderers

**Today:** `art/kit` paints with 24 colours (`void, panel, line, linehi, text, warn, danger, synth ...`) while the
menu paints with 53 (`wall_0..3, floor_0..4, wood_0..4, teal_0..4, amber_0..4, cyan_0..2, scr_0..2 ...`). The menu
gets its look from that second palette: **every material owns a 2-5 step ramp, and light only ever walks a
surface one step up its own ramp** -- never a colour wash. That is the entire difference between the menu and the
desk, and it is why the desk looks flat and the menu looks lit.

**Plan:** port the ramp model to the desk without changing a single silhouette:

1. `menu_palette.py` becomes `art_palette.py`, the one source. The old kit names become **aliases** onto the
   nearest ramp step, so nothing that already draws breaks (`panel -> wall_1`, `line -> wall_2`, `warn ->
   amber_3`, ...).
2. `apply_palette_map.py` rewrites the desk PNGs colour-by-colour (24 colours in, ramp steps out). **Silhouettes
   are untouched** -- no resampling, no redraw -- so `twin_desk.py --verify` still matches and the proportions
   gate stays honest.
3. One light pass (`LightRig` in Core, shared) lights both the menu's 480x270 buffer and the desk's renderers.
   The desk's version works per-sprite-batch: a surface's ramp step is chosen by the light it is under, exactly
   as `MenuView.Light()` already does for the composite.

**The gate:** every pixel of every desk PNG must be a palette step, and every step must be reachable from its
ramp -- the same two rules the menu art already passes. This is a mechanical change, which is the point: it is
the *same* look applied by rule, not an artist's second pass.

---

## 3. The lamp (your catch)

**You are right, and here is the exact bug.** `MenuLayout.LampLight()` places the lamp's light at
`(LampX+28, LampY+70)` with a radius of 108x122 -- the centre sits **70 px down the sprite**, near the lamp's
*foot*, and the radius reaches **60 px above** the shade and only **52 px below** the foot. So the brightest
part of the pool is the wall *behind* the lamp and the fixture's base, while the desk under the shade catches
almost nothing. The paper under the lamp gets no light at all, because the pool is centred too low to reach it
and the desk's front face is outside the menu's frame.

**Fix:** the light's origin moves to the **shade's mouth** (the bulb is inside the shade, so the pool is centred
under the shade and offset slightly toward the shade's overhang), the radius becomes asymmetric -- long on the
desk, short on the wall -- and a second, dimmer **bounce** light is placed on the desk under the mouth so the
paper, mug and keys catch a warm skirting. That is how a real desk lamp reads: a bright ellipse on the surface,
a falloff up the wall, and a small lift where the light bounces off the paper.

---

## 4. The screen as a light (your catch)

**The desk's monitor is not a light source at all today.** `RoomLighting` tints *every* sprite by the shift
clock; nothing in the desk knows the monitor is on. The menu is the opposite: its monitor is the cold key and
everything else is lit by it.

**Fix -- a 3-state screen, owned by rules and drawn by the room:**

| state | when | what the room does |
|---|---|---|
| **ON** | an applicant is in the chair, the letter is open, the debrief is up | the cold key is ON: the desk's front edge, the keyboard's near faces, the mug's rim, the papers' top edges and the monitor's own cheeks lift one ramp step; **shadows fall away from the screen** and get darker with distance |
| **IDLE** | between applicants, the day summary, the menu | the key drops to a slow 0.5 Hz breathe (phosphor, not a switch) |
| **OFF** | the end of a shift, the ending screen, the after-hours walk | the key is gone; the room falls back to its lamp/ceiling fill, and the desk is visibly darker |

Shadows: 2 px contact shadows already exist under the props. They get **direction**: a prop left of the monitor
casts right, right of it casts left, and the shadow's length scales with the key. That is a per-prop offset in
the layout JSON, not a per-frame computation, so it stays 16-bit.

---

## 5. Selection, the mobile confirm, and the lamp's hover

* **Selection glow.** The selected row's label gets a lit step of its own ink (one ramp step up, dithered at the
  glyph edges) so the words *glow* inside the bar instead of being cut out of it. The bar's fill steps **down**
  one so the glow has somewhere to go -- that is what makes the two contrasts read against each other.
* **Two taps on mobile.** Touch: the first tap on a row **previews** it (the glow), the second tap within
  1.5 s **confirms**. Mouse and keyboard keep one press (a mouse selects on the way in and clicks on the way
  down; asking twice would be a tax on desktop). The rule lives in Core so it is testable: `MenuConfirm`.
* **The lamp's hover.** Instead of 3 rows of amber under the shade, hovering warms the whole fixture (shade,
  pole, base) one step, its pool on the desk one step, and the tops of everything inside the pool. Turning it
  off removes all four -- which is the demonstration you asked for.

---

## 6. GitHub, instructions, backend

* **Merge + push.** The workspace has 226 commits on `main`; your repo has your copy. The push script refuses to
  force, so the safe order is: install the bundle (which adopts the history), then `Push.ps1`, then check the
  remote is fast-forward.
* **New instructions.** `docs/80-COMMAND-BOOK.md` gets the v120 block; `AGENTS.md` (the file an agent reads
  first) gets the round's new rules -- the palette alias table, the lamp/screen light rig, and the "one renderer,
  one light pass" line.
* **The backend's "AI".** There is no model in `backend/` -- it is FastAPI + SQLite with 22 contract tests, and
  the game never needs it. What *is* owed there (from its own README): object storage under hash names, rate
  limits that are actually enforced (the table exists), a `DELETE /v1/devices/{key}` purge, and a Postgres
  placeholder switch. This round does the **rate limit** and the **purge**, because they are small and they are
  the two that matter before a public build.

---

## 7. The HUD research, condensed to rules we will follow

From the current design literature (diegetic vs non-diegetic, HUD readability, safe zones):

1. **The centre belongs to the play.** HUD sits in the corners; nothing permanent crosses the applicant's face.
2. **A HUD line is read in ~200 ms.** Hierarchy by urgency, not by importance: what changes now is loud, what is
   true always is quiet.
3. **Shape before number.** A bar is read before a digit; numbers are for exact values the player must act on.
4. **Stable anchoring.** Positions never move; only appearance animates.
5. **Earn every pixel.** Anything that can appear only when relevant should fade in and out -- our ALERTS panel
   is the model.
6. **Survive the background.** Every line needs a scrim or a shadow, because the room behind it is both dark and
   busy. (This is why the HUD already draws a backing panel; it will now be a *ramp step*, not a grey rectangle.)
7. **Safe zones.** 90 % title-safe inset for anything critical -- matters on the 20:9 phone target.
8. **Accessibility.** Reduced motion and no colour-only tells; the game already refuses colour-only tells in the
   tells themselves, and the reduce-motion path is M-5.

**What changes in our HUD because of this:** the shift block moves to the two bottom corners (day/quota left,
security right), keeps the applicant's identity line centre-bottom where it is read against the desk, and the
middle of the frame is left clear for the person you are judging.

---

## 8. What was built (v120 as shipped)

The plan above changed twice while being built, and both changes are worth keeping because they were
the *player's own words* winning an argument with the plan.

### 8.1 One palette, and the alias table is the whole trick

`tools/art/art_palette.py` merges the desk's 24 kit colours onto the menu's 53-step material ramps and
**fails a gate** if any of it is dishonest:

```
PASS: one palette -- 74 colours, 13 ramps, 26 legacy aliases, all within 64 of their home
      hue moved on purpose: orange, signal, synth, synthdim, text, textdim, warn, white
```

Four decisions the gate now enforces, so nobody has to remember them:

1. **Every colour belongs to a ramp** (`material_N`), so the light pass can always address it.
2. **An alias may not move a colour by more than 64** unless it is listed in `HUE_MOVED` with a reason.
   The eight that do move are listed, in the file, next to the colour they move.
3. **`paper` and `paperdim` do not move at all** and are the single authored exception: the letter is the
   surface the whole game is read on, and a warm cream sheet under a lamp is not negotiable.
4. **`synth` (violet) becomes the room's emitter cyan.** Violet was never in the art bible; the game's one
   synthetic accent is now the colour of the tube that does the judging.

`synth` moving is the only *visible* change in the art from the palette work, and it is the one that
makes the desk and the menu look like the same game.

### 8.2 The room got wider, not the person

Request C -- "stack the monitor panel IN FRONT OF THE WINDOW so the applicant has room to be looked at" --
was built as `window_wide()` in `promote_composition_L.py`, not as a change to the applicant's scale:

* the frame runs **x 168..472, y 8..152**: 88 px wider than v119, and its left edge meets the monitor's
  right edge, so the monitor (draw order 0, background at -30) genuinely covers the window's left mullion;
* the glass is three lit panes round a cross of mullions, and the **centre opening is deliberately empty**
  -- sky, city and frame only -- a dark plate for the head and shoulders;
* the sill sits at y152, **54 px above the desk**, so the body runs from the head into the desk band and
  he reads as standing behind a desk that is in front of him;
* the filing cabinet is **not drawn** any more: there is no honest wall left for it and a cabinet in the
  sky reads as a building. The function is still in the kit for a composition that has a wall.

### 8.3 The applicant is backlit now, because he is in front of a window

His face gained four things, all from the *same* rule (light comes from the window, on his left):

| added | why |
|---|---|
| a cold 1 px rim down his left temple and shoulder, dithered to 2 px at its brightest | without it he is a flat cut-out standing in front of a light, i.e. pasted on |
| a dithered shadow row under the right side of the jaw | a flat fill is a shape; a chin is a bottom |
| one pixel at the jaw's corner | the cheapest way to say "this edge turns" |
| one cast pixel under the nose's right | a nose that casts nothing is a sticker |

Two things were *removed* while doing it: the `paperdim` line across the throat (it read as a gold
collar painted on his neck) and a black `ink` chin line (it read as an outline, which the bible forbids).

### 8.4 The lamp, quantified

`MenuLayout.LampLight()` was centred at `(LampX+28, LampY+70)` -- 70 px down a 122 px-tall sprite, which is
its **foot** -- with a symmetric 108x122 radius. The pool therefore lit the wall behind the fixture, and
reached only ~52 px below the foot: the paper under the shade caught nothing. That is the inversion the
player reported, and it was arithmetic, not taste.

Now: origin at the shade's **mouth** (`LampY+96`), a tighter falloff (1.15) so the cone has an edge, and a
second dimmer **bounce** source lying on the desk surface (`LampY+118`, 74x54). Both switch with the
fixture, which `IsLampLight` now tests for.

### 8.5 The tube is a light with three states

`ScreenLight.cs` (new) bakes the monitor's light into the room sprite once per state change:

* **ON / IDLE (0.5 Hz phosphor breathe) / OFF**, derived from the game by `StateFor(ending, walk, summary,
  menu)` -- one function, so the desk, the walk, the ending and the menu cannot disagree;
* a CRT strike (three flickers) on the way in, 0.55 s to full, because a tube that snaps on reads as a
  sprite being enabled;
* the pool is an ellipse **projected down onto the desk** (`cy = glass.Y + 104`) -- the first attempt
  centred it on the glass and lit the wall, which is the *same bug as the lamp*, found the same way;
* **shadows point away from the tube**: the desk to the right of each prop goes down its own ramp, the
  length scaling with the prop's height and the tube's level, and the prop's near-left face lifts.

The ladder it walks is `PaletteLadder` (new): `material_N -> material_N+1`, read from the **published**
`palette.json` -- not sorted by brightness. The first attempt sorted by luminance and lit a blue-grey into
a cream; the authored names cannot make that mistake.

`tools/comp/preview_screenlight.py` applies the same rule offline and prints the before/after sheet, so the
look can be reviewed without opening Unity.

### 8.6 Selection, two taps, and a hover that means something

* the selected row's bar steps **down** to `void` and its label steps **up** with a dithered `cyan_2` halo
  around each glyph -- phosphor, not a drop shadow, because the glow is the same dither the room's light uses;
* a touch tap **arms** a row (hot `cyan_2` fill, label cut out in `void` -- the opposite contrast from the
  selection, so "aiming" and "pressed" cannot be confused) and a second tap within 1.5 s confirms. Mouse and
  keyboard still decide in one press. `MenuLayout.ConfirmWindow(touch)` is the rule, `MenuView.ArmOrConfirm`
  is the state, `Desk_Bootstrap.MenuClick(x, y, touch)` is the wiring;
* the lamp's hover now walks **the whole fixture** and **its pool** up their ramps, and fades back when the
  pointer leaves, instead of painting three rows of amber under the shade.

---

## 9. What the player's first session found (v125 -> v125.1), and what it cost

Four failures, reported with a log rather than a complaint. Each one is a class of bug, so each one gets a
rule, and every rule is now checked by something that runs.

| # | The report | What it actually was | The fix | The gate |
|---|---|---|---|---|
| 1 | `Test-ArtPipeline.ps1` will not parse: "The Try statement is missing its Catch or Finally block" | **My own edit.** I added a block with an unclosed `if` under the `try`. The file was fine in `211594f`; the fix is to revert and re-apply seven lines, not to restructure it. | the gate ran again the same day | the file is reverted to the committed version plus one insertion, and `git diff` is 7 lines |
| 2 | `[HR//0D] ScreenLight: the background is not readable` | the room, the desk and the monitor were imported with `isReadable: 0`, so `GetPixels32` threw and the tube did nothing at all | `write_unity_metas.py` READABLE + `ArtKitImporter` both now ask for Read/Write on `backgrounds/office_L0..L2` and `desk/desk_foreground` | `publish_to_unity.py --verify` fails if a readable asset stops being readable |
| 3 | "the monitor reads as if it is switched off" still, after my fix | **the tube was lighting the wrong sprite.** It was handed `L_background`, which is the picture `MenuView` REPLACES with its own composition at the front door -- and which never contained the desk's props. The desk in front of the glass stayed dark because nothing had ever touched it. | `ScreenLight` now captures EVERY static layer of the layout (room, monitor, stand, papers, desk), converts bottom-up -> top-down once, lights each in place, and leaves the animated applicant alone | `LightingTests.the_tube_lights_every_static_surface` asserts the code walks the layout and no longer takes a single renderer |
| 4 | "clicking the lamp only lights the objects behind it, not the desk and the paper" | the pool's centre was row **172** on a canvas whose desk surface starts at **206 in the game's layout** (the twin uses 150 for the menu's desk) -- i.e. the brightest part of the cone was *under the desk's front face*, and the bounce sat at row 180, in the floor. | the cone leaves the shade's mouth (row 158) and the bounce lies ON the surface (`DeskY+34`); the pool is derived from the desk band | `LightingTests.the_tube_lights_the_desk_not_the_floor` asserts `ProjectY > DeskRow` and `ProjectY - DeskRow < 60` |

And one thing the player asked for twice that the table alone could not deliver:

* **"clicking new game/continue still shows the old art and it does not look like the new menu"** -- because
  the alias table was a *description* of where the kit's colours should go, and nothing had applied it. It
  is applied now: `python tools/art/art_palette.py --apply` rewrites the desk's PNGs colour by colour (13
  distinct colours, ~1.8 M pixels), and the kit's navy/steel/red now sit on the front door's own ramp steps.
  What it refuses to touch is as important as what it moves: `orange`, `warn` and `synthdim` are **skin
  tones** as well as accents, so moving them turned every face khaki in the dry run -- they stay, and the
  skin ramp is unchanged. The desk also had to be able to climb more than one step for the light to show, so
  `PaletteLadder` now follows `MenuLayout.Ramps` and joins the pairs into a whole chain (`wood_1 -> ... ->
  wood_4`), which is exactly the ladder the menu's own light walks.

**Workspace size (also asked for).** `art/kit/reference` was 44 files / 2.8 MB of review sheets, twelve of
them the player's own screenshots from closed rounds, all of it duplicated into `unity/Assets/Art/Kit/`.
It is 27 files / 0.5 MB now, with a README that says what is kept and why; the review WAV is written to
`%TEMP%` instead of into the repo; and the bundle builder replaces `vNN` instead of accumulating them.
