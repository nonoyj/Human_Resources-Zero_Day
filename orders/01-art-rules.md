# ORDER 01 -- ART RULES

## The ruling

> *"I have said COPY what's in the menu art and just change things that's necessary for the game but you kept
> on making a totally different one which has the same vibes."*

This is the single most-repeated correction of the whole project. **Copy is a call, not a resemblance.**

- If the menu has it, the game **calls the menu's function**. `make_menu_room.window()`, `.crt()`,
  `.floor_band()`, `.cup()`, `.keyboard()`, `.mouse()`, `.forms()`.
- If a function is not parametric yet, **make it parametric**. Do not write a second version.
- After changing a shared generator, **prove the menu did not move**: render the menu sprites and compare to
  `HEAD` with zero differing pixels. That has to be quoted in the reply.
- "In the menu's materials", "in the menu's palette", "the same vibe" are all failures wearing a disguise.

## What must differ between menu and game (and only this)

| Thing | Menu | Game | Why |
|---|---|---|---|
| Desk depth | 38 px slab inside the desk sprite | 44 px slab as its own layer | the game layer must sort over the applicant |
| Dead hardware under the desk | present | **absent** | player: *"Remove the random monitors and servers underneath the table IN THE GAME version since thats only for the menu version."* |
| Applicant | none | a bust beside the desk | the game's subject |
| CRT contents | title, menu rows | the applicant's file | live data |
| Lamp | present | absent | no lamp in the game's room yet |

Everything else is copied.

## Placement

- The canvas is **top-down**; the layout's anchors are **y-UP from the bottom**, so
  `y_top = 270 - anchorY - spriteH`.
- **A prop standing on a surface has its base ON the surface line and its body ABOVE it.** The desk surface
  line is `DESK_ROW`. A prop whose base is drawn along the *front* lip is the "sitting on the edge of the
  table" bug.
- **A prop belongs in the surface's layer.** Separating props into their own sprites costs the contact shadow,
  the shared light and the shared perspective -- which is *precisely* what "does not look like it is ON the
  table" describes.
- Props are ordered left to right the way the player reads the desk. Current order, as instructed:
  **coffee, forms, keyboard, mouse** -- the coffee swapped with the keyboard and mouse, because the monitor is
  on the right and the keyboard belongs under it.

## Subjects

- The applicant **sits beside the desk**, not behind it and not under it. His base row is proud of the desk's
  far edge, his whole body is above the wood, and no part of him is occluded by it.
- Every colour comes from `art/kit/palette.json`.
- Hair is darker than skin; a cloth is darker than the face. Adjacent materials must differ in **value**.
- No ordered dither across a face.
- One specular per object, on the surface facing the light.
