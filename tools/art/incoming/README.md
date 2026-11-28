# incoming/ -- put YOUR art and sound here

Drop a file in here with the name of the asset it replaces, run **one** command, and the manifest, the
Unity mirror, the import settings and the gates all update to match:

```powershell
python tools\art\replace_assets.py --from tools\art\incoming --dry-run   # check first
python tools\art\replace_assets.py --from tools\art\incoming             # replace + publish
```

The folder mirrors `art/kit/`, so the same name lands in the same place:

| you put | it replaces |
|---|---|
| `incoming/desk/stamp.png` | `art/kit/desk/stamp.png` |
| `incoming/audio/phone.wav` | `art/kit/audio/phone.wav` |
| `incoming/sweep/lift.png` | `art/kit/sweep/sweep_lift_480x270.png` (alias) |
| `incoming/audio/intro_01_beat3.wav` | the intro's beat 3 voice clip (v107 layout) |

`python tools\art\replace_assets.py --list` prints every name you can replace.

## What it will refuse, and why that is a kindness

- **A PNG of the wrong size.** Sprites are placed in pixels at 16 ppu. A 2x export is not "higher
  resolution", it is twice as big on the desk. (Override: `--allow-any-size`.)
- **A fully transparent PNG.** The most common export mistake; it replaces a working sprite with nothing.
- **A WAV that is not 24 kHz mono 16-bit.** The mix levels are measured in that format, so anything else
  changes the audio tests' numbers without changing the game. (Override: `--allow-any-format`.)
- **A name that is not a shipped asset.** A typo must not look like a successful replacement.

## After it lands

Gates first, then everything else:

```powershell
.\scripts\Test-ArtPipeline.ps1      # art + audio + import settings
dotnet run --project tools\CoreTests
```

If a replacement changes how a thing should *behave* (a sprite that is a different size on purpose, a sound
that needs a different level), that is a code change in `AudioRules.cs` / the layout json, not a file drop.
Ask for it rather than forcing a file through.
