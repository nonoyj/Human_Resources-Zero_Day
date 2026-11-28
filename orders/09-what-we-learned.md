# 09 -- WHAT WE LEARNED: the ledger of things that must not happen twice

**What this file is.** Every bug in this project cost at least a day and, more importantly, cost the player's
trust. This is the list of what each one taught, written as a **RULE**, sorted by the symptom you would
actually see. When something breaks, read the section that matches the symptom FIRST -- four out of five times
the answer is already here, and it is here because we paid for it once already.

**How to use it.** (1) Symptom first. (2) The rule. (3) The trap in one line -- the thing that looks safe and
is not. (4) The gate that now catches it. If a new bug has no gate, the fix is not finished: add the gate, then
add the entry here.

**The three sentences that would have prevented half of this file:**
1. *A file is only a source of truth if the build reads that file.*
2. *A check that passes because it never ran is not a check.*
3. *Every number in a comment is a claim; if you did not measure it this round, it is probably stale.*

---

## A. THE BUILD WILL NOT START, OR THE SCREEN IS BLACK

| # | Symptom | The rule | The trap |
|---|---|---|---|
| A1 | `[HR//0D] could not start the game: <sprite> has no frame list in the manifest` (black Game view) | Any sprite a layer draws must have a **manifest entry with a frame list**. Regenerate `unity_sprites.json` after adding art. | The sprite exists, imports fine and shows in the Inspector -- so it looks "done". The manifest is a second file that also has to know. |
| A2 | `could not start the game: sprite ... has 1 frame` for the applicant | The applicant layer must point at a **4-cell strip**, not the single bust | Both files are 192x216 and both look right in isolation; one is an animator source and the other is a still |
| A3 | A prop is invisible in the room | A prop drawn into `backgrounds/office_M0.png` at a row the desk layer covers **does not exist**. Props that stand on the wood must be their own layer, sorted **above** the slab | Same file, same palette, same coordinates -- only the sorting order is wrong, and it is a number in a json |
| A4 | The room is black except the HUD | `GetPixels32` threw because the texture was imported with Read/Write off, and the light pass bailed | It logs a `LogWarning`, not an error; in a busy console it is invisible |
| A5 | Everything draws but the light does nothing | Check the **coordinate space first**: canvas numbers are TOP-DOWN rows; Unity's `Color32[]` is BOTTOM-UP; `desk_layout.json` anchors are **y-UP** | Three conventions in one project. Every one of them has already produced a "light on the ceiling" bug |

## B. THE LIGHT IS IN THE WRONG PLACE

| # | Symptom | The rule | The trap |
|---|---|---|---|
| B1 | The pool lights the wall, not the desk | A screen's light is projected **down onto the surface in front of it**; its centre must be below the desk's far edge and its ry short | A pool centred on the glass looks correct in isolation and is 40 px too high |
| B2 | The pool is a blob with a cyan edge | Light **steps a pixel up its own ramp**. Never blend, never multiply, never tint | Tinting looks better for about four seconds, then it stops being pixel art |
| B3 | The prop facing the light got darker | a) a shadow never covers the flank the source reaches; b) a light with no `Level`/`IsOn` is a decal | `!=` on `Color32` and "everything left of the prop" both shipped a wrong picture |
| B4 | Turning the monitor off changes nothing | Every source needs a **switch** and a test that flips it | The visual difference is subtle at 1x; only the numbers show it |
| B5 | Two lights that should be one room disagree | One list, or a gate that holds two lists together | `LightRig.ForMenu` and `MenuLayout.Lights()` are one room; the gate `menu_lights_are_one_list` exists because they drifted |
| B6 | The shadow is a dark bar up the side of a cup | See `orders/06` section 3: for a light on the **same plane**, the shadow is a flat smear at the foot | Physically the bar is "correct" for an overhead light, and this scene has none |

## C. THE SAME PERSON / THE WRONG FACE

| # | Symptom | The rule | The trap |
|---|---|---|---|
| C1 | Every applicant of an archetype looks identical | A variant is chosen **per applicant** from the run's rng and **stored on the record** | Mapping archetype -> file is the obvious implementation and makes 23 people into 6 faces; it also breaks the one rule the game has (read the tell, not the face) |
| C2 | A face is orange on one side and olive on the other | Every skin tone comes from the `skin` ramp, three **adjacent** steps, value cap 0.42 | `paper`/`warn`/`orange` were convenient names and are 35-50% brighter than any lit face |
| C3 | A face has no shading at all and every `P.ramp(...)` silently does nothing | `P.ramp(name, step)` must resolve against the **material palette**, not the legacy `painterly.RAMPS` table | `'skin_mid'` existed in a comment and in a dict nobody read; the fallback returns step 0 and nothing throws |
| C4 | Hair is a black helmet | Hair is a **ramp** (`hair_0..3`); black/brown/grey are places on it | `void`/`ink` are UI colours; using them for a material is the same bug as C2 |

