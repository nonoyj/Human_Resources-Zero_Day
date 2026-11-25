<#
.SYNOPSIS
    Connects this working copy to the GitHub repo and pushes it -- safely, in one reviewable step.

.DESCRIPTION
    Why a script and not three pasted commands:

      * Your local history (42+ commits) was created here, so it does NOT share an ancestor with the
        repository GitHub made for you. A plain `git push` will be REJECTED as a non-fast-forward.
      * Blindly forcing is how people destroy a teammate's first commit. This script looks first,
        prints exactly what would be lost, and refuses to replace history unless you pass
        -ReplaceRemoteHistory.
      * It never runs a destructive command on its own. Default mode is: connect + fetch + report.

.AUTHENTICATION
    You do NOT need to create a Personal Access Token by hand. Collaborator access only grants
    permission; git still has to authenticate, and on Windows "Git for Windows" ships the Git
    Credential Manager, which opens a browser window and logs you in once. That token is then cached
    in Windows Credential Manager. If no browser appears, fall back to a PAT (see the printed notes).

.PARAMETER RepoUrl
    HTTPS remote. Default: the project repo.

.PARAMETER Push
    Actually push after the checks pass.

.PARAMETER ReplaceRemoteHistory
    Required together with -Push when the remote already has commits. Rewrites the remote branch to
    match this working copy. Use ONLY when the remote holds nothing but the auto-created README.

.PARAMETER UseSsh
    Use git@github.com:... instead of HTTPS (needs an SSH key uploaded to GitHub).

.EXAMPLE
    .\scripts\Connect-Remote.ps1
        connects, fetches, tells you the story. Changes nothing on GitHub.

.EXAMPLE
    .\scripts\Connect-Remote.ps1 -Push
        pushes when the remote is empty or already behind you.

.EXAMPLE
    .\scripts\Connect-Remote.ps1 -Push -ReplaceRemoteHistory
        first push when GitHub already made an initial commit you do not want to keep.
#>
[CmdletBinding()]
param(
    [string]$RepoUrl = 'https://github.com/nonoyj/Human_Resources-Zero_Day.git',
    [switch]$Push,
    [switch]$ReplaceRemoteHistory,
    [switch]$UseSsh
)

$ErrorActionPreference = 'Stop'
function S  ($m) { Write-Host "`n==> $m" -ForegroundColor Cyan }
function OK ($m) { Write-Host "  [ok]   $m" -ForegroundColor Green }
function W  ($m) { Write-Host "  [warn] $m" -ForegroundColor Yellow }
function D  ($m) { Write-Host "  [FAIL] $m" -ForegroundColor Red; exit 1 }

# PowerShell 5.1 turns the FIRST stderr line of a native command into a terminating
# NativeCommandError when that stream is redirected (2>&1), which killed this script at 'git fetch'.
# Keeping stderr on its own channel is harmless and works identically in PS 5.1 and 7+.
$ErrorActionPreference = 'Continue'

$repo = Split-Path -Parent $PSScriptRoot
Push-Location $repo
try {
    if (-not (Test-Path '.git')) { D "not a git repo: $repo" }
    if (-not (Get-Command git -ErrorAction SilentlyContinue)) { D 'git is not on PATH' }

    if ($UseSsh) { $RepoUrl = 'git@github.com:nonoyj/Human_Resources-Zero_Day.git' }

    $currentBranch = ((& git rev-parse --abbrev-ref HEAD) 2>$null | Out-String).Trim()
    $localCount = (& git rev-list --count HEAD) 2>$null
    S "local"
    OK "branch $currentBranch, $localCount commits, HEAD $((& git rev-parse --short HEAD))"
    $dirty = @(& git status --porcelain) | Where-Object { $_ -and $_.Trim() }
    if ($dirty) { W "$($dirty.Count) uncommitted change(s) -- commit before pushing"; $dirty | Select-Object -First 5 | ForEach-Object { Write-Host "         $_" } }
    else { OK 'working tree clean' }

    S "commit identity"
    $userName  = ((& git config user.name)  2>$null | Out-String).Trim()
    $userEmail = ((& git config user.email) 2>$null | Out-String).Trim()
    if (-not $userName -or -not $userEmail) {
        W 'this machine has no user.name / user.email -- the first commit you make will fail'
        Write-Host '  fix once:' -ForegroundColor Yellow
        Write-Host '    git config --global user.name  "June0904"' -ForegroundColor Yellow
        Write-Host '    git config --global user.email "YOUR-GITHUB-EMAIL"' -ForegroundColor Yellow
        Write-Host '  (the email should match your GitHub account, or commits will not link to you)' -ForegroundColor Yellow
    } else {
        OK "$userName <$userEmail>"
    }

    S "remote"
    $remotes = ((& git remote) 2>$null)
    if ($remotes -contains 'origin') {
        $current = (& git remote get-url origin) 2>$null
        if ($current -ne $RepoUrl) {
            W "origin currently points at $current"
            & git remote set-url origin $RepoUrl
            OK "origin repointed to $RepoUrl"
        } else { OK "origin already $RepoUrl" }
    } else {
        & git remote add origin $RepoUrl
        OK "added origin $RepoUrl"
    }

    S "fetching (no changes yet)"
    $fetchOk = $true
    & git fetch origin --prune | Out-Host
    if ($LASTEXITCODE -ne 0) { $fetchOk = $false }

    if (-not $fetchOk) {
        W 'could not read the remote. Most likely authentication or network.'
        Write-Host @"

Nothing was changed. To authenticate:

  A) The easy path (recommended on Windows)
     Just run:  git push -u origin $currentBranch
     Git Credential Manager opens a browser, you sign in once, and the credential is stored.
     You do NOT need to create a token by hand.

  B) Personal Access Token (when no browser is available)
     GitHub > Settings > Developer settings > Personal access tokens > Fine-grained tokens
       Repository access: only this repo
       Permissions: Contents = Read and write
     Then when git asks for a password, paste the token (not your account password).

  C) SSH instead of HTTPS
     Re-run with -UseSsh after adding a key:  ssh-keygen -t ed25519 -C "you@example.com"
     and pasting ~/.ssh/id_ed25519.pub into GitHub > Settings > SSH and GPG keys.

