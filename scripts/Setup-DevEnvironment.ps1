<#
.SYNOPSIS
    HR//0D — developer environment installer / verifier.

.DESCRIPTION
    Installs the tooling listed in docs/tooling-setup-database.md, then verifies it.
    Everything is idempotent: safe to run repeatedly. GUI-only steps (Unity Hub sign-in,
    editor + Android module install) are reported at the end, not attempted here.

.PARAMETER Verify
    Only verify; install nothing.

.PARAMETER Skip
    Comma-separated tool IDs to skip, e.g. -Skip D07,D12

.EXAMPLE
    .\scripts\Setup-DevEnvironment.ps1
    .\scripts\Setup-DevEnvironment.ps1 -Verify
    .\scripts\Setup-DevEnvironment.ps1 -Skip D07

.NOTES
    Requires Windows 10/11 + winget (App Installer). Run from the repo root.
#>

[CmdletBinding()]
param(
    [switch]$Verify,
    [string[]]$Skip = @()
)

$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'   # much faster Invoke-WebRequest

$REQUIRED_UNITY = '6000.3.29f1'
$Downloads = Join-Path $env:USERPROFILE 'Downloads'
$Tools     = Join-Path $env:USERPROFILE 'Tools'
$Android   = Join-Path $env:USERPROFILE 'Android'
$Results   = [System.Collections.Generic.List[object]]::new()

function Write-Step ($m) { Write-Host "`n==> $m" -ForegroundColor Cyan }
function Write-Ok   ($m) { Write-Host "  [ok]   $m" -ForegroundColor Green }
function Write-Warn ($m) { Write-Host "  [warn] $m" -ForegroundColor Yellow }
function Write-Fail ($m) { Write-Host "  [FAIL] $m" -ForegroundColor Red }
function Write-Skip ($m) { Write-Host "  [skip] $m" -ForegroundColor DarkGray }

function Add-Result ($id, $name, $ok, $detail) {
    $Results.Add([pscustomobject]@{ ID = $id; Tool = $name; Status = $(if ($ok) { 'PASS' } else { 'FAIL' }); Detail = $detail })
}

function Test-Cmd ($exe) {
    return [bool](Get-Command $exe -ErrorAction SilentlyContinue)
}

function Test-Winget {
    if (-not (Test-Cmd 'winget')) {
        Write-Warn 'winget not found. Install "App Installer" from the Microsoft Store, or install tools manually from docs/tooling-setup-database.md.'
        return $false
    }
    return $true
}

function Install-Winget ($id, $name, $wingetId, $extraArgs = @()) {
    if ($Skip -contains $id) { Write-Skip "$id $name"; return }
    if ($Verify) { return }
    Write-Step "$id  $name"
    try {
        winget install --id $wingetId --exact --silent --accept-package-agreements --accept-source-agreements @extraArgs
        Write-Ok "winget $wingetId"
    } catch {
        Write-Warn "winget failed ($($_.Exception.Message)). Install manually: see docs/tooling-setup-database.md#B"
    }
}

function Download-File ($id, $name, $url, $outFile, $expectedBytes) {
    if ($Skip -contains $id) { Write-Skip "$id $name"; return }
    if ($Verify) { return }
    Write-Step "$id  $name"
    if (Test-Path $outFile) {
        $size = (Get-Item $outFile).Length
        if (-not $expectedBytes -or $size -eq $expectedBytes) { Write-Ok "already downloaded ($([math]::Round($size/1MB,1)) MB)"; return }
        Write-Warn "partial/incorrect size ($size / $expectedBytes) — re-downloading"
        Remove-Item $outFile -Force
    }
    New-Item -ItemType Directory -Force -Path (Split-Path $outFile) | Out-Null
    Invoke-WebRequest -Uri $url -OutFile $outFile -UseBasicParsing
    $size = (Get-Item $outFile).Length
    if ($expectedBytes -and $size -ne $expectedBytes) { Write-Warn "size $size != expected $expectedBytes" }
    else { Write-Ok "$([math]::Round($size/1MB,1)) MB -> $outFile" }
}

