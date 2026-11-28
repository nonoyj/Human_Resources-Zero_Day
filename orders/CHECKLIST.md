# CHECKLIST -- run this, in order, before every reply

Tick each line. If one cannot be ticked, the reply is not ready.

## A. Before touching anything
- [ ] `orders/00-READ-FIRST.md` re-read this turn.
- [ ] `orders/01-art-rules.md` re-read (menu = the standard, copy the function).
- [ ] `orders/02-report-format.md` open while writing the reply.
- [ ] `orders/03-unity-and-bundle.md` open if a bundle is being handed over.
- [ ] `orders/04-IMPORTANT-MEMORY.md` -- the player's own words, verbatim, this turn's instructions included.
- [ ] `orders/05-phases.md` -- know the phase, the phases left, and this phase's remaining steps.
- [ ] The workspace is clean: no stale bundles, no scratch PNGs, no tool caches.

## B. While working
- [ ] Did I look for an existing generator before writing anything new? (`make_menu_room`, `make_desk_room`,
      `make_desk_props`.)
- [ ] Is the new thing a **call** to the menu's function, or a second drawing of it? If a second drawing, stop.
- [ ] Have I checked the numbers, not the picture: surface rows, base rows, anchor y-UP conversion?
- [ ] Have I looked at the render, not just the code?
- [ ] Have I bounds-guarded any `putpixel` loop with a computed coordinate?
- [ ] If I changed something the menu uses, did I verify the menu is **byte-identical** afterwards?

## C. Before saying it works
- [ ] `CoreTests` -> `ALL TESTS PASSED`
- [ ] `CompileCheck` -> `0 Error(s)`
- [ ] `Test-ArtPipeline.ps1` -> `ART PIPELINE OK`
- [ ] `publish_to_unity.py --verify` -> sprite count + sha256 verified
- [ ] `twin_desk.py --verify`
- [ ] `verify_fresh_clone.py` -> `PASS`
- [ ] The measured number for any light/position change is quoted in the reply.
- [ ] Anything unverified is named as unverified.

## D. The reply itself
- [ ] All four headings, spelled exactly: **What I did / What you did / What to expect / What's next**.
- [ ] "What I did" is an instruction list for the player, and it contains the **full PowerShell block**.
- [ ] The PowerShell block is complete: copy the zip, extract, apply, push, and write the gate log to a file.
- [ ] "What I did" says which file to send back and where the ERROR lines are in it.
- [ ] "What you did" records the player's instructions from this round, in their own terms.
- [ ] "What to expect" names what is deliberately **not** done.
- [ ] The phase line: current phase, phases left, what is left in this one.
- [ ] No claim of "works" without a render or an executed check.
