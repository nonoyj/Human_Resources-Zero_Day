# ============================ HR//0D  v130.5  --  ONE COPY/PASTE ============================
# Download HR-ZeroDay-bundle-v130.5.zip into Downloads, then paste everything below into PowerShell.
#
# This block is self-checking, in this order, and it never prints [ok] for a check it could not run:
#     1. is the file the one I built        (sha256, spelled out -- not a name, not a size)
#     2. is it the right BUILD              (the archive must contain orders\RUN-v130.5.ps1)
#     3. does it extract
#     4. apply it, push it, run every gate, keep the log
# Any step that cannot be done says so in one sentence and stops. There is no path through this that prints
# [FAIL] underneath an [ok].
# ==========================================================================================
$ErrorActionPreference = 'Stop'
$name = 'HR-ZeroDay-bundle-v130.5.zip'
$want = '13b716c085d02eb6b47c205c642155732f9fac6369ea9d7d44c01d365e0c625e'
$base = if ($env:USERPROFILE) { $env:USERPROFILE } elseif ($env:HOME) { $env:HOME } else { '' }
if (-not $base) { Write-Host '  [FAIL] I cannot find your home folder, so I cannot find Downloads.' -ForegroundColor Red; exit 1 }
$zip   = Join-Path (Join-Path $base 'Downloads') $name
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
if ($got -ne $want) {
    Write-Host "  [FAIL] this is NOT the v130.5 bundle. Do not run it." -ForegroundColor Red
    Write-Host "         expected $want" -ForegroundColor Yellow
    Write-Host "         got      $got  ($($len.ToString('N0')) bytes)" -ForegroundColor Yellow
    Write-Host "         Delete $name and download it again: the download was incomplete, or it is an older file." -ForegroundColor Yellow
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
    Die "the archive has no HR-ZeroDay/orders/RUN-v130.5.ps1 -- it is a bundle from another round."
}
Ok "the handover script for v130.5 is inside it ($($names.Count) entries)"

# ---- 3. extract --------------------------------------------------------------------------
Step "3/4  extracting"
if (Test-Path -LiteralPath $stage) { Remove-Item -LiteralPath $stage -Recurse -Force }
Expand-Archive -LiteralPath $zip -DestinationPath $stage -Force
$handover = Join-Path $stage 'HR-ZeroDay\orders\RUN-v130.5.ps1'
if (-not (Test-Path -LiteralPath $handover)) { Die 'the archive extracted but the handover script is not there -- delete the zip and download it again.' }
Ok "extracted to $stage"

# ---- 4. hand over ------------------------------------------------------------------------
Step "4/4  applying, pushing, running the gates (2-4 minutes; this is the slow part)"
& $handover

# ============================== THEN LOOK AT THESE FIVE THINGS ==============================
# The gates are half the report. These five are the other half, and only your eyes can answer them:
#
#   1. NEW SHIFT    menu -> NEW SHIFT -> does a new day actually start on screen?
#   2. THE LAMP     menu: click the lamp (or press K) -> a warm ring pops at the shade.
#                   in game: press K or tap the lamp -> the mug, the forms, the keyboard and the wood change.
#   3. THE MONITOR  in game: press F or tap the glass -> the GLASS changes, not only the sides of the table.
#   4. THE MAN      does the applicant look different from last round? He should: the frames he was drawn
#                   from were weeks old and all 56 of them were re-cut in this bundle.
#   5. THE TITLE    is "HUMAN RESOURCES" whole at the top of the tube, or still cut?
#
# Send me gate-v130.5.log (written into your HR-ZeroDay folder) plus one screenshot of the menu and one of the
# desk. If something is wrong, the screenshot tells me more than the log.
# ==========================================================================================
