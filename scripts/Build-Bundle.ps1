<#
.SYNOPSIS
    Builds HR-ZeroDay-bundle-vN.zip from this working copy, safely.

.DESCRIPTION
    Two bugs came from hand-rolled zipping (BUG-014, BUG-015), so packaging is a script now:

      BUG-014  local tool caches (.nuget\, .local\) were swept into the zip. Their folder names
               contain ':' (NuGet's http-cache uses '<hash>$ps:<url>'), which Windows cannot
               write, so Expand-Archive failed with a wall of Remove-Item "path not found"
               errors. Those folders are now never packaged.
      BUG-015  the bootstrap Apply-Bundle.ps1 was only inside HR-ZeroDay\ (or only at the zip
               root), so ".\Apply-Bundle.ps1" failed from Downloads. It is now written at BOTH
               the zip root and inside HR-ZeroDay\.

    Rules this script enforces:
      - never package: .git-less caches, node_modules, Unity Library/Temp/Logs/obj, __pycache__,
                       .local, .nuget, .dotnet, .tooling, *.zip, *.bundle
      - never package a path containing a character Windows forbids ( : * ? " < > | )
      - always package .git (Apply-Bundle adopts the history)
      - always package Apply-Bundle.ps1 at the root AND inside HR-ZeroDay\
      - fail loudly if any of the above is violated, and print sha256 for the handoff notes

.PARAMETER Version
    Bundle number. Produces HR-ZeroDay-bundle-v<Version>.zip next to the repo.

.PARAMETER OutDir
    Where to write the zip. Default: the repo root.

.EXAMPLE
    .\scripts\Build-Bundle.ps1 -Version 17
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][int]$Version,
    [string]$OutDir = (Split-Path -Parent $PSScriptRoot)
)

$ErrorActionPreference = 'Stop'
function S  ($m) { Write-Host "`n==> $m" -ForegroundColor Cyan }
function OK ($m) { Write-Host "  [ok]   $m" -ForegroundColor Green }
function W  ($m) { Write-Host "  [warn] $m" -ForegroundColor Yellow }
function D  ($m) { Write-Host "  [FAIL] $m" -ForegroundColor Red; exit 1 }

$root    = (Resolve-Path -LiteralPath (Split-Path -Parent $PSScriptRoot)).Path
$zip     = Join-Path $OutDir "HR-ZeroDay-bundle-v$Version.zip"
$skipDir = @('__pycache__', '.cache', '.pytest_cache', 'node_modules', '.local', '.nuget', '.dotnet',
             '.tooling', '.vs', '.idea', 'Library', 'Temp', 'Logs', 'obj', 'UserSettings',
             'Build', 'Builds', 'bin', 'hr0d-boot')
$skipExt = @('.zip', '.bundle', '.pyc')
$badChar = [char[]](':','*','?','"','<','>','|')

if (Test-Path -LiteralPath $zip) { Remove-Item -LiteralPath $zip -Force }

