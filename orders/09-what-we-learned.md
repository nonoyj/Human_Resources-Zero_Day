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

---

## H. v130.1 -- the four the player found in one screenshot (the newest, so read these first)

### H1. TWO GENERATORS, ONE FILENAME (BUG-032)
- **What the player saw:** "Applicant's art seems to have reverted back to the older version."
- **What happened:** `tools/art/make_applicants.py` (old, waist-up with fists on a drawn desk) and
  `tools/art/make_applicant_M.py` (the v127 approved bust) both wrote
  `art/kit/applicants/app_<key>_192x216.png`. In v130 I ran the OLD one to add skin variety to the palette,
  and it overwrote every approved applicant. Every gate stayed green: both painters are palette-clean and
  both produce a valid 4-cell strip.
- **RULE: a file that can be written by two generators has no owner.** When a second generator appears for
  the same output, one of them is retired in the same commit (`make_applicants.py` now refuses to paint and
  prints the survivor's name), and the survivor owns the WHOLE table of what exists.
- **RULE: when the player says "it reverted", believe the words and `git log -p` the binary.** In this repo
  `git log --oneline -- <sprite>` names the commit that changed it; the size and the bbox name the culprit.

### H2. A VARIANT TABLE IS NOT VARIETY (BUG-033)
- **What the player saw:** fourteen "variants" that were one person in fourteen hats.
- **What happened:** the identity table keyed tone to a PLACE on one ramp, and four of the five tones
  collapsed into the same steps -- so every family shared a complexion. Skin variety needs more than one
  ramp (`skin` and `skin2`), and every identity axis (hair colour, style, glasses, beard, tone) has to be
  varied ACROSS the fourteen, not stored per variant and forgotten.
- **RULE: look at the contact sheet, not at the count.** `make_applicant_M.py --contact-sheet` renders all
  fourteen side by side; a count passing is not a cast passing (orders/09 F1).

### H3. FOUR IDENTICAL FRAMES IS A BROKEN MECHANIC (the blink)
- **What happened:** v127 wrote one drawing into all four cells, so the idle did nothing -- and `no_blink`
  (a synthetic that never blinks) stopped being observable at all.
- **RULE: an animation is the only place some mechanics live.** If the four frames are byte-identical the
  test can see it, and there is a gate for it now (`variety_frames_are_not_copies`). Breathing, a blink and
  a fidget are three DIFFERENT drawings, not one picture with a one-pixel nudge.

### H4. A VIEW MAY NOT DEREFERENCE A COMPONENT IT DID NOT CREATE (BUG-031)
- **What the player saw:** `could not start the game: There is no 'SpriteRenderer' attached to the "Lamp"
  game object` -- a black desk instead of the game.
- **What happened:** `LampView.Awake()` did `GetComponent<SpriteRenderer>()`, and the lamp controller was
  added to a GameObject that had no renderer (the fixture was a separate layer). `AddComponent<>` throws only
  for a missing SCRIPT; a missing component is a null, and a null in `Awake` is an exception inside `Start`
  -- which is why the whole desk never built.
- **RULE: a controller is handed what it needs. If it cannot have it, it says so once and does nothing.**
  Use `TryGetComponent`, never a bare `GetComponent` in `Awake`, and `Bind(renderer)` for anything that is
  created by someone else.

### H5. A BUILDER THAT CAN RUN TWICE MUST CLEAN UP FIRST (the three applicants)
- **What the player saw:** three identical applicants side by side in the Game view.
- **What happened:** `Build` only removed a stale `DeskRoot` when it was given a parent. `GameBoot` passes
  `null`, so each Play added a desk next to the previous one -- and the previous one came from the scene
  `HR//0D > Build Desk Scene` had saved.
- **RULE: "build" means "the world contains exactly one of these when I am done".** Find and destroy the
  old root first, in the builder itself, so the editor menu and Play mode cannot disagree.

### H6. A VERSION IS A TAG, NOT A NUMBER (BUG-034)
- **What the player saw:** the block died with `the term '...RUN-v130.1.ps1' is not recognized`, and the
  sha256 printed at the top was the PREVIOUS build's.
- **What happened:** `Build-Bundle.ps1` typed its version as `[int]`, so `-Version 130.1` bound as **130**: the
  new bundle was written to the OLD file name. The player downloaded it, the name matched the name I quoted,
  and the only thing that could tell the difference was the content. My block then looked for a script that
  was not in that zip and produced a filesystem error instead of a sentence.
- **RULE: anything that names a delivery is a STRING.** A version, a tag, a build id -- never an int that a
  binder can round.
- **RULE: the bundle verifies ITS OWN contents before it applies anything.** `Build-Bundle.ps1` now refuses to
  produce a zip that lacks `RUN-v<version>.ps1`, and the handover block opens the zip, looks for its own
  script, and stops with one sentence ("this is not the v130.2 bundle; the file in Downloads is older")
  instead of a stack trace. **A download that is silently the wrong build is the most expensive kind of bug
  there is, because every other conclusion you draw from it is wrong too.**
- **RULE: versioned file names.** `HR-ZeroDay-bundle-v130.2.zip`, never a reused `v130`. Two rounds in a row
  were spent on a stale zip that Windows happily kept under the same name.

### H7. A SCRIPT MUST SAY HOW IT ENDED (BUG-035)
- The player's v130.2 apply worked: `HEAD 7229d9b`, 252 commits, `working tree clean -- files match HEAD
  exactly`. Then the block printed `[FAIL] Apply-Bundle returned 1`.
- **Cause:** `Apply-Bundle.ps1` never set an exit code on the success path, so `$LASTEXITCODE` was whatever
  the last `git` call happened to leave. The caller read that as failure.
- **RULE: every script exits with a value it chose.** `0` applied / `2` applied-but-the-tree-does-not-match-
  HEAD / `1` did not apply. A caller must be able to branch on it, and the number must agree with the
  `[ok]`/`[warn]` lines printed two inches above it. **A message that contradicts its own output is worse
  than no message** -- it teaches the player to distrust both.

### H8. A GATE THAT NEEDS A TOOL MUST SKIP, NOT FAIL (BUG-036)
- `Test-Scripts.ps1` shelled out to `pwsh` (PowerShell 7). The player's machine has only the PowerShell 5.1
  that ships with Windows, so a healthy repo produced `FAIL  Where-Am-I does not survive every repository
  state` -- a red line that says "your scripts are broken" when the truth is "you do not have a second
  PowerShell installed".
- **RULE: locate the tool, fall back to an equivalent, and only then skip -- loudly, in one sentence.** A
  gate that cannot run must never look like a gate that ran and failed. Sixteen PASS lines followed by one
  fake FAIL is the most expensive kind of red.

### H9. TWO COPIES OF A SCRIPT IS ONE COPY THAT IS STALE
- The repo-root `Apply-Bundle.ps1` (the launcher that ships to Downloads) had drifted from
  `scripts/Apply-Bundle.ps1`: 30,010 bytes vs 35,586. `Test-Scripts.ps1` had been catching it -- and saying
  nothing else was wrong -- for several rounds, and I had read it as noise. The player's apply had to print
  `this copy of the installer was out of date -- it updated itself and re-ran`.
- **RULE: a generated copy is checked by the gate that ships it.** `tools/check_powershell_syntax.py` now
  fails when the two files differ, so the drift is caught on my side before a bundle exists, not on his side
  in the middle of an install.

