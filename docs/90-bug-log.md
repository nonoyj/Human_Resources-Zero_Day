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

### BUG-007 — the headless test harness shipped without its `.csproj`
- **Found:** 2026-11-25 by the agent during bundle verification · **Severity:** P1
- **Symptom:** fresh clone -> `dotnet run --project tools\CoreTests` ->
  *"Couldn't find a project to run."* Only `Program.cs` was committed.
- **Root cause:** the standard Unity `.gitignore` contains `*.csproj`; it silently excluded
  `tools/CoreTests/CoreTests.csproj`. `git status` looked clean, so nothing warned me.
- **Fix:** added `!tools/**/*.csproj` to `.gitignore` and committed the project file.
- **Regression test:** the bundle check now extracts the zip and runs
  `dotnet run --project tools/CoreTests` from the extracted copy -- a clone that cannot build its own
  tests fails the check.
- **Prevention rule:** **verify a distribution by exercising it from a clean clone** (build + test),
  not by inspecting the source tree. Ignore rules are part of the build.

### BUG-008 — workspace snapshot "over budget" while the project was only 9 MB
- **Found:** 2026-11-25 by the user (platform warning) · **Severity:** P1 (blocks persistence)
- **Symptom:** "went over budget", yet the repo was 53 tracked files / 9 MB and no single file exceeded
  the limits.
- **Root cause:** the **agent** installed a .NET SDK into `/home/user/.tooling/` to compile and test the
  C# core (636 MB). The platform excludes `.cache`, `.local`, `.venv`, `node_modules`, `bin`, `obj`, etc.
  from snapshots -- `.tooling` is **not** on that list, so every snapshot captured the whole SDK.
  The file-count and per-file limits were never the problem; total snapshot size was.
- **Fix:** the SDK now lives in `.cache/dotnet` (an excluded directory); `.tooling`, `.dotnet`, `.nuget`,
  `.local`, `bin`, `obj` are in `.gitignore`; workspace dropped 649 MB -> 9 MB of real content.
- **Regression test:** before delivering, run `du -sh --exclude=.cache --exclude=.git .` and confirm it
  is under ~10 MB, and `find . -type f | wc -l` stays in the hundreds.
- **Prevention rule:** **the agent installs toolchains only into excluded directories**
  (`.cache/`, `.venv/`, `node_modules/`, `/tmp`), never into a new dot-folder, and never into the repo.
  Check `du -sh` before the end of any turn that installs something.

### BUG-009 — `.cache/` (a 567 MB SDK) was committed into git
- **Found:** 2026-11-25 by the agent (git printed CRLF warnings for 700+ SDK files) · **Severity:** P1
- **Symptom:** bundle/jRepo suddenly had 794 tracked files (expected 53) and `.git` grew 3 MB -> 36 MB.
- **Root cause:** the platform *excludes* `.cache/` from workspace snapshots, but **git has its own
  ignore list** -- and `.cache/` was not in `.gitignore`. `git add -A` then swept the entire SDK in.
  Two independent ignore systems were conflated.
- **Fix:** `.cache/`, `.tooling/`, `.dotnet/`, `.local/`, `uploads/` added to `.gitignore`; history
  rewritten to drop the blobs (`filter-branch` + `gc`); `.git` back to 3.0 MB, 52 tracked files.
- **Regression test:** after any `git add -A`, assert `git ls-files | wc -l` is under 100 and
  `du -sh .git` is under 10 MB. Both are now part of the pre-delivery checklist.
- **Prevention rule:** **the platform's snapshot-exclusion list and `.gitignore` are different lists --
  satisfy both.** Never run `git add -A` in this workspace without checking the tracked-file count first.

### BUG-010 — two art PNGs showed as permanently modified, blocking every update
- **Found:** 2026-11-25 by the user (`Update-FromBundle.ps1` refused: ` M art/concepts/*.png`) · **Severity:** P1
- **Symptom:** `git status` reported both concept mockups modified on a machine where nobody had edited
  them; the update script correctly refused to run.
- **Root cause:** `.gitattributes` declared `*.png filter=lfs ...`, but the files had been committed in an
  environment **without** the LFS client, so history contains real PNG blobs, not LFS pointers. On a
  machine **with** LFS installed, the filter compares "expected pointer" against "actual file" and always
  reports a difference. Mixed state: filter declared, content never migrated.
- **Fix:** dropped LFS entirely (`.gitattributes` now marks art as `binary`); documented the decision and
  the one-way migration procedure in `docs/GIT-LFS-DECISION.md`; `Update-FromBundle.ps1` now classifies
  dirty files and distinguishes harmless binary noise from real edits, and prints the three ways out.
- **Regression test:** on a clean clone with the LFS client installed, `git status --short` must be empty
  and `git check-attr` must show `binary: set` (no `filter: lfs`).
- **Prevention rule:** **never declare an LFS filter without migrating the history in the same change**,
  and verify `git status` on a machine where the client is installed -- which is not the machine that
  authored the commit.

### BUG-011 — the update script nested every folder (`docs/docs/`) and never replaced `.git`
- **Found:** 2026-11-25 by the user (update output showed `HEAD before == HEAD after` and `?? docs/docs/`) · **Severity:** P0 (broke the only safe update path)
- **Symptom:**
  - `art/art/`, `docs/docs/`, `handoff/handoff/`, `prototype/prototype/`, `scripts/scripts/`, `tools/tools/`, `unity/unity/` appeared as untracked duplicates
  - `HEAD before: cb2bab6`, `HEAD after: cb2bab6` -- the bundle's history was **not** applied, so a brand-new bundle left the copy at an old commit
- **Root cause:** two defects in my `Copy-Item` loop:
  1. `Copy-Item <src dir> (Join-Path $WorkingCopy $name) -Recurse` **nests** when the destination already
     exists. Correct behaviour is to copy the *contents* into the destination, entry by entry.
  2. `.git` copy targeted `Join-Path $WorkingCopy '.git'` -- an existing directory, so git's internal
     folders were nested the same way and the old refs/index survived; the update was a no-op for history.
  A third, environment-only defect: `$env:TEMP` is null in some shells, so extraction crashed.
- **Fix:** `Copy-Tree` recursive helper copies file-by-file into explicit destinations (never nesting);
  `.git` is deleted-and-replaced wholesale (the bundle is a byte-for-byte snapshot, so its history must
  match its files); temp path falls back through `$env:TEMP` -> `$env:TMP` -> `GetTempPath()`; added
  `-CleanNesting` to repair copies already damaged by the old version.
- **Regression test:** build a damaged copy (reset to an older commit + injected `docs/docs/`), run the
  update, then assert: HEAD advances, `git status` shows only the injected damage, no new `X/X` folders.
  Performed 2026-11-25: `HEAD cb2bab6 -> d6ad239`, 10 top-level items, `.git replaced`, no nesting.
- **Prevention rule:** **test a copy/merge tool against a deliberately damaged target, not a clean one.**
  A "works on an empty folder" test proves nothing about an update. And: when replacing a VCS directory,
  replace it entirely -- never merge into it.

---

## BUG-014 — bundle zip contained a path Windows cannot write; every apply failed at extraction

- **Found:** 2026-11-26 by the user, applying v16 (a wall of
  `Remove-Item : Cannot find path '...\hr0d-boot-b28400fa\HR-ZeroDay\.git\objects\5e\decd...'`)
- **Severity:** P0 — the update path was dead for everyone
- **Symptom:** `Expand-Archive` created some entries and then failed on the rest, ending with
  `New-Object ... "The given path's format is not supported."` Nothing was applied.
- **Root cause:** my ad-hoc zip step walked the whole repo root and swept up **sandbox tool caches**:
  `.nuget/NuGet/NuGet.Config` and `.local/share/NuGet/http-cache/<hash>$ps:_api.nuget.org_v3_index.json/…`.
  That folder name contains a colon. NTFS forbids `:` in a path, so `Expand-Archive` cannot create it,
  and its cleanup pass then reported every *other* entry as "cannot find path" — a misleading cascade
  that points at `.git` when the real culprit is one NuGet cache folder. 514 zip entries; 2 of them
  were unrepresentable on Windows.
- **Fix:** packaging is now a script, `scripts/Build-Bundle.ps1` (ASCII+BOM+CRLF, covered by
  `Test-Scripts.ps1`). It never packages `.local`, `.nuget`, `.dotnet`, `.tooling`, `node_modules`,
  Unity `Library/Temp/Logs/obj`, `__pycache__`, `*.zip`, `*.bundle`; it **skips and reports** any path
  containing `: * ? " < > |`; and it aborts if any of those leak in.
- **Regression test:** build with the script, then verify the archive before shipping —
  `391 entries`, `bootstrap at root and inside HR-ZeroDay`, `git history present, no tooling caches,
  no unwritable paths`, then extract to a clean folder and run both palette checks + `dotnet run`.
- **Prevention rule:** **never zip a working directory you have also used as a toolchain home.** Package
  from an explicit allow-list (or an explicit deny-list that includes every cache dir), and validate the
  archive against the *target* OS's path rules before calling it done.

## BUG-015 — `.\Apply-Bundle.ps1` was not in the zip root (bootstrap unreachable)