S "collecting files from $root"
$files, $skipped = @(), @()
foreach ($f in (Get-ChildItem -LiteralPath $root -Recurse -File -Force)) {
    $rel = $f.FullName.Substring($root.Length).TrimStart('\', '/')
    $parts = $rel -split '[\\/]'
    if ($parts | Where-Object { $skipDir -contains $_ }) { continue }
    if ($parts[0] -eq '.git') { continue }   # history ships as history.bundle, see below
    if ($skipExt -contains $f.Extension.ToLower()) { continue }
    if ($rel.IndexOfAny($badChar) -ge 0) { $skipped += $rel; continue }
    $files += $f
}
OK "$($files.Count) files selected"
if ($skipped.Count) { W "$($skipped.Count) files skipped (unwritable on Windows)" }

S "checking prerequisites"
foreach ($req in @('README.md', 'scripts\Apply-Bundle.ps1')) {
    if (-not (Test-Path -LiteralPath (Join-Path $root $req))) { D "missing $req" }
}
OK "README.md and scripts\Apply-Bundle.ps1 present"

# ---------------------------------------------------------------------------------------------
# History travels as a git bundle, NOT as a copied .git folder.
# BUG-021: Copy-Item -Recurse of a .git folder silently produced an unusable repository on Windows
# (files copied, refs not readable) and the apply ended with HEAD/fists disagreeing. A git bundle is
# one file that git itself verifies ("The bundle records a complete history").
# NOTE: 'git bundle create --all' segfaults in this environment -- always name the ref explicitly.
# ---------------------------------------------------------------------------------------------
S "writing git history bundle"
$bundleTmp = Join-Path ([System.IO.Path]::GetTempPath()) ("hr0d-history-" + [Guid]::NewGuid().ToString('N').Substring(0, 8) + ".bundle")
$gitTop = ($gitOutput = (& git -C $root rev-parse --show-toplevel 2>$null) | Out-String).Trim()
if (-not $gitTop) { D "not a git repo: $root" }
$branchName = ((& git -C $root rev-parse --abbrev-ref HEAD 2>$null) | Out-String).Trim()
& git -C $root bundle create $bundleTmp $branchName | Out-Host
if ($LASTEXITCODE -ne 0) { D "git bundle create failed" }
$bundleCheck = (& git -C $root bundle verify $bundleTmp 2>&1 | Out-String)
if ($bundleCheck -notmatch 'complete history') { D "git bundle verify did not confirm a complete history`n$bundleCheck" }
OK "history bundle: $branchName, $([math]::Round((Get-Item $bundleTmp).Length / 1MB, 2)) MB, verified"

$branchFileTmp = Join-Path ([System.IO.Path]::GetTempPath()) ("hr0d-branch-" + [Guid]::NewGuid().ToString('N').Substring(0, 8) + ".txt")
Set-Content -LiteralPath $branchFileTmp -Value $branchName -NoNewline -Encoding ascii
OK "branch recorded: $branchName"

S "writing $zip"
Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive = [System.IO.Compression.ZipFile]::Open($zip, 'Create')
try {
    foreach ($f in $files) {
        $rel = $f.FullName.Substring($root.Length).TrimStart('\', '/')
        $entry = "HR-ZeroDay/" + ($rel -replace '\\', '/')
        [void][System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile(
            $archive, $f.FullName, $entry, [System.IO.Compression.CompressionLevel]::Optimal)
    }
    [void][System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile(
        $archive, $bundleTmp, 'HR-ZeroDay/history.bundle',
        [System.IO.Compression.CompressionLevel]::Optimal)
    [void][System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile(
        $archive, $branchFileTmp, 'HR-ZeroDay/history.branch',
        [System.IO.Compression.CompressionLevel]::Optimal)
    # the bootstrap must be reachable from the folder the operator stands in
    [void][System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile(
        $archive, (Join-Path $root 'scripts\Apply-Bundle.ps1'), 'Apply-Bundle.ps1',
        [System.IO.Compression.CompressionLevel]::Optimal)
}
finally { $archive.Dispose() }
OK "archive closed"
Remove-Item -LiteralPath $bundleTmp -Force -ErrorAction SilentlyContinue
Remove-Item -LiteralPath $branchFileTmp -Force -ErrorAction SilentlyContinue

S "verifying the archive"
$check = [System.IO.Compression.ZipFile]::OpenRead($zip)
try {
    $names = $check.Entries | ForEach-Object { $_.FullName }
} finally { $check.Dispose() }

if ($names -notcontains 'Apply-Bundle.ps1')                        { D "bootstrap missing at zip root" }
if ($names -notcontains 'HR-ZeroDay/scripts/Apply-Bundle.ps1')     { D "bootstrap missing inside HR-ZeroDay" }
if ($names -notcontains 'HR-ZeroDay/history.bundle')               { D "history.bundle missing from zip" }
if ($names -notcontains 'HR-ZeroDay/history.branch')               { D "history.branch missing from zip" }
if ($names | Where-Object { $_ -like 'HR-ZeroDay/.git/*' })        { D ".git leaked into the zip (must ship as history.bundle)" }
$leak = $names | Where-Object { $_ -match '/(\.local|\.nuget|\.dotnet|\.tooling|node_modules|Library)/' }
if ($leak) { D "tooling cache leaked into the zip: $($leak | Select-Object -First 3)" }
$bad = $names | Where-Object { $_.IndexOfAny($badChar) -ge 0 }
if ($bad) { D "path Windows cannot write: $($bad | Select-Object -First 3)" }

OK "$($names.Count) entries"
OK "bootstrap at root and inside HR-ZeroDay"
OK "git history present, no tooling caches, no unwritable paths"

$size = (Get-Item -LiteralPath $zip).Length / 1MB
$hash = (Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash.Substring(0, 16).ToLower()
Write-Host @"

Bundle ready
  path   $zip
  size   $([math]::Round($size, 2)) MB
  sha256 $hash

Apply it (from Downloads):
  cd `$env:USERPROFILE\Downloads
  Set-ExecutionPolicy -Scope Process -ExecutionPolicy Bypass -Force
  .\Apply-Bundle.ps1 -BundlePath .\HR-ZeroDay-bundle-v$Version.zip -Clean

"@ -ForegroundColor Cyan