# ---------------------------------------------------------------- INSTALL PHASE
if (-not $Verify) {

    Write-Host @'
  _   _ ____    _  _ ____        _  _
 | | | |  _ \  | || |___ \      | || |
 | |_| | |_) | | || | __) |_____| || |_
 |  _  |  _ <  |__   _/ __/______|__   _|
 |_| |_|_| \_\    |_||_____|        |_|
 H U M A N   R E S O U R C E S  //  Z E R O   D A Y
 environment installer
'@ -ForegroundColor DarkCyan

    $haveWinget = Test-Winget

    # --- D01 Unity Hub (direct CDN: winget's Unity Hub package lags versions) ---
    Download-File 'D01' 'Unity Hub' `
        'https://public-cdn.cloud.unity3d.com/hub/prod/UnityHubSetup-x64.exe' `
        (Join-Path $Downloads 'UnityHubSetup-x64.exe') 193133232
    if (-not $Verify -and -not ($Skip -contains 'D01')) {
        $hubExe = Join-Path $Downloads 'UnityHubSetup-x64.exe'
        if (Test-Path $hubExe) {
            $hubInstalled = Test-Path "${env:ProgramFiles}\Unity Hub\Unity Hub.exe"
            if (-not $hubInstalled) {
                Write-Step 'D01  installing Unity Hub (silent)'
                Start-Process -FilePath $hubExe -ArgumentList '/S' -Wait
                Write-Ok 'Unity Hub installed'
            } else { Write-Ok 'Unity Hub already installed' }
        }
    }

    # --- D05/D06 Git + LFS ---
    if ($haveWinget) {
        Install-Winget 'D05' 'Git for Windows' 'Git.Git'
        if (Test-Cmd 'git') {
            Write-Step 'D06  Git LFS'
            try { git lfs install | Out-Null; Write-Ok 'git lfs installed' } catch { Write-Warn 'git lfs install failed' }
        }
    } else {
        Download-File 'D05' 'Git for Windows' 'https://github.com/git-for-windows/git/releases/latest' `
            (Join-Path $Downloads 'git-installer.exe') $null
        Write-Warn 'Open the GitHub releases page from the link and run the 64-bit installer manually.'
    }

    # --- D07/D08 Visual Studio with Unity workload ---
    if ($haveWinget) {
        Install-Winget 'D07' 'Visual Studio 2022 Community (Unity + .NET workloads)' `
            'Microsoft.VisualStudio.2022.Community' @(
                '--override', '"--add Microsoft.VisualStudio.Workload.ManagedGame --add Microsoft.VisualStudio.Workload.NetDesktop --includeRecommended --quiet --wait --norestart"'
            )
    }

    # --- D09 GitHub CLI ---
    if ($haveWinget) { Install-Winget 'D09' 'GitHub CLI' 'GitHub.cli' }

    # --- D10 Android platform-tools (zip, no installer) ---
    Download-File 'D10' 'Android platform-tools (adb)' `
        'https://dl.google.com/android/repository/platform-tools-latest-windows.zip' `
        (Join-Path $Downloads 'platform-tools-latest-windows.zip') $null
    $ptZip = Join-Path $Downloads 'platform-tools-latest-windows.zip'
    if ((-not $Verify) -and (Test-Path $ptZip) -and -not (Test-Path (Join-Path $Android 'platform-tools'))) {
        Write-Step 'D10  extracting platform-tools'
        New-Item -ItemType Directory -Force -Path $Android | Out-Null
        Expand-Archive -Path $ptZip -DestinationPath $Android -Force
        $adbPath = Join-Path $Android 'platform-tools'
        $userPath = [Environment]::GetEnvironmentVariable('Path', 'User')
        if ($userPath -notlike "*$adbPath*") {
            [Environment]::SetEnvironmentVariable('Path', "$userPath;$adbPath", 'User')
            Write-Ok "added $adbPath to user PATH (restart your shell)"
        }
        Write-Ok 'adb ready'
    }

    # --- D11 scrcpy ---
    if ($haveWinget) { Install-Winget 'D11' 'scrcpy' 'Genymobile.scrcpy' }

    # --- D12/D13 Python + VS Code ---
    if ($haveWinget) {
        Install-Winget 'D12' 'Python 3.13' 'Python.Python.3.13'
        Install-Winget 'D13' 'VS Code' 'Microsoft.VisualStudioCode'
    }

    # --- Windows long-path + dev-mode niceties ---
    Write-Step 'Windows settings'
    try {
        $lp = Get-ItemProperty 'HKLM:\SYSTEM\CurrentControlSet\Control\FileSystem' -Name LongPathsEnabled -ErrorAction SilentlyContinue
        if (-not $lp -or $lp.LongPathsEnabled -ne 1) {
            Write-Warn 'LongPathsEnabled is off. Run as admin: Set-ItemProperty "HKLM:\SYSTEM\CurrentControlSet\Control\FileSystem" LongPathsEnabled 1  (Unity asset paths break without it)'
        } else { Write-Ok 'long paths enabled' }
    } catch { Write-Warn 'could not read LongPathsEnabled' }
}

