<#
.SYNOPSIS
    Applies an HR//0D bundle to a working copy. The only script you run from OUTSIDE the repo.

.DESCRIPTION
    Why this exists: fixes for the project live inside the repo, so an update command that uses the
    repo's own script cannot fix the very script being replaced (chicken-and-egg, BUG-012). This
    bootstrap file has no dependencies and is safe to overwrite.

    It replaces the project file set with the bundle's, removes folder/folder duplicates left by
    older versions, and (unless -NoGit) adopts the bundle's git history so HEAD and files agree.

.PARAMETER BundlePath
    Path to HR-ZeroDay-bundle-vN.zip

.PARAMETER WorkingCopy
    Your existing checkout. Default: C:\users\user\HR-ZeroDay

.PARAMETER Reinstall
    Like -Clean (removes the local uncommitted changes so the files match the bundle commit) but KEEPS
    the existing .git, so the history is not replaced by the one inside the zip. Use this when you are
    running the copy of Apply-Bundle.ps1 that lives inside the working copy: PowerShell reads a script
    before running it, so the script cannot replace the .git folder it is executing from.

.PARAMETER Clean
    Also remove nesting damage (art\art, docs\docs, ...).

.PARAMETER Branch
    Branch to check out after adopting the history. Default: main.

.PARAMETER NoGit
    Do not touch .git (keeps your current history).

.EXAMPLE
    .\Apply-Bundle.ps1 -BundlePath .\HR-ZeroDay-bundle-v12.zip -Clean

.EXAMPLE
    .\Apply-Bundle.ps1 -BundlePath C:\Users\User\Downloads\HR-ZeroDay-bundle-v12.zip
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$BundlePath,
    [string]$WorkingCopy = (Join-Path $(if ($env:USERPROFILE) { $env:USERPROFILE } else { $HOME }) 'HR-ZeroDay'),
    [switch]$Clean,
    [switch]$Reinstall,
    [switch]$NoGit,
    [string]$Branch = 'main'
)

$ErrorActionPreference = 'Stop'
function S  ($m) { Write-Host "`n==> $m" -ForegroundColor Cyan }
function OK ($m) { Write-Host "  [ok]   $m" -ForegroundColor Green }
function W  ($m) { Write-Host "  [warn] $m" -ForegroundColor Yellow }
function D  ($m) { Write-Host "  [FAIL] $m" -ForegroundColor Red; exit 1 }

if (-not (Test-Path -LiteralPath $BundlePath)) { D "bundle not found: $BundlePath" }
$BundlePath = (Resolve-Path -LiteralPath $BundlePath).Path

$projectDirs = @('art','content','docs','handoff','prototype','scripts','tools','unity','backend')
$keepDirs    = @('Library','Temp','Logs','obj','UserSettings','Build','Builds','bin',
                 '.tooling','.cache','.dotnet','.nuget','.local','.vs','.idea')

$tempRoot = if ($env:TEMP) { $env:TEMP } elseif ($env:TMP) { $env:TMP } else { [System.IO.Path]::GetTempPath() }
$temp = Join-Path $tempRoot ("hr0d-boot-" + [Guid]::NewGuid().ToString('N').Substring(0, 8))

