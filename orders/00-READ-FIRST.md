# ORDER 00 -- READ THIS BEFORE EVERY REPLY

This file is the **gate**. Every response starts by working through `CHECKLIST.md`. If that file has not been
re-read in this turn, the response is not ready.

The other files in this folder are one subject each, and `CHECKLIST.md` indexes them:

| File | What it holds |
|---|---|
| `CHECKLIST.md` | the tick-list, walked before every reply |
| `01-art-rules.md` | copy the menu; placement; props in the surface's layer |
| `02-report-format.md` | the four headings, defined; the mandatory PowerShell block |
| `03-unity-and-bundle.md` | source of truth, what each gate's pass looks like, the bundle |
| `04-IMPORTANT-MEMORY.md` | the player's corrections, verbatim, newest first |
| `05-phases.md` | phase board, steps left in each, and every pending item |
| `06-art-and-light-research.md` | the study (colour, light, texture, UI, mobile, animation) with **the enforcer named next to each rule** |
| `07-THE-REPO.md` | the repo in undeletable memory: what the game is, its mechanics, its locked decisions |
| `08-angles-and-placement.md` | the second study: perspective, desk zones, occlusion, a person behind a table, mobile hover, pacing, animation |
| `09-what-we-learned.md` | **the ledger of every bug, as a rule, sorted by the symptom you would see.** Read this first when something breaks |
| `RUN-v130.ps1` | the one-paste handover for this round |

## The four-heading report -- the exact definition

The player corrected my reading of these headings once, so they are written down verbatim:

```
## What I did      -> WHAT YOU TOLD ME TO DO, as an instruction list, INCLUDING the PowerShell
                      command I am giving the player. This is my deliverable to them.
## What you did     -> WHAT THE PLAYER DID: the instructions and corrections they gave me.
## What to expect   -> what they will see, where, and what is deliberately NOT done.
## What's next      -> the ordered next steps, smallest first.
```

The distinction matters and has been got wrong twice: **the headings describe the reader's actions, so
"What I did" is my instruction to the player** -- it is not a diary of my edits. A diary of my edits belongs in
`What to expect` or in the commit message, never in `What I did`.

## The six hard rules

1. **Always hand over the command.** Every delivery ends with a PowerShell block the player can paste, that
   extracts the bundle, applies it, pushes it, and **pipes the gate output to a file they can send back**.
   A bundle without that block will not be extracted, and refusing is correct.
2. **The player's owed output is part of the deliverable.** "What I did" says exactly which log file to send
   back and where to look in it for the ERROR lines.
3. **Copy the menu; change only what the game requires.** Not "in the same palette", not "the same vibe" --
   the same *function*. If it exists in `make_menu_room`, call it.
4. **Never guess.** Every claim is a render or an executed check with its output quoted.
5. **State the phase.** Every response names the phase we are in, the phases left, and what is left in this one
   (see `PHASES.md`).
6. **Verify alignment numerically.** Anchors are y-UP; a prop's base sits ON the surface line; nothing is drawn
   in a band another layer covers. Check the numbers, then look at the render.

## Why this folder exists

The player asked, on 2026-11-28: *"make a folder for it that you will always check and it will contain EACH AND
EVERY instructions I have given to you all this time that you need to do at every response."* That is this
folder. Each entry in it came from a round where I got it wrong.
