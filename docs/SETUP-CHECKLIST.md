# Setup Checklist — do these in order, tick as you go

Machine: Windows · user folder `C:\users\user` · target repo `C:\users\user\HR-ZeroDay`
Full detail + verified URLs: `docs/tooling-setup-database.md`

---

## Step 0 — 30 seconds, admin check

Open **PowerShell** (Start → type `powershell` → Enter) and run:

```powershell
$PSVersionTable.PSVersion
winget --version
```

- PowerShell **5.1+** — fine as-is.
- `winget` missing → install **App Installer** from the Microsoft Store, reopen PowerShell.

## Step 1 — Allow scripts for this session only

```powershell
Set-ExecutionPolicy -Scope Process -ExecutionPolicy Bypass -Force
```

> `-Scope Process` means it resets when you close the window. It does **not** lower your
> machine's security permanently. Do this in every new window you use the scripts from.

## Step 2 — Get the repo (so the scripts exist locally)

```powershell
cd C:\users\user
git clone https://github.com/nonoyj/Human_Resources-Zero_Day HR-ZeroDay
cd HR-ZeroDay
```

If git isn't installed yet, do **Step 3 first**, then come back here.

## Step 3 — Install the tooling

```powershell
.\scripts\Setup-DevEnvironment.ps1
```

Installs: Unity Hub, Git + LFS, Visual Studio 2022 (Unity + .NET workloads), GitHub CLI,
Android platform-tools (adb), scrcpy, Python 3.13, VS Code. Downloads land in
`C:\users\user\Downloads`.

## Step 4 — The two things a script cannot do (≈ 30–40 min, mostly download)

1. Open **Unity Hub** → **Sign in** (use the team's shared account, and tell the others the login).
2. **Installs → Install Editor → Archive** → search **`6000.3.29f1`** → **Install**. Tick:
   - ✅ Android Build Support
   - ✅ Android SDK & NDK Tools
   - ✅ OpenJDK
   - ✅ Windows Build Support (IL2CPP)
   - ❌ everything else (saves ~10 GB)

   Install path: keep the default `C:\Program Files\Unity\Hub\Editor\6000.3.29f1`.
3. **Hub → Preferences (gear) → Licenses → Add → Get a free personal license.**

## Step 5 — Verify (this is the gate; don't skip)

Close and reopen PowerShell (so PATH changes apply), then:

```powershell
cd C:\users\user\HR-ZeroDay
Set-ExecutionPolicy -Scope Process -ExecutionPolicy Bypass -Force
.\scripts\Setup-DevEnvironment.ps1 -Verify
```

You want **ten `PASS` lines and “All checks passed.”** Anything `FAIL` → `docs/tooling-setup-database.md`
section I. The three most common:

| Symptom | Fix |
|---|---|
| `D02 FAIL ... found: 6000.6.x` | You installed the wrong editor stream. Install 6000.3.29f1 from *Archive* |
| `D04 FAIL SDK=False` | Hub → Installs → gear on 6000.3.29f1 → *Add modules* → Android SDK & NDK Tools + OpenJDK |
| `D07 FAIL ... Unity workload missing` | Open VS Installer → Modify → tick *Game development with Unity* |

Then run the workspace setup (folders + LFS + git identity + download folder contract):

```powershell
.\scripts\setup-workspace.ps1
```

## Step 6 — Put files in the right places

| What | Goes where |
|---|---|
| Repo (this project) | `C:\users\user\HR-ZeroDay` |
| Installers (Hub, VS...) | `C:\users\user\Downloads` — auto, clear after install |
| Android `adb` | `C:\users\user\android\platform-tools` — auto, already on PATH |
| Phone mirroring | `C:\users\user\Tools\scrcpy` |
| **Android keystore** (created later, Day 12) | `C:\users\user\keys\hrzeroday` — **never in the repo, never in git** |
| Unity Editor | `C:\Program Files\Unity\Hub\Editor\6000.3.29f1` (default; don't change) |
| Unity projects | inside the repo at `HR-ZeroDay\unity` (created on Day 1–2) |

## Step 7 — First contact with the Unity project

The Unity project folder doesn't exist yet (Day 1–2 task for Dev A/C — I'll generate it or one of
you creates it). When it appears:

```powershell
cd C:\users\user\HR-ZeroDay
git pull
```

Then Unity Hub → **Add → Add project from disk** → `C:\users\user\HR-ZeroDay\unity` →
open with **6000.3.29f1**. First open takes 5–15 minutes (asset import). **Never** commit the
`Library/` folder it creates.

## Step 8 — Phone setup (for testing; build machine / Dev C especially)

1. On the Android phone: Settings → About phone → tap **Build number** 7× → Developer options.
2. Enable **USB debugging**. Connect via USB, accept the "Allow USB debugging?" prompt.
3. In PowerShell:

```powershell
adb devices           # should list your phone as "device"
adb install -r path\to\HR-ZeroDay.apk
scrcpy                # mirrors the phone to your laptop (great for the event screen)
```

## Step 9 — Daily rhythm from now on

```powershell
cd C:\users\user\HR-ZeroDay
git pull
git checkout -b feat/your-task
# ... work ...
git add -A
git commit -m "feat(desk): applicant panel layout"
git push -u origin feat/your-task
```

Rules you must not break: never commit `Library/`, `Temp/`, `*.apk`, or the keystore; one owner per
scene; write a handoff file when you hand work to someone. Full protocol: `docs/04-collaboration-protocol.md`.

## Step 10 — Tell me the result

Paste back the output of the `-Verify` run (or just the summary table). I'll update
the `-Verify` output and hand out the Day-1 tasks. If any step failed, give me the exact
error text and I'll fix the script or the guide.