- **Found:** 2026-11-26 by the user: `The term '.\Apply-Bundle.ps1' is not recognized…`
- **Severity:** P1 — a one-line fix, but it blocks the whole handoff
- **Symptom:** standing in `Downloads`, the documented first command failed with
  `CommandNotFoundException`, even though the zip was downloaded correctly.
- **Root cause:** the bootstrap existed only at `HR-ZeroDay/Apply-Bundle.ps1` in the earlier build
  recipe, so after extracting the zip there was nothing in the folder the operator stands in.
- **Fix:** `Build-Bundle.ps1` now writes `Apply-Bundle.ps1` at **both** the zip root and
  `HR-ZeroDay/Apply-Bundle.ps1`, and fails the build if either is missing.
- **Regression test:** assert `'Apply-Bundle.ps1' -in $names` and
  `'HR-ZeroDay/scripts/Apply-Bundle.ps1' -in $names` on every build (wired into the script itself).
- **Prevention rule:** **the operator's very first command must be tested from the folder the docs name.**
  Bundles are judged from `Downloads`, not from a repo.

## BUG-016 — the palette checks needed Pillow, so verification failed on a clean machine

- **Found:** 2026-11-26 by the user on the freshly applied v17:
  `ModuleNotFoundError: No module named 'PIL'` from both `tools/art/make_art_kit.py --check` and
  `tools/art/make_applicants.py --check`.
- **Severity:** P1 — the art was fine; the *proof* was unavailable. The team's verify loop broke on
  any machine that had not run `pip install pillow`.
- **Root cause:** both checkers imported Pillow at module scope. `--check` only needs to *read* pixels,
  and Pillow was doing work the standard library can do.
- **Fix:** new `tools/art/pngcheck.py` — a dependency-free PNG decoder (colour types 0/2/3/4/6, 8-bit,
  non-interlaced; all five filter types). Both `--check` paths now use it. Pillow is imported lazily and
  is only required to *regenerate* art; without it the generators print
  `Pillow is required to REGENERATE…` and exit 2 instead of crashing.
  `Setup-DevEnvironment.ps1 -Verify` gained check `D13` so the dependency is visible, not implied.
- **Regression test:** `tools/art/selfcheck_pngcheck.py` writes RGBA / palette+tRNS / grey+alpha PNGs,
  decodes them back, and (when Pillow exists) cross-checks the decoder against Pillow on real kit files
  -> `RESULT: PASS -- stdlib decoder is trustworthy`. Additionally both checkers were run with PIL
  import-blocked (`PYTHONPATH` stub): both still print PASS, `73 sprites` and `28 applicant sprites`;
  regeneration without Pillow exits 2 with a one-line explanation.
- **Prevention rule:** **verification must never depend on a package that only the generator needs.**
  If a check can be written against the standard library, write it that way — a green check that
  requires an install is not a green check.

## BUG-017 — the twin renderer drew every applicant upside-down

- **Found:** 2026-11-26 while building `tools/comp/twin_desk.py`; caught by looking at the render, not by a test.
- **Severity:** P2 — offline renderer only, no shipped art affected. Would have poisoned every future
  layout decision made against the twin.
- **Root cause:** `pngcheck.read_rgba` already returns rows **top-down** (PNG order normalised). The
  slicer re-flipped them as if they came from a bottom-up source, so each frame was mirrored vertically.
- **Fix:** splice `pixels[row * w + ox + col]` straight through; no flip. Only the frame crop needed
  changing, not the strip geometry.
- **Prevention rule:** **know which direction your byte rows run before writing any blit.** Add a
  deliberately asymmetric test sprite (a 3-pixel notch in one corner) whenever a new blitter appears.

## BUG-018 — `[MenuItem("x", priority = N)]` is not valid C#

- **Found:** 2026-11-26 by `tools/UnityStubs` (the stub compile), **before any Unity install existed**.
- **Severity:** P1 — the whole Editor menu (and therefore the art importer and scene builder) would have
  failed to compile on the first project open, blocking Dev B.
- **Root cause:** C# attributes do not accept named arguments to a constructor unless the attribute
  declares a settable property of that name. Unity's `MenuItem` has **no** `priority` property — only
  positional ctor parameters.
- **Fix:** `[MenuItem("HR//0D/Build Desk Scene", false, 20)]` — the third positional parameter *is* the
  priority. Verified by stubbing the four real ctor overloads and compiling.
- **Prevention rule:** **the Unity-facing code must compile without Unity.** `tools/UnityStubs` +
  `.\scripts\Test-ArtPipeline.ps1` do this in ~6 s; it has already paid for itself three times
  (also caught a missing `using`, an ambiguous `TextAsset`, and `Color32` -> `Color`).

## BUG-019 — applying a bundle without `-Clean` left the files newer than HEAD

- **Found:** 2026-11-26 by the user while applying v21: files landed correctly (`Test-Path` -> `True`)
  but `git log` still showed `dc55e59` and `git status` listed 6 modified/untracked entries.
- **Severity:** P1 — the exact "is my copy right?" doubt the bundle exists to remove. It also made the
  new `Connect-Remote.ps1` look broken when it was merely uncommitted.
- **Root cause:** `.git` was only adopted when `-Clean` was passed, but the file copy always ran. So a
  non-Clean apply overwrote the working tree while keeping the older history — dirty by construction.
- **Fix:** `Apply-Bundle.ps1` now adopts the bundle's `.git` on **every** apply (unless `-NoGit`),
  saves and restores the user's `origin` URL (a bundle's `.git` has no remote), and runs
  `git reset --mixed HEAD` so index and files agree.
- **Prevention rule:** **the file copy and the history copy must be one operation.** If a step can
  leave files and HEAD disagreeing, it must not exist.

## BUG-020 — `git fetch ... 2>&1 | Out-String` kills the script in PowerShell 5.1

- **Found:** 2026-11-26 by the user on Windows PowerShell 5.1 (`Connect-Remote.ps1` printed `==> fetching`
  then `NativeCommandError` and stopped, even though the fetch itself had succeeded).
- **Severity:** P1 — the runbook died at the moment it was supposed to report good news.
- **Root cause:** PS 5.1 wraps a native command's **first stderr line** in an `ErrorRecord`; with
  `$ErrorActionPreference = 'Stop'` and a redirection/pipe (`2>&1 |`), that record is *terminating*.
  Git writes progress and `From https://…` to stderr by design. PowerShell 7 changed this behaviour,
  which is why the sandbox never reproduced it.
- **Fix:** never redirect a native command's stderr when the script may run on 5.1 — use
  `& git fetch origin --prune | Out-Host`, and set `$ErrorActionPreference = 'Continue'` before native
  calls, checking `$LASTEXITCODE` instead of relying on exceptions. Also dropped a stale `$branch`
  reference that printed `git push -u origin ` with no branch name.
- **Prevention rule:** **test on the operator's shell, not the one you have.** The sandbox has
  PowerShell 7; the user has 5.1. Any stderr redirection of a native tool is a 5.1 landmine.
  `.\scripts\Test-Scripts.ps1` only catches *parse* errors — it cannot catch this class.

## BUG-021 — a copied `.git` folder produced a working copy that was dirty no matter what

- **Found:** 2026-11-26 while reproducing the user's v22 apply: files arrived (including the *new*
  `Apply-Bundle.ps1`) but `git log` still showed the OLD commit and `git status` listed 8 entries.
- **Severity:** P1 — every bundle after the first was untrustworthy, and the GitHub push published a
  snapshot whose own scripts were older than the files next to them.
- **Root cause:** three independent faults, all in "copy `.git` with `Copy-Item -Recurse`":
  1. the copy loop set `ErrorActionPreference='Stop'`, so the first file Windows refused (a locked
     `.git/index`) aborted the whole `.git` copy inside `try{}catch{}` — the exception was swallowed and
     the swap silently never happened;
  2. a replaced `.git` was therefore never introduced, leaving HEAD old while files were new;
  3. later, discovering the branch by running `git` inside the *extracted* folder resolved to an
     unrelated parent repository (the test sandbox), returning an empty branch name.
- **Fix:** history no longer travels as a `.git` folder. `Build-Bundle.ps1` writes a **git bundle**
  (`git bundle create … main`, verified with `git bundle verify` → *"records a complete history"*) plus a
  literal `history.branch` file. `Apply-Bundle.ps1` then does `git init` + `git fetch history.bundle`
  + `symref HEAD` + `reset --mixed`, preserving the user's `origin`, and excludes the two transport
  files from the file copy. `-Reinstall` added for "match the files to HEAD without touching a remote".
  `Apply-Bundle` now **exits 1 and prints the fix** when the tree is dirty instead of warning politely.
- **Proven on three scenarios:** stale `.git` + real origin → `history adopted: main at a70342c`,
  `origin preserved`, `working tree clean -- files match HEAD exactly`; empty folder → same, with a
  `no origin` hint; running the repo's own script copy → clean.
- **Prevention rule:** **never package a `.git` directory.** Ship a bundle file that git validates, and
  make the transport self-describing (a `history.branch` literal) instead of inferring state by running
  git in a folder that may not be a repo. Also: never let a `catch {}` swallow the loop that performs a
  required swap.

## BUG-022 — `return` inside a `foreach` exited the whole apply script

