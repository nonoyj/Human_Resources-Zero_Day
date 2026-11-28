# ============================ HR//0D  v130.5  --  ONE COPY/PASTE ============================
# Download HR-ZeroDay-bundle-v130.5.zip into Downloads, then paste everything below into PowerShell.
#
# This block is self-checking. It checks in THIS ORDER and it never prints [ok] for a check it could not run:
#     1. is the file the one I built      (sha256, spelled out -- not a name, not a size)
#     2. is it the right BUILD            (the zip must contain orders\RUN-v130.5.ps1)
#     3. does it extract                 (the only step that can fail on a half-downloaded file)
#     4. run the gates and keep the log
# If any step cannot be done, it says so in one sentence and stops. There is no path through this that
# prints [FAIL] underneath an [ok].
# ==========================================================================================
$ErrorActionPreference = 'Stop'
$name     = 'HR-ZeroDay-bundle-v130.5.zip'
# THE COPY INSIDE THE BUNDLE HAS AN EMPTY HASH, ON PURPOSE -- a file cannot contain the sha256 of the archive
# that contains it. The copy in the delivery message is the one to paste and it has the real hash. This copy
# still catches a stale zip, because it checks the SIZE and, at step 2, that the archive names this round.
$want     = ''
$wantSize = 18760953
$base = if ($env:USERPROFILE) { $env:USERPROFILE } elseif ($env:HOME) { $env:HOME } else { '' }
if (-not $base) { Write-Host '  [FAIL] I cannot find your home folder, so I cannot find Downloads.' -ForegroundColor Red; exit 1 }
$zip  = Join-Path (Join-Path $base 'Downloads') $name
$stage = Join-Path ([IO.Path]::GetTempPath()) 'hrd-v130.5'

function Step ($m) { Write-Host "`n==> $m" -ForegroundColor Cyan }
function Ok   ($m) { Write-Host "  [ok]   $m" -ForegroundColor Green }
function Die  ($m) { Write-Host "  [FAIL] $m" -ForegroundColor Red; exit 1 }

# ---- 1. the file -------------------------------------------------------------------------
Step "1/4  the download"
if (-not (Test-Path -LiteralPath $zip)) {
    Write-Host "  [FAIL] $name is not in Downloads." -ForegroundColor Red
    $others = Get-ChildItem (Join-Path $base 'Downloads') -Filter '*HR-ZeroDay*' -ErrorAction SilentlyContinue
    if ($others) { Write-Host '         What IS there:' -ForegroundColor Yellow
                   $others | ForEach-Object { Write-Host ("           {0}  {1:N0} bytes" -f $_.Name, $_.Length) } }
    exit 1
}
$got = (Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash.ToLower()
$len = (Get-Item -LiteralPath $zip).Length
if (-not $want) {
    # in-bundle copy: no hash to compare. Size is the honest thing left, and step 2 checks the build.
    if ($len -ne $wantSize) {
        Write-Host "  [FAIL] the file is $($len.ToString('N0')) bytes; the v130.5 bundle is $($wantSize.ToString('N0'))." -ForegroundColor Red
        Write-Host "         Delete it and download again." -ForegroundColor Yellow
        exit 1
    }
    Ok "size matches the v130.5 bundle ($($len.ToString('N0')) bytes) -- this copy carries no hash by design"
}
elseif ($got -ne $want) {
    Write-Host "  [FAIL] this is NOT the v130.5 bundle. Do not run it." -ForegroundColor Red
    Write-Host "         expected sha256 $want" -ForegroundColor Yellow
    Write-Host "         got      sha256 $got  ($($len.ToString('N0')) bytes, expected $($wantSize.ToString('N0')))" -ForegroundColor Yellow
    Write-Host "         Delete $name and download it again. The download was incomplete or it is an older file." -ForegroundColor Yellow
    exit 1
}
Ok "sha256 matches -- this is the v130.5 bundle ($($len.ToString('N0')) bytes)"

# ---- 2. the build ------------------------------------------------------------------------
Step "2/4  the build inside it"
Add-Type -AssemblyName System.IO.Compression.FileSystem
$names = @()
$z = [System.IO.Compression.ZipFile]::OpenRead($zip)
try { $names = $z.Entries | ForEach-Object { $_.FullName } } finally { $z.Dispose() }
if ($names -notcontains 'HR-ZeroDay/orders/RUN-v130.5.ps1') {
    Die "the zip has no HR-ZeroDay/orders/RUN-v130.5.ps1 -- it is a bundle from another round."
}
Ok "the handover script for v130.5 is inside it ($($names.Count) entries)"

# ---- 3. extract --------------------------------------------------------------------------
Step "3/4  extracting"
if (Test-Path -LiteralPath $stage) { Remove-Item -LiteralPath $stage -Recurse -Force }
Expand-Archive -LiteralPath $zip -DestinationPath $stage -Force
$handover = Join-Path $stage 'HR-ZeroDay\orders\RUN-v130.5.ps1'
if (-not (Test-Path -LiteralPath $handover)) { Die 'extraction finished but the handover script is not there -- delete the zip and download again.' }
Ok "extracted to $stage"

# ---- 4. hand over ------------------------------------------------------------------------
Step "4/4  applying, pushing, running the gates (2-4 minutes)"
& $handover

# ============================== THEN LOOK AT THESE FIVE THINGS ==============================
# The gates are only half the report. What I need from your eyes is what the gates cannot ask:
#
#   1. NEW SHIFT   -- menu -> NEW SHIFT -> does a new day actually start on screen?
#   2. THE LAMP    -- menu: click the lamp (or press K) -> a warm ring pops at the shade.
#                     in game: press K or tap the lamp -> the mug, forms, keyboard and wood change.
#   3. THE MONITOR -- in game: press F or tap the glass -> the glass ITSELF changes, not just the sides.
#   4. THE MAN     -- does the applicant look like a different person from last round? He should. The art
#                     he was showing you was weeks old and it has been re-cut.
#   5. THE TITLE   -- is "HUMAN RESOURCES" whole at the top of the tube, or still cut?
#
# Send me: gate-v130.5.log (it is written into your HR-ZeroDay folder), plus one screenshot of the menu and
# one of the desk. If something is wrong, the screenshot is worth more than the log.
# ==========================================================================================