# ---------------------------------------------------------------- VERIFY PHASE
Write-Step 'VERIFY'
Write-Host "  machine: $env:COMPUTERNAME  user: $env:USERNAME" -ForegroundColor DarkGray

# Unity Hub
$hubExe = "${env:ProgramFiles}\Unity Hub\Unity Hub.exe"
$hubOk = Test-Path $hubExe
Add-Result 'D01' 'Unity Hub' $hubOk $(if ($hubOk) { $hubExe } else { 'not found: install from docs/tooling-setup-database.md' })

# Unity Editor 6000.3.29f1
$editorRoot = "${env:ProgramFiles}\Unity\Hub\Editor"
$editorPath = Join-Path $editorRoot $REQUIRED_UNITY
$editorExe  = Join-Path $editorPath 'Editor\Unity.exe'
$editorOk = Test-Path $editorExe
$installedVersions = @()
if (Test-Path $editorRoot) { $installedVersions = Get-ChildItem $editorRoot -Directory | Select-Object -ExpandProperty Name }
Add-Result 'D02' "Unity Editor $REQUIRED_UNITY" $editorOk $(
    if ($editorOk) { $editorPath }
    elseif ($installedVersions.Count) { "found: $($installedVersions -join ', ') — MISMATCH, install $REQUIRED_UNITY" }
    else { 'no editor installed' })

# Android modules
$androidPlayer = Join-Path $editorPath 'PlaybackEngines\AndroidPlayer'
$sdkOk = Test-Path (Join-Path $androidPlayer 'SDK')
$ndkOk = Test-Path (Join-Path $androidPlayer 'NDK')
$jdkOk = Test-Path (Join-Path $androidPlayer 'OpenJDK')
Add-Result 'D03' 'Android Build Support' (Test-Path $androidPlayer) $androidPlayer
Add-Result 'D04' 'Android SDK / NDK / OpenJDK' ($sdkOk -and $ndkOk -and $jdkOk) "SDK=$sdkOk NDK=$ndkOk JDK=$jdkOk"

# Git + LFS
$gitOk = Test-Cmd 'git'
$gitVer = if ($gitOk) { (git --version) } else { 'missing' }
Add-Result 'D05' 'Git' $gitOk $gitVer
$lfsVer = ''
if ($gitOk) { try { $lfsVer = (git lfs version) } catch { $lfsVer = 'missing' } }
Add-Result 'D06' 'Git LFS' ([bool]$lfsVer -and $lfsVer -notmatch 'not a git command') $lfsVer

