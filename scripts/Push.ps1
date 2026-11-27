<#
.SYNOPSIS
    Pushes the HR//0D branch to GitHub and sets its upstream. One command, no guessing.

.DESCRIPTION
    Why this exists. On 2026-11-25 the operator ran `git pull` and got:

        There is no tracking information for the current branch.
        fatal: The current branch main has no upstream branch.

    Nothing was broken. `Apply-Bundle.ps1` rebuilds .git from history.bundle by initialising a FRESH
    repository and moving refs into it -- deliberately, because cloning/copying a .git folder is what
    silently corrupted the tree twice (BUG-021). A side effect is that the rebuilt repo has no
    upstream configured, so `git pull` and `git push` cannot infer a branch and stop with that message.

    The fix is one line of config per working copy, which is what this script does -- and it explains
    the rest of the ritual in the order that actually works:

      1. PUSH FIRST. Your copy already contains the merge of origin/main, so the push is a plain
         fast-forward. If you update from a bundle first, your local main is *replaced* by the
         bundle's history and the push would need a force -- which is the thing we are avoiding now.
      2. `git pull` is only needed if someone else pushed after you.
    -Clean never touches git, so nothing here needs a force and nothing here rewrites history.

.PARAMETER Remote
    Remote name. Default: origin

.PARAMETER Branch
    Branch to publish. Default: main

.PARAMETER Message
    Optional commit message: commits everything (git add -A) before pushing.

.EXAMPLE
    .\scripts\Push.ps1

.EXAMPLE
    .\scripts\Push.ps1 -Message "fix: letter ink"
#>
[CmdletBinding()]
param(
    [string]$Remote = 'origin',
    [string]$Branch = 'main',
    [string]$Message
)

$ErrorActionPreference = 'Stop'
function S  ($m) { Write-Host "`n==> $m" -ForegroundColor Cyan }
function OK ($m) { Write-Host "  [ok]   $m" -ForegroundColor Green }
function W  ($m) { Write-Host "  [warn] $m" -ForegroundColor Yellow }
function D  ($m) { Write-Host "  [FAIL] $m" -ForegroundColor Red; exit 1 }

# native git through a process, never '&' -- PS 5.1 turns native stderr into a terminating error
# under $ErrorActionPreference='Stop', which is how a harmless git message once killed an apply.
$script:Code = 0
function Git {
    param([Parameter(Mandatory = $true)][string[]]$GitArgs, [switch]$Quiet)
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
    $script:Code = $p.ExitCode
    if (-not $Quiet) {
        if ($out.Trim()) { Write-Host $out.TrimEnd() }
        if ($err.Trim()) { Write-Host $err.TrimEnd() -ForegroundColor DarkGray }   # diagnostics, never fatal
    }
    return $out
}

$repo = Split-Path -Parent $PSScriptRoot
if (-not (Test-Path (Join-Path $repo '.git'))) { D "no .git at $repo -- run Apply-Bundle.ps1 first" }

if ($Message) {
    S "committing"
    Git @('-C', $repo, 'add', '-A') | Out-Null
    Git @('-C', $repo, 'commit', '-m', $Message) | Out-Null
    if ($script:Code -ne 0) { W "nothing to commit (or commit failed) -- continuing" }
}

S "remote"
$url = (Git @('-C', $repo, 'remote', 'get-url', $Remote) -Quiet).Trim()
if ($script:Code -ne 0 -or -not $url) {
    W "no remote '$Remote' -- adding origin"
    Git @('-C', $repo, 'remote', 'add', 'origin', 'https://github.com/nonoyj/Human_Resources-Zero_Day.git') | Out-Null
    $url = (Git @('-C', $repo, 'remote', 'get-url', $Remote) -Quiet).Trim()
}
OK "$Remote = $url"

S "fetch"
Git @('-C', $repo, 'fetch', '--no-tags', $Remote) | Out-Null
if ($script:Code -ne 0) { D "fetch failed -- check that git can authenticate to GitHub (git credential manager / PAT)" }

$head    = (Git @('-C', $repo, 'rev-parse', '--short', 'HEAD') -Quiet).Trim()
$upstream = "refs/remotes/$Remote/$Branch"
$hasUp   = $true
Git @('-C', $repo, 'rev-parse', '--verify', '--quiet', $upstream) -Quiet | Out-Null
if ($script:Code -ne 0) { $hasUp = $false }

if ($hasUp) {
    Git @('-C', $repo, 'merge-base', '--is-ancestor', $upstream, 'HEAD') -Quiet | Out-Null
    if ($script:Code -eq 0) {
        OK "$Remote/$Branch is already contained in HEAD ($head) -- push is a fast-forward"
    } else {
        Git @('-C', $repo, 'merge-base', '--is-ancestor', 'HEAD', $upstream) -Quiet | Out-Null
        if ($script:Code -eq 0) {
            W "HEAD is BEHIND $Remote/$Branch."
            Write-Host @"
    Someone pushed after you. Do this, in this order:
        git -C `"$repo`" pull --no-rebase $Remote $Branch
        .\scripts\Push.ps1
    Do NOT apply a bundle in between: -Clean replaces main with the bundle's history and a push would
    then need --force. Push first, update second -- always.
"@ -ForegroundColor Yellow
            exit 1
        }
        W "HEAD and $Remote/$Branch have diverged (someone else pushed)."
        Write-Host "    Reconcile by merging, never by force:" -ForegroundColor Yellow
        Write-Host "        git -C `"$repo`" pull --no-rebase $Remote $Branch" -ForegroundColor Yellow
        Write-Host "    Resolve conflicts, run the gates, then re-run this script." -ForegroundColor Yellow
        exit 1
    }
} else {
    W "$Remote/$Branch not seen locally yet -- the push will create it"
}

S "upstream"
Git @('-C', $repo, 'branch', '--set-upstream-to', "$Remote/$Branch", $Branch) -Quiet | Out-Null
if ($script:Code -ne 0) { W "could not set upstream now -- the push below sets it" }

S "push"
Git @('-C', $repo, 'push', '--set-upstream', $Remote, $Branch)
if ($script:Code -ne 0) {
    D @"
push failed. The two causes, in order of likelihood:
  * not signed in -- run:  git credential-manager github login     (or set a PAT as the password)
  * the remote moved -- pull first (see above), then re-run this script.
This script never forces. If it ever says divergence, that is a merge, not an overwrite.
"@
}
OK "pushed $Branch at $head"
Write-Host "`n  Teammates get it with:  git pull --no-rebase`n" -ForegroundColor Cyan
