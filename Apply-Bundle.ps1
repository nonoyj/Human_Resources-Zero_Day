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

.PARAMETER Reinstall
    Like -Clean (removes the local uncommitted changes so the files match the bundle commit) but KEEPS
    the existing .git, so the history is not replaced by the one inside the zip. Use this when you are
    running the copy of Apply-Bundle.ps1 that lives inside the working copy: PowerShell reads a script
    before running it, so the script cannot replace the .git folder it is executing from.

.PARAMETER Clean
    Also remove nesting damage (art\art, docs\docs, ...).

.PARAMETER Branch
    Branch to check out after adopting the history. Default: main.

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
    [switch]$NoGit,
    [string]$Branch = 'main'
)

$ErrorActionPreference = 'Stop'
function S  ($m) { Write-Host "`n==> $m" -ForegroundColor Cyan }
function OK ($m) { Write-Host "  [ok]   $m" -ForegroundColor Green }
function W  ($m) { Write-Host "  [warn] $m" -ForegroundColor Yellow }
function D  ($m) { Write-Host "  [FAIL] $m" -ForegroundColor Red; exit 1 }

# --- native git, invoked so that STDOUT/STDERR are data, not errors -----------------------------
# BUG-036/037: PS 5.1 turns any native command's stderr into a NativeCommandError, and this script runs
# with $ErrorActionPreference='Stop', so `git remote get-url origin` on a repo with no origin (stderr:
# "No such remote 'origin'") KILLED the apply -- twice, at line 131 and again at 135 after a partial fix.
# No amount of redirection fixes that class: the redirect happens too late. So git is now started as a
# PROCESS and its streams are read as text. Nothing native is invoked with '&' in this file any more.
$script:GitExitCode = 0
function Git {
    param([Parameter(Mandatory = $true)][string[]]$GitArgs, [switch]$Quiet)
    $psi = New-Object System.Diagnostics.ProcessStartInfo
    $psi.FileName  = 'git'
    $psi.Arguments = (($GitArgs | ForEach-Object { if ($_ -match '\\s') { '"' + $_ + '"' } else { $_ } }) -join ' ')
    $psi.UseShellExecute = $false
    $psi.CreateNoWindow = $true
    $psi.RedirectStandardOutput = $true
    $psi.RedirectStandardError  = $true
    $p = [System.Diagnostics.Process]::Start($psi)
    $out = $p.StandardOutput.ReadToEnd()
    $err = $p.StandardError.ReadToEnd()
    $p.WaitForExit()
    $script:GitExitCode = $p.ExitCode
    if (-not $Quiet) {
        if ($out.Trim()) { Write-Host $out.TrimEnd() }
        if ($err.Trim()) { Write-Host $err.TrimEnd() -ForegroundColor DarkGray }   # diagnostics, never fatal
    }
    return $out
}



if (-not (Test-Path -LiteralPath $BundlePath)) { D "bundle not found: $BundlePath" }
$BundlePath = (Resolve-Path -LiteralPath $BundlePath).Path

$projectDirs = @('art','content','docs','handoff','prototype','scripts','tools','unity','backend')
$keepDirs    = @('Library','Temp','Logs','obj','UserSettings','Build','Builds','bin',
                 '.tooling','.cache','.dotnet','.nuget','.local','.vs','.idea')

$tempRoot = if ($env:TEMP) { $env:TEMP } elseif ($env:TMP) { $env:TMP } else { [System.IO.Path]::GetTempPath() }
$temp = Join-Path $tempRoot ("hr0d-boot-" + [Guid]::NewGuid().ToString('N').Substring(0, 8))

