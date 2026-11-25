<#
.SYNOPSIS
    Updates an existing HR//0D working copy from a downloaded bundle -- no delete, no re-install.

.DESCRIPTION
    Extracts the bundle to a temporary folder, verifies it looks like the project, then copies the
    files over your working copy. Because a bundle carries the full .git directory, the copy is a
    byte-for-byte match of the repo, so your history stays consistent (no phantom modifications).

    By default it REFUSES to run if you have uncommitted changes, so you cannot lose work.

    What it never touches (they are generated, not source):
        Library\  Temp\  Logs\  obj\  UserSettings\  Build\  Builds\
        .tooling\ .cache\ .dotnet\ .nuget\ .local\  bin\

.PARAMETER BundlePath
    Path to HR-ZeroDay-bundle-vN.zip. Defaults to the newest one in your Downloads folder.

.PARAMETER WorkingCopy
    Your existing checkout. Default: C:\users\user\HR-ZeroDay

.PARAMETER Target
    Extract straight into a folder instead of updating a working copy (setup mode).
    Example: -Target C:\users\user\HR-ZeroDay

.PARAMETER Force
    Proceed even with uncommitted changes (they will be overwritten).

.EXAMPLE
    .\scripts\Update-FromBundle.ps1
    .\scripts\Update-FromBundle.ps1 -BundlePath C:\Users\User\Downloads\HR-ZeroDay-bundle-v7.zip
    .\scripts\Update-FromBundle.ps1 -BundlePath ... -Force
#>
[CmdletBinding()]
param(
    [string]$BundlePath,
    [string]$WorkingCopy = (Join-Path $(if ($env:USERPROFILE) { $env:USERPROFILE } else { $HOME }) 'HR-ZeroDay'),
    [string]$Target,
    [switch]$Force
)

$ErrorActionPreference = 'Stop'
$home0 = if ($env:USERPROFILE) { $env:USERPROFILE } else { $HOME }

function Step ($m) { Write-Host "`n==> $m" -ForegroundColor Cyan }
function Ok   ($m) { Write-Host "  [ok]   $m" -ForegroundColor Green }
function Warn ($m) { Write-Host "  [warn] $m" -ForegroundColor Yellow }
function Die  ($m) { Write-Host "  [FAIL] $m" -ForegroundColor Red; exit 1 }

# ---------------------------------------------------------------- locate the bundle
if (-not $BundlePath) {
    $downloads = Join-Path $home0 'Downloads'
    $found = @()
    foreach ($dir in @($downloads, $home0, (Join-Path $home0 'Downloads\HR0D-installers'))) {
        if (Test-Path $dir) {
            $found += Get-ChildItem $dir -Filter 'HR-ZeroDay-bundle-v*.zip' -File -ErrorAction SilentlyContinue
        }
    }
    if (-not $found) { Die "No HR-ZeroDay-bundle-v*.zip found. Pass -BundlePath explicitly." }
    $BundlePath = ($found | Sort-Object LastWriteTime | Select-Object -Last 1).FullName
    Ok "using newest bundle: $BundlePath"
}
if (-not (Test-Path $BundlePath)) { Die "bundle not found: $BundlePath" }