"@ -ForegroundColor Yellow
        exit 2
    }

    $remoteRef = "origin/$currentBranch"
    $hasRemote = ((& git rev-parse --verify --quiet $remoteRef) 2>$null)
    $mode = 'unknown'
    if (-not $hasRemote) {
        OK "remote branch '$currentBranch' does not exist yet -- this would be the first push"
        $mode = 'newbranch'
    } else {
        $remoteCount = (& git rev-list --count $remoteRef) 2>$null
        $ahead  = (& git rev-list --count "$remoteRef..HEAD") 2>$null
        $behind = (& git rev-list --count "HEAD..$remoteRef") 2>$null
        OK "remote has $remoteCount commits; you are $ahead ahead, $behind behind"

        if ($behind -eq '0') {
            $mode = 'fastforward'
        } else {
            $mode = 'diverged'
            W 'the two histories do not share an ancestor -- a normal push will be rejected'
            Write-Host "`n  commits only on the remote (would be replaced):" -ForegroundColor Yellow
            & git log --oneline "HEAD..$remoteRef" | Select-Object -First 10 | ForEach-Object { Write-Host "    $_" }
        }
    }

    S "what I would do"
    switch ($mode) {
        'newbranch'   { Write-Host '  create the branch on GitHub and push all commits' -ForegroundColor Cyan }
        'fastforward' { Write-Host '  fast-forward push, nothing on the remote is lost' -ForegroundColor Cyan }
        'diverged'    { Write-Host '  REPLACE the remote branch with this history (destructive to those commits)' -ForegroundColor Yellow }
    }

    if (-not $Push) {
        Write-Host "`nDry run only -- nothing was pushed." -ForegroundColor Cyan
        Write-Host "Re-run with -Push to do it" -NoNewline
        if ($mode -eq 'diverged') { Write-Host " plus -ReplaceRemoteHistory" -NoNewline }
        Write-Host ":`n  .\scripts\Connect-Remote.ps1 -Push$([string]::Empty)$(if ($mode -eq 'diverged') { ' -ReplaceRemoteHistory' })" -ForegroundColor Cyan
        exit 0
    }

    if ($mode -eq 'diverged' -and -not $ReplaceRemoteHistory) {
        D 'refusing to replace remote history without -ReplaceRemoteHistory'
    }

    if ($mode -eq 'diverged') {
        $remoteHead = ((& git ls-remote --symref origin HEAD) 2>$null | Select-Object -First 1) -replace '^ref: refs/heads/(\S+)\s+HEAD$', '$1'
        Write-Host "`n  to get the SAME content under the correct commit (keeps the clean history):" -ForegroundColor Yellow
        Write-Host "    git reset --hard <hash of the newest commit>" -ForegroundColor Yellow
        Write-Host "  (or simply re-apply this bundle with -Clean)" -ForegroundColor Yellow
        if ($remoteHead -and $remoteHead -ne $currentBranch -and $remoteHead -notmatch '^[0-9a-f]{40}$') {
            W "GitHub's default branch is '$remoteHead', but you are pushing '$currentBranch'"
            Write-Host "  that leaves two branches. To make '$currentBranch' the default:" -ForegroundColor Yellow
            Write-Host "    repo > Settings > Branches > Default branch > switch to $currentBranch" -ForegroundColor Yellow
            Write-Host "  then delete '$remoteHead' with:  git push origin --delete $remoteHead" -ForegroundColor Yellow
        }
    }

    S "pushing"
    if ($mode -eq 'diverged') {
        & git push -u origin $currentBranch --force-with-lease
    } else {
        & git push -u origin $currentBranch
    }
    if ($LASTEXITCODE -ne 0) {
        W 'push failed -- see the message above'
        Write-Host @"

If it mentions 'Authentication failed' or 'could not read Username':
  run  git config --global credential.helper manager   then push again (browser opens).
If it mentions 'protected branch' or 'permission denied':
  you are a member but not a writer, or the branch is protected -- ask the repo owner.

"@ -ForegroundColor Yellow
        exit 3
    }

    S "result"
    OK "pushed $currentBranch to $RepoUrl"
    & git log --oneline -1
    Write-Host "`nFrom now on a bundle is no longer needed:" -ForegroundColor Cyan
    Write-Host '  git pull        # get the others work' -ForegroundColor Cyan
    Write-Host '  git push        # share yours' -ForegroundColor Cyan
}
finally { Pop-Location }
