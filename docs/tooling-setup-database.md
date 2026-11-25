# Tooling & Download Database (single source of truth)

**Owner:** you (the human) · **Machine:** `C:\users\user` · **Updated:** 2026-11-25
Every tool is verified before it goes in this table. Column meanings:

- **ID** — stable key used by `scripts/Setup-DevEnvironment.ps1` (`-Skip D07`).
- **Size** — from a live `Content-Length` check, not a guess.
- **Required** — `P0` blocks all work · `P1` blocks Android builds/QA · `P2` nice-to-have.
- **Status** — flip to `done` + date when installed.

> **Hard rule:** nothing in this file may be installed from an unofficial mirror. Every URL
> below resolves to a first-party vendor domain and was HEAD-checked on 2026-11-25.

---

## A. Required on ALL three machines (P0)

| ID | Tool | Version | Size | Official URL | Default target | Install method |
|---|---|---|---|---|---|---|
| D01 | **Unity Hub** | latest | 184.2 MB | `https://public-cdn.cloud.unity3d.com/hub/prod/UnityHubSetup-x64.exe` | `C:\Program Files\Unity Hub` | silent `/S`, then sign in |
| D02 | **Unity Editor** | **6000.3.29f1** | **4.21 GB** | Unity Hub → Installs → Archive (or direct: `https://download.unity3d.com/download_unity/96d2fe17529c/Windows64EditorInstaller/UnitySetup64-6000.3.29f1.exe`) | `C:\Program Files\Unity\Hub\Editor\6000.3.29f1` | Hub install w/ modules (do **not** side-load the .exe — the Hub won't manage it) |
| D03 | **Android Build Support** | bundled | 1.55 GB | installed *as a module of D02* in Hub | `...\Editor\6000.3.29f1\PlaybackEngines\AndroidPlayer` | Hub module tick-box |
| D04 | **Android SDK & NDK Tools + OpenJDK** | bundled | ~3 GB total | Hub module tick-boxes under D02 | `...\Editor\6000.3.29f1\PlaybackEngines\AndroidPlayer\{SDK,NDK,OpenJDK}` | Hub module — **never** install a separate JDK/SDK; Unity's versions are pinned |
| D05 | **Git for Windows** | ≥ 2.45 | 65 MB | `https://github.com/git-for-windows/git/releases/latest` | `C:\Program Files\Git` | winget `Git.Git` |
| D06 | **Git LFS** | ≥ 3.5 | 20 MB | bundled with D05 | — | `git lfs install` |
| D07 | **Visual Studio 2022 Community** | 17.10+ | 4–8 GB | `https://visualstudio.microsoft.com/vs/community/` | `C:\Program Files\Microsoft Visual Studio\2022\Community` | winget `Microsoft.VisualStudio.2022.Community` |
| D08 | **VS workloads** | — | ~3 GB | inside VS | — | `--add Microsoft.VisualStudio.Workload.ManagedGame` (Unity) + `.NetDesktop` |

**VS 2022 must have:** *Game development with Unity* + *.NET desktop development*. Verify with
`scripts/Setup-DevEnvironment.ps1 -Verify` (checks `vswhere`).

## B. Required on ONE machine (the build machine — Dev C)

| ID | Tool | Version | Size | Official URL | Target | Notes |
|---|---|---|---|---|---|---|
| D09 | **GitHub CLI** | ≥ 2.50 | 15 MB | `https://cli.github.com/` | `C:\Program Files\GitHub CLI` | `gh auth login` once; `gh` is faster than the web UI |
| D10 | **Android platform-tools (adb)** | latest | 15 MB | `https://developer.android.com/tools/releases/platform-tools` → `platform-tools-latest-windows.zip` | `C:\Android\platform-tools` | add to PATH; needed for sideloading the demo APK |
| D11 | **scrcpy** | ≥ 2.0 | 20 MB | `https://github.com/Genymobile/scrcpy/releases` | `C:\Tools\scrcpy` | mirror the phone to a laptop at the event booth — use for *demoing on a big screen* |

## C. P2 — optional, useful

| ID | Tool | Purpose | URL |
|---|---|---|---|
| D12 | **Python 3.13** | run `prototype/generator_sim.py` (the fairness simulator) | `https://www.python.org/downloads/` — tick *Add python.exe to PATH* |
| D13 | **VS Code** | edit JSON content, docs, handoffs | `https://code.visualstudio.com/` |
| D14 | **LibreSprite** | touch up pixel art | `https://libresprite.github.io/` |
| D15 | **Aseprite** | paid, better; only if someone already owns it | `https://www.aseprite.org/` |
| D16 | **OBS Studio** | record playtests for the retrospective | `https://obsproject.com/` |
| D17 | **7-Zip** | opening the SDK/NDK zips if Hub fails | `https://www.7-zip.org/` |

## D. NEVER install / never commit

| Item | Why |
|---|---|
| A second JDK or a global `JAVA_HOME` | Unity's OpenJDK is pinned; a stray JDK breaks Gradle |
| Android Studio's SDK for the build | version skew vs Unity's bundled SDK → unexplained build failures |
| Anything from a "Unity download mirror" site | malware; D02 is only safe via Hub or `download.unity3d.com` |
| `keystore.jks` **into the repo** | signing keys stay in a password manager + `C:\keys\hrzeroday\` |
| `Library/`, `Temp/`, `*.apk` **into git** | repo bloat, merge hell |

---

## E. The one command you run (does D01, D05, D06, D07, D09, D12, D13)

```powershell
# from the repo root, in PowerShell 7 or Windows PowerShell 5.1
Set-ExecutionPolicy -Scope Process -ExecutionPolicy Bypass -Force
.\scripts\Setup-DevEnvironment.ps1
```

Then the two things a script **cannot** do for you (they need GUI sign-in / licensing):

1. Open **Unity Hub** → sign in → *Installs* → *Install Editor* → **Archive** → search
   **6000.3.29f1** → tick modules:
   - ✅ **Android Build Support**
   - ✅ **Android SDK & NDK Tools**
   - ✅ **OpenJDK**
   - ✅ **Windows Build Support (IL2CPP)**
   - ❌ everything else (saves ~10 GB and ~25 min)
2. Accept the Unity Personal licence in Hub → *Preferences → Licenses*.

## F. Verify everything (run after the two GUI steps)

```powershell
.\scripts\Setup-DevEnvironment.ps1 -Verify
```

Expected output — ten green `PASS` lines. Anything `FAIL` must be fixed before you clone the
Unity project, because a version mismatch corrupts `Library/` and costs a whole day.

## G. Where things go

```
C:\users\user\
├─ HR-ZeroDay\                 <- clone of the git repo (everything below lives here)
│  ├─ docs\  handoff\  art\  content\  prototype\  scripts\  unity\
├─ Downloads\                  <- installers land here; safe to clear after install
├─ Tools\scrcpy\
├─ Android\platform-tools\
└─ keys\hrzeroday\             <- keystore, NEVER inside the repo
```

## H. Install status

| ID | Machine (owner) | Installed | Date | Verified |
|---|---|---|---|---|
| D01 | | ☐ | | ☐ |
| D02 | | ☐ | | ☐ |
| D03 | | ☐ | | ☐ |
| D04 | | ☐ | | ☐ |
| D05 | | ☐ | | ☐ |
| D07 | | ☐ | | ☐ |
| D09 | Build machine only | ☐ | | ☐ |
| D10 | Build machine only | ☐ | | ☐ |
| D11 | Event laptop | ☐ | | ☐ |
| D12 | | ☐ | | ☐ |

## I. Troubleshooting (add to this table whenever it bites someone)

| Symptom | Cause | Fix |
|---|---|---|
| Hub download fails "validation failed" | CDN cache or proxy | Retry; or download D02 direct URL and install via Hub's manual path |
| Build error `Gradle build failed ... JDK` | stray `JAVA_HOME`/JDK | `setx JAVA_HOME ""` then delete the installer JDK; let Unity use its own |
| `Gradle ... SDK not found` | Android SDK module not ticked | Hub → 6000.3.29f1 → *Add modules* → Android SDK & NDK Tools |
| Editor opens project but every script shows errors | VS missing Unity workload | Add workload D08, then *Assets → Open C# Project* |
| `git lfs` files show as pointer text | LFS not installed before clone | `git lfs install` then `git lfs pull` |
| Build takes > 20 min | stripped/IL2CPP + first build | normal on first IL2CPP build; subsequent ~4 min |
