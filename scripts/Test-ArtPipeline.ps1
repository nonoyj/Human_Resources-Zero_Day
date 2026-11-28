<#
.SYNOPSIS
    One command that proves the art -> Unity bridge is intact.

.DESCRIPTION
    The art is generated in art/kit, published into unity/Assets/Art/Kit, sliced by the Editor
    importer, and drawn offline by tools/comp/twin_desk.py. Five links can silently drift:

      1. art/kit/unity_sprites.json  does not match the PNGs on disk        -> make_sprite_manifest.py --verify
      2. unity/Assets/Art/Kit        does not match art/kit                -> publish_to_unity.py --verify
      3. the Resources json copies   are stale                             -> publish_to_unity.py --verify
      4. the desk layout breaks the brief (monitor left / applicant right) -> twin_desk.py --verify
      5. the Unity-facing C# stops compiling                               -> dotnet build CompileCheck

    Run this before any commit that touches art, layout or scripts. Needs Python 3 and the
    .NET SDK (Setup-DevEnvironment.ps1 installs both). Pillow is NOT needed.

.EXAMPLE
    .\scripts\Test-ArtPipeline.ps1
#>
[CmdletBinding()]
param([switch]$SkipCompile)

$ErrorActionPreference = 'Stop'
function S  ($m) { Write-Host "`n==> $m" -ForegroundColor Cyan }
function OK ($m) { Write-Host "  [ok]   $m" -ForegroundColor Green }
function D  ($m) { Write-Host "  [FAIL] $m" -ForegroundColor Red; $script:failed++ }