- **Found:** 2026-11-26 while regression-testing the BUG-021 rewrite: the apply printed
  `==> copying project files` and then simply stopped, exit code 0, no error.
- **Severity:** P1 — a silent half-apply. The worst possible failure: no message, no non-zero exit.
- **Root cause:** the copy loop was converted from `Get-ChildItem | ForEach-Object { ... return }` to a
  plain `foreach ($it in $items) { ... }` but the `return` statements were kept. In a `ForEach-Object`
  block `return` skips one item; in a plain `foreach` inside a script it returns from the *script*.
- **Fix:** `continue` for skips, `foreach` kept for the early-exit-free structure. The comment in the
  code now says so explicitly.
- **Prevention rule:** after changing a pipeline block into a `foreach` statement (or back), grep the
  block for `return`. They are not interchangeable.

## BUG-023 — `Get-Item` silently refuses dot-files off Windows

- **Found:** 2026-11-26 by the user-visible symptom `Could not find item /home/user/.gitattributes`,
  thrown by the *first* tracked file during bundle build.
- **Severity:** P2 — build-tooling only; Windows (where the team builds) is unaffected because
  "hidden" there is an attribute, not a leading dot.
- **Root cause:** on Linux/macOS PowerShell reports dot-prefixed names as hidden, and `Get-Item` (like
  `Get-ChildItem` without `-Force`) skips hidden items and then complains the path does not exist.
- **Fix:** `Get-Item -LiteralPath $full -Force`. Also kept the forward-slash form from `git ls-files`
  instead of rewriting it to `\` (BUG-023b: that normalisation broke every non-Windows run).
- **Prevention rule:** **any code path that touches dot-files must use `-Force`.** The Windows-only
  habit hides this class of bug until someone runs the script in CI or on a Mac.

## Bundle build is now exact (closing the whole class)

`Build-Bundle.ps1` collects **`git ls-files`** (the tracked tree), refuses to build when the working
tree is dirty, and ships `history.bundle` + `history.branch` instead of a `.git` folder. That makes
"the zip" and "the commit it names" the same thing by construction — BUG-019, BUG-021 and every
variant of "bundle newer than its HEAD" cannot recur.

## BUG-024 — `HR//0D > Build Desk Scene` throws NullReferenceException before creating anything

- **Found:** 2026-11-26 by the agent while wiring the interaction layer (type-check review).
  **Severity:** P1 — the documented way to create the first scene cannot work at all.
- **Symptom:** the menu calls `DeskSceneBuilder.Build(layout, null)`, then `Build` immediately runs
  `parent.Find(RootName)` on the null parent. Unity reports a `NullReferenceException` and no
  `Assets/Scenes/Desk.unity` is written.
- **Repro:** 1. Open the Unity project. 2. `HR//0D > Build Desk Scene`. 3. Read the Console.
- **Root cause:** the builder's "rebuild an existing desk" path was written assuming a parent
  transform always exists; the editor menu legitimately passes `null` to build at the root.
- **Fix:** treat `parent == null` as the root case: skip the `Find` and parent the new root with
  `SetParent(null, false)`. (The same fix landed independently on `main`; kept here so the number
  in this log is not reused.)
- **Regression test:** `dotnet build tools/UnityStubs/CompileCheck.csproj` plus a manual
  `HR//0D > Build Desk Scene` in Unity; the Console must end with `built Assets/Scenes/Desk.unity`.
- **Prevention rule:** **every API with an optional parent must be called with null at least once in
  a test.** A helper that only works from one call site is not a helper.

## BUG-025 -- monitor floated 12 px above the desk (found by the user, 2026-11-26)

- **Symptom:** the monitor read as hanging in mid-air; the desk surface started below it with nothing
  joining the two.
- **Cause:** `monitor_small.png` included a 4 px stand *inside the monitor sprite*, but the monitor
  layer sorts at `−20` and the desk foreground at `+10`. The desk therefore always painted over any
  part of the stand that reached the desk surface, so only the bezel showed and it looked detached.
- **Fix:** the stand became its own layer, `desk/monitor_stand.png` (44x26, ragged neck + ellipse base
  + contact shadow), sorting `−19`, anchored `[69, 60]` so its bottom 4 px hide behind the desk edge.
  The monitor sprite moved from anchor y `76` to `86` to sit on the neck.
- **Prevention rule:** a prop that stands on a surface may never rely on pixels inside its own sprite
  crossing a *higher* sorting layer. Split it, or it silently cannot touch the floor. (Bible §7b.)

## BUG-026 -- workspace snapshot over budget, 4,283 files silently dropped (2026-11-26)

- **Symptom:** the Workspace panel showed *"577.4 MB across 5,177 files, past the 128 MB / 10,000-file
  limit. 4,283 files were not saved."*
- **Cause:** the agent toolchain (.NET SDK 8.0.404 + PowerShell 7.4.6) was installed into
  `/home/user/.tooling/` -- 4,644 files / 569 MB *inside the workspace*, which is snapshot-capped.
- **Fix:** the SDK now installs to `/tmp/tc` (outside the workspace), `.tooling/` is reduced to a
  `.keep` marker and gitignored, and the retained old bundles were pruned (only the current version is
  kept on disk). Workspace is back to ~16 MB / 319 files.
- **Prevention rule:** never install a toolchain, package cache or SDK under `/home/user`. If a tool
  needs to persist between turns and is larger than a few MB, it belongs in `/tmp` or `/opt`, not the
  repo -- the snapshot is a deliverable, not a scratch disk.

## Repo clean-up (2026-11-26) -- what was removed and why

Redundant, not broken. Nothing shipped was touched; every gate re-ran green after the sweep.

| Removed | Why |
|---|---|
| `tools/art/make_artifact_legend.py`, `make_character_spread.py`, `make_composition_preview.py` | One-off contact-sheet scripts with no caller and no gate. All three were superseded by `twin_desk.py` |
| `art/kit/reference/{applicants_sheet,artifact_legend,character_spread,composition_preview,window_detail}.png` | Their outputs. The live previews are `twin_desk_*.png` (which the gates verify) |
| `art/concepts/study_K_*.png`, `study_L_*.png`, `study_contact_sheet_JK.png` | Studies A–K were deleted when L was promoted; these three were the leftovers. The generator stays (`study_desk_workdesk.py` is referenced by a gate) |
| `docs/PROJECT-STATUS.md` | Pinned `dfea20f` / 53 files. Had been false for weeks — the most confusing file in the repo |
| `docs/NEXT-STEPS.md` | Superseded by `docs/16-the-later-list.md` |
| `docs/03-title-shortlist.md` | Title is locked |
| `docs/05-design-suggestions.md` | Superseded by `docs/06-decisions.md` |
| `docs/92-retrospective.md` | Once-off, already acted on |
| `.cache/`, `.dotnet/`, `.local/`, `.nuget/` at the workspace root | Toolchain droppings (BUG-026) |
| `tools/art/__pycache__/` | Build droppings |

**Docs now have an index** (`docs/README.md`) with a "deleted on purpose — do not recreate" list, and
`README.md` links to it instead of describing files that no longer exist (it still pointed at a
`content/` folder that was never created).

## BUG-027 -- RAGGED was shredding hard objects (user: "the PC looks too ragged", 2026-11-26)

- **Symptom:** the monitor read as debris -- a dotted outline with 2 px gaps, a dotted inner frame and
  speckle, on the largest object on screen.
- **Cause:** `ragged_rect(..., amp=1)` was applied to a 154x106 box. Its 1 px step-by-step edges are
  designed for hand-drawn *props*; on a hard rectangle at that size every edge becomes a broken run,
  and `scuff(n=14)` added the rest.
- **Fix:** the monitor is now drawn straight (case → bezel face with bevel → glass → 1 dent, 2 chips,
  4 scuffs). Global wear was recalibrated: `LEVELS` scuffs 10/22/36 -> **4/9/16**, applicants halved,
  desk 22 -> 14, noticeboard 6 -> 3, stamps 3 -> 2, documents 10 -> 5.
- **Rule (bible §6b):** big hard objects stay hard. Only paper and soft goods get torn silhouettes.
- **Also rejected:** a diagonal glare streak across the glass -- it read as a scratch and competed with
  the email text the player is reading.

## BUG-028 / BUG-029 -- document overflowed the CRT; the arm was not an arm (2026-11-26)

- **BUG-028:** document layer anchor `[34,93]` put the 108x63 sheet at rows 114-177 while the monitor
  glass ends at row 171 -- 6 px of paper hung out of the screen. Sheet resized to 96x54, anchor moved
  to `[38,100]`. Prevention: measure against the glass rect (`x+6, y+6 .. x+w-7, y+h-13`).
- **BUG-029:** `monitor_stand.png` was a ragged column + ellipse with dotted edges, no articulation.
  Redrawn 56x34: base plate / lower column / joint collar / leaning upper column / mount plate.
  Anchor `[63,60]` so its mount meets the bezel bottom at row 184 and its foot overlaps the desk.
- Also this pass: desk props re-laid around the arm (keyboard moved off x=104 to x=200), coffee
  cleared off the clipboard, notepad pen shortened (a 15 px diagonal read as a scratch).

## BUG-030 -- Test-ArtPipeline threw a red traceback in 5.1 (found on the user's PC, 2026-11-26)