try {
    Clear-Host
    S "extracting bundle"
    New-Item -ItemType Directory -Force -Path $temp | Out-Null
    Expand-Archive -LiteralPath $BundlePath -DestinationPath $temp -Force
    $root = Get-ChildItem -LiteralPath $temp -Directory |
            Where-Object { Test-Path (Join-Path $_.FullName 'README.md') } |
            Select-Object -First 1
    if (-not $root) { D "bundle does not contain a project folder (no README.md inside)" }
    OK "project found in bundle: $($root.Name)"

    if (-not (Test-Path -LiteralPath $WorkingCopy)) {
        New-Item -ItemType Directory -Force -Path $WorkingCopy | Out-Null
        OK "created $WorkingCopy"
    }

    if ($Reinstall -and -not $Clean) {
        S "discarding local changes (keeping .git)"
        $resetOut = & git -C $WorkingCopy reset --hard HEAD 2>&1
        $cleanOut = & git -C $WorkingCopy clean -fd 2>&1
        OK "working copy reset to HEAD ($((& git -C $WorkingCopy rev-parse --short HEAD)))"
    }

    if ($Clean) {
        S "cleaning folder/folder duplicates"
        foreach ($d in $projectDirs) {
            $inner = Join-Path (Join-Path $WorkingCopy $d) $d
            if (Test-Path -LiteralPath $inner) {
                Remove-Item -LiteralPath $inner -Recurse -Force
                OK "removed $d\$d"
            }
        }
    }

    # Adopt the bundle's history on EVERY apply, not just -Clean: the files are about to be
    # overwritten wholesale, so keeping an older .git guarantees a dirty tree (BUG-019).
    # The history ships as history.bundle -- a single file git verifies itself. Copying a .git
    # folder recursively is what silently broke on Windows (BUG-021).
    if (-not $NoGit) {
        S "adopting the bundle's git history"
        $srcBundle = Join-Path $root.FullName 'history.bundle'
        if (Test-Path -LiteralPath $srcBundle) {
            $dstGit = Join-Path $WorkingCopy '.git'
            # NB: do NOT run git inside the extracted folder to discover the branch -- the extracted
            # project has no .git, so git walks up to an unrelated parent repository (this bit us in
            # testing: it reported the outer repo's branch). Build-Bundle.ps1 records the branch in
            # history.branch instead.
            $branchFile = Join-Path $root.FullName 'history.branch'
            $bBranch = if (Test-Path -LiteralPath $branchFile) { (Get-Content -LiteralPath $branchFile -Raw).Trim() } else { $Branch }
            if (-not $bBranch) { $bBranch = 'main' }

            # remember origin before anything is replaced
            $savedRemote = $null
            if (Test-Path -LiteralPath $dstGit) {
                $savedRemote = (& git -C $WorkingCopy remote get-url origin 2>$null | Out-String).Trim()
                if (-not $savedRemote) { $savedRemote = $null }
                Remove-Item -LiteralPath $dstGit -Recurse -Force
                OK "old .git removed"
            }

            & git -C $WorkingCopy init -q 2>&1 | Out-Host
            & git -C $WorkingCopy fetch -q $srcBundle "refs/heads/*:refs/heads/*" 2>&1 | Out-Host
            if ($LASTEXITCODE -ne 0) { D "fetch from history.bundle failed" }
            & git -C $WorkingCopy symbolic-ref HEAD "refs/heads/$bBranch" 2>&1 | Out-Host
            & git -C $WorkingCopy reset -q --mixed "refs/heads/$bBranch" 2>&1 | Out-Host
            OK "history adopted: $bBranch at $((& git -C $WorkingCopy rev-parse --short $bBranch))"
            if ($savedRemote) {
                & git -C $WorkingCopy remote add origin $savedRemote 2>&1 | Out-Host
                OK "origin preserved: $savedRemote"
            } else {
                W "no origin in the old .git -- set it with: git remote add origin <url>"
            }
            # hooks/config from the source repo are not needed; keep the bundle's identity out of it
            & git -C $WorkingCopy config core.autocrlf false 2>&1 | Out-Host
        } else {
            W "bundle has no history.bundle (older bundle) -- history left untouched"
        }
    }

    S "copying project files"
    $copied = 0
    Get-ChildItem -LiteralPath $root.FullName -Force | ForEach-Object {
        if ($_.Name -eq '.git') { return }
        # bundle transport files, not project files
        if ($_.Name -in @('history.bundle', 'history.branch')) { return }
        if ($_.PSIsContainer -and ($keepDirs -contains $_.Name)) { W "skipped $($_.Name)\"; return }
        $dest = Join-Path $WorkingCopy $_.Name
        if ($_.PSIsContainer) {
            if (Test-Path -LiteralPath $dest) { Remove-Item -LiteralPath $dest -Recurse -Force }
            Copy-Item -LiteralPath $_.FullName -Destination $dest -Recurse -Force
        } else {
            Copy-Item -LiteralPath $_.FullName -Destination $dest -Force
        }
        $copied++
    }
    OK "$copied top-level items"

    S "result"
    Push-Location $WorkingCopy
    try {
        if ((Test-Path '.git') -and (Get-Command git -ErrorAction SilentlyContinue)) {
            $head  = (& git rev-parse --short HEAD) 2>$null
            $count = (& git rev-list --count HEAD) 2>$null
            OK "HEAD $head ($count commits)"
            $st = @(& git status --porcelain) | Where-Object { $_ -and $_.Trim() }
            if ($st) {
                W "working tree not clean ($($st.Count) entries) -- files do NOT match HEAD"
                $st | Select-Object -First 12 | ForEach-Object { Write-Host "    $_" }
                Write-Host @"

  This means the .git folder could not be updated (you ran the copy of this script that lives
  inside the working copy -- PowerShell cannot replace the folder it is running from).
  Fix it with EITHER:
      git reset --hard $head && git clean -fd          # keep history, make files match HEAD
      .\scripts\Apply-Bundle.ps1 -BundlePath <zip> -Reinstall   # same thing, from the repo copy
  or run this script from OUTSIDE the repo (e.g. from Downloads) with -Clean.
"@ -ForegroundColor Yellow
                Set-Location $PSScriptRoot | Out-Null
                exit 1
            }
            else { OK "working tree clean -- files match HEAD exactly" }
        }
        foreach ($d in $projectDirs) {
            if (Test-Path (Join-Path (Join-Path $WorkingCopy $d) $d)) { W "still nested: $d\$d" }
        }
    } finally { Pop-Location }

    Write-Host @"

Next:
  cd "$WorkingCopy"
  .\scripts\Test-Scripts.ps1
  .\scripts\Setup-DevEnvironment.ps1 -Verify
  dotnet run --project tools\CoreTests

"@ -ForegroundColor Cyan
}
finally {
    if (Test-Path -LiteralPath $temp) { Remove-Item -LiteralPath $temp -Recurse -Force -ErrorAction SilentlyContinue }
}
