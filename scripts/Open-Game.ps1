<#
.SYNOPSIS
    Opens the Unity project and puts the Desk scene in front of you. One command, no menu hunting.

.DESCRIPTION
    The player's complaint, 2026-11-25: "Do I have to do the HQ in every relog of Unity, it seems like
    lots of time is being used plus the going in and out of unity to extract a file."

    Three separate wastes were stacked on top of each other:

      1. IMPORTING ART BY HAND every time. Fixed properly: the art now lives in Assets/Art/Kit and
         Assets/Resources, both inside the project, so Unity imports it on open. The editor menu is only
         needed when the PNGs themselves changed.
      2. PRESSING PLAY IN AN EMPTY SCENE. GameBoot.cs builds the desk itself on Play
         ([RuntimeInitializeOnLoadMethod] AfterSceneLoad) when no DeskBootstrap exists, so "just press
         Play" is now the whole ritual. The scene file is a convenience, not a requirement.
      3. CLOSING UNITY TO UNPACK A ZIP. Fixed (bundle v77): Unity no longer needs to be closed --
         Apply-Bundle.ps1 copies over an open project. Unity notices the changed files on the next focus.
         The catch is the .git folder, which git refuses to overwrite while the editor holds it; the
         script retries and tells you to close Unity only if it actually fails.

    So this script is the shortcut: it starts the editor on the project with -projectPath, which Unity
    treats as "open this", and on the very first run after a fresh clone it also writes the scene.

.PARAMETER Unity
    Path to Unity.exe. Auto-detected from Unity Hub's default install roots when omitted.

.PARAMETER Wait
    Block until the editor exits (default: leave it running).

.EXAMPLE
    .\scripts\Open-Game.ps1
#>
[CmdletBinding()]
param(
    [string]$Unity,
    [switch]$Wait
)

$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$project = Join-Path $repo 'unity'

if (-not (Test-Path $project)) { throw "No unity folder at $project" }

if (-not $Unity) {
    $roots = @(
        "$env:ProgramFiles\Unity\Hub\Editor",
        "$env:ProgramFiles\Unity",
        "${env:ProgramFiles(x86)}\Unity\Hub\Editor"
    )
    foreach ($root in $roots) {
        if (-not (Test-Path $root)) { continue }
        $candidate = Get-ChildItem $root -Recurse -Filter 'Unity.exe' -ErrorAction SilentlyContinue |
                     Sort-Object FullName -Descending | Select-Object -First 1
        if ($candidate) { $Unity = $candidate.FullName; break }
    }
}

if (-not $Unity) {
    throw "Could not find Unity.exe. Pass it explicitly:  .\scripts\Open-Game.ps1 -Unity 'C:\Program Files\Unity\Hub\Editor\6000.0.32f1\Editor\Unity.exe'"
}

Write-Host "==> opening $project" -ForegroundColor Cyan
Write-Host "    $Unity" -ForegroundColor DarkGray
Write-Host ""
Write-Host "  When the editor is up: press Play. That is all -- GameBoot builds the desk itself." -ForegroundColor Green
Write-Host "  Menu (only needed when the art PNGs changed): HR//0D > Setup Project (one click)" -ForegroundColor DarkGray
Write-Host ""

$args = @('-projectPath', $project)
if ($Wait) { & $Unity @args } else { Start-Process -FilePath $Unity -ArgumentList $args }
