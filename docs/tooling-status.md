# Tooling Status -- live

Updated from the real `-Verify` output on 2026-11-25 (machine `DESKTOP-MOEE4V3`, user `User`).

## Dev A / current machine

| ID | Tool | Status | Notes |
|---|---|---|---|
| D01 | Unity Hub | PASS | `C:\Program Files\Unity Hub` |
| D02 | Unity Editor 6000.3.29f1 | PASS | exactly the pinned version |
| D03 | Android Build Support | **FAIL** | module not installed |
| D04 | Android SDK / NDK / OpenJDK | **FAIL** | all three absent |
| D05 | Git | PASS | 2.56.1 |
| D06 | Git LFS | PASS | 3.8.1 |
| D07 | Visual Studio + Unity workload | PASS | VS 2022 17.14.45 with Unity workload |
| D09 | GitHub CLI | PASS | 2.105.0 (not yet `gh auth login`) |
| D10 | adb | **FAIL** | installed at `C:\Users\User\Android\platform-tools`; PATH change needs a new shell |
| D12 | Python | PASS | 3.13.17 via winget (PATH resolves to 3.12.10 first -- harmless, any 3.x works) |
| -- | LongPathsEnabled | **WARN** | must be 1 for deep Unity asset paths |

**Verdict: 7 of 10 green. Blocking item is ONE action: install the Android modules in Unity Hub.**

### Remaining actions, in order
1. **Unity Hub** > Installs > gear icon on `6000.3.29f1` > **Add modules** > tick
   `Android Build Support` + `Android SDK & NDK Tools` + `OpenJDK` > Install (~3 GB, 15-25 min).
2. **New PowerShell window**, then:
   `cd C:\users\user\HR-ZeroDay; .\scripts\Setup-DevEnvironment.ps1 -Verify`  -> expect 10 PASS
   (adb passes in a fresh shell; the script now refreshes PATH itself too).
3. **Long paths** (elevated PowerShell, one time):
   `Set-ItemProperty "HKLM:\SYSTEM\CurrentControlSet\Control\FileSystem" LongPathsEnabled 1`
4. `gh auth login` (HTTPS, browser) so pushes do not need a token prompt.
5. Push the repo to the team remote.
6. `Unity Hub > Preferences > Licenses` -> Personal licence if not already active.

## Other devs

| Dev | Machine | D02 editor | Android modules | Verify run |
|---|---|---|---|---|
| Dev A (systems) | DESKTOP-MOEE4V3 | PASS | pending step 1 | 2026-11-25 |
| Dev B (UI) | -- | ? | ? | not yet |
| Dev C (build/QA) | -- | ? | ? | not yet |
| Agent (art/docs) | sandboxed Linux | n/a | n/a | scripts parse-tested + dry-run |

Anyone whose editor is not exactly `6000.3.29f1` must reinstall before touching `unity/`.
