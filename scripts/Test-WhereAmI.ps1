<#
.SYNOPSIS
    Runs Where-Am-I.ps1 against every repository state it is expected to survive.

.DESCRIPTION
    Why this exists (2026-11-27, docs/90 BUG-060):
      Where-Am-I is the one command that tells the player what to type next, and it broke three times in a
      row -- once on "no upstream configured", once on "does not contain a method named Trim" -- while the
      source looked fine and every other gate was green. The parse gate (Test-Scripts.ps1) cannot catch
      that: both failures were RUNTIME behaviour on a repository state that only exists on the player's
      machine (a bundle install has no upstream; a fresh Unity open dirties the tree).

    So this test builds the states for real, in a scratch folder outside the repo, and runs the actual
    script in each one. It asserts two things: the script exits 0, and it printed its LAST section (the
    five commands) -- because "it started" is not "it finished".

    Six states:
      1. no remote at all              (a repo cloned from nothing / remote removed)
      2. remote, no upstream           (THE bundle install: exactly what Apply-Bundle produces)
      3. upstream, in sync             (after the first Push.ps1)
      4. upstream, ahead of the remote (a local commit nobody has)
      5. upstream, behind the remote   (the team pushed; this copy must be told)
      6. a dirty tree on top of 2      (Unity opened the project and wrote .meta files)
      7. not a git repository at all   (worst case: must still print the command book)

.EXAMPLE
    .\scripts\Test-WhereAmI.ps1
#>
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'

function S  ($m) { Write-Host "`n==> $m" -ForegroundColor Cyan }
function OK ($m) { Write-Host "  [ok]   $m" -ForegroundColor Green }
function D  ($m) { Write-Host "  [FAIL] $m" -ForegroundColor Red; exit 1 }

$repo = Split-Path -Parent $PSScriptRoot
$subject = Join-Path $PSScriptRoot 'Where-Am-I.ps1'
if (-not (Test-Path -LiteralPath $subject)) { D "no Where-Am-I.ps1 next to this test" }

$marker = 'the five commands, verbatim'
$failures = @()
$scratch = Join-Path ([System.IO.Path]::GetTempPath()) ('hr0d-whereami-' + [Guid]::NewGuid().ToString('N').Substring(0, 8))

function New-Repo {
    param([string]$Name)
    $dir = Join-Path $scratch $Name
    New-Item -ItemType Directory -Path $dir -Force | Out-Null
    Push-Location $dir
    $null = & git init -q . 2>&1
    Set-Content -LiteralPath (Join-Path $dir 'README.md') -Value 'scratch' -Encoding ascii
    $null = & git add -A 2>&1
    $null = & git -c user.name=t -c user.email=t@t commit -q -m 'init' 2>&1
    Pop-Location
    return $dir
}

function New-Bare {
    param([string]$Name)
    $dir = Join-Path $scratch $Name
    New-Item -ItemType Directory -Path $dir -Force | Out-Null
    $null = & git init -q --bare $dir 2>&1
    return $dir
}

function Run-Case {
    param([string]$Name, [string]$Dir, [string]$Expect)
    if (-not $Dir -or -not (Test-Path -LiteralPath $Dir)) { D "test bug: no directory for case '$Name'" }
    $out = & pwsh -NoProfile -File $subject -RepoRoot $Dir 2>&1
    $text = ($out | Out-String)
    $exit = $LASTEXITCODE
    $finished = $text.Contains($marker)
    $threw = ($text -match 'could not finish|MethodNotFound|RuntimeException|does not contain a method')
    if ($exit -ne 0 -or -not $finished -or $threw) {
        $script:failures += $Name
        Write-Host "  [FAIL] $Name  exit=$exit finished=$finished threw=$threw" -ForegroundColor Red
        ($text -split "`r?`n" | Select-Object -First 14) | ForEach-Object { Write-Host "         $_" -ForegroundColor DarkGray }
        return
    }
    if ($Expect -and -not $text.Contains($Expect)) {
        $script:failures += $Name
        Write-Host "  [FAIL] $Name  did not say '$Expect'" -ForegroundColor Red
        ($text -split "`r?`n" | Select-Object -First 14) | ForEach-Object { Write-Host "         $_" -ForegroundColor DarkGray }
        return
    }
    OK "$Name -- finished, said '$Expect'"
}