- **Symptom:** the gate printed a green `[ok]` list, then `python.exe : Traceback (most recent call last):`
  with `NativeCommandError` at `Test-ArtPipeline.ps1:64`.
- **Cause:** the gate RAN `tools/art/study_desk_workdesk.py`. That script is a leftover from composition
  study L: it draws from `desk/monitor_bezel.png` and `desk/document_sheet.png` (pre-v42 assets), and it
  rewrites `art/concepts/study_*.png` on every gate run -- so the working tree went dirty every time the
  gate ran. In PowerShell 5.1 any stderr from a native command surfaces as a NativeCommandError, which
  looks like a failure even when the exit code is 0.
- **Fix:** retired the script and its three output PNGs; the gate now checks that no stray `study_*.py`
  exists. `twin_desk.py` already renders the same 480x270 view and is verified byte-for-byte, so nothing
  was lost. Also removed the three one-off art scripts that survived the earlier sweep (they had been
  re-added to the index by the bundle round-trip).
- **Prevention rule:** a gate may not run a script that WRITES into the repo. Gates read, they do not
  produce -- otherwise every run dirties the tree and the "clean tree" bundle rule trips.

## BUG-038 / BUG-039 / BUG-040 / BUG-041 -- the history step, four bugs deep (v51 report)

The user's apply got further every time and failed somewhere new. All four are in the same 30 lines.

- **BUG-038 `refusing to fetch into branch 'refs/heads/main' checked out at ...`** — `git init` puts
  `HEAD` on `refs/heads/main` on git builds whose default branch is `main`; fetching
  `refs/heads/*:refs/heads/*` then writes into the checked-out branch and git refuses.
  **This box defaults to `master`, so the rehearsal could never reproduce it.** Rule: *behaviour that
  depends on a machine setting is never proven by one machine.*
- **BUG-039 `git clone` was the obvious fix and is wrong** — it refuses a non-empty target, and the
  working copy is always full of the previous version. Replaced with: `init`, then fetch into
  `refs/hr0d-incoming/*` (a namespace that can never be checked out), then `update-ref` each branch
  into place, then `symbolic-ref` + `reset --mixed`.
- **BUG-040 the sequelae, all silent:**
  - source and destination were swapped in the fetch refspec (`+refs/hr0d-incoming/*:refs/heads/*`), so
    the fetch "succeeded" and created nothing;
  - `%(refname:short)` strips `refs/`, so the prefix match never fired and branches landed at
    `refs/heads/hr0d-incoming/main` — and the next check then blamed the bundle;
  - `for-each-ref --format='%(refname) %(objectname)'` survives PowerShell as one argument but **not**
    through `ProcessStartInfo` quoting: git received two args, expanded only the first, and the sha
    silently vanished. Format strings are now **one token** (`'%(refname)|%(objectname)'`).
  - the fetch was `-Quiet`, so its failure reason was hidden and reported as "the bundle has no branches".
- **BUG-041 `Expand-Archive` refused a dot-file entry** (`Cannot find path .../.gitattributes`). Windows
  PowerShell 5.1 extracts the same zip happily, but a script that only runs on one host cannot be
  rehearsed — the zip is now extracted entry by entry through the `ZipFile` API.
- **Also:** after a `-Clean` copy the tree can hold files the new `HEAD` never had (they came from the
  *previous* version), which was reported as "files do NOT match HEAD" — alarming and wrong. A `-Clean`
  apply now ends with `reset --hard` + `clean -fd` so the tree check means what it says.

**Proof:** under `$PSNativeCommandUseErrorActionPreference = $true` (reproduces 5.1), three scenarios:
existing repo on `main` + a stale untracked file, a second apply with `origin` present, and a fresh
folder with no `.git`. All three end at `HEAD ... working tree clean -- files match HEAD exactly`, and
`dotnet run --project tools/CoreTests` in the applied copy is green.

## BUG-036 / BUG-037 -- `git remote get-url origin` killed the apply, twice (v49 report)

- **Symptom:** the user ran v49 and got a red PS traceback at `Apply-Bundle.ps1:135`:
  `git.exe : error: No such remote 'origin'` -> `NativeCommandError`. The apply stopped dead.
- **Cause:** the working copy had **no `origin`**. `git remote get-url origin` reports that on **stderr**,
  and Windows PowerShell 5.1 converts *any* native stderr into a `NativeCommandError`, which is fatal
  because the script sets `$ErrorActionPreference = 'Stop'`. `git remote get-url` has **no `-q`**, so
  there is no way to ask it politely.
- **Why it happened twice:** my BUG-036 fix redirected `2>&1` on *that one call*. v49 still died at the
  same class of call one line earlier. Redirection happens after PowerShell has already decided the
  record is an error. The class, not the site, was the problem.
- **Fix (BUG-037):** no native command is invoked with `&` in `Apply-Bundle.ps1` any more. There is one
  `Git` helper that starts `git` as a `System.Diagnostics.Process` with `RedirectStandardOutput/Error`,
  returns stdout as a string, prints stderr in grey, and leaves the exit code in `$script:GitExitCode`.
  Ten call sites migrated. **Rule: in a 5.1 script, treat native stderr as data — start the process.**
- **Second bug found by rehearsing:** `Clear-Host` shells out to `clear`, which exits non-zero when
  `TERM` is unset or output is piped -- fatal under the same error handling. Removed (cosmetic).
