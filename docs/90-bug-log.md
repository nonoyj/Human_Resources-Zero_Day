# Bug Log

Every non-trivial bug gets an entry. Format is mandatory — the **Prevention rule** line is
what stops us from repeating problems.

```
### BUG-000 — <one-line symptom>
- **Found:** <date> by <who>   **Severity:** P0/P1/P2
- **Symptom:** what the player/dev sees
- **Repro:** numbered steps
- **Root cause:** the actual reason
- **Fix:** what changed + which file/branch
- **Regression test:** the check added (or "manual: <steps>")
- **Prevention rule:** the rule we add to the checklist
```

Severity: **P0** blocks the event · **P1** breaks a core loop · **P2** cosmetic/annoying.

---

## Standing prevention rules (accumulated)

1. Never write directly to a save path — write `.tmp`, then replace.
2. Never `FindObjectOfType` inside `Update`.
3. No applicant data hardcoded in prefabs — always from JSON.
4. Every screen ships with a back affordance + safe-area anchor.
5. Any coroutine that animates a transition must be cancelled in `OnDisable`.
6. All string literals in UI come from content files.
7. Test every screen at 720×1280 *and* 1080×2400 before merging.
8. Never assume `Application.persistentDataPath` exists — create it.
9. Guard against double-tap on any irreversible action (stamp, grant access, fire).
10. Audio must survive scene loads → single persistent `AudioManager` in `00_Boot`.

---

## Entries

### BUG-001 — `Setup-DevEnvironment.ps1` fails to parse with "Unexpected token" / "string is missing the terminator"
- **Found:** 2026-11-25 by the user (Windows PowerShell 5.1) · **Severity:** P0 (blocked all setup)
- **Symptom:** script refuses to run; error points at an em-dash mid-string, then cascades into 8 more
  parse errors at unrelated lines (`â€” MISMATCH`, `Unexpected token '", 'User')`).
- **Repro:** run any `.ps1` containing non-ASCII characters from Windows PowerShell 5.1.
- **Root cause:** Windows PowerShell 5.1 reads `.ps1` files **as ANSI unless they start with a UTF-8
  BOM**. My files were UTF-8 *without* BOM, so `U+2014 EM DASH` was decoded as 3 ANSI bytes and broke
  string termination. PowerShell 7 defaults to UTF-8, which is why the script looked fine to me.
- **Fix:** all `.ps1` files are now pure ASCII (em-dashes replaced with `--`), saved **UTF-8 with BOM**,
  CRLF line endings. Added `scripts/Test-Scripts.ps1` which fails the build on any non-ASCII byte.
- **Regression test:** `scripts/Test-Scripts.ps1` (parse + encoding gate) — must print `PASS` for every
  script before any commit touching `scripts/`.
- **Prevention rule:** **`scripts/*.ps1` must be ASCII-only, UTF-8 with BOM, CRLF. Run
  `scripts/Test-Scripts.ps1` before committing.** Never put typographic punctuation in code.

### BUG-002 — `-Verify` crashed with "Cannot bind argument to parameter 'Path' because it is null"
- **Found:** 2026-11-25 by the agent (dry-run in PowerShell 7) · **Severity:** P1
- **Root cause:** `$PSScriptRoot` is null when the script is dot-sourced/invoked in some contexts, and
  `$env:USERPROFILE` / `$env:ProgramFiles` are null off-Windows, so `Join-Path` got a null base.
- **Fix:** fallbacks for `$PSScriptRoot` (`$MyInvocation.MyCommand.Definition`), `$UserHome`
  (`$HOME`), and `$ProgramFiles`/`$ProgramFilesX86`; git-lfs detection rewritten to be
  exit-code based and safe against null LASTEXITCODE.
- **Regression test:** `pwsh -NoProfile -File scripts/Setup-DevEnvironment.ps1 -Verify` must complete
  and print the result table (verified by the agent in PowerShell 7.4.6).
- **Prevention rule:** **never build a path from an environment variable without a fallback**; verify
  scripts by executing them in CI or at least once locally, not only by reading them.

