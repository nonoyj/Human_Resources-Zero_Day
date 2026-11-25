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

Write-Host @'
  HR//0D  workspace setup
'@ -ForegroundColor DarkCyan

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
    try { git pull --ff-only } catch { Write-Host "  [warn] pull failed: $($_.Exception.Message)" -ForegroundColor Yellow }
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
Write-Host "`n================ DONE ================" -ForegroundColor Cyan
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
