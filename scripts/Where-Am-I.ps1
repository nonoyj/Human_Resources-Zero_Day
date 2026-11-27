<#
.SYNOPSIS
    Where are we, what changed, what is the next command. One file, no reading required.
.DESCRIPTION
    The player asked for a single page that survives a memory reset: "always refer to our handouts
    and files where we have stated every command". This script IS that page. It does not change
    anything -- it reads the repo and prints a report.

    It prints, in this order:
      1. branch + HEAD + whether the working tree is dirty (and how many files)
      2. the last 3 commits
      3. any release bundle at the repo root, with its sha256 (first 16 chars)
      4. which gate commands to run, and the exact string each one prints when it passes
      5. ONE recommended next command, chosen from the state above
      6. the five commands of the project, verbatim (docs/80-COMMAND-BOOK.md)

    On a bundle-installed copy there is no upstream branch yet; that is expected and the
    recommended command says so.
.EXAMPLE
    .\scripts\Where-Am-I.ps1
#>
[CmdletBinding()]
param(
    [string]$RepoRoot = (Split-Path -Parent $PSScriptRoot)
)

$ErrorActionPreference = 'Stop'
function Line { param([string]$t = '') ; Write-Host $t }
function Head { param([string]$t) ; Write-Host ''; Write-Host ("== " + $t) -ForegroundColor Cyan }
function Ok   { param([string]$t) ; Write-Host ("  ok   " + $t) -ForegroundColor Green }
function Warn { param([string]$t) ; Write-Host ("  note " + $t) -ForegroundColor Yellow }
function Bad  { param([string]$t) ; Write-Host ("  ATT  " + $t) -ForegroundColor Red }

# THE ONE WAY THIS SCRIPT ASKS GIT ANYTHING (2026-11-27, BUG-060).
# With $ErrorActionPreference = 'Stop', PowerShell treats a native command's non-zero exit as a
# TERMINATING error -- so `git rev-parse '@{u}'` on a branch with no upstream, or `git config --get` for a
# key that does not exist, killed the report mid-sentence in the player's own terminal ("fatal: no
# upstream configured for branch 'main'" then nothing). A "does this branch have an upstream" question is
# not a failure; it is a question whose answer may be "no".
#
# Same technique Push.ps1 uses, for the same reason: git through a process, never '&'. stdout is the
# answer, stderr is a diagnostic we keep one line of, and nothing ever throws.
$script:GitCode = 0
$script:GitWhy = ''
function GitText {
    param([string[]]$GitArgs)
    $psi = New-Object System.Diagnostics.ProcessStartInfo
    $psi.FileName = 'git'
    $psi.Arguments = (($GitArgs | ForEach-Object { if ($_ -match '\s') { '"' + $_ + '"' } else { $_ } }) -join ' ')
    $psi.UseShellExecute = $false
    $psi.CreateNoWindow = $true
    $psi.RedirectStandardOutput = $true
    $psi.RedirectStandardError = $true
    $p = [System.Diagnostics.Process]::Start($psi)
    $out = $p.StandardOutput.ReadToEnd()
    $err = $p.StandardError.ReadToEnd()
    $p.WaitForExit()
    $script:GitCode = $p.ExitCode
    if ($p.ExitCode -ne 0) {
        $script:GitWhy = (($err -split "`n" | Where-Object { $_.Trim() } | Select-Object -First 1) -replace '^fatal:\s*', '').Trim()
        return ''
    }
    return $out.Trim()
}

