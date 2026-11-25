# Downloads — what to fetch, from where, and where it goes

Two kinds of downloads: **(A) the project bundle** (once) and **(B) tool installers**
(per machine). Every URL in B is first-party and was HEAD-checked on 2026-11-25; sizes are real.

---

## A. Project bundle

| Item | Where it comes from | Where it goes |
|---|---|---|
| `HR-ZeroDay-repo.zip` (workspace file) | download from the workspace file viewer | extract so that the folder contains `README.md`, `docs\`, `scripts\`, `prototype\` directly → `C:\users\user\HR-ZeroDay` |
| The same content via git | `git clone https://github.com/nonoyj/Human_Resources-Zero_Day HR-ZeroDay` | same path (use whichever arrives first; the zip also carries the commit history) |

After extracting, run:

```powershell
cd C:\users\user\HR-ZeroDay
Set-ExecutionPolicy -Scope Process -ExecutionPolicy Bypass -Force
.\scripts\Setup-DevEnvironment.ps1        # installs B1-B8
.\scripts\Setup-DevEnvironment.ps1 -Verify
.\scripts\setup-workspace.ps1             # folders, LFS, git identity, desktop shortcut
```

---

## B. Tool installers (the download database)

### B1 — Required on all 3 machines

| ID | Tool | Size | URL | Install |
|---|---|---|---|---|
| D01 | Unity Hub | 184.2 MB | https://public-cdn.cloud.unity3d.com/hub/prod/UnityHubSetup-x64.exe | `/S` silent; then sign in |
| D02 | **Unity Editor 6000.3.29f1** | **4.21 GB** | Unity Hub → Installs → **Archive** → `6000.3.29f1` (direct: https://download.unity3d.com/download_unity/96d2fe17529c/Windows64EditorInstaller/UnitySetup64-6000.3.29f1.exe) | Hub install, keep default path |
| D03 | Android Build Support | 1.55 GB | Hub module tick-box on D02 | Hub only — do not side-load |
| D04 | Android SDK & NDK Tools + OpenJDK | ~3 GB | Hub module tick-boxes on D02 | Hub only — a separate JDK **will** break Gradle |
| D05 | Git for Windows | 65 MB | https://github.com/git-for-windows/git/releases/latest | `winget install Git.Git` |
| D06 | Git LFS | 20 MB | bundled with D05 | `git lfs install` |
| D07 | Visual Studio 2022 Community | 4–8 GB | https://visualstudio.microsoft.com/vs/community/ | `winget install Microsoft.VisualStudio.2022.Community` |
| D08 | VS workloads: *Game development with Unity* + *.NET desktop* | ~3 GB | VS Installer | `-Verify` checks this (D07 PASS) |

### B2 — Build machine (Dev C) and event laptop

| ID | Tool | Size | URL | Why |
|---|---|---|---|---|
| D09 | GitHub CLI | 15 MB | https://cli.github.com/ | push/PR without the web UI |
| D10 | Android platform-tools (adb) | 8.2 MB | https://dl.google.com/android/repository/platform-tools-latest-windows.zip | sideload the APK → `C:\users\user\Android\platform-tools` |
| D11 | scrcpy ≥ 2.0 | 20 MB | https://github.com/Genymobile/scrcpy/releases | mirror the phone to a projector at the booth |

### B3 — Optional (P2)

| ID | Tool | URL |
|---|---|---|
| D12 | Python 3.13 (runs `prototype/generator_sim.py`) | https://www.python.org/downloads/ |
| D13 | VS Code | https://code.visualstudio.com/ |
| D14 | LibreSprite / D15 Aseprite | https://libresprite.github.io/ · https://www.aseprite.org/ |
| D16 | OBS Studio (record playtests) | https://obsproject.com/ |
| D17 | 7-Zip | https://www.7-zip.org/ |

### B4 — Never

A second JDK / global `JAVA_HOME` · Android Studio's SDK for Unity builds · "Unity mirror" sites ·
keystore inside the repo · `Library/` or `*.apk` in git.

Full troubleshooting table: `docs/tooling-setup-database.md` §I.
