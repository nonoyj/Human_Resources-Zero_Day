$ErrorActionPreference = 'Stop'
$name = 'HR-ZeroDay-bundle-v130.2.zip'
$want = '090d9c4fcb8f6efa5aceb2bcce0be6e1a145442fdb0e04f8ef4148b68a1d6e2c'
$zip  = Join-Path (Join-Path $env:USERPROFILE 'Downloads') $name

if (-not (Test-Path -LiteralPath $zip)) {
    Write-Host "  [FAIL] $name is not in Downloads." -ForegroundColor Red
    Write-Host "         Files there now:" -ForegroundColor Yellow
    Get-ChildItem (Join-Path $env:USERPROFILE 'Downloads') -Filter '*HR-ZeroDay*' |
        ForEach-Object { Write-Host ("           {0}  {1:N0} bytes" -f $_.Name, $_.Length) }
    exit 1
}
$got = (Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash.ToLower()
Write-Host "  sha256 $got"
if ($got -ne $want) {
    Write-Host "  [FAIL] that is not the v130.2 bundle -- do not run it." -ForegroundColor Red
    Write-Host "         expected $want" -ForegroundColor Yellow
    Write-Host "         Delete $name and the old HR-ZeroDay-bundle-v130.zip, download again, run this." -ForegroundColor Yellow
    exit 1
}
Write-Host "  [ok]   hash matches the v130.2 bundle" -ForegroundColor Green

$stage = Join-Path $env:TEMP 'hrd-v130.2'
if (Test-Path -LiteralPath $stage) { Remove-Item -LiteralPath $stage -Recurse -Force }
Expand-Archive -LiteralPath $zip -DestinationPath $stage -Force
$handover = Join-Path $stage 'HR-ZeroDay\orders\RUN-v130.2.ps1'
if (-not (Test-Path -LiteralPath $handover)) { Write-Host "  [FAIL] no RUN-v130.2.ps1 in the extracted tree" -ForegroundColor Red; exit 1 }
& $handover
