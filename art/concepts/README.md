# Art concepts (not shipped art)

`art/kit/` is the shipped art. This folder holds only the two reference images the user approved the
**mood** from:

| File | Why it is kept |
|---|---|
| `menu_concept_A.png` | the approved theme/vibe/palette reference (title screen) |
| `desk_concept_A.png` | the approved desk mood reference |

Read `docs/15-art-bible.md` before drawing anything. The style rules, palette, composition and
rejected ideas all live there — not here.

## How a change happens now

1. Change the generator: `tools/art/promote_composition_L.py` owns the room + desk sprites,
   `tools/art/make_applicants.py` owns the 14 archetypes, `tools/art/make_art_kit.py` owns UI/icons/
   stamps (it no longer touches backgrounds — one path, one owner).
2. Run `.\scripts\Test-ArtPipeline.ps1` — it re-runs every generator and fails on any off-palette pixel.
3. Look at `art/kit/reference/twin_desk_human_solid_a_f0_L0.png`; that render **is** the engine result.

The intermediate composition studies (A–L) were deleted on 2026-11-26 once L was promoted; they are in
git history if ever needed. A single study remains for A/B comparisons:
`tools/art/study_desk_workdesk.py` → `art/concepts/study_K_workdesk_bold.png`,
`study_L_window_behind_shoulders.png`.
