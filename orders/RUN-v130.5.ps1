<#
.SYNOPSIS
    v130.5, one paste: verify the bundle, extract it, apply it, push, and capture every gate to a log.

.DESCRIPTION
    Run this from anywhere (PowerShell 5.1 or 7). It does NOT need the repo to be healthy first.

    WHAT THIS ROUND IS (read me, then run me):
      * NEW SHIFT now actually starts a new day, visibly. Two causes: the menu painted five rows and acted on
        four (so the second row was NEW SHIFT on the glass and FIELD MANUAL to the game), and the restart
        never switched the room's actors back on.
      * The applicant you were looking at was OLD ART -- 56 animation frames had never been re-cut. They are
        rewritten in this bundle. He may now look like a different person. That is the point.
      * The desk lamp has a switch (K, or tap the fixture) and it is now worth a whole ramp step, so the mug,
        the forms, the keyboard and the wood change when it is switched.
      * F (or a tap on the glass) now changes the MONITOR itself, not only the light at the table's sides.
      * The menu's lamp answers a click: a warm ring at the shade's mouth, and K works there too.
      * The front-door composite gate no longer needs Pillow -- the "(could not run)" line is gone.

    What it leaves behind for me:
        <repo>\gate-v130.5.log     every gate's full output, plus the ERROR/FAIL lines at the end

    Change $zip / $repo below if your paths differ. Nothing else needs editing.
#>
$ErrorActionPreference = 'Stop'

# Base = the real profile folder on any OS. $env:USERPROFILE is Windows-only and is NULL elsewhere, which
# turns a working block into "Cannot bind argument to parameter 'Path' because it is null" at line 15 --
# a failure that says nothing about the actual problem. Resolve it once, loudly.
$base  = if ($env:USERPROFILE) { $env:USERPROFILE } elseif ($env:HOME) { $env:HOME } else { '' }
if (-not $base) { Write-Host '  [FAIL] cannot locate your home folder ($env:USERPROFILE / $env:HOME are empty)' -ForegroundColor Red; exit 1 }
$zip   = Join-Path (Join-Path $base 'Downloads') 'HR-ZeroDay-bundle-v130.5.zip'
$repo  = Join-Path $base 'HR-ZeroDay'
$stage = Join-Path ([IO.Path]::GetTempPath()) 'hrd-v130.5'

function Step ($m) { Write-Host "`n==> $m" -ForegroundColor Cyan }
function Ok   ($m) { Write-Host "  [ok]   $m" -ForegroundColor Green }
function Die  ($m) { Write-Host "  [FAIL] $m" -ForegroundColor Red; exit 1 }