S "building scratch repositories in $scratch"
try {
    # 1. no remote at all
    $c1 = New-Repo 'no-remote'
    Run-Case 'no remote' $c1 'no origin remote'

    # 2. remote but no upstream -- THE bundle-install state
    $bare2 = New-Bare 'origin-2.git'
    $c2 = New-Repo 'no-upstream'
    Push-Location $c2
    $null = & git remote add origin $bare2 2>&1
    $null = & git push -q origin HEAD 2>&1
    Pop-Location
    Run-Case 'remote, no upstream' $c2 'no upstream branch'

    # 3. upstream, in sync
    $bare3 = New-Bare 'origin-3.git'
    $c3 = New-Repo 'in-sync'
    Push-Location $c3
    $null = & git remote add origin $bare3 2>&1
    $null = & git push -q -u origin HEAD 2>&1
    Pop-Location
    Run-Case 'upstream, in sync' $c3 'ahead 0'

    # 4. ahead: one local commit the remote has not seen
    $bare4 = New-Bare 'origin-4.git'
    $c4 = New-Repo 'ahead'
    Push-Location $c4
    $null = & git remote add origin $bare4 2>&1
    $null = & git push -q -u origin HEAD 2>&1
    Set-Content -LiteralPath (Join-Path $c4 'more.txt') -Value 'x' -Encoding ascii
    $null = & git add -A 2>&1
    $null = & git -c user.name=t -c user.email=t@t commit -q -m 'local' 2>&1
    Pop-Location
    Run-Case 'upstream, ahead' $c4 'ahead 1'

    # 5. behind: somebody else pushed after this copy was taken
    $bare5 = New-Bare 'origin-5.git'
    $c5 = New-Repo 'behind'
    Push-Location $c5
    $null = & git remote add origin $bare5 2>&1
    $null = & git push -q -u origin HEAD 2>&1
    Pop-Location
    $other = Join-Path $scratch 'other'
    $null = & git clone -q $bare5 $other 2>&1
    Push-Location $other
    Set-Content -LiteralPath (Join-Path $other 'team.txt') -Value 'y' -Encoding ascii
    $null = & git add -A 2>&1
    $null = & git -c user.name=t -c user.email=t@t commit -q -m 'team' 2>&1
    $null = & git push -q origin HEAD 2>&1
    Pop-Location
    Run-Case 'upstream, behind' $c5 'behind 1'

    # 6. dirty on top of 2 (Unity wrote .meta files into a tracked folder)
    $bare6 = New-Bare 'origin-6.git'
    $c6 = New-Repo 'dirty'
    Push-Location $c6
    $null = & git remote add origin $bare6 2>&1
    Set-Content -LiteralPath (Join-Path $c6 'generated.meta') -Value 'unity' -Encoding ascii
    Pop-Location
    Run-Case 'remote, no upstream, dirty' $c6 'working tree DIRTY'

    # 7. not a git repository at all
    $plain = Join-Path $scratch 'not-a-repo'
    New-Item -ItemType Directory -Path $plain -Force | Out-Null
    Run-Case 'not a git repository' $plain 'could not read the log'
}
finally {
    if (Test-Path -LiteralPath $scratch) { Remove-Item -LiteralPath $scratch -Recurse -Force -ErrorAction SilentlyContinue }
}

if ($failures.Count -gt 0) {
    Write-Host "`n$($failures.Count) case(s) failed: $($failures -join ', ')" -ForegroundColor Red
    exit 1
}
Write-Host "`nWhere-Am-I survives all seven repository states." -ForegroundColor Green
