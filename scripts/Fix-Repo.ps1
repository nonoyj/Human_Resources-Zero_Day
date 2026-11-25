<#
.SYNOPSIS
    Repairs a working copy so its files match the commit they claim to be on. Reads the bundle .zip
    directly -- no extraction, no Copy-Item, nothing to get stale.

.DESCRIPTION
    The problem this solves: applying a bundle copies files into the working folder, but the folder's
    .git can end up from an older moment. Result: `git log` shows an old commit while the files next to
    it are new, and `git status` is full of modifications you never made.

    This script rebuilds .git from the bundle's verified history (history.bundle inside the zip):
        1. remember the current `origin` URL
        2. remove .git
        3. git init + git fetch <history.bundle> + checkout the recorded branch
        4. restore `origin`
    Then it compares the working files to that commit and tells you, in one line, whether they match.

    It is safe to run repeatedly and it never pushes anything.

.PARAMETER BundlePath
    Path to HR-ZeroDay-bundle-vNN.zip (the one in Downloads).

.PARAMETER WorkingCopy
    The repo folder. Default: C:\Users\User\HR-ZeroDay

.PARAMETER KeepOrigin
    Remote URL to restore. Default: the repo's own URL.

.EXAMPLE
    .\Fix-Repo.ps1 -BundlePath C:\Users\User\Downloads\HR-ZeroDay-bundle-v31.zip
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$BundlePath,
    [string]$WorkingCopy = (Join-Path $(if ($env:USERPROFILE) { $env:USERPROFILE } else { $HOME }) 'HR-ZeroDay'),
    [string]$KeepOrigin = 'https://github.com/nonoyj/Human_Resources-Zero_Day.git'
)

$ErrorActionPreference = 'Continue'
function S  ($m) { Write-Host "`n==> $m" -ForegroundColor Cyan }
function OK ($m) { Write-Host "  [ok]   $m" -ForegroundColor Green }
function W  ($m) { Write-Host "  [warn] $m" -ForegroundColor Yellow }
function D  ($m) { Write-Host "  [FAIL] $m" -ForegroundColor Red; exit 1 }

$BundlePath = (Resolve-Path -LiteralPath $BundlePath).Path
$temp = Join-Path ([System.IO.Path]::GetTempPath()) ("hr0d-fix-" + [Guid]::NewGuid().ToString('N').Substring(0, 8))
New-Item -ItemType Directory -Force -Path $temp | Out-Null

try {
    S 'reading the bundle directly'
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $zip = [System.IO.Compression.ZipFile]::OpenRead($BundlePath)
    try {
        $bundleEntry = $zip.Entries | Where-Object { $_.FullName -match '(^|/)history\.bundle$' } | Select-Object -First 1
        $branchEntry = $zip.Entries | Where-Object { $_.FullName -match '(^|/)history\.branch$' } | Select-Object -First 1
        if (-not $bundleEntry) { D "this zip has no history.bundle -- it is an OLD bundle (v19-v25). Use v31" }
        $bundleFile = Join-Path $temp 'history.bundle'
        [System.IO.Compression.ZipFileExtensions]::ExtractToFile($bundleEntry, $bundleFile, $true)
        $branchOut = 'main'
        if ($branchEntry) {
            [System.IO.Compression.ZipFileExtensions]::ExtractToFile($branchEntry, (Join-Path $temp 'history.branch'), $true)
            $branchOut = (Get-Content -LiteralPath (Join-Path $temp 'history.branch') -Raw).Trim()
        }
        OK "history.bundle found ($([math]::Round((Get-Item $bundleFile).Length / 1MB, 2)) MB), branch $branchOut"
    }
    finally { $zip.Dispose() }

    $verify = (& git -C $temp bundle verify $bundleFile 2>&1 | Out-String)
    if ($verify -notmatch 'complete history') { D "the bundle inside this zip is damaged`n$verify" }
    OK 'bundle verified by git'

    if (-not (Test-Path -LiteralPath $WorkingCopy)) { D "working copy not found: $WorkingCopy" }

    S "rebuilding .git in $WorkingCopy"
    $gitDir = Join-Path $WorkingCopy '.git'
    if (Test-Path -LiteralPath $gitDir) {
        $existing = (& git -C $WorkingCopy remote get-url origin 2>$null | Out-String).Trim()
        if ($existing) { $KeepOrigin = $existing }
        Remove-Item -LiteralPath $gitDir -Recurse -Force
        OK 'old .git removed'
    }
    & git -C $WorkingCopy init -q | Out-Host
    & git -C $WorkingCopy fetch -q $bundleFile "refs/heads/*:refs/heads/*" | Out-Host
    if ($LASTEXITCODE -ne 0) { D 'fetch from history.bundle failed' }
    & git -C $WorkingCopy symbolic-ref HEAD "refs/heads/$branchOut" | Out-Host
    & git -C $WorkingCopy reset -q --mixed "refs/heads/$branchOut" | Out-Host
    & git -C $WorkingCopy remote add origin $KeepOrigin 2>&1 | Out-Host
    & git -C $WorkingCopy config core.autocrlf false | Out-Host
    OK "history adopted: $branchOut at $((& git -C $WorkingCopy rev-parse --short $branchOut))"
    OK "origin: $KeepOrigin"

    S 'does the working tree match that commit?'
    & git -C $WorkingCopy checkout -q -- . | Out-Host
    $st = @(& git -C $WorkingCopy status --porcelain) | Where-Object { $_ -and $_.Trim() }
    if ($st) {
        W "$($st.Count) path(s) still differ:"
        $st | Select-Object -First 15 | ForEach-Object { Write-Host "         $_" }
        Write-Host @"

  The bundle is good but the folder holds files from a different version.
  Either move them out of the way and re-extract, or force the folder to match the commit:
      cd "$WorkingCopy"
      git checkout -- .
      git clean -fd
  (git clean deletes untracked files -- check the list above before running it)
"@ -ForegroundColor Yellow
        exit 3
    }
    OK 'working tree clean -- files match HEAD exactly'
    Write-Host "`nREPO OK" -ForegroundColor Green
    Write-Host "  HEAD $((& git -C $WorkingCopy rev-parse --short HEAD)) on $branchOut" -ForegroundColor Green
    Write-Host "  next:  cd `"$WorkingCopy`"; .\scripts\Test-Scripts.ps1" -ForegroundColor Cyan
}
finally {
    if (Test-Path -LiteralPath $temp) { Remove-Item -LiteralPath $temp -Recurse -Force -ErrorAction SilentlyContinue }
}
