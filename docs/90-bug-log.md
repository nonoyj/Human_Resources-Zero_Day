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
