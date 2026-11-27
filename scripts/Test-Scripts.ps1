<#
.SYNOPSIS
    Parse-checks every .ps1 in the repo (and optionally runs -Verify).
.DESCRIPTION
    Run this BEFORE committing any PowerShell change. It catches the two failure modes
    that cost us real time:
      1. syntax errors (unbalanced quotes/braces)
      2. non-ASCII characters, which Windows PowerShell 5.1 mis-reads as ANSI and turns
         into parse errors (see docs/90-bug-log.md BUG-001)
.EXAMPLE
    .\scripts\Test-Scripts.ps1
    .\scripts\Test-Scripts.ps1 -RunVerify
#>
[CmdletBinding()]
param([switch]$RunVerify, [switch]$SkipWhereAmI)

$ErrorActionPreference = 'Stop'
$failed = 0
$files = Get-ChildItem -Path (Join-Path (Split-Path -Parent $PSScriptRoot) 'scripts') -Filter *.ps1
# The bootstrap at the REPO ROOT is the one that ships to Downloads and the one that bit us twice
# (BUG-032..037). It is not under scripts\, so it was never linted. It is now.
$rootBoot = Join-Path (Split-Path -Parent $PSScriptRoot) 'Apply-Bundle.ps1'
if (Test-Path -LiteralPath $rootBoot) { $files = @($files) + @(Get-Item -LiteralPath $rootBoot) }

foreach ($f in $files) {
    $tokens = $null; $errors = $null
    [System.Management.Automation.Language.Parser]::ParseFile($f.FullName, [ref]$tokens, [ref]$errors) | Out-Null

    $bytes  = [System.IO.File]::ReadAllBytes($f.FullName)
    $hasBom = ($bytes.Length -ge 3 -and $bytes[0] -eq 0xEF -and $bytes[1] -eq 0xBB -and $bytes[2] -eq 0xBF)
    $start  = if ($hasBom) { 3 } else { 0 }
    $nonAscii = @()
    for ($i = $start; $i -lt $bytes.Length; $i++) { if ($bytes[$i] -gt 127) { $nonAscii += $i } }

    if ($errors.Count -gt 0) {
        $failed++
        Write-Host "FAIL (parse)  $($f.Name)" -ForegroundColor Red
        $errors | Select-Object -First 5 | ForEach-Object {
            Write-Host ("    line {0}: {1}" -f $_.Extent.StartLineNumber, $_.Message)
        }
    }
    elseif ($nonAscii.Count -gt 0 -or -not $hasBom) {
        $failed++
        Write-Host "FAIL (encoding) $($f.Name)  bom=$hasBom nonAscii=$($nonAscii.Count)" -ForegroundColor Red
        Write-Host "    Fix: save as UTF-8 WITH BOM and replace every non-ASCII char with plain ASCII."
        Write-Host "    This is what breaks Windows PowerShell 5.1 (see BUG-001)."
    }
    else {
        Write-Host "PASS  $($f.Name)  ($($bytes.Length) bytes, ascii, bom)" -ForegroundColor Green
    }
}

if ($RunVerify) {
    Write-Host "`n==> running Setup-DevEnvironment.ps1 -Verify" -ForegroundColor Cyan
    & (Join-Path $PSScriptRoot 'Setup-DevEnvironment.ps1') -Verify
    $failed += $LASTEXITCODE
}

# Where-Am-I must survive every repository state it is meant to report on. It broke three times in a row
# while this gate was green, because a parse check cannot see runtime behaviour on a state that only
# exists on the player's machine (BUG-060). The functional test builds those states for real.
if (-not $SkipWhereAmI) {
    $wa = Join-Path $PSScriptRoot 'Test-WhereAmI.ps1'
    if (Test-Path -LiteralPath $wa) {
        Write-Host "`n==> running Test-WhereAmI.ps1 (seven repository states)" -ForegroundColor Cyan
        & pwsh -NoProfile -File $wa
        if ($LASTEXITCODE -ne 0) {
            $failed++
            Write-Host 'FAIL  Where-Am-I does not survive every repository state' -ForegroundColor Red
        }
        else { Write-Host 'PASS  Where-Am-I survives every repository state' -ForegroundColor Green }
    }
}

