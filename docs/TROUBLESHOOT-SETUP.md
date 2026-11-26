# Fix: `.\scripts\Setup-DevEnvironment.ps1 is not recognized`

**What the error actually means:** PowerShell couldn't find the file *relative to the folder you
were standing in*. Nothing is broken, nothing is installed wrongly. It is almost always one of
three causes:

| Cause | How to confirm |
|---|---|
| **A.** You never extracted the bundle, so `HR-ZeroDay` doesn't exist on disk | `Test-Path C:\users\user\HR-ZeroDay` → `False` |
| **B.** You extracted it but the folder is **nested** (`HR-ZeroDay\HR-ZeroDay-bundle\docs\...`) | the search in Step 2 finds the file several levels deeper than expected |
| **C.** You were in a different directory (`C:\users\user` instead of `C:\users\user\HR-ZeroDay`) | `Get-Location` shows something else |

---

## Step 1 — Find out where you are and what exists

```powershell
Get-Location
Test-Path C:\users\user\HR-ZeroDay
```

## Step 2 — Find the script wherever it landed (this is the reliable way)

```powershell
Get-ChildItem C:\users\user -Recurse -Filter Setup-DevEnvironment.ps1 -ErrorAction SilentlyContinue |
  Select-Object -ExpandProperty FullName
```

Three possible outcomes — act on the one you get:

### Outcome 1: it prints nothing
The bundle isn't extracted yet.

1. Download `HR-ZeroDay-bundle-v2-with-git.zip` from the workspace file viewer.
2. In File Explorer, right-click the zip → **Extract All…** → type `C:\users\user\` → **Extract**.
3. Check: `Test-Path C:\users\user\HR-ZeroDay\scripts\Setup-DevEnvironment.ps1` must be `True`.
4. Continue to **Step 3**.

### Outcome 2: it prints `C:\users\user\HR-ZeroDay\scripts\Setup-DevEnvironment.ps1`
You're just in the wrong folder.

```powershell
cd C:\users\user\HR-ZeroDay
```
Then **Step 3**.

### Outcome 3: it prints a longer path, e.g. `C:\users\user\HR-ZeroDay\HR-ZeroDay-bundle\scripts\...`
Nested folder. Flatten it:

```powershell
$src = "C:\users\user\HR-ZeroDay\HR-ZeroDay-bundle"
if (Test-Path $src) {
    Copy-Item "$src\*" "C:\users\user\HR-ZeroDay\" -Recurse -Force
    Remove-Item $src -Recurse -Force
}
Test-Path C:\users\user\HR-ZeroDay\scripts\Setup-DevEnvironment.ps1   # must be True
cd C:\users\user\HR-ZeroDay
```
Then **Step 3**.

## Step 3 — Run setup (copy-paste the whole block, it must be run from the repo root)

```powershell
cd C:\users\user\HR-ZeroDay
Set-ExecutionPolicy -Scope Process -ExecutionPolicy Bypass -Force
Get-ChildItem .\scripts
.\scripts\Setup-DevEnvironment.ps1
```

The `Get-ChildItem .\scripts` line is a deliberate sanity check: it must list
`Setup-DevEnvironment.ps1` and `setup-workspace.ps1` **before** you run anything. If that listing is
empty, stop and go back to Step 2.

### If it now says `cannot be loaded because running scripts is disabled`
You skipped the `Set-ExecutionPolicy` line, or you opened a new window (Process scope resets).
Run it again in the same window, then re-run the script.

### If it says `You must run this from the repo root` style errors, or paths look wrong
Confirm `Get-Location` prints exactly `C:\users\user\HR-ZeroDay`.

---

## Step 4 — Unity Hub (manual, ~30–40 min mostly download)

1. Open **Unity Hub** → sign in.
2. **Installs** → **Install Editor** → **Archive** tab → search `6000.3.29f1` → **Install**.
3. Tick exactly: **Android Build Support** ✅ (its children **Android SDK & NDK Tools** ✅,
   **OpenJDK** ✅) and **Windows Build Support (IL2CPP)** ✅. Everything else ❌.
4. Accept the default install path.
5. **Preferences** (gear) → **Licenses** → **Add** → **Get a free personal license**.

## Step 5 — Verify (close and reopen PowerShell first, so PATH refreshes)

```powershell
cd C:\users\user\HR-ZeroDay
Set-ExecutionPolicy -Scope Process -ExecutionPolicy Bypass -Force
.\scripts\Setup-DevEnvironment.ps1 -Verify
```

Expected: a table of **10 rows, all `PASS`**, then `All checks passed.`
Then:

```powershell
.\scripts\setup-workspace.ps1
```

## Step 6 — Push to GitHub (already done 2026-11-25; from here use `git push` / `git pull`, see `docs/BUNDLE-WORKFLOW.md`)

```powershell
cd C:\users\user\HR-ZeroDay
git remote -v
git remote add origin https://github.com/nonoyj/Human_Resources-Zero_Day.git
git branch -M main
git push -u origin main --force
```

`--force` here is intentional **only if the remote is empty or you want my history to replace it**.
If the remote already has commits you care about, use `git push -u origin main` without `--force`
and tell me — we'll merge instead.

---

## Paste-back request

If anything still fails, send me:

```powershell
Get-Location
Get-ChildItem C:\users\user | Select-Object Name, Mode
Get-ChildItem C:\users\user\HR-ZeroDay -ErrorAction SilentlyContinue
```

plus the exact red error text. That's enough for me to pinpoint it in one pass.

---

## Known fixed issue: parse errors on the first run (BUG-001)

If you ever see:

```
Unexpected token '", 'User')
The string is missing the terminator: ".
    + CategoryInfo : ParserError: (:) [], ParseException
```

**Cause:** the `.ps1` file had non-ASCII characters (em-dashes) and Windows PowerShell 5.1 decoded it
as ANSI. Fixed on 2026-11-25 — all scripts are now ASCII-only with a UTF-8 BOM.

**What to do:**
1. Re-extract the latest bundle (`HR-ZeroDay-bundle-v2-with-git.zip`) or `git pull`.
2. Confirm the file is healthy before running it:

```powershell
cd C:\users\user\HR-ZeroDay
.\scripts\Test-Scripts.ps1
```

Expect `PASS` for all three scripts, then `All scripts healthy.` If you see `FAIL (encoding)`, the file
is still the old copy — you are in a stale folder; check `Get-Location`.
