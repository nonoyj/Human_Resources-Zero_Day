# art/kit/reference — review sheets, not game art

Nothing in this folder is loaded by the game or read by any gate. It is here so a human can look at the
current output without regenerating it.

**Deliberately kept small (v120).** `git ls-files` in this folder used to be 44 files and 2.8 MB: twelve
of the player's own screenshots (`user_samples/`) and five superseded menu composites (`archive/`). Those
were review material for rounds that are closed, they were duplicated byte-for-byte into
`unity/Assets/Art/Kit/reference/`, and they were the largest single block of dead weight in the repository.

The two `user_samples` that still matter -- the approved mood references -- live in `art/concepts/`, which
is where the art bible points. If a future round needs a screenshot again, put it back for that round and
delete it when the round closes.

| Kept | Why |
|---|---|
| `contact_sheet.png` | every applicant, one page |
| `menu_v119_1x.png`, `menu_v119_3x.png` | the approved front door look |
| `palette_swatch.png` | the palette as a picture |
| `letter_preview.png`, `screen_preview.png` | the two reading surfaces |
| `sweep_*` | the after-hours walk |
| `twin_desk_human_solid_a_f0_L0.png` | the approved desk composition |

`make_sprite_manifest.py` lists this folder in `referenceSheets`; an empty folder is fine, a missing .wav
here is fine too (it is an audio preview, not a cue).