### BUG-003 — distribution zip told the user it contained git history but did not
- **Found:** 2026-11-25 by the agent (self-caught) · **Severity:** P2 (wasted time, damaged trust)
- **Root cause:** the zip builder skipped `.git`, and the bundle accidentally committed its own
  previous zip into history.
- **Fix:** bundle is now produced from a fresh `git clone` (so history is intact), `*.zip` added to
  `.gitignore`, and the zip files were purged from history with `git filter-branch`.
- **Prevention rule:** **verify a distributed artefact by extracting it and running the command the
  user will run** (`git log`) before announcing it as ready.

### BUG-004 — `-Verify` reported `D10 adb: not on PATH` right after installing it in the same run
- **Found:** 2026-11-25 by the user (install + verify in one invocation) · **Severity:** P1 (false alarm)
- **Symptom:** `-D10 extracting platform-tools` printed `added ... to user PATH`, then the verify table
  said `D10 FAIL not on PATH`. A second run in a *new* shell would have passed.
- **Root cause:** `[Environment]::SetEnvironmentVariable(..., 'User')` writes the **registry**, but the
  current process keeps the `$env:Path` it started with. `Get-Command adb` therefore can't see it.
- **Fix:** `Update-SessionPath` rebuilds `$env:Path` from Machine+User registry values before the verify
  pass; `D10` also looks for `adb.exe` on disk and refreshes PATH as a fallback. `D03`/`D04`/`D12` now
  print the exact remediation instead of a bare `False`.
- **Regression test:** run `-Verify` in the same process as an install; adb must report PASS or an
  explicit "reopen PowerShell" message, never a misleading "not on PATH".
- **Prevention rule:** **any check that depends on PATH/env must refresh from the registry first.**
  Never verify an environment mutation inside the process that made it without re-reading the source of truth.

### BUG-005 — `D03/D04` permanently FAILed even with the Android modules installed
- **Found:** 2026-11-25 by the user (Unity Hub showed Android Build Support 6.43 GB installed) · **Severity:** P1
- **Symptom:** Hub listed `Android Build Support`, `OpenJDK`, `Android SDK & NDK Tools` as *Installed*,
  but `-Verify` reported `D03 FAIL` and `D04 SDK=False NDK=False JDK=False`.
- **Root cause:** wrong path. I checked `<version>\PlaybackEngines\AndroidPlayer`. Unity 6 puts it at
  **`<version>\Editor\Data\PlaybackEngines\AndroidPlayer`**. The check could *never* pass — a false
  negative that wasted a Hub reinstall cycle.
- **Fix:** the script now tries both layouts, selects the first that exists, and prints the
  `PlaybackEngines` directory listing when neither matches (so the next failure is diagnosable from the
  output alone). `Unity.exe` is likewise probed in both `Editor\` and `Editor\Data\`.
- **Regression test:** fake-layout test in the sandbox — create
  `<pf>\Unity\Hub\Editor\6000.3.29f1\Editor\Data\PlaybackEngines\AndroidPlayer\{SDK,NDK,OpenJDK}` and run
  `-Verify` with `$env:ProgramFiles` pointed at it → D01–D04 all PASS (confirmed 2026-11-25).
- **Prevention rule:** **never hardcode a third-party installation path from memory.** Find it by probing
  known candidates + listing the directory on failure.

### BUG-006 — `D10 adb` still FAILed after the installer added it to PATH
- **Found:** 2026-11-25 by the user (second verify run, fresh window) · **Severity:** P1
- **Root cause:** the check tested `Test-Cmd 'adb'`, which only sees the *current process* PATH. When the
  user PATH is written by a non-elevated installer or the shell was started before the change, the
  command is still invisible even in a "new" window. Inferring "not installed" from that is wrong.
- **Fix:** D10 now checks three independent facts — `adb.exe` on disk at the expected location, `adb`
  resolvable on PATH, and whether the User PATH *registry value* contains `platform-tools` — and reports
  the distinction ("installed; reopen PowerShell" vs "not installed").
- **Regression test:** run `-Verify` in a shell whose PATH lacks the new entry; the detail must say
  "installed ... reopen PowerShell", never a bare "not installed".
- **Prevention rule:** **distinguish "not installed" from "not visible in this shell".** Report the fact
  and the remedy separately.
