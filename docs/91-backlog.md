# Backlog (post-event / nice-to-have)

Anything not in `01-roadmap-14-days.md` lands here. No exceptions. Adding to this file is
cheap; adding to the build is not.

## v127 OPEN -- the player's own list, in order

- **P. In-game art rebuild.** DONE this round for the room, the desk, the props, the junk and the applicant
  (`office_M0`, `desk_slab_M`, `foreground_M`, `monitor_M`, `monitor_stand_M`, `props_{coffee,keyboard,mouse,
  clipboard,papers,junk}`, `bust_M` + the 4-cell strip). Composition N is wired in `Content/desk_layout.json`
  as 13 layers. **Still open:** the OTHER applicant archetypes (bait, nervous, synthetic) still use the old
  generator and the old off-palette skin -- only `human_solid_a` was rebuilt.
- **Q. HUD as objects.** Idle desk, letter, debrief, summary, ending: done and tested. Still to do: the ALERTS
  popup drawn as objects (it still prints the Core panel's lines), and the object layer's `screens/` variant.
  **Unconfirmed on screen** -- the player has not yet reported seeing it.
- **O2/T. Lamp & monitor light.** MEASURED this round and fixed at the root (BUG-014): the desk now lifts
  **+16.0/255** between tube off and on and the wall does not move. The MENU's lamp still needs the same
  measurement re-run against the player's own build before either is called closed.
- **R. Missing mechanics.** Next candidates, in the order they appear in play: the Field Manual (menu row exists,
  no screen behind it), SETTINGS/CREDITS from the menu, save slots beyond the single run, and the day-5 ending
  screens' missing inputs.

## v127 notes for whoever picks this up
- **`unity/Assets/Content/desk_layout.json` is the source of truth**, not the copy under `Resources/`.
  `publish_to_unity.py` copies Content -> Resources, so editing the Resources copy is a change that gets
  silently reverted on the next publish. (Cost: one round, this round.)
- The layout's anchors are **y-UP from the canvas bottom**; compositing a sprite top-down needs
  `y = 270 - anchorY - spriteH`.
- `bust_M_192x216.png` is the applicant LAYER (one frame); `app_human_solid_a_192x216.png` is the 4-cell strip
  the animator crops. They are different files on purpose -- see `DeskSceneBuilder.LoadFrames`.

## v126 (superseded by v127)

- **P. In-game art rebuild.** Slice 1 (the room) is painted and gated: `office_M0`, `monitor_M`,
  `monitor_stand_M`, `foreground_M`. Still to do: wire the layout, then rebuild the **applicants** in the menu's
  idiom (the current faces are flat fills with a 1 px rim), then the props.
- **Q. HUD as objects.** Idle desk, letter, debrief, summary, ending: done and tested. Still to do: the ALERTS
  popup drawn as objects (it still prints the Core panel's lines), and the object layer's `screens/` variant.
- **R. Missing mechanics.** Next candidates, in the order they appear in play: the Field Manual (menu row exists,
  no screen behind it), SETTINGS/CREDITS from the menu, save slots beyond the single run, and the day-5 ending
  screens' missing inputs.

## Design ideas
- Voice-interview round with waveform analysis (audio tells)
- "Deepfake video call" round at Day 6+
- Multi-day persistent applicant who reappears after rejection with a changed name
- Endless mode with a leaderboard of "longest unbroken human streak"
- Field Manual as an unlockable in-game glossary with quiz at the end (certification screen)
- Photo-forensics zoom: examine EXIF-look metadata, shadow direction, hair/ear edges

## Education & outreach
- Post-run "what you just learned" card with the real-world equivalents
- Shareable result card ("I kept 19 humans safe, let 2 in")
- Teacher/workshop mode: printable scoring sheet, discussion prompts
- Localized EN / Filipino / Cebuano
- Companion web quiz for booths with no phones available

## Technical
- Cloud save, achievements, analytics (privacy-respecting, local-only aggregation)
- Hot-swappable content packs via StreamingAssets download
- Automated golden-image screenshot tests per screen
- WebGL build of the vertical slice for remote playtests
- Controller/keyboard accessibility paths for desktop streaming at the event

## Polish
- Applicant idle animations (breathe, blink, look-around)
- Per-day colour grading of the office background (escalating corruption)
- Terminal "boot sequence" on app launch with typed text
- Title screen ambient CRT shader
