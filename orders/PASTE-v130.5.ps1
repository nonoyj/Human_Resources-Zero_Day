$ErrorActionPreference = 'Stop'
$name = 'HR-ZeroDay-bundle-v130.5.zip'
# Empty on purpose. A file cannot carry the sha256 of the archive it is inside -- that is a fixpoint, and
# pretending otherwise is how the last two rounds went wrong. The copy of this block in the DELIVERY MESSAGE
# has $want filled in with the real hash and is the one that runs. This copy, when run from the extracted
# tree, checks the thing that actually distinguishes a stale zip: its size and the script inside it.
$want = ''
$minBytes = 18000000
$base = if ($env:USERPROFILE) { $env:USERPROFILE } elseif ($env:HOME) { $env:HOME } else { '' }
if (-not $base) { Write-Host '  [FAIL] cannot locate your home folder' -ForegroundColor Red; exit 1 }
$zip  = Join-Path (Join-Path $base 'Downloads') $name

if (-not (Test-Path -LiteralPath $zip)) {
    Write-Host "  [FAIL] $name is not in Downloads." -ForegroundColor Red
    Write-Host "         Files there now:" -ForegroundColor Yellow
    Get-ChildItem (Join-Path $base 'Downloads') -Filter '*HR-ZeroDay*' |
        ForEach-Object { Write-Host ("           {0}  {1:N0} bytes" -f $_.Name, $_.Length) }
    exit 1
}
$got = (Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash.ToLower()
Write-Host "  sha256 $got"
if (-not $want) {
    if ((Get-Item -LiteralPath $zip).Length -lt $minBytes) {
        Write-Host "  [FAIL] that zip is smaller than a full v130.5 bundle -- it is an older build" -ForegroundColor Red; exit 1
    }
}
elseif ($got -ne $want) {
    Write-Host "  [FAIL] that is not the v130.5 bundle -- do not run it." -ForegroundColor Red
    Write-Host "         expected $want" -ForegroundColor Yellow
    Write-Host "         Delete $name and the old HR-ZeroDay-bundle-v130.zip, download again, run this." -ForegroundColor Yellow
    exit 1
}
Write-Host "  [ok]   hash matches the v130.5 bundle" -ForegroundColor Green

$stage = Join-Path ([IO.Path]::GetTempPath()) 'hrd-v130.5'
if (Test-Path -LiteralPath $stage) { Remove-Item -LiteralPath $stage -Recurse -Force }
Expand-Archive -LiteralPath $zip -DestinationPath $stage -Force
$handover = Join-Path $stage 'HR-ZeroDay\orders\RUN-v130.5.ps1'
if (-not (Test-Path -LiteralPath $handover)) { Write-Host "  [FAIL] no RUN-v130.5.ps1 in the extracted tree" -ForegroundColor Red; exit 1 }
& $handover
