# ORDER 02 -- THE REPORT FORMAT, AND THE POWERSHELL HANDOVER

## The headings, defined

```
## What I did
```
**= what the player must do, written as instructions, including the PowerShell.** It is the deliverable. It is
not a diary of my edits; my edits appear in `What to expect` and in the commit message.

```
## What you did
```
**= what the player did**: their instructions and corrections this round, in their own terms.

```
## What to expect
```
**= what they will see and where, plus what is deliberately not done.**

```
## What's next
```
**= ordered next steps, smallest first.**

Missing any of the four = incomplete delivery. This has been missed twice and refused twice.

## The PowerShell block -- mandatory every time

It must, in one paste: fetch the bundle from the repo, extract it to a versioned folder, apply it with a
reinstall, push, and **run the gates with the output saved to a file the player can send back**.

```powershell
# ---- HR//0D vNNN: extract, apply, push, and capture the gate log -------------------------------
$ErrorActionPreference = 'Stop'
$repo = 'C:\path\to\Human_Resources-Zero_Day'      # <-- the player's clone
Set-Location $repo

# 1. the bundle (already committed in the repo, so a pull is all that is needed)
git pull --ff-only

# 2. extract beside the repo, never on top of it
$zip = Join-Path $repo 'HR-ZeroDay-bundle-vNNN.zip'
$dir = Join-Path $repo 'hrd-vNNN'
if (Test-Path $dir) { Remove-Item $dir -Recurse -Force }
Expand-Archive -LiteralPath $zip -DestinationPath $dir

# 3. apply it (Reinstall = the bundle is the whole tree, not a patch)
Copy-Item (Join-Path $dir 'Apply-Bundle.ps1') . -Force
.\Apply-Bundle.ps1 -BundlePath $zip -Reinstall

# 4. push (fast-forward only; it will stop rather than overwrite)
.\scripts\Push.ps1

# 5. the gates, captured in one file
$log = Join-Path $repo 'gate-vNNN.log'
& {
  '=== CoreTests ===';        dotnet run --project tools\CoreTests -c Release
  '=== CompileCheck ===';     dotnet build tools\UnityStubs\CompileCheck.csproj -c Release
  '=== ArtPipeline ===';      .\scripts\Test-ArtPipeline.ps1
  '=== FreshClone ===';       python tools\verify_fresh_clone.py
} *>&1 | Tee-Object -FilePath $log
Write-Host ''
Write-Host "Send back: $log" -ForegroundColor Cyan
Select-String -Path $log -Pattern 'FAIL|ERROR|error CS|Exception' | ForEach-Object { $_.Line }
```

## What the player sends back

1. `gate-vNNN.log` -- the whole file.
2. The **ERROR lines** the block prints at the end (it prints them, so they are already in the file and in the
   console).
3. A screenshot of the running game, if the round changed art.

The reply must say this explicitly, in `What I did`, with the file name.
