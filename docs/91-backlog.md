# Backlog (post-event / nice-to-have)

Anything not in `01-roadmap-14-days.md` lands here. No exceptions. Adding to this file is
cheap; adding to the build is not.

## v126 OPEN — the player's own list, in order

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
