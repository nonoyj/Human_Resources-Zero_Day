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

.PARAMETER Clean
    Also remove nesting damage (art\art, docs\docs, ...).

.PARAMETER Reinstall
    The recovery path when the file copy worked but the history did not: `git reset --hard` + `git clean -fd`
    into the new working copy, without re-extracting. Safe to run as often as you like.

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
    [switch]$NoGit
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

if ($Reinstall) {
    # Recovery, no extraction: make the files match whatever HEAD says. This is the answer to
    # "the apply copied files but the tree is dirty" -- and it is idempotent.
    S "reinstalling from HEAD"
    if (-not (Test-Path -LiteralPath (Join-Path $WorkingCopy '.git'))) { D "no .git in $WorkingCopy -- run a normal apply first" }
    & git -C $WorkingCopy reset --hard 2>&1 | Out-Host
    if ($LASTEXITCODE -ne 0) { D "git reset --hard failed" }
    & git -C $WorkingCopy clean -fd 2>&1 | Out-Host
    OK "files now match HEAD ((& git -C $WorkingCopy rev-parse --short HEAD))"
    exit 0
}

try {
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

    if ($Clean -and -not $NoGit) {
        S "adopting the bundle's git history"
        # Two shipping formats, both supported:
        #   history.bundle  a git bundle at the ZIP ROOT (Build-Bundle v45+) or inside HR-ZeroDay\
        #   .git            a whole folder, older bundles
        # BUG-032: this used to look for `$root\.git` only -- but $root is the extracted project and
        # the bundle never puts .git inside it, so every -Clean apply printed "bundle has no .git" and
        # left the old history in place, which is exactly how a correct file copy ends up "dirty".
        $histSrc = $null; $histKind = $null
        $histCandidates = @(
            @{ p = (Join-Path $temp 'history.bundle');        kind = 'bundle' },
            @{ p = (Join-Path $root.FullName 'history.bundle'); kind = 'bundle' },
            @{ p = (Join-Path $root.FullName '.git');           kind = 'git'    }
        )
        foreach ($c in $histCandidates) {
            if (Test-Path -LiteralPath $c.p) { $histSrc = $c.p; $histKind = $c.kind; break }
        }
        $bBranch = $null
        foreach ($bf in @((Join-Path $temp 'history.branch'), (Join-Path $root.FullName 'history.branch'))) {
            if (Test-Path -LiteralPath $bf) { $bBranch = (Get-Content -LiteralPath $bf -Raw).Trim(); break }
        }
        if (-not $bBranch) { $bBranch = 'main' }

        if (-not $histSrc) {
            W "bundle has neither history.bundle nor .git -- history left untouched"
        } else {
            $dstGit = Join-Path $WorkingCopy '.git'
            $savedRemote = $null
            if (Test-Path -LiteralPath $dstGit) {
                $savedRemote = (& git -C $WorkingCopy remote get-url origin 2>$null | Out-String).Trim()
                if (-not $savedRemote) { $savedRemote = $null }
                Remove-Item -LiteralPath $dstGit -Recurse -Force
                OK "old .git removed"
            }
            if ($histKind -eq 'git') {
                Copy-Item -LiteralPath $histSrc -Destination $dstGit -Recurse -Force
                OK "history copied from the bundle's .git"
            } else {
                & git -C $WorkingCopy init -q 2>&1 | Out-Host
                & git -C $WorkingCopy fetch -q $histSrc "refs/heads/*:refs/heads/*" 2>&1 | Out-Host
                if ($LASTEXITCODE -ne 0) { D "fetch from history.bundle failed: $histSrc" }
                OK "history fetched from history.bundle"
            }
            & git -C $WorkingCopy symbolic-ref HEAD "refs/heads/$bBranch" 2>&1 | Out-Host
            & git -C $WorkingCopy reset -q --mixed "refs/heads/$bBranch" 2>&1 | Out-Host
            # If the bundle's branch does not exist the reset above failed silently and HEAD would be
            # dangling, which reads as "0 commits" and as a dirty tree. Say so, do not limp on.
            $headSha = (& git -C $WorkingCopy rev-parse --verify --quiet "refs/heads/$bBranch") 2>$null
            if (-not $headSha) { D "branch '$bBranch' is not in the bundle (history.branch says otherwise)" }
            OK "history adopted: $bBranch at $((& git -C $WorkingCopy rev-parse --short $bBranch))"
            if ($savedRemote) {
                & git -C $WorkingCopy remote add origin $savedRemote 2>&1 | Out-Host
                OK "origin preserved: $savedRemote"
            } else {
                W "no origin in the old .git -- set it with: git remote add origin <url>"
            }
            & git -C $WorkingCopy config core.autocrlf false 2>&1 | Out-Host
        }
    }

    S "copying project files"
    $copied = 0
    Get-ChildItem -LiteralPath $root.FullName -Force | ForEach-Object {
        if ($_.Name -eq '.git') { return }
        if ($_.Name -in @('history.bundle', 'history.branch')) { return }   # transport, not project
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
            if ($st) { W "working tree not clean:"; $st | ForEach-Object { Write-Host "    $_" } }
            else { OK "working tree clean" }
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
