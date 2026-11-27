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

    Write-Host ''
    if ($failed -eq 0) {
        Write-Host 'ART PIPELINE OK' -ForegroundColor Green
        exit 0
    }
    Write-Host "$failed ART PIPELINE CHECK(S) FAILED" -ForegroundColor Red
    exit 1
}
finally { Pop-Location }

