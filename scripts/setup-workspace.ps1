<#
.SYNOPSIS
    Sets up C:\users\user for HR//0D: clone/pull the repo, create the folder contract,
    optionally create a Desktop shortcut and print the next steps.

.DESCRIPTION
    Idempotent. Safe to re-run. Does NOT install tools (that is Setup-DevEnvironment.ps1).

.EXAMPLE
    .\scripts\setup-workspace.ps1
    .\scripts\setup-workspace.ps1 -RepoUrl https://github.com/nonoyj/Human_Resources-Zero_Day
#>

[CmdletBinding()]
param(
    [string]$RepoUrl = 'https://github.com/nonoyj/Human_Resources-Zero_Day',
    [string]$Root    = (Join-Path $(if ($env:USERPROFILE) { $env:USERPROFILE } else { $HOME }) 'HR-ZeroDay')
)

$ErrorActionPreference = 'Stop'
$Home0 = if ($env:USERPROFILE) { $env:USERPROFILE } else { $HOME }

function Info ($m) { Write-Host "  $m" }
function Step ($m) { Write-Host "`n==> $m" -ForegroundColor Cyan }

# One-liner for the operator: copy the CURRENT bootstrap from the repo into Downloads.
# The repo copy is updated by every bundle; Downloads only ever holds a copy of it.
function Refresh-Bootstrap {
    $here = (Get-Location).Path
    $found = $null
    while ($here) {
        $cand = Join-Path $here 'scripts\Apply-Bundle.ps1'
        if (Test-Path -LiteralPath $cand) { $found = $cand; break }
        $parent = Split-Path -Parent $here
        if ($parent -eq $here) { break }
        $here = $parent
    }
    if (-not $found) {
        Write-Host "  no scripts\Apply-Bundle.ps1 above $((Get-Location).Path) -- cd into the repo first" -ForegroundColor Red
        return
    }
    $dstDir = Join-Path $Home0 'Downloads'
    if (-not (Test-Path -LiteralPath $dstDir)) { New-Item -ItemType Directory -Force -Path $dstDir | Out-Null }
    $dst = Join-Path $dstDir 'Apply-Bundle.ps1'
    Copy-Item -LiteralPath $found -Destination $dst -Force
    Write-Host "  bootstrap refreshed: $dst ($((Get-Item -LiteralPath $dst).Length) bytes)" -ForegroundColor Green
}

Write-Host @'
  HR//0D  workspace setup
'@ -ForegroundColor DarkCyan

# The one-liner only helps if it survives the session, so it is written into the profile once.
try {
    $prof = $PROFILE.CurrentUserAllHosts
    $profDir = Split-Path -Parent $prof
    if (-not (Test-Path -LiteralPath $profDir)) { New-Item -ItemType Directory -Force -Path $profDir | Out-Null }
    $needle = 'function Refresh-Bootstrap'
    $existing = if (Test-Path -LiteralPath $prof) { Get-Content -LiteralPath $prof -Raw } else { '' }
    if ($existing -notmatch [regex]::Escape($needle)) {
        $repoScripts = $PSScriptRoot
        Add-Content -LiteralPath $prof -Value @"

# HR//0D: copy the current bundle bootstrap from the repo into Downloads.
function Refresh-Bootstrap {
    # Walk UP from wherever you are: the helper is used from Downloads, from the repo, from a scene
    # folder, and assuming one of those was the bug in the first version of this script.
    $here = (Get-Location).Path
    $found = $null
    while ($here) {
        $cand = Join-Path $here 'scripts\Apply-Bundle.ps1'
        if (Test-Path -LiteralPath $cand) { $found = $cand; break }
        $parent = Split-Path -Parent $here
        if ($parent -eq $here) { break }
        $here = $parent
    }
    if (-not $found) {
        Write-Host "  no scripts\Apply-Bundle.ps1 above $((Get-Location).Path) -- cd into the repo first" -ForegroundColor Red
        return
    }
    $dstDir = Join-Path $env:USERPROFILE 'Downloads'
    if (-not (Test-Path -LiteralPath $dstDir)) { New-Item -ItemType Directory -Force -Path $dstDir | Out-Null }
    $dst = Join-Path $dstDir 'Apply-Bundle.ps1'
    Copy-Item -LiteralPath $found -Destination $dst -Force
    Write-Host "  bootstrap refreshed: $dst ($((Get-Item -LiteralPath $dst).Length) bytes)" -ForegroundColor Green
}
"@
        Info "profile updated: $prof"
    } else { Info "profile already has Refresh-Bootstrap" }
} catch {
    Info "could not update the profile ($($_.Exception.Message)) -- use the Copy-Item form instead"
}


# ---------------------------------------------------------------- folders
Step 'folder contract'
$dirs = @(
    $Root,
    (Join-Path $Home0 'Downloads\HR0D-installers'),
    (Join-Path $Home0 'Tools\scrcpy'),
    (Join-Path $Home0 'Android'),
    (Join-Path $Home0 'keys\hrzeroday')
)
foreach ($d in $dirs) {
    if (-not (Test-Path $d)) { New-Item -ItemType Directory -Force -Path $d | Out-Null; Info "created $d" }
    else { Info "exists  $d" }
}