$failed = 0
$repo = Split-Path -Parent $PSScriptRoot
Push-Location $repo
try {
    S 'manifest matches the PNGs on disk'
    & python tools/art/make_sprite_manifest.py --verify
    if ($LASTEXITCODE -ne 0) { D 'manifest verify failed' } else { OK 'manifest current' }

    S 'unity copy matches art/kit'
    & python tools/art/publish_to_unity.py --verify
    if ($LASTEXITCODE -ne 0) { D 'published art is stale' } else { OK 'Assets/Art/Kit byte-identical' }

    S 'desk layout'
    & python tools/comp/twin_desk.py --verify
    if ($LASTEXITCODE -ne 0) { D 'layout verify failed' } else { OK 'layout matches the brief' }

    S 'palette gates'
    & python tools/art/make_art_kit.py --check
    if ($LASTEXITCODE -ne 0) { D 'kit palette check failed' } else { OK 'kit palette clean' }
    & python tools/art/make_applicants.py --check
    if ($LASTEXITCODE -ne 0) { D 'applicant palette check failed' } else { OK 'applicant palette clean' }

    S 'front door room: sprites + composite'
    # The room is generated, not hand-drawn: six palette-only sprites plus the composite that mirrors
    # MenuView.BuildRoom()/Light(). Both must pass or the menu the player sees is not the menu we tested.
    & python tools/art/make_menu_room.py --check
    if ($LASTEXITCODE -ne 0) { D 'room sprite palette check failed' } else { OK 'room sprites palette clean' }
    & python tools/art/make_menu_composite.py --check
    if ($LASTEXITCODE -ne 0) { D 'room composite check failed' } else { OK 'room geometry + menu fit' }

    S 'palette json: the front door materials are published'
    # Unity reads Resources/palette.json for every colour the HUD, the letter, the sweep and the front
    # door paint. Nothing used to WRITE it (BUG-074): the file was committed once and then silently went
    # stale. The writer merges, so the legacy keys the rest of the game needs survive.
    & python tools/art/menu_palette.py --write-json
    if ($LASTEXITCODE -ne 0) { D 'could not write art/kit/palette.json' } else { OK 'palette.json published' }

    S 'material palette: value separation + hue shifting'
    # A palette gate asks "is this pixel allowed"; this asks "can the eye tell these two things apart".
    # Six touching pairs failed the first version of it, and the room read as ONE object for two rounds.
    & python tools/art/menu_palette.py --check
    if ($LASTEXITCODE -ne 0) { D 'palette audit failed (value collision or grey ramp)' } else { OK 'palette ramps + value separation' }

    S 'motion manifest: frame rates are sane'
    & python tools/art/motion_manifest.py --check
    if ($LASTEXITCODE -ne 0) { D 'motion manifest failed' } else { OK 'animation layers within budget' }

    S 'wear overlays (corruption drift)'
    & python tools/art/make_wear_overlays.py --check
    if ($LASTEXITCODE -ne 0) { D 'wear overlay check failed' } else { OK 'wear overlays palette clean' }

    if (-not $SkipCompile) {
        S 'art bible is present and referenced'
    if (-not (Test-Path (Join-Path $repo 'docs\15-art-bible.md'))) { D 'docs/15-art-bible.md missing' }
    else { OK 'docs/15-art-bible.md present' }

    S 'monitor font atlas'

    & python tools/art/make_font.py --check

    if ($LASTEXITCODE -ne 0) { D 'font atlas check failed' } else { OK 'font atlas palette clean, metrics match' }


    S 'monitor screen codepage dump (stdlib only, writes to TEMP)'

    # BUG-031: this gate used to run preview_screen.py's Pillow path, which dies on a box without
    # Pillow -- and it wrote a PNG into art/kit/reference, so it broke the 'a gate never writes into
    # the repo' rule as well. The gate now runs the dependency-free --codepage mode into TEMP and
    # asserts the SHAPE of the glass (row count + column width) plus the card markers. The pretty
    # PNG stays a human tool: python tools/comp/preview_screen.py --zoom 4 --out screen_preview.png
    # $env:TEMP is not set on every host (it is on Windows, not under pwsh on Linux), so fall back
    # to the .NET temp path -- this gate must run anywhere the repo does.
    $tmpDir = if ($env:TEMP) { $env:TEMP } else { [System.IO.Path]::GetTempPath() }
    $dump = Join-Path $tmpDir 'hr0d_screen_codepage.txt'
    if (Test-Path $dump) { Remove-Item $dump -Force }
    & python tools/comp/preview_screen.py --codepage $dump *> $null
    if ($LASTEXITCODE -ne 0 -or -not (Test-Path $dump)) { D 'screen codepage dump failed to render' }
    else {
        $rows = @(Get-Content $dump | Where-Object { $_ -like '|*|' })
        $want = 9
        if ($rows.Count -ne $want) { D ("glass grid is $($rows.Count) rows, expected $want (metrics drifted?)") }
        elseif ($rows[0].Length -ne 22) { D ("glass grid is $($rows[0].Length - 2) columns, expected 20") }
        elseif (-not (Select-String -Path $dump -Pattern 'GRAD' -Quiet)) { D 'card is missing the GRAD line' }
        else { OK ("glass grid $($rows[0].Length - 2) x $($rows.Count), card markers present") }
    }


    S 'kit study scripts park their output (no writes to art/concepts)'
    # The old gate RAN study_desk_workdesk.py, which (a) draws from pre-v42 assets, (b) writes PNGs into
    # art/concepts on every run, and (c) is the only place PowerShell saw Python's stderr as a
    # NativeCommandError in 5.1 -- a red traceback in the middle of a green gate. Retired 2026-11-26.
    $stray = Get-ChildItem -Path (Join-Path $repo 'tools\art') -Filter 'study_*.py' -ErrorAction SilentlyContinue |
        Where-Object { $_.Name -ne 'study_desk_workdesk.py' }
    if ($stray) { D ("unexpected study script(s): " + ($stray.Name -join ', ')) }
    else { OK 'study outputs come from twin_desk.py' }

    S 'Unity scripts type-check (stubs, no Unity install needed)'
        Push-Location (Join-Path $repo 'tools\UnityStubs')
        try {
            & dotnet build -v q --nologo | Out-Host
            if ($LASTEXITCODE -ne 0) { D 'Game/Editor scripts do not compile' } else { OK 'Game + Editor compile' }
        } finally { Pop-Location }
    }

    S 'the game''s own rule suite runs (CoreTests)'
        # v119: this suite existed since v100 and had NEVER been executed, because there was no C# compiler
        # in the build box. The first run found two assertions that could never pass, a stale published
        # palette and a stub JsonUtility that returned nothing. It runs on every gate from now on: a rule
        # nobody enforces is a comment.
        Push-Location $repo
        try {
            & dotnet run --project tools/CoreTests -v q --nologo 2>&1 | Out-Host
            if ($LASTEXITCODE -ne 0) { D 'the core rule tests fail' } else { OK 'every core rule test passes' }
        } finally { Pop-Location }
    }

    S 'stubs do not declare package-only namespaces'
        # A stub that invents a package namespace makes the local gate green while Unity refuses to
        # compile the asmdef and opens in Safe Mode. Rules: tools/UnityStubs/ASSEMBLY-RULES.md
        $forbidden = @('UnityEngine.EventSystems', 'UnityEngine.UI;', 'UnityEngine.InputSystem',
                       'UnityEditor.U2D.Sprites')
        $stubText = Get-Content (Join-Path $repo 'tools\UnityStubs\UnityStubs.cs') -Raw
        $offenders = @()
        foreach ($ns in $forbidden) {
            if ($stubText -match [regex]::Escape($ns)) { $offenders += $ns }
        }
        if ($offenders.Count -gt 0) {
            $failed++
            Write-Host ("  [FAIL] package-only namespace in stubs: " + ($offenders -join ', ')) -ForegroundColor Red
        } else { OK 'stubs declare engine/editor types only' }



    S 'renderer usage matches Unity''s real type hierarchy'
        $bad = @()
        Get-ChildItem (Join-Path $repo 'unity\Assets\Scripts') -Recurse -Filter *.cs | ForEach-Object {
            $text = Get-Content $_.FullName -Raw
            # Match CODE, not commentary. The first version of this rule failed SweepView.cs because a
            # comment explaining the rule ('never via GetComponent<MeshRenderer>()') sat within the
            # window. A gate that fires on its own documentation teaches people to delete the docs.
            $code = [regex]::Replace($text, '(?m)//.*$', '')
            # SpriteRenderer IS a Renderer: never ask it for a MeshRenderer.
            if ($code -match '(?s)SpriteRenderer[^;]{0,80}GetComponent<MeshRenderer>') {
                $bad += "$($_.Name): asks a SpriteRenderer for its MeshRenderer (throws at Play)"
            }
            # TextMesh is NOT a Renderer: never set sortingOrder on it directly.
            if ($code -match '(?m)^\s*\w*[Mm]esh\w*\.sortingOrder\s*=') {
                $bad += "$($_.Name): sets sortingOrder on a TextMesh (CS1061)"
            }
        }
        if ($bad.Count -gt 0) {
            $failed++
            foreach ($b in $bad) { Write-Host "  [FAIL] $b" -ForegroundColor Red }
        } else { OK 'sprite renderers set their own order; text meshes go through the guarded helper' }

    S 'stub honesty (TextMesh has no renderer members)'
        $stub = Get-Content (Join-Path $repo 'tools\UnityStubs\UnityStubs.cs') -Raw
        $tm = [regex]::Match($stub, 'class TextMesh[^{]*\{(?<body>.*?)\n    \}', 'Singleline')
        if (-not $tm.Success) { $failed++; Write-Host '  [FAIL] TextMesh stub not found' -ForegroundColor Red }
        else {
            # sortingOrder / sortingLayerName live on Renderer in Unity. Declaring them on TextMesh made
            # the compile gate pass while the EDITOR failed with CS1061 and dropped into Safe Mode.
            $bad = @('sortingOrder', 'sortingLayerName') | Where-Object { $tm.Groups['body'].Value -match ("public [\w<>\[\]]+ " + $_ + "\b") }
            if ($bad.Count -gt 0) {
                $failed++
                Write-Host ("  [FAIL] TextMesh stub declares " + ($bad -join ', ') + ' -- those are Renderer members in Unity') -ForegroundColor Red
            } else { OK 'TextMesh stub has no renderer members' }
        }

    S 'every texture painted from the raster is flipped out of PNG row order'
        $bad = @()
        Get-ChildItem (Join-Path $repo 'unity\Assets\Scripts') -Recurse -Filter *.cs | ForEach-Object {
            $code = [regex]::Replace((Get-Content $_.FullName -Raw), '(?m)//.*$', '')
            # Only TEXT textures are at risk: the raster (MonitorScreenRaster.Render / TextPanel.Paint)
            # writes row 0 = TOP, SetPixels32 wants row 0 = BOTTOM. A symmetric sprite (the hotspot
            # corner brackets) is identical either way and needs no flip.
            $paintsText = $code -match 'MonitorScreenRaster\.Render|TextPanel\.Paint'
            if ($paintsText -and $code -match 'SetPixels32') {
                if ($code -notmatch 'FlipToTextureOrder') {
                    $bad += "$($_.Name): SetPixels32 without FlipToTextureOrder -- text will render upside-down"
                }
            }
        }
        if ($bad.Count -gt 0) {
            $failed++
            foreach ($b in $bad) { Write-Host "  [FAIL] $b" -ForegroundColor Red }
        } else { OK 'every painted texture flips PNG row order before SetPixels32' }

    S 'hotspot placement uses the shared maths (no second copy of the scale arithmetic)'
        $builders = @(
            (Join-Path $repo 'unity\Assets\Scripts\Game\Interaction\DeskRuntimeScene.cs'),
            (Join-Path $repo 'unity\Assets\Scripts\Game\Input\GameInputRouter.cs'),
            (Join-Path $repo 'unity\Assets\Scripts\Game\Interaction\GameInputRouter.cs')
        ) | Where-Object { Test-Path -LiteralPath $_ }
        $bad = @()
        foreach ($f in $builders) {
            $code = [regex]::Replace((Get-Content $f -Raw), '(?m)//.*$', '')
            # The bug this guards: rect maths written inline instead of through HotspotHitTest, which is
            # how every hotspot ended up at the applicant's centre on 2026-11-26 (9 of 9 unclickable).
            if ($code -match 'rect\[0\][^;\n]*0\.5f') {
                $bad += "$(Split-Path $f -Leaf): places a hotspot with inline rect maths -- use HotspotHitTest"
            }
        }
        if ($bad.Count -gt 0) {
            $failed++
            foreach ($b in $bad) { Write-Host "  [FAIL] $b" -ForegroundColor Red }
        } else { OK 'hotspot geometry comes from HotspotHitTest, which the reach test also calls' }

    S 'assets the game reads pixels from are imported readable'
        $bad = @()
        $needReadable = @(
            'unity\Assets\Resources\Art\Kit\sweep\sweep_desk_480x270.png.meta',
            'unity\Assets\Resources\Art\Kit\sweep\sweep_corridor_480x270.png.meta',
            'unity\Assets\Resources\Art\Kit\sweep\sweep_lift_480x270.png.meta',
            'unity\Assets\Resources\fonts\monitor_font_5x7.png.meta',
            # THE FRONT DOOR'S ROOM (v119.4, BUG-082). MenuView blits these into one buffer with
            # GetPixels32; with Read/Write off the call throws, the room comes up empty, and the menu
            # draws its text over the desk scene -- the player's "menu looks bugged" screenshot. The
            # list is short on purpose: the unit test enumerates MenuView.Blit and checks EVERY sprite,
            # so this gate only has to prove the pipeline wrote readable metas at all.
            'unity\Assets\Resources\Art\Kit\menu\shell.png.meta',
            'unity\Assets\Resources\Art\Kit\menu\crt.png.meta',
            'unity\Assets\Resources\Art\Kit\menu\desk.png.meta',
            'unity\Assets\Resources\Art\Kit\menu\glow.png.meta'
        )
        foreach ($rel in $needReadable) {
            $p = Join-Path $repo $rel
            if (-not (Test-Path -LiteralPath $p)) { $bad += "missing meta: $rel"; continue }
            # SweepView composites a floor's art into the page texture, and every text renderer reads the
            # glyph atlas. Without Read/Write Enabled, GetPixels throws and the walk draws text over a
            # flat fill -- which is exactly what the player photographed on 2026-11-27.
            if ((Get-Content -LiteralPath $p -Raw) -notmatch 'isReadable:\s*1') {
                $bad += "not readable: $rel (run python tools/art/write_unity_metas.py)"
            }
        }
        if ($bad.Count -gt 0) {
            $failed++
            foreach ($b in $bad) { Write-Host "  [FAIL] $b" -ForegroundColor Red }
        } else { OK 'sweep art, the glyph atlas and the menu room are Read/Write Enabled' }

    S 'text is painted by one renderer, from one glyph source'
        $bad = @()
        Get-ChildItem (Join-Path $repo 'unity\Assets\Scripts') -Recurse -Filter *.cs | ForEach-Object {
            $code = [regex]::Replace((Get-Content $_.FullName -Raw), '(?m)//.*$', '')
            # BUG-057: TextPanel skipped a glyph row when its leftmost pixel was empty, which erased half
            # of every painted character. The CRT (MonitorScreenRaster) never had the shortcut, which is
            # why the same words read correctly on the glass and as noise in a callout.
            if ($code -match 'InkAt\([^)]*,\s*0\s*,\s*gy\)\)\s*continue') {
                $bad += "$($_.Name): skips glyph rows whose first column is empty -- that erases most letters"
            }
            # BUG-057: two glyph sources meant two different atlases on screen; the PNG path is
            # row-bottom-up and produced scrambled text. Only MonitorScreenView.Load may build one.
            if ($code -match 'new FontAtlasGlyphSource\(' -and $_.Name -ne 'MonitorScreenView.cs') {
                $bad += "$($_.Name): builds its own FontAtlasGlyphSource -- call MonitorScreenView.Load"
            }
        }
        if ($bad.Count -gt 0) {
            $failed++
            foreach ($b in $bad) { Write-Host "  [FAIL] $b" -ForegroundColor Red }
        } else { OK 'one glyph source, and no renderer skips glyph rows' }

    S 'screen elements are placed in canvas px, through PageTransform'
        $views = @(
            (Join-Path $repo 'unity\Assets\Scripts\Game\Interaction\SweepView.cs'),
            (Join-Path $repo 'unity\Assets\Scripts\Game\Interaction\DeskEvidenceOverlay.cs'),
            (Join-Path $repo 'unity\Assets\Scripts\Game\Interaction\GameHud.cs')
        ) | Where-Object { Test-Path -LiteralPath $_ }
        $bad = @()
        foreach ($f in $views) {
            $code = [regex]::Replace((Get-Content $f -Raw), '(?m)//.*$', '')
            # The class of bug behind three player reports in two days: a page element positioned by
            # dividing a canvas-pixel value by the ppu inline, or a sprite scaled by its own pixel size a
            # second time (Sprite.Create already divides by the ppu). Both look right in code and are
            # metres off on screen. PageTransform owns the conversion; nothing else may do it.
            if ($code -match '(KeyBandTop|BandTop|KeyStripH)\s*/\s*Ppu') {
                $bad += "$(Split-Path $f -Leaf): divides a y-down page offset by Ppu -- use PageTransform.TopLeftToWorld"
            }
            if ($code -match 'localScale\s*=\s*new Vector3\(\s*w\s*/\s*pixelsPerUnit') {
                $bad += "$(Split-Path $f -Leaf): scales a sprite by its own pixel size (double size) -- a ppu sprite needs no scale"
            }
        }
        if ($bad.Count -gt 0) {
            $failed++
            foreach ($b in $bad) { Write-Host "  [FAIL] $b" -ForegroundColor Red }
        } else { OK 'page elements convert canvas px through PageTransform, and paint at natural scale' }

    S 'applicant proportions (no floating heads, no cone skulls)'
        & python (Join-Path $repo 'tools\art\check_proportions.py')
        if ($LASTEXITCODE -ne 0) { $failed++; Write-Host '  [FAIL] applicant proportions' -ForegroundColor Red }
        else { OK 'applicant proportions are human' }

    # INVERTED 2026-11-25. This used to REQUIRE Resources/Art to be untracked, on the reasoning that it is
    # derived from art/kit. That is what shipped a bundle with no runtime art: Resources.Load cannot read
    # outside a Resources folder, Unity never runs python, so the folder MUST be committed. What matters is
    # not that it is uncommitted -- it is that it MATCHES art/kit, which is the check below.


    S 'animation frames and import settings are committed'
        & python (Join-Path $repo 'tools\art\extract_frames.py') --check | Out-Null
        if ($LASTEXITCODE -ne 0) {
            $failed++; Write-Host '  [FAIL] animation frames missing -- run: python tools/art/extract_frames.py' -ForegroundColor Red
        } else { OK 'all animation frames exist in both mirrors' }
        & python (Join-Path $repo 'tools\art\write_unity_metas.py') --check | Out-Null
        if ($LASTEXITCODE -ne 0) {
            $failed++; Write-Host '  [FAIL] a runtime image has no .meta -- Unity would import it as a plain texture, not a sprite' -ForegroundColor Red
        } else { OK 'every runtime image has import settings' }

    S 'font atlas is imported readable (no menu needed on a fresh clone)'
        $metas = @('unity\Assets\Resources\fonts\monitor_font_5x7.png.meta',
                   'unity\Assets\Resources\Art\Kit\fonts\monitor_font_5x7.png.meta')
        $badMeta = @()
        foreach ($m in $metas) {
            $full = Join-Path $repo $m
            if (-not (Test-Path $full)) { $badMeta += "$m (missing)"; continue }
            if (-not (Select-String -Path $full -Pattern 'isReadable:\s*1' -Quiet)) { $badMeta += "$m (isReadable not 1)" }
        }
        if ($badMeta.Count -gt 0) {
            $failed++
            Write-Host ("  [FAIL] " + ($badMeta -join '; ') + " -- the runtime reads this texture pixel by pixel, so it MUST be imported readable") -ForegroundColor Red
        } else { OK 'atlas import settings committed (isReadable: 1)' }

    S 'runtime art mirror is committed and matches art/kit'
        $trackedCount = (& git ls-files "unity/Assets/Resources/Art" 2>$null | Measure-Object).Count
        $needed = & python -c "import json;print(len(json.load(open('art/kit/unity_sprites.json'))['sprites']))" 2>$null
        if ($trackedCount -lt [int]$needed) {
            $failed++
            Write-Host ("  [FAIL] only " + $trackedCount + " of " + $needed + " runtime sprites are in git -- a clone would fail at Play with 'sprite not found at Resources/Art/Kit/...'") -ForegroundColor Red
        } else {
            & python (Join-Path $repo 'tools\art\publish_to_unity.py') --verify | Out-Null
            if ($LASTEXITCODE -ne 0) {
                $failed++; Write-Host '  [FAIL] runtime mirror does not match art/kit -- run: python tools/art/publish_to_unity.py' -ForegroundColor Red
            } else { OK "$trackedCount runtime sprites committed and byte-identical to art/kit" }
        }

    # -----------------------------------------------------------------------------------------
    # THE SOUNDS (v105). Every cue the game can make is a generated WAV, committed twice: art/kit/audio
    # (authoring) and Resources/Art/Kit/audio (what Resources.Load reads). Three things are checked, and
    # each of them is a player-visible failure if it slips:
    #   1. every WAV is present and matches its manifest hash (no silent half-published kit)
    #   2. every WAV has a .meta with audio import settings (without it Unity uses defaults and a cue may
    #      decompress on the frame it is played -- a hitch exactly when the player presses A)
    #   3. every cue maps to a file that exists (a renamed WAV must not become a silent hole in the game)
    # -----------------------------------------------------------------------------------------
    S 'audio kit matches its manifest (9 sounds, 24 kHz mono)'
    $audioOut = & python (Join-Path $repo 'tools\audio\make_audio.py') --check 2>&1
    if ($LASTEXITCODE -ne 0) {
        $audioOut | ForEach-Object { Write-Host "    $_" -ForegroundColor DarkGray }
        $failed++
        Write-Host '  [FAIL] the audio kit is missing or stale -- run: python tools/audio/make_audio.py' -ForegroundColor Red
    } else { OK ($audioOut | Select-Object -Last 1) }

    $audioDir = Join-Path $repo 'unity\Assets\Resources\Art\Kit\audio'
    if (-not (Test-Path -LiteralPath $audioDir)) {
        $failed++
        Write-Host '  [FAIL] sounds are not published to Resources/Art/Kit/audio -- run: python tools/art/publish_to_unity.py' -ForegroundColor Red
    } else {
        $wavs = @(Get-ChildItem -LiteralPath $audioDir -Filter *.wav)
        $noMeta = @($wavs | Where-Object { -not (Test-Path -LiteralPath ($_.FullName + '.meta')) })
        if ($noMeta.Count -gt 0) {
            $failed++
            Write-Host ('  [FAIL] sound(s) with no Unity meta: ' + (($noMeta | ForEach-Object { $_.Name }) -join ', ')) -ForegroundColor Red
        } else { OK "$($wavs.Count) sounds published with audio import settings" }

        # every cue name in the C# enum resolves to a published file
        $rules = Get-Content -Raw -LiteralPath (Join-Path $repo 'unity\Assets\Scripts\Core\Rules\AudioRules.cs')
        $mapped = [regex]::Matches($rules, 'return "([a-z]+)";') | ForEach-Object { $_.Groups[1].Value } | Select-Object -Unique
        $missingCue = @($mapped | Where-Object { -not (Test-Path -LiteralPath (Join-Path $audioDir ($_ + '.wav'))) })
        if ($missingCue.Count -gt 0) {
            $failed++
            Write-Host ('  [FAIL] cue(s) name a sound that does not exist: ' + ($missingCue -join ', ')) -ForegroundColor Red
        } else { OK ("every cue maps to a published sound (" + $mapped.Count + " sounds referenced)") }
    }

    # -----------------------------------------------------------------------------------------
    # THE INTRO SCRIPT (v102). content/intro/intro_01.json is written by the content team and painted
    # with the game's own 5x7 atlas. A beat that is too long, or that uses a character the atlas has no
    # glyph for, is DROPPED at runtime -- so the player experiences a hole in the story, not an error.
    # Checked here, where the answer is a red line instead of a silent hole. The same script also proves
    # the slot works end to end: a placeholder ships, and the content team replaces the words.
    # -----------------------------------------------------------------------------------------
    S 'intro script is showable (lengths, atlas characters, 25 s budget)'
    $introSrc = Join-Path $repo 'content\intro\intro_01.json'
    if (-not (Test-Path -LiteralPath $introSrc)) {
        W 'no content\intro\intro_01.json -- the game starts at the desk with no intro'
    } else {
        $introOut = & python (Join-Path $repo 'tools\comp\preview_intro.py') --check 2>&1
        if ($LASTEXITCODE -ne 0) {
            $introOut | ForEach-Object { Write-Host "    $_" -ForegroundColor DarkGray }
            $failed++
            Write-Host '  [FAIL] the intro script cannot be shown as written' -ForegroundColor Red
        } else {
            OK ($introOut | Select-Object -Last 1)
        }
        # the Unity-side copy must be the same bytes: the game loads it through Resources.Load
        $introDst = Join-Path $repo 'unity\Assets\Resources\Content\intro\intro_01.json'
        if (-not (Test-Path -LiteralPath $introDst)) {
            $failed++
            Write-Host '  [FAIL] the intro is not published to unity/Assets/Resources/Content/intro -- run: python tools/art/publish_to_unity.py' -ForegroundColor Red
        } elseif ((Get-FileHash -LiteralPath $introSrc).Hash -ne (Get-FileHash -LiteralPath $introDst).Hash) {
            $failed++
            Write-Host '  [FAIL] content\intro and Resources\Content\intro differ -- run: python tools/art/publish_to_unity.py' -ForegroundColor Red
        } else { OK 'intro is published to Resources byte-for-byte' }
    }

    Write-Host ''
    if ($failed -eq 0) {
        Write-Host 'ART PIPELINE OK' -ForegroundColor Green
        exit 0
    }
    Write-Host "$failed ART PIPELINE CHECK(S) FAILED" -ForegroundColor Red
    exit 1
}
finally { Pop-Location }