try {
    # NO Clear-Host: in pwsh 7 it shells out to `clear`, and when TERM is unset or output is piped that
    # exits non-zero -- which under 5.1-style error handling kills the apply before it starts. A tidy
    # console is not worth a fatal native command.
    Write-Host ''
    S "extracting bundle"
    New-Item -ItemType Directory -Force -Path $temp | Out-Null
    # Expand-Archive is a cmdlet over System.IO.Compression, and on this host it refuses entries whose
    # name starts with a dot ('Cannot find path .../.gitattributes'). Windows PowerShell 5.1 extracts
    # the same zip fine, but a script that only works on one host is a script nobody can rehearse.
    # So: extract every entry by hand through the ZipFile API. Same bytes, no cmdlet quirks.
    Add-Type -AssemblyName System.IO.Compression.FileSystem | Out-Null
    $zip = [System.IO.Compression.ZipFile]::OpenRead($BundlePath)
    try {
        foreach ($entry in $zip.Entries) {
            if (-not $entry.Name) { continue }                      # directory entry
            $target = Join-Path $temp ($entry.FullName -replace '/', '\')
            $dir = Split-Path -Parent $target
            if (-not (Test-Path -LiteralPath $dir)) { New-Item -ItemType Directory -Force -Path $dir | Out-Null }
            [System.IO.Compression.ZipFileExtensions]::ExtractToFile($entry, $target, $true)
        }
    } finally { $zip.Dispose() }
    $root = Get-ChildItem -LiteralPath $temp -Directory |
            Where-Object { Test-Path (Join-Path $_.FullName 'README.md') } |
            Select-Object -First 1
    if (-not $root) { D "bundle does not contain a project folder (no README.md inside)" }
    OK "project found in bundle: $($root.Name)"

    if (-not (Test-Path -LiteralPath $WorkingCopy)) {
        New-Item -ItemType Directory -Force -Path $WorkingCopy | Out-Null
        OK "created $WorkingCopy"
    }

    if ($Reinstall -and -not $Clean) {
        S "discarding local changes (keeping .git)"
        Git @('-C', $WorkingCopy, 'reset', '--hard', 'HEAD') | Out-Null
        Git @('-C', $WorkingCopy, 'clean', '-fd') | Out-Null
        $rsShort = (Git @('-C', $WorkingCopy, 'rev-parse', '--short', 'HEAD') -Quiet).Trim()
        OK "working copy reset to HEAD ($rsShort)"
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

    # Adopt the bundle's history on EVERY apply, not just -Clean: the files are about to be
    # overwritten wholesale, so keeping an older .git guarantees a dirty tree (BUG-019).
    # The history ships as history.bundle -- a single file git verifies itself. Copying a .git
    # folder recursively is what silently broke on Windows (BUG-021).
    if (-not $NoGit) {
        S "adopting the bundle's git history"
        # BUG-032: the transport files sit at the ZIP ROOT (or inside the project for v45+).
        # Testing $root only meant this branch never matched a real bundle.
        $srcBundle = $null
        foreach ($cand in @(
            (Join-Path $temp 'history.bundle'),
            (Join-Path $root.FullName 'history.bundle'),
            (Join-Path (Join-Path $root.FullName $root.Name) 'history.bundle'))) {
            if (Test-Path -LiteralPath $cand) { $srcBundle = $cand; break }
        }
        if ($srcBundle) {
            $dstGit = Join-Path $WorkingCopy '.git'
            # NB: do NOT run git inside the extracted folder to discover the branch -- the extracted
            # project has no .git, so git walks up to an unrelated parent repository (this bit us in
            # testing: it reported the outer repo's branch). Build-Bundle.ps1 records the branch in
            # history.branch instead.
            $bBranch = $null
            foreach ($bf in @((Join-Path $temp 'history.branch'), (Join-Path $root.FullName 'history.branch'))) {
                if (Test-Path -LiteralPath $bf) { $bBranch = (Get-Content -LiteralPath $bf -Raw).Trim(); break }
            }
            if (-not $bBranch) { $bBranch = $Branch }
            if (-not $bBranch) { $bBranch = 'main' }

            # remember origin before anything is replaced
            $savedRemote = $null
            if (Test-Path -LiteralPath $dstGit) {
                # `git remote get-url origin` on a repo WITHOUT an origin prints to stderr, and PS 5.1 turns
                # native stderr into a NativeCommandError -> the whole apply dies (BUG-036, exactly what
                # the operator hit). get-url has no -q, so redirect the stream and trust the exit code.
                $savedRemote = $null
                $remoteOut = Git @('-C', $WorkingCopy, 'remote', 'get-url', 'origin') -Quiet
                if ($script:GitExitCode -eq 0 -and $remoteOut.Trim()) { $savedRemote = $remoteOut.Trim() }
                if (-not $savedRemote) { $savedRemote = $null }
                Remove-Item -LiteralPath $dstGit -Recurse -Force
                OK "old .git removed"
            }

            # BUG-038/039: two traps in a row here, both machine-dependent:
            #   a) `git init` + `fetch refs/heads/*:refs/heads/*` dies with
            #      "refusing to fetch into branch 'refs/heads/main' checked out at ..." on installs whose
            #      default branch is main (ours here defaults to master, which is why rehearsing on this
            #      box proved nothing -- behaviour that depends on a machine setting is never proven by
            #      one machine);
            #   b) `git clone <bundle> <workingcopy>` refuses a non-empty folder, and the working copy is
            #      ALWAYS full of the previous version.
            # So: init, fetch into a namespace that can never be checked out, then move the refs by hand.
            # Nothing is ever "checked out into", so there is nothing for git to refuse.
            Git @('-C', $WorkingCopy, 'init', '-q') -Quiet | Out-Null
            $incoming = 'refs/hr0d-incoming'
            # source -> destination. Getting this backwards is silent: the fetch "succeeds" and creates
            # nothing, which then looks like "the bundle has no branch". It did, once.
            # NOT -Quiet: a failed fetch must show its reason. It failed silently once and the script
            # then blamed the bundle for having no branches.
            Git @('-C', $WorkingCopy, 'fetch', '-q', '--no-tags', $srcBundle, ('+refs/heads/*:' + $incoming + '/*')) | Out-Null
            if ($script:GitExitCode -ne 0) { D "fetch from history.bundle failed: $srcBundle" }
            if ($script:GitExitCode -ne 0) {
                # older syntax fallback: fetch the single branch straight to a temp ref
                Git @('-C', $WorkingCopy, 'fetch', '-q', $srcBundle, ('+' + 'refs/heads/' + $bBranch + ':refs/heads/' + $bBranch)) -Quiet | Out-Null
                if ($script:GitExitCode -ne 0) { D "fetch from history.bundle failed ($bBranch): $srcBundle" }
            }
            # Two traps, both found the hard way:
            #   * %(refname:short) strips `refs/`, so the prefix match never fires;
            #   * a format string with a SPACE in it survives as one PowerShell argument but not
            #     through ProcessStartInfo quoting -- git receives two args, expands only the first,
            #     and the output silently loses the sha. Use one token with a separator.
            $refs = Git @('-C', $WorkingCopy, 'for-each-ref', '--format=%(refname)|%(objectname)', $incoming) -Quiet
            $moved = 0
            foreach ($line in ($refs -split "`n")) {
                $t = $line.Trim()
                $bar = $t.IndexOf('|')
                if (-not $t -or $bar -lt 0) { continue }
                $full = $t.Substring(0, $bar)
                $sha  = $t.Substring($bar + 1).Trim()
                $prefix = $incoming + '/'
                if (-not $full.StartsWith($prefix)) { continue }
                $name = $full.Substring($prefix.Length)
                Git @('-C', $WorkingCopy, 'update-ref', ('refs/heads/' + $name), $sha) -Quiet | Out-Null
                $moved++
            }
            if ($moved -eq 0) { D "the bundle carried no branches (nothing fetched into $incoming)" }
            Git @('-C', $WorkingCopy, 'symbolic-ref', 'HEAD', ('refs/heads/' + $bBranch)) | Out-Null
            $headSha = Git @('-C', $WorkingCopy, 'rev-parse', '--verify', '--quiet', ('refs/heads/' + $bBranch)) -Quiet
            if (-not $headSha.Trim()) { D "branch '$bBranch' is not in the bundle (history.branch says otherwise)" }
            Git @('-C', $WorkingCopy, 'reset', '-q', '--mixed', ('refs/heads/' + $bBranch)) | Out-Null
            $shortSha = (Git @('-C', $WorkingCopy, 'rev-parse', '--short', $bBranch) -Quiet).Trim()
            OK "history adopted: $bBranch at $shortSha"
            # hooks/config from the source repo are not needed; keep the bundle's identity out of it
            Git @('-C', $WorkingCopy, 'config', 'core.autocrlf', 'false') -Quiet | Out-Null
            if ($savedRemote) {
                Git @('-C', $WorkingCopy, 'remote', 'add', 'origin', $savedRemote) | Out-Null
                OK "origin preserved: $savedRemote"
            } else {
                W "no origin in the old .git -- set it with: git remote add origin <url>"
            }
        } else {
            W "bundle has no history.bundle (older bundle) -- history left untouched"
        }
    }

    S "copying project files"
    $copied = 0
    # Snapshot the list FIRST. Deleting from a folder while Get-ChildItem is still enumerating it makes
    # the enumerator throw mid-loop and the apply dies after a half-copy. Materialise, then act.
    # NOTE: this is a plain foreach, so `continue` (not `return`) is the skip -- `return` here would
    # exit the whole script silently (BUG-022).
    $scriptCopied = $false
    $items = @(Get-ChildItem -LiteralPath $root.FullName -Force)
    foreach ($it in $items) {
        if ($it.Name -eq '.git') { continue }
        # bundle transport files, not project files
        if ($it.Name -in @('history.bundle', 'history.branch')) { continue }
        if ($it.Name -eq 'Apply-Bundle.ps1') { $scriptCopied = $true }
        if ($it.PSIsContainer -and ($keepDirs -contains $it.Name)) { W "skipped $($it.Name)"; continue }
        $dest = Join-Path $WorkingCopy $it.Name
        if ($it.PSIsContainer) {
            if (Test-Path -LiteralPath $dest) { Remove-Item -LiteralPath $dest -Recurse -Force }
            Copy-Item -LiteralPath $it.FullName -Destination $dest -Recurse -Force
        } else {
            Copy-Item -LiteralPath $it.FullName -Destination $dest -Force
        }
        $copied++
    }
    OK "$copied top-level items"
    # Keep the copy of THIS script beside the bundle current. The operator runs .\Apply-Bundle.ps1
    # from Downloads, which no bundle ever overwrites -- so a bug fixed in the script would never
    # reach them (that is exactly how BUG-032 survived a release). Self-heal on every apply.
    $selfSrc = Join-Path $temp 'Apply-Bundle.ps1'
    if ((Test-Path -LiteralPath $selfSrc) -and $PSScriptRoot) {
        $selfDst = Join-Path $PSScriptRoot 'Apply-Bundle.ps1'
        if ((Test-Path -LiteralPath $selfDst) -and
            ((Get-FileHash -LiteralPath $selfSrc).Hash -ne (Get-FileHash -LiteralPath $selfDst).Hash)) {
            Copy-Item -LiteralPath $selfSrc -Destination $selfDst -Force
            OK 'bootstrap refreshed (the next apply uses this version)'
        }
    }

    if ($Clean) {
        # The bundle's file set is the truth. Anything left over from the previous version (a file
        # that existed under the old HEAD) shows up as untracked and was reported as
        # "files do NOT match HEAD" -- alarming, and wrong. Force the tree to match, then verify.
        Git @('-C', $WorkingCopy, 'reset', '--hard') -Quiet | Out-Null
        Git @('-C', $WorkingCopy, 'clean', '-fd') -Quiet | Out-Null
    }

    S "result"
    Push-Location $WorkingCopy
    try {
        if ((Test-Path '.git') -and (Get-Command git -ErrorAction SilentlyContinue)) {
            $head  = (Git @('-C', $WorkingCopy, 'rev-parse', '--short', 'HEAD') -Quiet).Trim()
            $count = (Git @('-C', $WorkingCopy, 'rev-list', '--count', 'HEAD') -Quiet).Trim()
            OK "HEAD $head ($count commits)"
            $statusOut = Git @('-C', $WorkingCopy, 'status', '--porcelain') -Quiet
            $st = @($statusOut -split "`n" | ForEach-Object { $_.TrimEnd() } | Where-Object { $_ -and $_.Trim() })
            if ($st) {
                W "working tree not clean ($($st.Count) entries) -- files do NOT match HEAD"
                $st | Select-Object -First 12 | ForEach-Object { Write-Host "    $_" }
                Write-Host @"

  This means the .git folder could not be updated (you ran the copy of this script that lives
  inside the working copy -- PowerShell cannot replace the folder it is running from).
  Fix it with EITHER:
      git reset --hard $head && git clean -fd          # keep history, make files match HEAD
      .\scripts\Apply-Bundle.ps1 -BundlePath <zip> -Reinstall   # same thing, from the repo copy
  or run this script from OUTSIDE the repo (e.g. from Downloads) with -Clean.
"@ -ForegroundColor Yellow
                Set-Location $PSScriptRoot | Out-Null
                exit 1
            }
            else { OK "working tree clean -- files match HEAD exactly" }
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
