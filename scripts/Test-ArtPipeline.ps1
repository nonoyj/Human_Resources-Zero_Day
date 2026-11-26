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

    S 'concept studies stay on-palette'
    foreach ($study in @('study_desk_workdesk.py')) {
        $path = Join-Path $repo "tools\art\$study"
        if (-not (Test-Path $path)) { continue }
        $out = (& python $path 2>&1 | Out-String)
        if ($out -notmatch 'off-palette px: 0') { D "$study is off-palette`n$out" }
        else { OK "$study -- 0 off-palette px" }
    }

    S 'Unity scripts type-check (stubs, no Unity install needed)'
        Push-Location (Join-Path $repo 'tools\UnityStubs')
        try {
            & dotnet build -v q --nologo | Out-Host
            if ($LASTEXITCODE -ne 0) { D 'Game/Editor scripts do not compile' } else { OK 'Game + Editor compile' }
        } finally { Pop-Location }
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