Push-Location $RepoRoot
try {
    Write-Host ""
    Write-Host "HR//0D -- where am I   (Where-Am-I v2, 2026-11-27)" -ForegroundColor White
    Write-Host ("repo: " + $RepoRoot)

    # -- 1. state ----------------------------------------------------------------------------
    Head "state"
    $head = GitText @('rev-parse', '--short', 'HEAD')
    $branch = GitText @('rev-parse', '--abbrev-ref', 'HEAD')
    $dirty = @(GitText @('status', '--porcelain') -split "`n")
    # Two files are expected to differ in every clone on purpose: the repo-root Apply-Bundle.ps1 is
    # byte-identical to scripts\Apply-Bundle.ps1 (Apply-Bundle rewrites it) and .gitignore can be
    # extended locally. Counting them as "dirty" is a warning nobody can act on, so it is not counted --
    # but anything ELSE still is, because a dirty tree blocks Build-Bundle. (2026-11-27, BUG-058.)
    $expected = @('Apply-Bundle.ps1')
    $real = @($dirty | Where-Object {
        $line = $_.Trim()
        if ($line -eq '') { return $false }
        $path = ($line -replace '^[A-Z?!]{1,2}\s+', '')
        return -not ($expected -contains $path)
    })
    $count = $real.Count
    Write-Host ("  branch " + $branch + "   HEAD " + $head)
    if ($count -gt 0) { Warn ("working tree DIRTY -- " + $count + " file(s). Build-Bundle refuses a dirty tree.") }
    else { Ok "working tree clean" }

    # Ask the remote what it has. The player asked to be told when the team pushes something new, and
    # the agent's sandbox cannot reach GitHub -- so the check has to happen on this machine, every time
    # this script runs. A failure here is normal offline and is reported, not fatal.
    Write-Host ''
    Write-Host '  fetching origin (10s) ...' -NoNewline
    # Does this copy even have a remote? A bundle install does (Apply-Bundle preserves it), but a copy
    # cloned by hand might not, and `git fetch origin` on a repo with no origin writes three lines of
    # git fatal to stderr. Ask first, fetch second, and report ONE short line either way.
    $remoteUrl = GitText @('config', '--get', 'remote.origin.url')
    if (-not $remoteUrl) {
        Warn 'no origin remote on this copy -- nothing to fetch or push; add it with: git remote add origin <url>'
    } else {
        GitText @('fetch', '--quiet', '--prune', 'origin') | Out-Null
        if ($script:GitCode -ne 0) {
            $why = if ($script:GitWhy) { $script:GitWhy } else { 'git fetch failed' }
            Warn ('could not fetch from origin -- offline, or no credential: ' + $why)
        } else { Ok 'fetched' }
    }

    # A branch with no upstream is the NORMAL state of a copy installed from a bundle (Push.ps1 sets it
    # on the first push). Asking git about it writes "fatal: no upstream configured" to STDERR, and with
    # $ErrorActionPreference = 'Stop' that native stderr becomes a terminating error that kills the report
    # halfway through -- the player's own terminal (2026-11-27, BUG-060). So: ask `git config` first, and
    # only invoke `rev-parse` when there is something to resolve. Same answer, no error path.
    $remoteName = GitText @('config', '--get', ('branch.' + $branch + '.remote'))
    $mergeRef   = GitText @('config', '--get', ('branch.' + $branch + '.merge'))
    $upstream = ''
    if ($remoteName -and $mergeRef) { $upstream = GitText @('rev-parse', '--abbrev-ref', '--symbolic-full-name', '@{u}') }
    if (-not $upstream) {
        Warn "no upstream branch on this copy -- .\scripts\Push.ps1 sets it on the first run"
    } else {
        $ahead = GitText @('rev-list', '--count', '@{u}..HEAD')
        $behind = GitText @('rev-list', '--count', 'HEAD..@{u}')
        if (-not $ahead) { $ahead = '0' }
        if (-not $behind) { $behind = '0' }
        Write-Host ("  upstream " + $upstream + "   ahead " + $ahead + "   behind " + $behind)
        if ([int]$behind -gt 0) {
            Warn "the remote has commits this copy does not:"
            & git --no-pager log ('HEAD..@{u}') --oneline -5 | ForEach-Object { Write-Host ("    " + $_) }
            Write-Host "  take them with:  git pull --no-rebase" -ForegroundColor White
        }
    }

    # -- 2. last commits ---------------------------------------------------------------------
    Head "last commits"
    & git --no-pager log -3 --oneline --decorate | ForEach-Object { Write-Host ("  " + $_) }

    # -- 3. bundles --------------------------------------------------------------------------
    Head "bundles at the repo root"
    $zips = @(Get-ChildItem -LiteralPath $RepoRoot -Filter 'HR-ZeroDay-bundle-v*.zip' -File -ErrorAction SilentlyContinue |
              Sort-Object LastWriteTime -Descending)
    if ($zips.Count -eq 0) { Warn "no bundle present (normal once you are pushing with git pull)" }
    foreach ($z in $zips) {
        $h = (Get-FileHash -LiteralPath $z.FullName -Algorithm SHA256).Hash.Substring(0,16).ToLower()
        $mb = [math]::Round($z.Length / 1MB, 1)
        Write-Host ("  " + $z.Name + "   " + $mb + " MB   sha256 " + $h)
    }

    # -- 4. gates ----------------------------------------------------------------------------
    Head "gates (run all five before any bundle; the pass string is what you grep for)"
    $gates = @(
        @{ c = 'dotnet run --project tools/CoreTests';                 p = 'ALL TESTS PASSED' },
        @{ c = 'dotnet build tools/UnityStubs/CompileCheck.csproj';    p = 'Build succeeded.' },
        @{ c = '.\scripts\Test-ArtPipeline.ps1';                       p = 'ART PIPELINE OK' },
        @{ c = '.\scripts\Test-Scripts.ps1';                           p = 'All scripts healthy.' },
        @{ c = 'python tools/verify_fresh_clone.py';                   p = 'verifies itself' }
    )
    foreach ($g in $gates) { Write-Host ("  " + $g.c.PadRight(56) + "-> " + $g.p) }

    # -- 5. the next command -----------------------------------------------------------------
    Head "next command"
    if ($zips.Count -gt 0 -and $count -eq 0) {
        $z = $zips[0]
        Write-Host ("  You have a clean tree and " + $z.Name + ".")
        Write-Host "  Install it, then open the game:" -ForegroundColor White
        Line '    cd $env:USERPROFILE\Downloads'
        Line ('    .\Apply-Bundle.ps1 -BundlePath .\' + $z.Name)
        Line '    cd $env:USERPROFILE\HR-ZeroDay'
        Line '    .\scripts\Push.ps1        # sets upstream the first time'
        Write-Host "  Unity does NOT need to be closed. Stop, then Play." -ForegroundColor Yellow
    }
    elseif ($count -gt 0) {
        Write-Host "  Your tree is dirty. Verify, commit, bundle, then install:" -ForegroundColor White
        Line '    .\scripts\Push.ps1 -Message "wip: <what changed>"   # or commit by hand'
        Line '    pwsh -File scripts\Build-Bundle.ps1 -Version <n>'
    }
    else {
        Write-Host "  Everything is committed and no bundle is pending:" -ForegroundColor White
        Line '    git pull'
    }

    # -- 6. the five commands ----------------------------------------------------------------
    Head "the five commands, verbatim (source: docs/80-COMMAND-BOOK.md)"
    Line '  install a bundle   cd $env:USERPROFILE\Downloads ; .\Apply-Bundle.ps1 -BundlePath .\HR-ZeroDay-bundle-vNN.zip'
    Line '  push               .\scripts\Push.ps1                # add -Message "..." to commit first'
    Line '  open the game      .\scripts\Open-Game.ps1'
    Line '  build a bundle     pwsh -File scripts\Build-Bundle.ps1 -Version NN'
    Line '  all gates          .\scripts\Test-ArtPipeline.ps1 ; .\scripts\Test-Scripts.ps1 ; dotnet run --project tools\CoreTests'
    Line '  make it global     .\scripts\Install-Helpers.ps1        # then just type:  Where-Am-I'
    Line ''
}
finally { Pop-Location }
