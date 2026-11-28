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
# 0 = applied and the tree matches HEAD
# 2 = applied, but the working tree does NOT match HEAD (see the [warn] block -- it is a real problem, and it
#     must not be reported as a healthy install: v130.2 ended with "[ok] working tree clean" and still came
#     back as exit 1, which is a message that contradicts its own output)
# 1 = did not apply
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

# --- THE SCRIPT UPDATES ITSELF BEFORE IT RUNS THE OLD VERSION (v103, BUG-062 SECOND HALF) ---------
#
# The bug: the operator keeps ONE launcher in Downloads and it is never part of a bundle, so a fix to this
# file only reached them AFTER the apply that would have used it ("bootstrap refreshed (the next apply uses
# this version)"). v102 shipped the sweep for Unity's stray files, and the player's very next apply -- the
# one that should have proved it -- ran v101's copy, printed a dirty tree, and refreshed the bootstrap for
# next time. A fix that needs two applies is not a fix.
#
# So: read the incoming zip's Apply-Bundle.ps1, and if it differs from the file being executed, replace
# this file and re-run it as a CHILD process with the same arguments. Only when the caller did not already
# do that (the child carries a flag), only when the file is writable, and on any failure we fall through
# and apply with the version we have -- a self-update must never be the reason an install fails.
function Get-BundleMember([string]$zip, [string]$member) {
    Add-Type -AssemblyName System.IO.Compression.FileSystem -ErrorAction Stop
    $z = [System.IO.Compression.ZipFile]::OpenRead($zip)
    try {
        $entry = $z.Entries | Where-Object { $_.FullName -eq $member } | Select-Object -First 1
        if ($null -eq $entry) { return $null }
        $reader = New-Object System.IO.StreamReader($entry.Open())
        try { return $reader.ReadToEnd() } finally { $reader.Dispose() }
    } finally { $z.Dispose() }
}