# ---------------------------------------------------------------- 0. the bundle is the one I built
Step "checking the bundle"
if (-not (Test-Path -LiteralPath $zip)) { Die "bundle not found: $zip  (put the v130.5 zip in Downloads, or edit `$zip)" }
# The zip carries THIS file, so it cannot carry its own final hash -- that is why there is no hardcoded
# checksum here. What it checks instead is the thing a bad download actually looks like: a short file.
# The delivery message quotes a sha256; this prints the one it found, so the two can be compared.
$size = (Get-Item -LiteralPath $zip).Length
if ($size -lt 15MB) { Die "the zip is only $([int]($size/1MB)) MB -- a full v130.5 bundle is ~18 MB, re-download it" }
$got = (Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash
Write-Host "  sha256 $got" -ForegroundColor Cyan

# BUG-034: a bundle whose CONTENT is new but whose FILE still has the old name is indistinguishable from a
# good download until something looks inside it. The last round failed exactly there. So: the zip must
# contain the handover script that names this version, or the block stops with one sentence and no stack.
Add-Type -AssemblyName System.IO.Compression.FileSystem
$inZip = @()
$zipCheck = [System.IO.Compression.ZipFile]::OpenRead($zip)
try { $inZip = $zipCheck.Entries | ForEach-Object { $_.FullName } } finally { $zipCheck.Dispose() }
if ($inZip -notcontains 'HR-ZeroDay/orders/RUN-v130.5.ps1') {
    Die ("this zip is not the v130.5 bundle -- it has no HR-ZeroDay/orders/RUN-v130.5.ps1.`n" +
         "  The file in Downloads is an older one with the same name. Delete it, download the new bundle, " +
         "and check that its size is ~18 MB (17,500,000 bytes or more).")
}
Ok "this is the v130.5 bundle ($($inZip.Count) entries, handover script present)"

# ---------------------------------------------------------------- 1. extract beside the repo
Step "extracting to $stage"
if (Test-Path -LiteralPath $stage) { Remove-Item -LiteralPath $stage -Recurse -Force }
Expand-Archive -LiteralPath $zip -DestinationPath $stage -Force
if (-not (Test-Path -LiteralPath (Join-Path $stage 'Apply-Bundle.ps1'))) { Die 'the zip has no Apply-Bundle.ps1 at its root' }
Ok (Get-ChildItem -LiteralPath $stage | Measure-Object).Count.ToString() + ' entries at the top level'

# ---------------------------------------------------------------- 2. apply it (adopts the bundle's history)
Step "applying to $repo"
if (-not (Test-Path -LiteralPath $repo)) { New-Item -ItemType Directory -Path $repo | Out-Null }
& (Join-Path $stage 'Apply-Bundle.ps1') -BundlePath $zip -WorkingCopy $repo -Clean
$applyExit = $LASTEXITCODE
# Apply-Bundle returns 2 for "applied, but the tree does not match HEAD". That is a warning with a next step,
# never a crash -- and it prints its own explanation. Anything else non-zero is a real failure.
if ($applyExit -eq 2) { Write-Host "`n  [warn] applied, but the working tree does not match HEAD -- read the [warn] block above" -ForegroundColor Yellow }
elseif ($applyExit -ne 0) { Die "Apply-Bundle returned $applyExit (1 = it did not apply; see the [FAIL] line above)" }
else { Ok "applied -- working tree matches HEAD" }
Ok 'applied'

# PowerShell 7 is used by two of the gates. Under Windows PowerShell 5.1 there is no 'pwsh' on PATH unless the
# player installed it -- and the last round's Test-Scripts.ps1 turned that into a red FAIL that looked like a
# code failure. Name it here, in the log, before the gates run, so the log says what it is.
if (-not (Get-Command pwsh -ErrorAction SilentlyContinue)) {
    Write-Host '  [note] pwsh 7 is not installed; the gates that need it will say so and skip, not fail' -ForegroundColor Yellow
}

# ---------------------------------------------------------------- 3. push (no force, ever)
Step "pushing to GitHub"
Set-Location $repo
& '.\scripts\Push.ps1'
if ($LASTEXITCODE -ne 0) { Write-Host '  [warn] Push.ps1 did not finish -- the gates below still run' -ForegroundColor Yellow }

# ---------------------------------------------------------------- 4. the gates, into one file
Step "running the gates (2-4 minutes; this is the slow part)"
$log = Join-Path $repo 'gate-v130.5.log'

# Tools are looked up, not assumed: a missing dotnet must appear in the log as a line, not as a red
# command-not-found that kills the block before the other three gates run.
function Have ($name) { $null -ne (Get-Command $name -ErrorAction SilentlyContinue) }
function Try-Run ($label, $block) {
    Write-Output "--- $label"
    try { & $block 2>&1 | Write-Output } catch { Write-Output "    (could not run: $($_.Exception.Message))" }
}

& {
    '=== who am I ==='
    git -C $repo log --oneline -1
    git -C $repo status --short

    '=== CoreTests (expect: ALL TESTS PASSED) ==='
    if (Have dotnet) { Try-Run 'dotnet run tools\CoreTests' { dotnet run --project tools/CoreTests -c Release } }
    else { '    dotnet not on PATH -- install the .NET 8 SDK, or run: dotnet run --project tools\CoreTests' }

    '=== CompileCheck (expect: 0 Error(s)) ==='
    if (Have dotnet) { Try-Run 'dotnet build CompileCheck' { dotnet build tools/UnityStubs/CompileCheck.csproj -c Release } }

    '=== ArtPipeline (expect: ART PIPELINE OK) ==='
    Try-Run 'Test-ArtPipeline.ps1' { & '.\scripts\Test-ArtPipeline.ps1' }

    '=== PowerShell parses (expect: PASS) ==='
    if (Have python) { Try-Run 'check_powershell_syntax' { python tools/check_powershell_syntax.py } }
    else { '    python not on PATH -- skip, the art gate above already parses the scripts it uses' }

    '=== FreshClone (expect: PASS) ==='
    if (Have python) { Try-Run 'verify_fresh_clone' { python tools/verify_fresh_clone.py } }
} *>&1 | Tee-Object -FilePath $log

# ---------------------------------------------------------------- 5. the lines I actually need
Write-Host "`n=== THE LINES I NEED ===" -ForegroundColor Cyan
$bad = Select-String -Path $log -Pattern 'ALL TESTS PASSED|TEST\(S\) FAILED|error CS|Error\(s\)|ART PIPELINE OK|CHECK\(S\) FAILED|^\s*PASS:|^\s*FAIL|Exception' |
       ForEach-Object { $_.Line.Trim() } | Select-Object -Unique
$bad | ForEach-Object { Write-Host "  $_" }

Write-Host "`nLog written: $log" -ForegroundColor Cyan
Write-Host 'Send me that file (or just the lines above, pasted) and, if the room changed, a screenshot of Play mode.' -ForegroundColor Cyan