# ---------------------------------------------------------------- setup mode
if ($Target) {
    Step "extracting into $Target"
    if ((Test-Path $Target) -and (Get-ChildItem $Target -Force | Select-Object -First 1) -and -not $Force) {
        Die "$Target is not empty. Re-run with -Force to overwrite, or delete it first."
    }
    New-Item -ItemType Directory -Force -Path $Target | Out-Null
    Expand-Archive -Path $BundlePath -DestinationPath $Target -Force
    # a bundle contains a single top folder; flatten it into Target
    $nested = Get-ChildItem $Target -Directory | Where-Object { Test-Path (Join-Path $_.FullName 'README.md') } |
              Select-Object -First 1
    if ($nested) {
        Ok "flattening $($nested.Name) -> $Target"
        Copy-Item (Join-Path $nested.FullName '*') $Target -Recurse -Force
        Remove-Item $nested.FullName -Recurse -Force
    }
    if (-not (Test-Path (Join-Path $Target 'scripts\Setup-DevEnvironment.ps1'))) {
        Die "extracted content does not look like the project (scripts\Setup-DevEnvironment.ps1 missing)"
    }
    Ok "ready: $Target"
    Write-Host "`nNext: cd `"$Target`"; .\scripts\Test-Scripts.ps1"
    exit 0
}

# ---------------------------------------------------------------- update mode
Step "checking working copy"
if (-not (Test-Path (Join-Path $WorkingCopy '.git'))) {
    Die "no git repo at $WorkingCopy. For a first-time setup use -Target <folder>."
}
Ok "working copy: $WorkingCopy"

Push-Location $WorkingCopy
try {
    $dirty = @(& git status --porcelain) 2>$null | Where-Object { $_ -and $_.Trim() }
    if ($dirty) {
        Warn "uncommitted changes detected:"
        $dirty | ForEach-Object { Write-Host "    $_" }

        # Classify: LFS-pointer churn and line-ending churn are harmless noise, not real work.
        $noise = $dirty | Where-Object { $_ -match '^\s*M\s+.*\.(png|psd|wav|mp3|ttf|otf|fbx)$' }
        $real  = $dirty | Where-Object { $noise -notcontains $_ }

        if ($real.Count -eq 0) {
            Warn "only binary/LFS noise above -- your files are not actually different."
            Warn "Safe to proceed; run: git checkout -- .   to clear it."
        } else {
            Write-Host "  These look like real edits:" -ForegroundColor Yellow
            $real | ForEach-Object { Write-Host "    $_" }
        }

        if (-not $Force) {
            Write-Host @"

  Stopping so you do not lose work. Pick one:

    # A) keep your work
    git add -A
    git commit -m "wip: my local changes before update"
    .\scripts\Update-FromBundle.ps1

    # B) throw the local changes away (safe if they are only noise)
    git checkout -- .
    .\scripts\Update-FromBundle.ps1

    # C) overwrite anyway
    .\scripts\Update-FromBundle.ps1 -Force

"@ -ForegroundColor Cyan
            Die "uncommitted changes (see options above)"
        }
        Warn "continuing with -Force; local changes will be overwritten"
    } else { Ok "no uncommitted changes" }

    $before = (git rev-parse --short HEAD)
} finally { Pop-Location }

Step "extracting $([System.IO.Path]::GetFileName($BundlePath)) to a temp folder"
$temp = Join-Path $env:TEMP ("hr0d-" + [Guid]::NewGuid().ToString('N').Substring(0, 8))
New-Item -ItemType Directory -Force -Path $temp | Out-Null
try {
    Expand-Archive -Path $BundlePath -DestinationPath $temp -Force
    $root = Get-ChildItem $temp -Directory | Where-Object { Test-Path (Join-Path $_.FullName 'README.md') } |
            Select-Object -First 1
    if (-not $root) { Die "bundle has no project folder inside" }
    if (-not (Test-Path (Join-Path $root.FullName '.git'))) { Warn "bundle has no .git (history will not match)" } else { Ok "bundle carries git history" }

    Step "copying files over $WorkingCopy"
    $excludeDirs = @('Library','Temp','Logs','obj','UserSettings','Build','Builds','.tooling','.cache','.dotnet','.nuget','.local','bin')
    $copied = 0
    Get-ChildItem $root.FullName -Force | ForEach-Object {
        $name = $_.Name
        if ($_.PSIsContainer -and ($excludeDirs -contains $name)) { Warn "skipping $name\"; return }
        if ($_.PSIsContainer -and $name -eq '.git') {
            Copy-Item $_.FullName (Join-Path $WorkingCopy '.git') -Recurse -Force
        } else {
            Copy-Item $_.FullName (Join-Path $WorkingCopy $name) -Recurse -Force
        }
        $copied++
    }
    Ok "$copied top-level items copied"
}
finally {
    if (Test-Path $temp) { Remove-Item $temp -Recurse -Force -ErrorAction SilentlyContinue }
}

Push-Location $WorkingCopy
try {
    Step "verifying"
    $after = (git rev-parse --short HEAD)
    $status = (& git status --porcelain) 2>$null
    Write-Host "  HEAD before: $before"
    Write-Host "  HEAD after : $after"
    if ($status) {
        Warn "working tree is not clean after the update:"
        Write-Host $status
        Warn "If the bundle and your copy had diverged, run: git status  and inspect before committing."
    } else {
        Ok "working tree clean -- history and files are consistent"
    }
    Write-Host "`n  commits: $((& git rev-list --count HEAD))"
    & git --no-pager log --oneline -3
} finally { Pop-Location }

Write-Host "`nNext steps:" -ForegroundColor Cyan
Write-Host "  cd `"$WorkingCopy`""
Write-Host "  .\scripts\Test-Scripts.ps1"
Write-Host "  .\scripts\Setup-DevEnvironment.ps1 -Verify"
Write-Host "  dotnet run --project tools\CoreTests"