if (-not $env:HR0D_SELF_UPDATED -and (Test-Path -LiteralPath $BundlePath)) {
    try {
        $incoming = Get-BundleMember ((Resolve-Path -LiteralPath $BundlePath).Path) 'HR-ZeroDay/Apply-Bundle.ps1'
        if ($incoming) {
            $selfPath = $PSCommandPath
            if (-not $selfPath) { $selfPath = $MyInvocation.MyCommand.Path }
            if ($selfPath -and (Test-Path -LiteralPath $selfPath)) {
                $onDisk = [System.IO.File]::ReadAllText($selfPath)
                if ($onDisk -ne $incoming) {
                    [System.IO.File]::WriteAllText($selfPath, $incoming, (New-Object System.Text.UTF8Encoding($true)))
                    Write-Host '  [ok]   this copy of the installer was out of date -- it updated itself and re-ran from the top' -ForegroundColor Green
                    $env:HR0D_SELF_UPDATED = '1'
                    # A HASHTABLE splat, not an array (BUG-064). The array form passed "-WorkingCopy" and
                    # its value as two separate strings and the child's binder rejected the value
                    # ("A positional parameter cannot be found that accepts argument '<path>'") -- the
                    # self-update then fell back to the on-disk script, silently defeating the whole point.
                    $childArgs = @{}
                    foreach ($k in $PSBoundParameters.Keys) {
                        $v = $PSBoundParameters[$k]
                        if ($v -is [System.Management.Automation.SwitchParameter]) { $childArgs[$k] = $v.IsPresent }
                        else { $childArgs[$k] = [string]$v }
                    }
                    if ($env:HR0D_SELF_UPDATED_DRYRUN -eq '1') { return }   # tests: prove the swap, skip the re-run
                    & $selfPath @childArgs
                    exit $LASTEXITCODE
                }
            }
        }
    } catch {
        Write-Host "  [warn] could not self-update ($($_.Exception.Message)) -- applying with the version on disk" -ForegroundColor Yellow
    }
}

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
        $removed = 0
        foreach ($d in $projectDirs) {
            $inner = Join-Path (Join-Path $WorkingCopy $d) $d
            # Only an UNTRACKED double-nest is a leftover to sweep. `tools/art` is a real, tracked
            # directory that happens to match "a folder inside a folder of the same name's parent" --
            # which is why this step once announced "removed tools\tools" and "removed art\art" on a
            # tree that never had either. Ask git; and if git cannot be asked, touch only a path that is
            # not in the bundle.
            if (-not (Test-Path -LiteralPath $inner)) { continue }
            $tracked = ''
            $tracked = Git @('-C', $WorkingCopy, 'ls-tree', '-r', '--name-only', 'HEAD', '--', "$d/$d/") -Quiet
            if ($tracked) { continue }   # tracked: it belongs to the bundle, leave it alone
            Remove-Item -LiteralPath $inner -Recurse -Force
            $removed++
            OK "removed $d\$d (leftover from an older apply)"
        }
        if ($removed -eq 0) { OK 'nothing to sweep -- no folder/folder duplicates on disk' }
    }

    # Adopt the bundle's history on EVERY apply, not just -Clean: the files are about to be
    # overwritten wholesale, so keeping an older .git guarantees a dirty tree (BUG-019).
    # The history ships as history.bundle -- a single file git verifies itself. Copying a .git
    # folder recursively is what silently broke on Windows (BUG-021).
    # The commit the working copy was on BEFORE the history was swapped. Needed after the adopt: the file
    # copy leaves the OLD version's files on disk as untracked, and (BUG-080) the sweep below only looked
    # under unity/, so seven superseded reference stills survived an apply and made the tree read dirty.
    $preAdoptHead = ''
    if (Test-Path -LiteralPath (Join-Path $WorkingCopy '.git')) {
        $preAdoptHead = (Git @('-C', $WorkingCopy, 'rev-parse', 'HEAD') -Quiet | Out-String).Trim()
    }

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
                # Unity does not lock .git, but its file watcher and Windows' indexer can hold a handle for a
                # second or two, and a hard Remove-Item then throws and aborts an otherwise fine apply. The
                # player's complaint was having to CLOSE Unity to unpack a bundle, so retry first and only
                # ask them to close the editor if every attempt fails.
                $removed = $false
                for ($attempt = 1; $attempt -le 5; $attempt++) {
                    try {
                        Remove-Item -LiteralPath $dstGit -Recurse -Force -ErrorAction Stop
                        $removed = $true
                        break
                    } catch {
                        if ($attempt -lt 5) { Start-Sleep -Milliseconds 400 }
                    }
                }
                if (-not $removed) {
                    Fail "could not replace .git -- a process still has it open. Close Unity (the editor holds the project) and re-run, or use 'git pull' instead of a bundle."
                }
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
            $bundleHead = $headSha.Trim()
            $script:bundleHead = $bundleHead
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
    $bundleHead = $null
    $items = @(Get-ChildItem -LiteralPath $root.FullName -Force)
    foreach ($it in $items) {
        if ($it.Name -eq '.git') { continue }
        # bundle transport files, not project files
        if ($it.Name -in @('history.bundle', 'history.branch')) { continue }
        if ($it.Name -eq 'Apply-Bundle.ps1') { $scriptCopied = $true }
        if ($it.PSIsContainer -and ($keepDirs -contains $it.Name)) { W "skipped $($it.Name)"; continue }
        $dest = Join-Path $WorkingCopy $it.Name
        if ($it.PSIsContainer) {
            # MERGE, never replace. A bundle carries no Unity caches (Library/Temp/obj are excluded), so
            # deleting the destination first bought nothing and cost a failed apply: on 2026-11-26
            # Remove-Item hit unity\Library\BurstCache\*.dll -- held open by the Burst compiler while
            # the editor was running -- and PowerShell's UnauthorizedAccessException aborted the entire
            # apply mid-copy. Copying over a live folder is exactly what Unity is designed to survive
            # (it reimports on focus), and a file that cannot be replaced is now a WARNING, not a death.
            # Windows PowerShell 5.1 QUIRK, and the source of every phantom "art\art" this project has
            # ever reported: `Copy-Item -Path <dir> -Destination <existing dir> -Recurse` puts the source
            # directory INSIDE the destination (dst\art\art) instead of merging its contents. Creating
            # $dest first and copying its CONTENTS avoids the ambiguity completely.
            if (-not (Test-Path -LiteralPath $dest)) { New-Item -ItemType Directory -Path $dest | Out-Null }
            $locked = @()
            Copy-Item -Path (Join-Path $it.FullName '*') -Destination $dest -Recurse -Force -ErrorAction SilentlyContinue `
                      -ErrorVariable copyErr
            foreach ($e in $copyErr) {
                $path = if ($e.TargetObject) { "$($e.TargetObject)" } else { "$($e.Exception.Message)" }
                if ($path -match '\\(Library|Temp|obj|Logs|UserSettings)\\') { $locked += $path }
                else { W "could not copy: $path" }
            }
            if ($locked.Count -gt 0) {
                W "$($locked.Count) engine-cache file(s) were open and were skipped (Unity is running). That is fine:"
                W "  they are generated, and Unity rebuilds them on the next focus. Nothing in the game lives there."
            }
        } else {
            Copy-Item -LiteralPath $it.FullName -Destination $dest -Force
        }
        $copied++
    }
    OK "$copied top-level items"

    # ---------------------------------------------------------------------------------------------
    # BUG-070: THE APPLY MUST NOT LEAVE A DIRTY TREE.
    #
    # .gitattributes says `* text=auto eol=lf`, so git stores LF and compares against LF. But this copy
    # step writes bytes straight out of the zip with Copy-Item -- git's line-ending filter never runs --
    # so a zip packed on a host that has CRLF in the working tree (Windows: PowerShell's Copy-Item in
    # Build-Bundle preserves them) lands CRLF files in a repository that expects LF. `git status` then
    # reports every one of them as modified, Where-Am-I says "copy them or stash them", and Build-Bundle
    # refuses to run on a tree that is, in fact, exactly the commit it names.
    #
    # The fix is normalisation by the owner of the bytes, not by git: after copying, ask git which files
    # it believes differ and rewrite those that differ ONLY in line endings. A real edit is left alone.
    S "normalising line endings (git expects LF)"
    $changed = @(Git @('-C', $WorkingCopy, 'diff', '--name-only') -Quiet) |
               Where-Object { $_ -and $_.Trim() }
    $fixed = 0
    $realEdits = @()
    foreach ($rel in $changed) {
        $rel = $rel.Trim()
        $full = Join-Path $WorkingCopy ($rel -replace '/', '\')
        if (-not (Test-Path -LiteralPath $full)) { continue }
        # binary files are excluded from the check on purpose: a PNG that "differs" is a real change
        if ($rel -match '\.(png|jpg|jpeg|wav|mp3|ogg|ttf|dll|so|zip|bundle|ico)$') { $realEdits += $rel; continue }
        try {
            $bytes = [System.IO.File]::ReadAllBytes($full)
            if ($bytes.Length -gt 8388608) { $realEdits += $rel; continue }   # 8 MB: not a source file
            $text = [System.Text.Encoding]::UTF8.GetString($bytes)
            $norm = $text -replace "`r`n", "`n"
            if ($norm -ne $text) {
                [System.IO.File]::WriteAllBytes($full, [System.Text.Encoding]::UTF8.GetBytes($norm))
                $fixed++
            } else {
                $realEdits += $rel
            }
        } catch {
            $realEdits += $rel
        }
    }
    if ($fixed -gt 0) { OK "$fixed file(s) had CRLF line endings and were converted to LF" }
    if ($realEdits.Count -gt 0) {
        W "$($realEdits.Count) file(s) genuinely differ from HEAD:"
        $realEdits | Select-Object -First 8 | ForEach-Object { Write-Host "    $_" }
        W "  That is either an edit you made, or a bundle packed from a dirty tree. The apply has not touched them."
    } elseif ($fixed -eq 0) { OK 'already LF -- nothing to convert' }
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

    # ---------------------------------------------------------------------------------------------
    # UNITY LEFT FILES BEHIND (v102, BUG-062).
    #
    # Every apply copies the bundle's tracked tree over whatever was there. Unity, having opened the
    # project, leaves its own untracked files next to it: folder .meta files for directories it created,
    # DefaultVolumeProfile.asset, and (until v98) all of Assets/Art's metas. Those are NOT the bundle's,
    # and they made an apply end with "working tree not clean (105 entries)" on a machine that had only
    # ever pressed Play. Reported as a dirty install; it was a leftover install.
    #
    # So: sweep untracked files under unity/ -- and only unity/, never the repo root, so a note the player
    # left in the repo survives. Ignored paths (Library/, Temp/, Logs/) are untouched: `git clean` removes
    # untracked AND not-ignored files, which is exactly the set that makes the tree look dirty.
    # ---------------------------------------------------------------------------------------------
    # $bundleHead is nulled just before the copy above (it is only used to gate the copy), and
    # $script:bundleHead is what the adopt block left behind. Restore it here so BOTH this sweep and the
    # -Clean branch below know which commit the tree is supposed to match. (Until v119.3 the -Clean branch
    # always saw $null and fell back to a blanket `reset --hard` + `clean -fd`, which is why it worked but
    # also why a non-Clean apply had no way to drop stale files at all.)
    if (-not $bundleHead -and $script:bundleHead) { $bundleHead = $script:bundleHead }

    # ---------------------------------------------------------------------------------------------
    # FILES THE NEW VERSION DELETED (v119.3, BUG-080).
    #
    # An apply replaces the bundle's files but does NOT remove the previous version's. Git never knew they
    # were gone -- the adopt is a `reset --mixed`, which moves HEAD and leaves the index and the tree alone --
    # so every file the new commit dropped came back as UNTRACKED: "working tree not clean (7 entries)" on a
    # copy that had only ever been installed over. The sweep below handles what UNITY leaves behind; this
    # handles what WE left behind, which is the install's own business and must not depend on -Clean.
    #
    # It is deliberately targeted: only paths that the pre-adopt commit TRACKED and the new commit does not.
    # A note the player wrote is untracked in BOTH, so it is never touched.
    # ---------------------------------------------------------------------------------------------
    if ($preAdoptHead -and $bundleHead -and ($preAdoptHead -ne $bundleHead)) {
        $gone = @((Git @('-C', $WorkingCopy, 'diff', '--name-only', '--diff-filter=D',
                          $preAdoptHead, $bundleHead) -Quiet) -split "`n" |
                  Where-Object { $_ -and $_.Trim() })
        $removed = 0
        foreach ($rel in $gone) {
            $rel = $rel.Trim()
            if (-not $rel) { continue }
            $full = Join-Path $WorkingCopy $rel
            if (Test-Path -LiteralPath $full) {
                Remove-Item -LiteralPath $full -Force -ErrorAction SilentlyContinue
                $removed++
            }
        }
        if ($removed -gt 0) { OK "removed $removed file(s) the new version no longer ships" }
        elseif ($gone.Count -gt 0) { OK 'nothing stale to remove -- the old tree was already current' }
    }

    $unityDir = Join-Path $WorkingCopy 'unity'
    if (Test-Path -LiteralPath $unityDir) {
        $before = @((Git @('-C', $WorkingCopy, 'status', '--porcelain', '--', 'unity') -Quiet) -split "`n" |
                    Where-Object { $_ -and $_.Trim() -and $_.Trim().StartsWith('??') })
        if ($before.Count -gt 0) {
            Git @('-C', $WorkingCopy, 'clean', '-fdq', '--', 'unity') | Out-Null
            $after = @((Git @('-C', $WorkingCopy, 'status', '--porcelain', '--', 'unity') -Quiet) -split "`n" |
                       Where-Object { $_ -and $_.Trim() -and $_.Trim().StartsWith('??') })
            $swept = $before.Count - $after.Count
            if ($swept -gt 0) { OK "swept $swept stray file(s) Unity left in unity/ (meta files, editor assets)" }
            if ($after.Count -gt 0) { W "$($after.Count) untracked file(s) under unity/ survived the sweep" }
        }
    }


    if ($Clean) {
        # The bundle's file set is the truth. Anything left over from the previous version (a file
        # that existed under the old HEAD) shows up as untracked and was reported as
        # "files do NOT match HEAD" -- alarming, and wrong. Force the tree to match, then verify.
        #
        # RECOVERED FROM THE RUNNING-COPY CASE FIRST (2026-11-27). Applying from INSIDE the working copy
        # (the repo's own scripts\Apply-Bundle.ps1 -- which is the only copy a bundle-installed machine
        # has before it downloads one) cannot replace .git under itself, so the history adopt is skipped,
        # and the file copy then writes the bundle's tree over a checkout of a DIFFERENT commit. That is
        # exactly how the player got 474 "working tree not clean" entries after a clean apply: the PNGs
        # were the bundle's, HEAD was the old commit. The fix is not to explain it, it is to make the tree
        # match the bundle: the same tree is already on disk, so reset to the bundle's commit -- walking
        # forward (fast-forward) or back (the old commit is in the new history). Only if that commit is
        # genuinely absent do we fall back to hard reset + clean, which is what -Reinstall means.
        $target = $bundleHead
        if ($target) {
            Git @('-C', $WorkingCopy, 'cat-file', '-e', ($target + '^{commit}')) -Quiet | Out-Null
            if ($LASTEXITCODE -eq 0) {
                Git @('-C', $WorkingCopy, 'reset', '-q', '--hard', $target) | Out-Null
                OK "tree forced to the bundle's commit $($target.Substring(0,7))"
            } else {
                Git @('-C', $WorkingCopy, 'reset', '--hard') -Quiet | Out-Null
                Git @('-C', $WorkingCopy, 'clean', '-fd') -Quiet | Out-Null
                W "bundle commit $($target.Substring(0,7)) is not in this copy's history -- tree reset to HEAD instead"
            }
        } else {
            Git @('-C', $WorkingCopy, 'reset', '--hard') -Quiet | Out-Null
            Git @('-C', $WorkingCopy, 'clean', '-fd') -Quiet | Out-Null
        }
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
                exit 2
            }
            else { OK "working tree clean -- files match HEAD exactly" }
        }
        foreach ($d in $projectDirs) {
            # A merge that is working produces a directory whose contents REPLACED the old ones. Only a
            # directory that is TRACKED but should not be (the BUG-021 double-nest: art\art committed)
            # is a problem, so ask git rather than the filesystem -- the filesystem check fired on every
            # clean apply for a week and made three real warnings look like noise.
            # (filesystem test removed entirely: see above -- ls-tree is the only question that has an
            # answer here. A 474-entry dirty tree is the real symptom; nesting is a phantom.)
            $rev = if ($bundleHead) { $bundleHead } else { 'HEAD' }
            $tracked = Git @('-C', $WorkingCopy, 'ls-tree', '-r', '--name-only', $rev, '--', "$d/$d/") -Quiet
            if ($tracked) { W "still nested: $d\$d -- the BUNDLE tracks $d\$d, which is a build problem. Tell the agent." }
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