# Visual Studio workloads
$vswhere = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
$vsOk = $false; $vsDetail = 'vswhere not found (no VS installer present)'
if (Test-Path $vswhere) {
    $vsPath = & $vswhere -latest -products * -requires Microsoft.VisualStudio.Workload.ManagedGame -property installationPath 2>$null
    $vsOk = [bool]$vsPath
    $vsDetail = if ($vsOk) { "$vsPath (Unity workload present)" } else { 'VS installed but the "Game development with Unity" workload is missing' }
}
Add-Result 'D07' 'Visual Studio + Unity workload' $vsOk $vsDetail

# GitHub CLI
$ghOk = Test-Cmd 'gh'
$ghDetail = 'missing'
if ($ghOk) {
    $ghDetail = (gh --version | Select-Object -First 1)
    try { $null = gh auth status 2>&1; if ($LASTEXITCODE -ne 0) { $ghDetail += ' — NOT logged in: run "gh auth login"' } }
    catch { $ghDetail += ' — NOT logged in: run "gh auth login"' }
}
Add-Result 'D09' 'GitHub CLI' $ghOk $ghDetail

# adb
$adbOk = Test-Cmd 'adb'
Add-Result 'D10' 'adb (platform-tools)' $adbOk $(if ($adbOk) { (adb version | Select-Object -First 1) } else { 'not on PATH' })

# Python
$pyOk = (Test-Cmd 'python') -or (Test-Cmd 'py')
Add-Result 'D12' 'Python 3.x' $pyOk $(if ($pyOk) { try { (python --version) } catch { 'py launcher only' } } else { 'optional: needed to run prototype/generator_sim.py' })

# Repo sanity
Write-Step 'repo checks'
$repoRoot = Split-Path $PSScriptRoot -Parent
$isRepo = Test-Path (Join-Path $repoRoot '.git')
Write-Ok "repo root: $repoRoot"
if (-not $isRepo) { Write-Warn 'not a git repo — clone first, then re-run' }
$tracked = @()
if ($isRepo -and $gitOk) {
    Push-Location $repoRoot
    try {
        $tracked = git ls-files
        $bad = $tracked | Where-Object { $_ -match '^(Library|Temp|Logs|obj|UserSettings)/' -or $_ -match '\.(apk|aab)$' }
        if ($bad) { Write-Fail "these must not be tracked: $($bad -join ', ')" } else { Write-Ok 'no build artifacts tracked' }
        $lfsAttrs = Test-Path (Join-Path $repoRoot '.gitattributes')
        if (-not $lfsAttrs) { Write-Warn '.gitattributes missing — art will not be LFS-tracked' } else { Write-Ok '.gitattributes present' }
    } finally { Pop-Location }
}

# ---------------------------------------------------------------- REPORT
Write-Host "`n================ RESULT ================" -ForegroundColor Cyan
$Results | Format-Table -AutoSize ID, Tool, Status, Detail | Out-String -Width 200 | Write-Host

$fails = $Results | Where-Object Status -eq 'FAIL'
if ($fails) {
    Write-Host "`n$($fails.Count) item(s) FAILED:" -ForegroundColor Red
    $fails | ForEach-Object { Write-Host "  - $($_.ID) $($_.Tool): $($_.Detail)" }
    Write-Host "`nFix guide: docs/tooling-setup-database.md section I (troubleshooting)." -ForegroundColor Yellow
} else {
    Write-Host "`nAll checks passed. You are ready to open the Unity project." -ForegroundColor Green
}

Write-Host @'

MANUAL STEPS (the script cannot do these):
  1. Open Unity Hub -> sign in with the team's Unity account.
  2. Installs -> Install Editor -> Archive -> 6000.3.29f1 -> tick ONLY:
       Android Build Support (+ Android SDK & NDK Tools, + OpenJDK)
       Windows Build Support (IL2CPP)
  3. Preferences -> Licenses -> activate Unity Personal.
  4. Re-run:  .\scripts\Setup-DevEnvironment.ps1 -Verify   (expect all PASS)
'@ -ForegroundColor DarkCyan

if ($fails) { exit 1 } else { exit 0 }