## D. THE SCRIPT DIED ON WINDOWS (OR ON A DIFFERENT MACHINE)

| # | Symptom | The rule | The trap |
|---|---|---|---|
| D1 | `python.exe : Traceback` before line one runs | **ASCII only.** PowerShell 5.1 reads a script in the ANSI codepage; one UTF-8 arrow in one comment kills the file | It is a *comment*. Comments are supposed to be harmless |
| D2 | `SyntaxError: invalid character` on a smart quote | Same rule. Curly quotes and em-dashes come from copy-pasting prose into code | The editor renders them as quotes |
| D3 | A gate passes locally and fails on the player's box | `PWSH`/`dotnet` paths differ; use the repo's scripts and check `$LASTEXITCODE` | A PowerShell pipeline returns the exit code of the LAST command, so a failed step can vanish |
| D4 | Files differ in name but not in content | A file can be UTF-8 **with BOM** on one box and without on another; byte comparison then fails for a reason nobody can see | Editors add BOMs silently |

**Gate:** `python tools/check_ascii.py` is part of `Test-ArtPipeline.ps1`.

## E. THE EDIT LANDED IN THE WRONG PLACE

| # | Symptom | The rule | The trap |
|---|---|---|---|
| E1 | A function you "added" is not there | After any scripted insert, `grep -n "^def <name>"` **and** run the file | A marker string that appears twice makes `str.replace` land in the first scope, which compiles and is wrong |
| E2 | A constant has two values | Never copy a number out of `LightRig`/`MenuLayout` into another file: **alias it** (`public const int GlassX = LightRig.TubeGlassX;`) | The copy compiles and is right on the day it is written |
| E3 | A test asserts the opposite of the build | When a fact changes, **rewrite and rename** the test; never delete it quietly | `desk_has_no_lamp` was true in v129 and false in v130 |
| E4 | A test asserts a promise nothing can keep | Assert the promise the physics allows: the keyboard takes a whole step, the forms at 237 px take a third | "the tube must floodlight the whole desk" is not a rule, it is a wish |

## F. ARTIFACTS THAT PASS A GATE BUT ARE STILL WRONG

| # | Symptom | The rule | The trap |
|---|---|---|---|
| F1 | A sheet of variants looks like a cast of masks | Look at the **contact sheet**, not just the counts. Every variant, in the real room, on one image | A palette gate checks hue and value, not whether it looks like a person |
| F2 | Brown dither on the wood reads as a stain | Only dither **between adjacent steps of the same ramp** | Any two colours can be dithered; the eye reads the difference in value |
| F3 | A radial halo around a light | Light is a field of steps, not a circle of alpha | A soft radial looks right at 3x and is obviously a gradient at 4x |
| F4 | A `pngcheck`-clean sprite that is the wrong shape | Checks prove constraint satisfaction, never intent | A sprite can be palette-conformant, alpha-clean and a completely wrong drawing |

## G. HONESTY RULES (the ones about the reply itself)

| # | The rule | Why it is here |
|---|---|---|
| G1 | **Never say "fixed" when the player has not tested it.** Say "written, gated, untested by you" | The lamp went out twice; "fixed" was true of the code and false of the room |
| G2 | Quote the command output you actually ran, this turn | A remembered number is a stale number |
| G3 | Never tell the player to re-run "Setup Project" as a fix | It has never been the cause and it costs them 40 minutes |
| G4 | Do not guess a traceback | Ask for the log file, name the path, say which lines matter |
| G5 | "What I did" is **the instructions to the player**, including the PowerShell | The headings describe the reader's actions; this was mis-read twice |
| G6 | Every reply names the phase, the phases left, the steps left in this one | A status that is not stated is a status the player has to reconstruct |

---

## When you are stuck: the order to check

1. **Is the file I edited the file the build reads?** (`Resources/` copies are generated; `art/kit` is source)
2. **Did the check actually run?** (`$LASTEXITCODE`, a printed count, a non-empty log line)
3. **Is the number measured this round?** (render it, or print it)
4. **Is it a coordinate-space bug?** (top-down vs bottom-up vs y-up -- the three conventions)
5. **Is it a second copy of a number?** (alias it, don't copy it)
6. **Did the change land where I think?** (`grep -n`, then run the file)
7. **Is it ASCII?** (the Windows parse gate)
8. **Would a contact sheet show it?** (if yes, make the sheet)
