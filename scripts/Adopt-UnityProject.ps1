<#
.SYNOPSIS
    Copies ProjectSettings/ and Packages/ from a freshly created Unity project into this repo.

.DESCRIPTION
    Why this exists: creating the Unity project is a 4-step manual job (Hub -> New project -> create ->
    Quit -> copy two folders) and every step has a way to go wrong:

      * Unity Hub defaults the Location somewhere that may not exist, and "Select Folder" makes it very
        easy to point the project AT THE REPO, which generates a second Assets/ inside it;
      * copying by hand invites copying Assets/, Library/ or Temp/ as well;
      * the wrong editor version is silently accepted.

    This script takes the path you actually created, proves it is a Unity project, proves it is not
    inside the repo, proves the editor version is the pinned one, and copies exactly the two folders.

.PARAMETER Source
    The folder Unity created (the one containing Assets/ and ProjectSettings/).

.EXAMPLE
    .\scripts\Adopt-UnityProject.ps1 -Source C:\Users\User\Projects\HRZeroDayTemp
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$Source,
    [string]$Repo = (Split-Path -Parent $PSScriptRoot),
    [string]$RequiredVersion = '6000.3.29f1'
)

$ErrorActionPreference = 'Stop'
function S  ($m) { Write-Host "`n==> $m" -ForegroundColor Cyan }
function OK ($m) { Write-Host "  [ok]   $m" -ForegroundColor Green }
function W  ($m) { Write-Host "  [warn] $m" -ForegroundColor Yellow }
function D  ($m) { Write-Host "  [FAIL] $m" -ForegroundColor Red; exit 1 }

if (-not (Test-Path -LiteralPath $Source)) { D "no such folder: $Source" }
$Source = (Resolve-Path -LiteralPath $Source).Path
$Repo   = (Resolve-Path -LiteralPath $Repo).Path

S "checking the source is a Unity project"
$projSettings = Join-Path $Source 'ProjectSettings'
$packages     = Join-Path $Source 'Packages'
if (-not (Test-Path -LiteralPath $projSettings)) { D "$Source has no ProjectSettings folder -- is this the project Unity created?" }
if (-not (Test-Path -LiteralPath $packages))     { D "$Source has no Packages folder -- is this the project Unity created?" }
$versionFile = Join-Path $projSettings 'ProjectVersion.txt'
if (-not (Test-Path -LiteralPath $versionFile))  { D "no ProjectVersion.txt -- Unity never finished creating the project" }
$version = (Get-Content -LiteralPath $versionFile | Where-Object { $_ -match 'm_EditorVersion:' }) -replace 'm_EditorVersion:\s*', ''
$version = $version.Trim()
OK "project found ($Source)"
OK "editor version: $version"

S "checking it is not inside the repo"
if ($Source.TrimEnd('\') -eq $Repo.TrimEnd('\')) { D "the source IS the repo -- create the temp project somewhere else" }
if ($Source.StartsWith($Repo.TrimEnd('\') + '\')) {
    D ("the source is INSIDE the repo (" + $Source + "). That creates a second Assets/ folder. " +
       "Delete it, recreate the project outside " + $Repo + ", then run this again.")
}
OK "outside the repo"

S "checking the editor version"
if ($version -ne $RequiredVersion) {
    D ("the project is on $version but this game is pinned to $RequiredVersion. In Unity Hub install " +
       "$RequiredVersion, then create the project again with that version selected. Do not upgrade the " +
       "repo to match this project -- the pin is deliberate (docs/01 risk register).")
}
OK "matches the pin ($RequiredVersion)"

S "copying ProjectSettings and Packages"
foreach ($name in @('ProjectSettings', 'Packages')) {
    $src = Join-Path $Source $name
    $dst = Join-Path $Repo $name
    if (Test-Path -LiteralPath $dst) { Remove-Item -LiteralPath $dst -Recurse -Force }
    Copy-Item -LiteralPath $src -Destination $dst -Recurse -Force
    OK "copied $name"
}

S "what was NOT copied (correctly)"
foreach ($name in @('Assets', 'Library', 'Temp', 'Logs', 'UserSettings', 'obj')) {
    if (Test-Path -LiteralPath (Join-Path $Source $name)) { Write-Host "  skipped $name" -ForegroundColor DarkGray }
}

S "result"
$check = Join-Path $Repo 'ProjectSettings\ProjectVersion.txt'
OK ((Get-Content -LiteralPath $check | Where-Object { $_ -match 'm_EditorVersion' }))
Write-Host @"

Next:
  1. Unity Hub > Add > Add project from disk > $Repo
  2. if Hub offers to UPGRADE the project, decline
  3. in Unity: HR//0D > Publish Art To Resources, then Import Art Kit, then Build Desk Scene
  4. commit: git add ProjectSettings Packages; git commit -m "feat(unity): project on $RequiredVersion"

"@ -ForegroundColor Cyan
