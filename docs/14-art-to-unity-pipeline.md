# Art → Unity pipeline (how the real sprites become the real game)

**Nothing is redrawn for Unity.** The PNGs in `art/kit/` are the shipped art. Unity gets a byte-for-byte
copy, and every import setting is generated from data instead of clicked.

```
tools/art/make_*.py          draw the art              ->  art/kit/**/*.png
tools/art/make_sprite_manifest.py                       ->  art/kit/unity_sprites.json   (what each PNG is)
publish_to_unity.py                                     ->  unity/Assets/Art/Kit/**        (byte-identical copy)
                                                        ->  unity/Assets/Resources/*.json  (data Unity loads)
desk_layout.json (hand-authored, frozen)                ->  where every sprite goes, 480x270 px
        |                                                          |
        |                                                          +--> tools/comp/twin_desk.py   offline renderer
        +--> Editor/HRZeroDayMenu.cs  "Build Desk Scene"  ->  Assets/Scenes/Desk.unity
        +--> Editor/ArtKitImporter.cs "Import Art Kit"    ->  Point / uncompressed / 16 PPU / sliced
```

## Why it is built this way

| Failure this prevents | Where it is caught now |
|---|---|
| Artist's preview and the game drift apart | `twin_desk.py` draws the same json Unity reads |
| One folder imported with the wrong PPU or bilinear filter | `Import Art Kit` applies the manifest; `Verify Art Import` re-checks |
| A sprite added to `art/kit` but never reaches Unity | `Manifest_covers_every_sprite` test |
| Someone edits a PNG in `unity/Assets` and the repo art goes stale | `Published_art_is_current` (byte compare) + `publish_to_unity.py --verify` |
| A strip sliced into 3 frames or the wrong cell size | `Applicant_sheets_slice_into_four_frames` test |
| A hotspot that floats off the applicant | `Hotspots_inside_applicant` test |
| The camera/desk composition breaking the brief | `Desk_layout_matches_brief` (monitor left, applicant right, 480×270 @ 16 PPU) |
| Unity-facing C# not compiling because nobody opened Unity | `tools/UnityStubs` builds Game + Editor against stubs |

## Commands (all of them run without Unity)

```powershell
# change art, then:
python tools\art\make_applicants.py                 # regenerate (needs: pip install pillow)
python tools\art\make_sprite_manifest.py            # refresh the manifest
python tools\art\publish_to_unity.py                # copy into unity/Assets + Resources
.\scripts\Test-ArtPipeline.ps1                      # the five-link proof, one command
dotnet run --project tools\CoreTests                # 21 tests incl. the 6 art-bridge ones
```

## Inside the Unity Editor (Dev B, after the project exists)

1. `HR//0D > Build Desk Scene` → writes `Assets/Scenes/Desk.unity` (camera, 4 layers, applicant with
   4-frame idle, 9 collision hotspots named `HS_<artifact>`).
2. `HR//0D > Import Art Kit` → applies every setting from the manifest.
3. `HR//0D > Verify Art Import` → Console prints `art import PASS -- 73 sprites …`.
4. Press Play. The face across the table is frame 0 of the real sheet, breathing at 4 fps.

## Rules that keep this honest

- `unity/Assets/Art/Kit/**` is **generated**. Never hand-edit it; edit `art/kit` and re-publish.
- `unity/Assets/Content/desk_layout.json` is **authored** (by hand, reviewed). Change it there, then
  re-publish so Unity's `Resources` copy matches.
- The twin renderer is the referee: if `twin_desk.py` and the phone disagree, the layout or the import
  settings are wrong — never "the art changed itself".