- **Third bug found:** the shipped bootstrap at the **repo root** was never linted, because
  `Test-Scripts.ps1` only walked `scripts\`. It now checks the root bootstrap too — that is why three
  releases of broken bootstrap went out green.
- **Proof:** rehearsed with `$PSNativeCommandUseErrorActionPreference = $true` (pwsh's switch that
  reproduces 5.1's behaviour) against a repo with **no origin**, twice in a row, plus the `-Reinstall`
  path. All three end at `working tree clean -- files match HEAD exactly`.

## BUG-035 -- the bundle could not fix the script that applies it (found by rehearsal, 2026-11-26)

- **Symptom:** after the BUG-032 fix, a *rolled-back* bootstrap still produced the old failure. The
  bundle was correct; the script reading it was not.
- **Cause:** `.\Apply-Bundle.ps1` lives in **Downloads**, outside the repo. No bundle can overwrite a
  file that is already there, so the bootstrap that runs is the one the operator downloaded the *first*
  time. Every bootstrap bug so far survived a release because of this.
- **Second cause, found while rehearsing:** shipping `history.bundle` inside `HR-ZeroDay\` made the
  **old** bootstrap copy it into the working copy as an untracked file, which it then reported as
  "working tree not clean" -- a third way to look broken while being fine.
- **Fix:** (a) transport files ship at the **zip root only**; (b) a current bootstrap **refreshes itself**
  in place after the copy, so from the next apply onward Downloads always holds the current version;
  (c) `docs/BUNDLE-WORKFLOW.md` documents the one-time manual replacement and the three recovery
  commands (`-Clean`, `-Reinstall`, `Fix-Repo.ps1`).
- **Rehearsal (proof, not assertion):** built v47, put the *old* bootstrap in a scratch Downloads,
  applied -> reproduced the user's exact output (`bundle has no .git`, old HEAD, dirty); replaced the
  bootstrap from the zip; applied again -> `HEAD 127c4ed (78 commits)`, `working tree clean`, and
  `dotnet run --project tools/CoreTests` -> **56 PASS** in the applied copy.
  `-Reinstall` was separately verified to clean a dirty tree (`reset --hard` + `clean -fd`).

## BUG-032 / BUG-033 / BUG-034 -- the apply copied files but could never adopt the history (v45 report)

The user applied v45 correctly and the apply **still** reported `HEAD 0b52098` (the v44 commit) and a
dirty tree. The files were right; three separate defects meant the history step could never work.

- **BUG-032 — the bootstrap looked for the history in a place the bundle never puts it.**
  `Build-Bundle.ps1` ships `history.bundle` / `history.branch` at the **zip root**; `Apply-Bundle.ps1`
  (the root bootstrap) builds `$root` = the extracted *project folder* and then tested
  `$root\.git`. There is no such folder in any bundle, so every `-Clean` apply printed
  `bundle has no .git; history left untouched` and left the old `HEAD` in place. The v45 manifest even
  *proves* the files were fine (`history.bundle present`).
  **Fix:** the bootstrap now accepts `history.bundle` at the zip root, inside the project, or a whole
  `.git` folder (older bundles), and fetches from the bundle with `git fetch <file> refs/heads/*:...`.
  `Build-Bundle.ps1` now writes the transport files in **both** locations and fails the build if either
  is missing.
- **BUG-033 — "history adopted" could be a lie.** `git symbolic-ref` + `reset --mixed` were run without
  checking that the branch actually exists in the bundle; a miss would leave `HEAD` dangling (which
  reads as `0 commits`). Now the script verifies `refs/heads/<branch>` exists and **fails loudly**.
- **BUG-034 — no recovery path.** The script's own advice was `git reset --hard && git clean -fd`, which
  the user would have to type. There is now **`-Reinstall`**: does exactly that, no extraction, idempotent.

**Also fixed here:** `scripts/Build-Bundle.ps1` shipped `history.bundle` at the root only, and the
tree-check message in `Apply-Bundle.ps1` pointed at a symptom instead of the cause. Both now say what
actually happened.

**Prevention rule:** a bundle is only "correct" when a clean machine, given nothing but the zip, ends at
the same `HEAD` as the box that built it. `history.bundle present` in the manifest is not that proof --
only the post-apply `HEAD` check is.

## BUG-031 -- the art gate needed Pillow (found on the user's PC, v44 report)

- **Symptom:** the user ran the 11 gates after applying v44: 9 `[ok]`, then
  `[FAIL] screen preview failed to render`. Everything else green.
- **Cause:** `tools/comp/preview_screen.py` imported Pillow at module scope, and the gate ran it.
  The user's box has stdlib Python; Pillow was never in the setup checklist because every *art* tool
  that needs it (`twin_desk.py`, `pngcheck.py` fallback) is a human tool, not a gate.
- **Fix:** `preview_screen.py` now degrades gracefully (`HAVE_PIL`) and gained a **`--codepage`** mode
  that prints the glass as an ASCII grid + a content dump using nothing but stdlib. The gate runs that
  mode into `$env:TEMP` and asserts the **shape** of the glass (20 columns x 9 rows) plus card markers
  (`GRAD`). The pretty PNG stays a human tool.
- **Second bug found in the same edit:** a python `str.replace` on a CRLF anchor in
  `scripts/Test-ArtPipeline.ps1` silently did not match and aborted the previous attempt. The script is
  **UTF-8 BOM + CRLF + ASCII-only**; edit it by line number or on raw bytes, never on a guessed string.
- **Third bug found while fixing:** the new gate block used `$env:TEMP`, which is null under pwsh on
  Linux and threw `Cannot bind argument to parameter 'Path' because it is null`. Now falls back to
  `[System.IO.Path]::GetTempPath()` so the gate runs on any host.
- **Prevention rule (same as BUG-016):** every **gate** runs on the standard library only. Pillow is a
  nice-to-have for humans, never a dependency of a gate.

## The legibility pass: four layout bugs, three caught by tests (2026-11-26)

The user said the glass text was "too close to each other". The gap change was one line; the rest was
where the real bugs were:

1. `preview_screen.py` still drew columns with the old pitch while `MonitorScreen` had the new one --
   caught by adding `ScreenMetricsTests`, which parses the python constants and compares them to C#.
2. The letter page's rule line was 68 `-` against a 65-column budget -> `-----~`. The rule is now
   derived from `LetterLayout.Cols`, not typed.
3. `Cols = 66` was arithmetic I did in my head ("456/7"); the real number is `(456+2)/7 = 65`. The test
   asserted the truncated tail and made the mistake obvious.
4. Zero inset on the letter page clipped the title's first glyph and the body's first column against the
   paper edge. Fixed by a **6 px inset** (`LetterLayout.TextRect()`), and the preview now renders through
   the same numbers.

Also fixed in the atlas: `[` and `]` at 5x7 read as `C` / `J` next to a following glyph (three-wide
arms are the whole glyph). They are now two bare vertical strokes; the prompt is `> READ LETTER`.

## MonitorScreen: three API bugs caught by the first test run (2026-11-26)

Written tests-first-with-code; the first `dotnet run` failed 3 of 7, all real:
`rows` was off by one (88 px of glass holds **10** lines, not 9 -- the last line needs no trailing gap);
`LinesToRects` put line 1 at the BOTTOM of the glass because canvas space is y-UP while text runs DOWN
(it would have rendered the page upside down, and nothing would have thrown); `TrustBar` reflowed from
16 chars to 15 when the value hit 100 (now zero-padded to 3 digits).

## Monitor screen, part 2 (2026-11-26) -- glyph atlas + raster + view

- **Font:** `tools/art/make_font.py` generates `art/kit/fonts/monitor_font_5x7.png` + `.json` (56 glyphs,
  110x34, padding 2). Generated, gated, palette-only -- NOT a TMP asset, because TMP font assets cannot
  be built headlessly and cannot land on a 5x7 grid at 480x270.
- **Why the json carries the glyph rows:** the first version of the raster tests decoded the PNG by hand
  in C# and the Deflate stream failed on a real file. The metrics now include `rows` (the same ink as
  the PNG), so tests verify coverage and clipping from data and never need an image decoder. The PNG
  remains the thing Unity renders.
- **Raster/view split:** `MonitorScreenRaster` (pure, Color32[] in/out, tested) and `MonitorScreenView`
  (MonoBehaviour: owns the Texture2D, repaints only when day/trust/applicant change).
- **Colour path:** `Resources/palette.json` (generated from `make_art_kit.PALETTE`) so the runtime tint
  can never invent a colour outside the bible's palette.
- **Gate additions:** `make_font.py --check` and `preview_screen.py` now run in `Test-ArtPipeline.ps1`,
  so a broken atlas or a text-layout regression fails the gate and is visible in the bundle as
  `art/kit/reference/screen_preview.png`.

## BUG-042 -- the art gate blessed code Unity rejects, and the editor went to Safe Mode (player report, 2026-11-25)

**Symptom.** Opening the project asked *"Enter Safe Mode? The project you are opening contains
compilation errors"*, with three `CS1061: 'TextMesh' does not contain a definition for 'sortingOrder'`.

**Root cause, and it is the worst class we have.** `sortingOrder` lives on `Renderer` in Unity, not on
`TextMesh`. The merged interaction layer set it on the `TextMesh`. The local gate passed because
`sortingOrder` had been **added to the `TextMesh` stub** to silence the same error there. The stub was a
lie, so the gate was green and the editor was broken.

This is the identical failure mode as `TextureImporter.spritesheet` and `UnityEngine.EventSystems`
(docs/../tools/UnityStubs/ASSEMBLY-RULES.md). The rule is not "make the gate pass", it is **"be exactly as
strict as Unity"**.

**Fix.**
- `TextMesh` keeps only members it really has. `sortingOrder` / `sortingLayerName` moved to a `Renderer`
  base class, with `MeshRenderer` and `SpriteRenderer` deriving from it — Unity's own hierarchy.
- The three call sites use `GetComponent<MeshRenderer>().sortingOrder`, which is what the engine requires.
- **New guard** in `scripts/Test-ArtPipeline.ps1`: it parses the `TextMesh` stub and fails by name if
  `sortingOrder` or `sortingLayerName` ever appears there again. Verified by re-adding the lie.

**Related CS0165 in the same merge:** `Vector2 point;` was used without a definite assignment in
`GameInputRouter`, so the Game assembly did not compile at all. Also `DayResult.SyntheticCaught` (the field
is `SyntheticsCaught`) and a missing `using HRZeroDay.Core.Data` in `StampAnimator`.

## BUG-043 -- every relog needed a manual menu ritual, and a closed Unity (player report, 2026-11-25)

**Symptom.** *"Do I have to do the HQ in every relog of Unity, it seems like lots of time is being used
plus the going in and out of unity to extract a file."*

**Three separate wastes, all fixed:**
1. The `HR//0D` menu is **not** required to play. `GameBoot` builds the desk at `AfterSceneLoad` when no
   `DeskBootstrap` exists, so pressing Play is the whole step. The menu is only for when art PNGs changed.
2. `scripts/Open-Game.ps1` opens the project on the detected Unity install — no Hub clicking.
3. `Apply-Bundle.ps1` used a hard `Remove-Item` on `.git`, which is what demanded a closed editor. It now
   retries five times and only asks for a closed editor if all five fail.

**And a documentation bug that cost the player a round trip:** the previous `docs/BUNDLE-WORKFLOW.md` said
`.\scripts\Open-Game.ps1` right after the apply line — but that script ships *inside* the bundle, so it
does not exist until the apply has run. It also never said the zip lives in `Downloads` while the repo is
elsewhere. The page is rewritten with the two commands in order and a symptom → cause → fix table.

## BUG-044 -- the bundle shipped without the art the game loads at runtime (player report, 2026-11-25)

**Symptom.** Fresh apply of v78, `Open-Game.ps1`, press Play:

```
[HR//0D] could not start the game: sprite not found at Resources/Art/Kit/backgrounds/office_L0.
Run: python tools/art/publish_to_unity.py
```

The art was in the repository the whole time.

**Root cause, three decisions compounding.**
1. `.gitignore` had `unity/Assets/Resources/Art/` with the note *"derived from art/kit/ every time,
   committing it makes every machine fight over the same files."*
2. The game loads sprites with `Resources.Load`, which **cannot see anything outside a `Resources`
   folder**, so `Assets/Resources/Art/Kit/...` is the file it actually reads.
3. Unity imports from the project folder and **never runs python**, so the runtime copy existed only if
   somebody had run `publish_to_unity.py` or `HR//0D > Publish Art To Resources` on that machine.

The art gate then *enforced* the mistake: it failed if `Resources/Art` was tracked in git. So the gate was
green while every bundle was missing 85 files the game needs.

**This is the same class as BUG-042** — a local check blessing something the real environment rejects.

**Fix.**
- `unity/Assets/Resources/Art/` is **committed**. A file the game reads at runtime is a source file.
- `publish_to_unity.py` now writes **both** mirrors (authoring `Assets/Art/Kit` and runtime
  `Assets/Resources/Art/Kit`) and verifies both; `--verify` names which mirror is missing or stale.
- The gate rule is **inverted**: it used to require the folder to be untracked, it now requires every
  manifest sprite to be tracked under `Resources/Art` and byte-identical. Verified by hiding the folder —
  the gate fails by name.
- `docs/BUNDLE-WORKFLOW.md` no longer claims the menu is needed before Play. It was, until this fix.

**Lesson to keep:** before any instruction of the form "just press Play", ask *what does the runtime
actually read, and is that file in the thing I am shipping?*

## BUG-045 -- the CRT crashed because a texture import flag lived in a file that was not in the repo (player report, 2026-11-25)

**Symptom.** After BUG-044 was fixed the room loaded, then:

```
[HR//0D] could not start the game: Texture2D.GetPixels32: texture data is either not readable,
corrupted or does not exist. (Texture 'monitor_font_5x7')
```

**Root cause.** `MonitorScreenView` and `LetterView` read the font atlas pixel by pixel with
`GetPixels32()`. That requires the texture to be imported with **Read/Write Enabled**, which is a flag
that exists **only in the texture's `.meta` file**. No `.meta` file was committed, so Unity generated one
on import with its default — Read/Write **disabled** — and the first `GetPixels32()` threw.

`ArtKitImporter.ConfigureFontAtlas()` had known about this for a while, but it is an editor menu step, and
it configured `Assets/Resources/fonts/...` and `Assets/Art/Kit/fonts/...` — not the new runtime mirror at
`Assets/Resources/Art/Kit/fonts/...`. So even running the menu was not guaranteed to fix it.

**Fix, in depth order.**
1. **The atlas `.meta` files are committed** with `isReadable: 1` and `filterMode: 0` (point). A fresh
   clone now imports correctly before anyone presses Play. `make_font.py` regenerates them, so they cannot
   drift from the atlas; the guid is derived from the path, so re-running never churns the repo.
2. **The atlas PNG is no longer required at all.** The font json already carried `rows` per glyph — the
   same ink as the PNG — and had done since the monitor screen was built, purely because the raster tests
   could not decode a PNG in C#. `FontAtlasGlyphSource` now reads those rows first and only falls back to
   the texture. `RowsFromMetrics()` is the bridge. If the atlas is readable it is used and wins, so art and
   data still cannot disagree.
3. `LetterView` had the identical bug and would have been the next crash — same fallback applied.
4. **Gate:** `Test-ArtPipeline` now fails if either atlas `.meta` is missing or does not say
   `isReadable: 1`. Verified by flipping the flag to 0 — it fails by file name.

**Lesson.** A build input that lives in an uncommitted file is not a build input. Unity is especially good
at hiding these: import settings, scene files and `.meta` guids all live in files that are easy to ignore
"because Unity makes them".

## BUG-046 -- animation frames were manufactured by an editor menu, so a clone had none (player report, 2026-11-25)

**Symptom.** Third boot failure in a row, each one a step further into the game:

```
[HR//0D] could not start the game: frame not found at
Resources/Art/Kit/applicants/frames/app_human_solid_a_192x216__human_solid_a_neutral
```

**Root cause.** `DeskSceneBuilder.LoadFrames` loads four child PNGs per applicant (neutral / breathe /
breathe_fidget / blink) through `Resources.Load<Sprite>`. Those files were cut by
`ArtKitImporter.ExtractFrames` — an **editor menu** step — because Unity 6 removed
`TextureImporter.spritesheet` and the replacement lives in a 2D package this assembly does not reference.
The code was fine. Its *address* was not: nothing in the repository produced the files.

**Fix.**
- `tools/art/extract_frames.py` cuts the 56 frames in python, into **both** mirrors, from the frame names
  and rects in `unity_sprites.json` — so the manifest stays the single source of truth and the script and
  the editor importer cannot disagree. `--check` verifies every frame exists.

## BUG-047 -- every sprite in the runtime mirror had no `.meta`, so `Resources.Load<Sprite>` returns null

**Found while fixing BUG-046, before the player hit it.**

A PNG with no `.meta` is imported by Unity with the **default** settings, and the default `textureType` is
`Default`, not `Sprite`. `Resources.Load<Sprite>()` then returns **null with no error at all** — every
piece of art in the game would silently fail to appear. This was the very next wall after the frames.

**Fix.** `tools/art/write_unity_metas.py` writes a `.meta` for every image in the runtime mirror: sprites
for the art, a readable plain texture for the glyph atlas. Guids derive from the repo path, so re-running
never churns the repository or breaks a reference Unity has already resolved. `--check` fails if any image
lacks one.

**Both are now driven by `publish_to_unity.py`** (write mode runs them, `--verify` checks them) and enforced
by `Test-ArtPipeline.ps1`, so they cannot be forgotten again.

## The pattern, stated once

BUG-044, BUG-045, BUG-046 and BUG-047 are **one bug with four faces**: a file the game reads at runtime was
produced by something that only runs on the developer's machine.

| Bug | The missing input | Why it was missing |
|---|---|---|
| 044 | `Assets/Resources/Art/**` | `.gitignore` called it "derived" |
| 045 | `*.png.meta` for the atlas | nobody thought import settings were source |
| 046 | `frames/<stem>__<frame>.png` | cut by an editor menu |
| 047 | `*.png.meta` for all art | same as 045 |

**The rule that closes it:** if the runtime reads a file, that file — and the import settings that make it
readable — belong in the commit. Before shipping, ask: *what does the build read that a fresh clone would
not have?* `publish_to_unity.py --verify` now answers that question in one command.

## BUG-048 -- the fix for BUG-042 crashed the game in the opposite direction (player report, 2026-11-25)

**Symptom.**

```
[HR//0D] could not start the game: There is no 'MeshRenderer' attached to the "Text" game object,
but a script is trying to access it.
```

**Root cause.** Fixing BUG-042 (`TextMesh.sortingOrder` does not exist) I replaced it with
`GetComponent<MeshRenderer>().sortingOrder` — on **every** object, including one that is a `SpriteRenderer`.
The failing object is named "Text" and it is the letter page's `SpriteRenderer`. `SpriteRenderer` **is** a
`Renderer` and sets `sortingOrder` directly; asking it for a `MeshRenderer` throws.

The real hierarchy, which decides everything:

| Type | Is it a Renderer? | Sort order |
|---|---|---|
| `SpriteRenderer` | **yes** | `spriteRenderer.sortingOrder = n` |
| `Renderer` (any) | yes | `.sortingOrder = n` |
| `TextMesh` | **no** (Component) | `textMesh.GetComponent<MeshRenderer>().sortingOrder = n` |

**Fix.** `DeskRuntimeScene.SetTextMeshSorting(mesh, order)` — one guarded helper used by every `TextMesh`
in the game; `SpriteRenderer`s set their order directly with a comment saying why.

**Gate.** A pattern check over all game scripts fails if
(a) any `SpriteRenderer` asks for a `MeshRenderer`, or (b) any `*.sortingOrder =` lands on a `TextMesh`.
Verified by re-injecting pattern (b) — the gate fails by name.

## BUG-049 -- the merge (2026-11-25)

See below: the merged tree broke the gates in ways neither branch did. One `Renderer` hierarchy; a
missing `EditorApplication` stub; a save test that serialised `{}` because `System.Text.Json` ignores
public fields without `IncludeFields`; and `GetComponent<Renderer>()` reintroduced for a `TextMesh`.
The merge gates are now the same five as a release: compile, tests, scripts, art, fresh clone.

## The real fix: stop finding these one crash at a time

Six boot failures in one day (BUG-042, 044, 045, 046, 047, 048) all had the same shape: **my checks read
the working directory; the player runs the commit.** Anything the working directory had that the commit did
not — a gitignored art folder, a `.meta` Unity generated earlier, frames an editor menu cut — passed the
gate and failed on his machine.

`tools/verify_fresh_clone.py` closes the entire class:

1. `git archive HEAD` → a temp folder, so the tree contains exactly what the commit contains.
2. Every verification runs **inside that folder**: both art mirrors, the frames, the import settings, the
   palette, the proportions, the font, the manifest.

If a clean checkout cannot verify itself, the script fails here instead of on the player's machine. This is
now the last step before any bundle is built, and it is the first thing to run when a bug report arrives
that "works here".

## BUG-049 -- the merge that "worked here" and broke on the peer's machine class

Found while reconciling `origin/main` (19 upstream commits) on 2026-11-25. Nothing crashed at Play;
these were found by running the gates on the **merged** tree, which is the only place they show up.

1. **Two `Renderer` hierarchies in the stub.** The peer branch and this one had each added
   `Renderer` / `SpriteRenderer` / `FindObjectsSortMode` / `FindObjectsByType`. Merged text compiled to
   four `CS0111` (member already defined) errors. Rule that already existed, restated: *one* hierarchy,
   modelled on Unity's real one, in `tools/UnityStubs/UnityStubs.cs`.
2. **`EditorApplication` was missing** from the stubs, so the peer's new Play-mode guard in
   `HRZeroDayMenu` made `CompileCheck` fail with `CS0103`. A stub that lacks a type the game genuinely
   uses is a broken gate, not a strict one.
3. **The peer's save tests proved nothing.** `tools/CoreTests/SaveResumeTests.cs` round-tripped `SaveData`
   through `System.Text.Json`, which **ignores public fields** unless `IncludeFields = true`. Every
   round-trip serialised to `{}`, and 3 of 5 tests failed with "seed changed: 0". `SaveData` is
   fields-only on purpose (`JsonUtility`'s rule), so the mirror, not the data, was wrong.
4. **The renderer mistake came back.** The peer's `DeskRuntimeScene` called
   `label.GetComponent<Renderer>().sortingOrder` for a `TextMesh`. `GetComponent<Renderer>` on a
   `TextMesh` is not guaranteed to return the `MeshRenderer` that carries the order. BUG-048's
   `SetTextMeshSorting` was re-applied over their newer file rather than the other way round.

**Lesson.** A merge has its own gate list: compile, tests, scripts, art, fresh clone -- on the merged
tree, before pushing. Two branches can each be green and the join be broken.

## BUG-055 -- the walk's controls were decoration, and the brackets were two bars (2026-11-27)

The player photographed the walk page showing `WALK . [.SPACE] [.CLOCK OUT` smeared across a band, and a
row of four neon caps with no readable marking on them. Three faults stacked:

1. **The font's square brackets were two vertical bars.** A previous pass changed `[` and `]` to `.#.#.` on
   all seven rows so an arm would not read as a `C` or a `J` at 5x7. The cure was worse: every `[SPACE]`
   on the walk rendered as `I SPACE I I`, which is machine noise and is exactly what the screenshot shows.
   They are real brackets again (`.###.` box with top and bottom hooks), and they are readable at 1x.
2. **The strip was art, not text.** Four neon caps whose marking was a single 5x7 glyph could never tell
   the player which key walks on. The strip is now painted from the same atlas as the copy above it:
   line 1 the page prompt (`[WALK 2/3] [SPACE] WALK ON`), line 2 the player's keys
   (`SPACE WALK [S] SHIFT [X] DESK`). Retiring the cap art freed exactly the 32 px it needs.
3. **`KeyBandTop` was being spent twice.** The prompt band was placed with a y-down constant as if it were
   y-up, and the strip's two lines were drawn from one 15 px texture, so the second line fell off the page.

**Class of bug:** *decoration standing in for instruction.* If the player has to guess a key, the art is
not telling them -- and no amount of polish on the art will fix it. The rule now: **every control the game
shows is spelled out in text the atlas can draw**, and `PageTransformTests` asserts it for the walk.

## BUG-056 -- `Copy-Item -Recurse` into an existing folder makes a double nest (2026-11-27)

`art\art`, `docs\docs`, `tools\tools` — the phantom nesting this project has chased since BUG-021 was not a
bad bundle and not a bad merge. On Windows PowerShell 5.1,
`Copy-Item -Path <dir> -Destination <existing dir> -Recurse` copies the source directory *into* the
destination instead of merging its contents, so every apply created `<dir>\<dir>`, then the next apply's
sweep "removed" it and the next copy made it again. A tree of 474 "modified" files is the same fault seen
from the other end.

**Fix:** create the destination, then copy the source's **contents** (`-Path (Join-Path $src '*')`). The
sweep now asks git whether the nested path is tracked, so it can never delete a real directory, and it says
"nothing to sweep" when there was nothing to sweep.

## BUG-051 -- "clicking on something always opens it too big" (player report, 2026-11-27)

The read-out opened as a card five times the desk. Two separate mistakes, one cause:

1. `Repaint()` created the sprite at `blocksPerUnit` = 16 ppu — which *already* returns a sprite whose
   world size is `pixels / 16` — and then set `localScale = (w/ppu, h/ppu)`. A 176 px panel became
   `176/16 * 176/16 = 121` units wide on a 30-unit camera. **A ppu sprite needs no scale at all.**
2. The panel's anchor was read as a CENTRE by `CalloutCanvasRect()` (the router) but placed by a function
   that skipped the canvas-centre offset, so the drawn panel and the "tap inside to close" rect described
   different places. Same class as BUG-050: two copies of one piece of maths.

**Fix:** `PageTransform` owns the canvas-px -> world conversion (one function, three call sites), the panel
is anchored by its TOP-RIGHT corner in canvas px (left of the applicant's head, never over the face), and
it is sized to its longest line. `Test-ArtPipeline` now fails if a view divides a y-down page offset by the
ppu inline or scales a ppu sprite by its own pixel size; `PageTransformTests` pins the numbers.

**Class of bug:** *a coordinate space converted in two places, one of them wrong.* The guard is structural
because the bug is: the same conversion must not exist twice.

## BUG-052 -- the walk page described a band it did not paint (2026-11-27)

`SweepView` allocated its caption texture for 22 rows and then called `SetPixels32` with a buffer sized for
*that page's* row count. A short buffer is an exception Unity swallows into the log, and the texture keeps
whatever the last page left in it -- which is what put half a sentence of the previous floor over the
current one. The key prompt had the same shape: a sprite placed at `KeyBandTop/Ppu` = 14.5 world units,
i.e. 12 units above the page, past the right edge -- the streak across the top of the walk the player saw
twice.

**Fix:** the band is painted at exactly the texture's size (`BandH`), the prompt is placed with
`PageTransform.TopLeftToWorld`, and the prompt band was raised from 11 px (which clipped its own descenders
at the font's 6 px top offset) to 15 px. Three tests now assert the bands stack without overlapping and
that every trace fits.

## BUG-053 -- a bundle applied from inside the working copy leaves a tree that does not match HEAD

The player's console: `HEAD 2ef358c, working tree not clean (474 entries)`, with a dozen `M` PNGs. The
history was adopted correctly -- pushing worked -- but the file copy then wrote the bundle's tree over a
checkout of a *different* commit, because `-Clean`'s `reset --hard` ran before the files were copied and
`reset --hard` with no argument resets to the OLD head. Nothing was corrupt: the files were right, HEAD was
behind.

**Fix:** `-Clean` now resets the tree to the **bundle's commit** (`$bundleHead`) after copying, so "the
bundle's file set is the truth" is true by construction rather than by hope. The `still nested: art\art`
warnings were the same script asking the filesystem a question only git can answer — it now asks git, and
only for a path the bundle actually tracks.

## BUG-054 -- the walk's caption band was sized from the font's glyph WIDTH

`SweepScript.RowStep` mirrored the font as `MonitorGlyph + 3` -- but `MonitorGlyph` is the glyph **width**
(5), not its **height** (7). So the caption band was sized at 8 px per row while `MonitorScreenRaster` laid
the rows out at 10: every caption's last lines were painted BELOW the band, i.e. straight onto the room art.
That is the "text streaked over the picture" the player photographed on the walk.

The mirror exists because Core may not reference the Game assembly (the dependency runs the other way), so
the metrics are duplicated on purpose and **nothing checked the duplicate**. `PageTransformTests` now asserts
`SweepScript.RowStep == MonitorScreen.GlyphH + MonitorScreen.LineGap`, and `MaxRows` came down 22 -> 20, which
is what actually fits between the band top and the prompt at the true 10 px pitch.

**Class of bug:** *a duplicated constant with no test tying it to the original.* Same family as BUG-050's
duplicated geometry; different flag, same lesson -- share it, or test that the copy still equals the source.

## BUG-050 -- "I can't click anything" (player report, 2026-11-26)

The game booted, the applicant bobbed and blinked, and the only thing that responded was the paper on
the monitor. Three faults, none of which throws:

1. **Every hotspot sat at the applicant's centre.** `BuildHotspots` converted an authored 192x216 rect
   into a child position and divided by the pixels-per-unit but **not** by the applicant layer's
   `scaleFactor` (0.96). All nine landed on the same point, 144.5 px from where they belong. The router
   then hit-tested "inside object origin +- collider/2" — the same wrong point — so the geometry agreed
   with itself and disagreed with the screen. *(The first version of this fix divided by the scale in
   the divisor and STILL failed by 3.84 px, because it also ignored that the applicant sprite is placed
   by its centre; the shared function now derives everything from the rect's canvas centre.)*
2. **The pointer map was camera-derived.** `orthographicSize x pixel aspect` equals the 480x270 canvas
   only in an exactly-16:9 window. Any other window letterboxes the desk and every hit shifts.
3. **The callout was a trap.** It opened at the top-right over part of the face, nothing closed it, and
   further taps went to the desk behind it. Now a tap inside it closes it, an empty-desk tap closes it,
   and it sorts at 60 so a mug cannot hide it.

**The reason no gate caught it:** nothing tested placement. `HotspotReachTests` now reproduces the
runtime placement **through the same function the runtime uses** and asserts, for all nine hotspots, that
a tap on the drawn body part resolves back to that artifact — plus that the object/collider maths lands
on the drawn rect. A gate rule fails the build if inline `rect[...] * 0.5f` maths reappears in a builder.

**Class of bug:** *a rule that is duplicated instead of shared.* The test I first wrote mirrored the
runtime by hand; when it failed I could not tell which copy was wrong. Making both call one function is
what turned "9 of 9 unclickable" into a two-line fix.

## BUG-057 -- the walk page was placed by the wrong sprite, and its copy was laid out by the wrong raster (2026-11-27)

Player, after v96: *"I tried clicking everything in the walk page but nothing responds"* and *"I confirm a
blank flash. After a blank flash it goes to a room with 3 or 4 doors, one of them looks opened."* Both
screenshots matched `art/kit/sweep/sweep_corridor_480x270.png` exactly -- the corridor page, full-bleed,
with no caption band, no key strip, no fade and no HUD.

Three separate faults, one class: **the view painted through code that had been written for another
screen.**

1. **The page was never painted onto the layer.** `SweepView.Show()` set `_background.sprite` to the
   floor ART and then called `Repaint()`. `Repaint()` composed the correct page into `_pageTex` and
   uploaded it -- and never bound it. `Build()` had bound a page sprite at startup, and `Show()` replaced
   it with the art. So the player saw: (a) the bare floor art, (b) then the correct composed page once
   the caption was painted for that page -- which is exactly the "blank flash, then a room I cannot
   interact with" they described. The composed page is now bound in `Repaint()`, in the same statement
   that sets its position, and `Show()` no longer touches the sprite.
2. **The strip and the caption were laid out through the CRT's raster.** `MonitorScreenRaster.Render`
   places a line against the BOTTOM of a `GlassRect` and SKIPS empty strings. The walk's caption is
   `heading, "", body...` and its band height counts the empty row, so every page lost one 10 px of pitch
   and its last line landed on the band's border and the room art. The strip had the same treatment via
   `TextPanel.Paint`'s hard-coded row 6. Both now paint rows that **Core publishes**: `BandRows()` /
   `StripRows()` give each line its glyph-TOP in page pixels, and `RenderRows()` paints top-down with an
   explicit clip. The CRT and the letter keep `Render()` untouched.
3. **The walk was tinted as room art.** `RoomLighting` multiplies every `SpriteRenderer` by the daylight
   stop, and by midnight that is ~0.65 per channel. The walk page -- the one screen in the game that is
   *read* -- was dimmed while it was being read. `SweepView` and `HR_Hud` are now in `NeverTint`.

**Tests that would have caught all three** (added this round, `TextLayoutTests`): the caption test now
drives the real `Sweep.AfterShift -> SweepScript.Build -> SweepPageComposer` path for corruption 0..3 and
asserts where the ink actually lands inside the band (7 pages); the strip test paints through
`StripRows()` + `RenderRows()` and asserts both lines render in full inside `KeyBandBox()` and no ink
outside it. The old tests passed because they painted with the same wrong routine as the view.

**Class of bug:** *a preview that mirrors the view by hand instead of through the view's own functions.*
`tools/comp/preview_sweep.py` showed a perfect page the whole time; it agreed with Core and disagreed with
the game.

## BUG-058 -- the key strip was flipped twice, so the walk's only UI was upside down (2026-11-27)

Player, one screenshot after v97 shipped: *"Bottom 32 px is upside down."* Exactly that: the page's art
and caption were correct, and the two lines in the strip read `ZT3C ]X[ ... ZT3C ]X[` -- mirrored.

**The double flip.** The strip was painted into its own 480x32 `Texture2D`, lowered to texture order with
`FlipToTextureOrder`, uploaded -- and then read straight back out with `GetPixels32()` so it could be
composited into the page. `GetPixels32()` hands rows back **bottom-up**. The composer treats every buffer
it is given as **PNG order, top-down**. So the strip was flipped once on the way in and once on the way
out, and the two cancelled into a picture nobody had looked at.

**The fix is deletion.** There is no strip texture any more. `SweepPageComposer.ComposeStrip()` paints the
two rows that `SweepScript.StripRows()` publishes **straight into the page buffer**, in the same pass and
the same coordinate space as the art and the caption. One buffer, one orientation, one flip right before
`SetPixels32`.

**The test that fails today and would have failed then:** `CheckStripIsNotUpsideDown` composes a whole
page the way the view does, applies the **one** flip the view applies, and then reads the strip's two rows
back out of the texture **as picture rows** -- the prompt must be nearer the page top than the key hints,
with a clean gap between them. Re-instating the old double flip makes it fail with
`the walk strip drew 21 of 24 glyph boxes` (verified by temporarily restoring the defect).

**Class of bug:** *a value that crosses a boundary twice and is transformed at both ends.* The lesson
written into the code: for every buffer in this project, the orientation is stated once, next to its
producer, and nothing reads a texture back to composite it.

## BUG-059 -- a clean install reported a dirty tree, so the player could not build a bundle (2026-11-27)

`Where-Am-I` on the player's machine: *working tree DIRTY -- 264 file(s)*, straight after a clean
`Apply-Bundle`. 140 of them were untracked `unity/Assets/Art/**/*.meta` files and 7 were `scripts\*.ps1`
line-ending churn.

`unity/Assets/Art/Kit/**` is a byte-for-byte mirror of `art/kit/**` that nothing loads at runtime (the game
reads `Assets/Resources`). Unity writes a `.meta` beside every file in it the first time the project opens,
and none of those metas were tracked -- so the first `Play` in a fresh install dirtied the tree, and every
`Build-Bundle` after that refused.

Fixed in `.gitignore`: the mirror's metas are ignored by rule, not by luck. The metas that **matter**
(`Assets/Resources/**`: `isReadable`, point filter, PPU 16) stay tracked, and `Test-ArtPipeline.ps1` still
fails the build if one of them loses `isReadable: 1`. `Where-Am-I` also stops counting the two files that
are *supposed* to differ in a working copy (the root `Apply-Bundle.ps1` bootstrap copy, and `.gitignore`
itself) as dirty, while still counting everything else.

## BUG-060 -- `Where-Am-I` died on a copy with no upstream, halfway through its own report (2026-11-27)

Player's terminal, straight after a clean v98 install:

```
  fetching origin (10s) ...  ok   fetched
git.exe : fatal: no upstream configured for branch 'main'
    + CategoryInfo          : NotSpecified: (fatal: no upstr...r branch 'main':String) [], RemoteException
```

Nothing was wrong with the repo. `Where-Am-I.ps1` runs with `$ErrorActionPreference = 'Stop'`, and in
PowerShell a **native command's non-zero exit becomes a terminating error** under that setting. So the
perfectly normal question *"does this branch have an upstream?"* -- asked with
`git rev-parse --abbrev-ref --symbolic-full-name '@{u}'` -- got the answer *no*, thrown as an exception,
and the report stopped in the middle of section 1. Every section after it (last commits, bundles, gates,
the five commands) was never printed.

A copy installed from a bundle **always** starts without an upstream: `Apply-Bundle` rebuilds `.git` from
`history.bundle` and `Push.ps1` sets the upstream on the first push. So this failed on the most common
path there is.

**Fixed the way `Push.ps1` already did it:** git runs through `System.Diagnostics.Process`, never `&`. One
helper, `GitText`, returns stdout on success and `''` on any non-zero exit, and stashes the first stderr
line in `$script:GitWhy` for the one place that wants to show it (a failed fetch, which now reads
`could not fetch from origin -- offline, or no credential: unable to access ...` instead of a category
dump). Branch, head, dirty state, remote url, upstream, ahead/behind and the fetch all go through it.

**Proven against the real states** (a scratch repo, /tmp/noUp): no remote at all; remote but no upstream;
upstream pointing at a host that does not resolve. All three now print the whole report and end with the
five commands. Before, the third one printed nothing after `fetching origin`.

**Class of bug:** *a diagnostic tool that throws on the state it exists to diagnose.*

# PARKED FOR REPLENISH

Added 2026-11-27 with `docs/82-replenish-phase.md`. A parked item is **not** forgotten and **not** fixed
today: it is scheduled into the replenish phase with a capture. Format: id, what is wrong, owning file,
acceptance test, capture path.

| # | What is wrong | Owning file | Done when | Capture |
|---|---|---|---|---|
| D-1 | **FIXED in v90** (kept here until the player confirms on screen). The after-hours key band (`[WALK n/N] [SPACE] ...`) renders streaked across the top of the page instead of on its band. Content is correct; the C# sprite placement is not. | `unity/Assets/Scripts/Game/Interaction/SweepView.cs` | The band sits on `KeyBandTop` at 1:1, legible, in Play mode | player screenshot, v87 session 3 |
| D-2 | **FIXED in v90.** `Apply-Bundle` prints `still nested: art\art`, `content\content`, ... on a clean tree. Cosmetic: the nesting check reports the *target* of a correct move. | `scripts/Apply-Bundle.ps1` | A clean apply prints no `still nested` line | console paste, 2026-11-26 |
| D-3 | **PARTLY FIXED in v90** (one HUD block, sized from its own proportions). Text scale differs between the desk HUD, the letter and the sweep page; the HUD has been reduced twice by eye. | `GameHud.cs`, `LetterView.cs`, `SweepView.cs` | One documented scale (sizes + steps) used by all three | screenshots v87 |
| D-4 | Cast still reads in three tone families even with four tones wired; faces are crude at 3x. | `tools/art/make_applicants.py` | Full cast sheet shows a continuous spread and a readable 3x face | `/tmp/cast_final.png` (regenerate) |
| D-5 | **STILL OPEN — now with a lead.** The save `IOException at hr0d_save.json` still appears in the Console on quit after the quit-skip. | `unity/Assets/Scripts/Game/Desk/*Save*` | Quitting from Play mode prints no IOException | console paste, v87 |
