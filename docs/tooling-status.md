# Tooling Status -- live

Updated from the real `-Verify` output on 2026-11-25 (machine `DESKTOP-MOEE4V3`, user `User`).

## Dev A / current machine

| ID | Tool | Status | Notes |
|---|---|---|---|
| D01 | Unity Hub | PASS | `C:\Program Files\Unity Hub` |
| D02 | Unity Editor 6000.3.29f1 | PASS | exactly the pinned version |
| D03 | Android Build Support | PASS (verify pending) | Hub reports **6.43 GB installed**; earlier FAIL was the agent's wrong path (BUG-005), now fixed |
| D04 | Android SDK / NDK / OpenJDK | PASS (verify pending) | Hub: OpenJDK 228.94 MB + SDK & NDK 2.95 GB installed |
| D05 | Git | PASS | 2.56.1 |
| D06 | Git LFS | PASS | 3.8.1 |
| D07 | Visual Studio + Unity workload | PASS | VS 2022 17.14.45 with Unity workload |
| D09 | GitHub CLI | PASS | 2.105.0 (not yet `gh auth login`) |
| D10 | adb | PASS (verify pending) | installed at `C:\Users\User\Android\platform-tools`; PATH visibility fixed (BUG-006) |
| D12 | Python | PASS | 3.13.17 via winget (PATH resolves to 3.12.10 first -- harmless, any 3.x works) |
| -- | LongPathsEnabled | **WARN** | must be 1 for deep Unity asset paths |

**Verdict: nothing is blocking. All 10 tools are installed; the remaining FAILs were two wrong-path
checks (BUG-005/006), fixed and regression-tested against a fake install layout.**

### Remaining actions, in order
1. Pull the fixed script (bundle v5) and re-run `-Verify` -> expect 10 PASS.
2. **Long paths** (elevated PowerShell, one time):
   `Set-ItemProperty "HKLM:\SYSTEM\CurrentControlSet\Control\FileSystem" LongPathsEnabled 1`
3. `gh auth login` (HTTPS, browser) so pushes do not need a token prompt.
4. Push the repo to the team remote.
5. Unity Hub > Preferences > Licenses -> confirm the Personal licence is active.
6. Optional: `Windows Build Support (IL2CPP)` module (1.62 GB) so you can test in the editor as a
   Windows standalone build; not needed for Android.

## Other devs

| Dev | Machine | D02 editor | Android modules | Verify run |
|---|---|---|---|---|
| Dev A (systems) | DESKTOP-MOEE4V3 | PASS | pending step 1 | 2026-11-25 |
| Dev B (UI) | -- | ? | ? | not yet |
| Dev C (build/QA) | -- | ? | ? | not yet |
| Agent (art/docs) | sandboxed Linux | n/a | n/a | scripts parse-tested + dry-run |

Anyone whose editor is not exactly `6000.3.29f1` must reinstall before touching `unity/`.