# The zip-root bootstrap is a COPY of scripts\Apply-Bundle.ps1 (Build-Bundle.ps1 writes it, and
# self-refresh compares hashes against it). If the two ever differ, one of them is stale and the
# operator can silently run an old script -- which is how BUG-032..041 kept resurfacing.
$rootCopy = Join-Path (Split-Path -Parent $PSScriptRoot) 'Apply-Bundle.ps1'
$scriptCopy = Join-Path $PSScriptRoot 'Apply-Bundle.ps1'
if ((Test-Path -LiteralPath $rootCopy) -and (Test-Path -LiteralPath $scriptCopy)) {
    if ((Get-FileHash -LiteralPath $rootCopy).Hash -ne (Get-FileHash -LiteralPath $scriptCopy).Hash) {
        $failed++
        Write-Host 'FAIL  Apply-Bundle.ps1 differs between the repo root and scripts\' -ForegroundColor Red
        Write-Host '    Fix: copy scripts\Apply-Bundle.ps1 over the root copy (Build-Bundle does this).'
    }
    else { Write-Host 'PASS  root bootstrap is byte-identical to scripts\Apply-Bundle.ps1' -ForegroundColor Green }
}

# THE INSTALLER UPDATES ITSELF BEFORE IT RUNS (v103, BUG-062 second half + BUG-064).
# The operator keeps ONE launcher in Downloads and no bundle ever overwrites it, so a fix to the installer
# only reached them after the apply that needed it: v102 shipped the sweep for Unity's stray files and the
# player's next apply ran v101's copy, warned "working tree not clean (105 entries)", and refreshed the
# bootstrap for next time. That is a fix that costs two applies. The script now reads the incoming zip's
# copy of itself, replaces itself, and re-runs as a child. These three checks are what stops it rotting:
#   (1) the self-update block exists and runs BEFORE the first work it does,
#   (2) it re-runs with a HASHTABLE splat -- the array form failed binding and silently fell back,
#   (3) the fallback is a warning, never a failure: a self-update must not be why an install dies.
$apply = Get-Content -Raw -LiteralPath $scriptCopy
$selfIdx = $apply.IndexOf('THE SCRIPT UPDATES ITSELF')
$workIdx = $apply.IndexOf('extracting bundle')
if ($selfIdx -lt 0) {
    $failed++
    Write-Host 'FAIL  Apply-Bundle.ps1 has no self-update block -- a stale launcher in Downloads would run old code' -ForegroundColor Red
} elseif ($workIdx -lt 0 -or $selfIdx -gt $workIdx) {
    $failed++
    Write-Host 'FAIL  the self-update block is not the first work Apply-Bundle.ps1 does' -ForegroundColor Red
} else { Write-Host 'PASS  the installer updates itself before it does anything else' -ForegroundColor Green }

if ($apply -notmatch '\$childArgs = \@{' -or $apply -match '\$childArgs = @\(\s*\)') {
    $failed++
    Write-Host 'FAIL  self-update does not splat a hashtable -- the array form fails to bind and falls back' -ForegroundColor Red
} else { Write-Host 'PASS  self-update re-runs the new copy with a hashtable splat' -ForegroundColor Green }

if ($apply -notmatch 'could not self-update \(.*\) -- applying with the version on disk') {
    $failed++
    Write-Host 'FAIL  self-update has no fallback path: a read-only Downloads folder would abort the apply' -ForegroundColor Red
} else { Write-Host 'PASS  self-update failure falls back to applying with the version on disk' -ForegroundColor Green }

if ($failed -gt 0) { Write-Host "`n$failed problem(s) found." -ForegroundColor Red; exit 1 }
Write-Host "`nAll scripts healthy." -ForegroundColor Green