# ---------------------------------------------------------------- git
Step 'git'
if (-not (Get-Command git -ErrorAction SilentlyContinue)) {
    Write-Host '  [FAIL] git not installed. Run .\scripts\Setup-DevEnvironment.ps1 first.' -ForegroundColor Red
    exit 1
}
Info (git --version)

if (-not (Test-Path (Join-Path $Root '.git'))) {
    if ((Test-Path $Root) -and (Get-ChildItem $Root -Force | Select-Object -First 1)) {
        Write-Host "  [warn] $Root is not empty and is not a repo." -ForegroundColor Yellow
        $ans = Read-Host '  Move existing contents to a .bak folder and clone fresh? (y/n)'
        if ($ans -eq 'y') {
            $bak = "$Root.bak.$(Get-Date -f yyyyMMddHHmmss)"
            Rename-Item $Root $bak
            New-Item -ItemType Directory -Force -Path $Root | Out-Null
            Info "previous contents moved to $bak"
        } else { Write-Host '  aborting clone; remove/fix the folder and re-run.' -ForegroundColor Yellow; exit 1 }
    }
    Step "cloning $RepoUrl"
    git clone $RepoUrl $Root
} else {
    Step 'pulling latest'
    Push-Location $Root
    try {
        # A bundle-built history has no upstream, so `git pull` answers "no tracking information".
        # Order matters and I got it wrong once: --set-upstream-to needs origin/main to EXIST, and it
        # only exists after a fetch. Fetch, confirm, then link, then pull -- and say which step failed.
        $remotes = (git remote) 2>$null
        if ($remotes -contains 'origin') {
            git fetch origin 2>&1 | ForEach-Object { Write-Host "  $_" }
            $hasRemoteBranch = (git rev-parse --verify --quiet 'refs/remotes/origin/main') 2>$null
            if (-not $hasRemoteBranch) {
                Write-Host "  [warn] origin/main does not exist on the remote yet -- nothing to pull" -ForegroundColor Yellow
            } else {
                $up = (git rev-parse --abbrev-ref '@{u}' 2>$null)
                if (-not $up) {
                    git branch --set-upstream-to=origin/main main 2>&1 | Out-Null
                    Write-Host "  linked main -> origin/main (one-time)" -ForegroundColor DarkGray
                }
            }
        } else {
            Write-Host "  [warn] no 'origin' remote -- skipping pull" -ForegroundColor Yellow
        }
        git pull --ff-only 2>&1 | ForEach-Object { Write-Host "  $_" }
        if ($LASTEXITCODE -ne 0) { Write-Host "  [warn] pull did not fast-forward -- see the lines above" -ForegroundColor Yellow }
    } catch { Write-Host "  [warn] pull failed: $($_.Exception.Message)" -ForegroundColor Yellow }
    finally { Pop-Location }
}

Push-Location $Root
try {
    Step 'git identity (one-time, per machine)'
    $name  = git config user.name 2>$null
    $email = git config user.email 2>$null
    if (-not $name)  { $name  = Read-Host '  Your name for commits';  git config user.name  "$name"  }
    if (-not $email) { $email = Read-Host '  Your email for commits'; git config user.email "$email" }
    Info "user.name  = $(git config user.name)"
    Info "user.email = $(git config user.email)"

    Step 'git lfs'
    try { git lfs install | Out-Null; Info 'lfs hooks installed' }
    catch { Write-Host '  [warn] git lfs missing -- install with: winget install Git.Git' -ForegroundColor Yellow }

    Step 'repo sanity'
    $tracked = git ls-files
    $bad = $tracked | Where-Object { $_ -match '^(Library|Temp|Logs|obj|UserSettings)/' -or $_ -match '\.(apk|aab)$' }
    if ($bad) { Write-Host "  [FAIL] artifacts are tracked: $($bad -join ', ')" -ForegroundColor Red }
    else { Info 'no build artifacts tracked' }
    Info "tracked files: $($tracked.Count)"

    Step 'recent history'
    git --no-pager log --oneline -8
} finally { Pop-Location }

# ---------------------------------------------------------------- shortcut
Step 'desktop shortcut'
$desktop  = [Environment]::GetFolderPath('Desktop')
$linkPath = Join-Path $desktop 'HR-ZeroDay (repo).lnk'
if (-not (Test-Path $linkPath)) {
    try {
        $sh = New-Object -ComObject WScript.Shell
        $lnk = $sh.CreateShortcut($linkPath)
        $lnk.TargetPath = $Root
        $lnk.IconLocation = '%SystemRoot%\System32\shell32.dll,3'
        $lnk.Save()
        Info "created $linkPath"
    } catch { Write-Host '  [warn] could not create shortcut (harmless)' -ForegroundColor Yellow }
} else { Info 'shortcut already exists' }

# ---------------------------------------------------------------- report
Write-Host "`n. $PROFILE.CurrentUserAllHosts

================ DONE ================" -ForegroundColor Cyan
Write-Host @"
Repo:      $Root
Read next: $Root\README.md
           $Root\docs\00-open-questions.md
           $Root\docs\tooling-setup-database.md
           $Root\handoff\2026-11-25_agent-to-systems-team_master-brief.md

If you still need tools:
  cd $Root
  Set-ExecutionPolicy -Scope Process -ExecutionPolicy Bypass -Force
  .\scripts\Setup-DevEnvironment.ps1

Then in Unity Hub: install editor 6000.3.29f1 + Android Build Support / SDK & NDK / OpenJDK.
"@
