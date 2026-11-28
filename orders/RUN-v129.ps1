<#
.SYNOPSIS
    v129, one paste: verify the bundle, extract it, apply it, push, and capture every gate to a log.

.DESCRIPTION
    Run this from anywhere (PowerShell 5.1 or 7). It does NOT need the repo to be healthy first.

    What it leaves behind for me:
        <repo>\gate-v129.log     every gate's full output, plus the ERROR/FAIL lines at the end

    Change $zip / $repo below if your paths differ. Nothing else needs editing.
#>
$ErrorActionPreference = 'Stop'

$zip  = Join-Path $env:USERPROFILE 'Downloads\HR-ZeroDay-bundle-v129.zip'
$repo = Join-Path $env:USERPROFILE 'HR-ZeroDay'
$stage = Join-Path ([IO.Path]::GetTempPath()) 'hrd-v129'

function Step ($m) { Write-Host "`n==> $m" -ForegroundColor Cyan }
function Ok   ($m) { Write-Host "  [ok]   $m" -ForegroundColor Green }
function Die  ($m) { Write-Host "  [FAIL] $m" -ForegroundColor Red; exit 1 }

# ---------------------------------------------------------------- 0. the bundle is the one I built
Step "checking the bundle"
if (-not (Test-Path -LiteralPath $zip)) { Die "bundle not found: $zip  (put the v129 zip in Downloads, or edit `$zip)" }
# The zip contains this very file, so it cannot contain its own final hash -- that is why this is a WARN
# and not a stop. The hash the delivery quoted is the one to eyeball; anything else means a partial copy.
$want = '8C97AA299E501F4B75566B25F12E3E4E9D50ECE99EA74460AEA118AB420C22E5'
$got  = (Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash
if ($got -ne $want) { Write-Host "  [warn] sha256 $($got.Substring(0,16))... -- the delivery quoted $($want.Substring(0,16))..., re-download if the zip looks truncated" -ForegroundColor Yellow }
else { Ok "sha256 matches ($($got.Substring(0,16))...)" }

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
if ($LASTEXITCODE -ne 0) { Die "Apply-Bundle returned $LASTEXITCODE" }
Ok 'applied'

# ---------------------------------------------------------------- 3. push (no force, ever)
Step "pushing to GitHub"
Set-Location $repo
& '.\scripts\Push.ps1'
if ($LASTEXITCODE -ne 0) { Write-Host '  [warn] Push.ps1 did not finish -- the gates below still run' -ForegroundColor Yellow }

# ---------------------------------------------------------------- 4. the gates, into one file
Step "running the gates (2-4 minutes; this is the slow part)"
$log = Join-Path $repo 'gate-v129.log'

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
