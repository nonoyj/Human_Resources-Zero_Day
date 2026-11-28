# 80 — THE COMMAND BOOK

**The one page with every command, in the order you actually run them.** Written 2026-11-27 because the
player said it plainly: *"Always refer to our handouts and files here where we have stated every command
to be remembered throughout the project so that we stay consistent."* This is that file. If a command is
not here, it does not exist; if this page and a chat message disagree, **this page wins**.

Two rules for keeping it honest:

1. **Every reply from me ends with the block from § 2.** No exceptions, even when the answer is "nothing".
2. **When this page changes, the reply says so** (one line, in section A).

`.\scripts\Where-Am-I.ps1` prints this page's contents plus the live state of your copy, so you never have
to remember which of these you are on.

---

## 1. The five commands (memorise only these)

| # | What | Command | Run from |
|---|---|---|---|
| 1 | **Install a bundle** | `cd $env:USERPROFILE\Downloads` then `.\Apply-Bundle.ps1 -BundlePath .\HR-ZeroDay-bundle-vNN.zip` | `Downloads` |
| 2 | **Push** (and set upstream) | `cd $env:USERPROFILE\HR-ZeroDay` then `.\scripts\Push.ps1` | repo root |
| 2b | **Push for a bundle installed from Downloads** (the repo was rebuilt, so nothing to push from the old copy) | `cd $env:USERPROFILE\Downloads\HR-ZeroDay` then `.\scripts\Push.ps1` | the extracted copy |
| 3 | **Open the game** | `.\scripts\Open-Game.ps1` | repo root |
| 4 | **Build a bundle** | `pwsh -File scripts\Build-Bundle.ps1 -Version NN` | repo root |
| 5 | **All gates** | `.\scripts\Test-ArtPipeline.ps1` ; `.\scripts\Test-Scripts.ps1` ; `dotnet run --project tools\CoreTests` | repo root |
| 6 | **Where am I + has the team pushed?** | `.\scripts\Where-Am-I.ps1` | repo root |
| 7 | **Reset a copy that arrived dirty** | `git reset --hard` then `git clean -fd` | repo root |
| 8 | **Make `Where-Am-I` work from anywhere** (once per machine) | `.\scripts\Install-Helpers.ps1` | repo root |

**Order that never breaks:** push **first**, install a bundle **second**. `Apply-Bundle` rebuilds `.git`
from the bundle's history, so a push after an install needs a force, and `Push.ps1` refuses to ever force.

---

## 2. The block every reply ends with (copy-paste)

```powershell
# 1) install this round's bundle (the zip is in Downloads)
cd $env:USERPROFILE\Downloads
Set-ExecutionPolicy -Scope Process -ExecutionPolicy Bypass -Force
# the launcher in Downloads is never overwritten by a bundle, so take this round's copy out of the zip
# FIRST -- otherwise you run last round's installer (that is exactly what cost you the v102 apply)
Expand-Archive -LiteralPath .\HR-ZeroDay-bundle-vNN.zip -DestinationPath .\hr0d-vNN -Force
Copy-Item .\hr0d-vNN\Apply-Bundle.ps1 .\Apply-Bundle.ps1 -Force
.\Apply-Bundle.ps1 -BundlePath .\HR-ZeroDay-bundle-vNN.zip

# 2) get the repo back on its feet and set the upstream branch (first push only needs the message once)
cd $env:USERPROFILE\HR-ZeroDay
.\scripts\Push.ps1

# 3) open the game -- Unity does NOT need to be closed; Stop, then Play
.\scripts\Open-Game.ps1
```

**`Where-Am-I` from anywhere, once:** run `.\scripts\Install-Helpers.ps1` in the repo (it writes a tiny
launcher into `%USERPROFILE%\bin`, which is on your user PATH, and it needs no admin rights). After that,
`Where-Am-I` works in any window. There is no `Pin-ToQuickAccess.ps1` — I invented that command once and
it cost you a round; the command book is the only place commands are allowed to come from.

**Why those two lines are there (v103, rewritten v104).** Two plain commands, on purpose: the first version
of this was a single .NET one-liner and the player — correctly — would not call it a PowerShell command.
`Expand-Archive` and `Copy-Item` are things everyone already knows. `hr0d-vNN` is scratch space; each round
overwrites it, and you can delete it whenever. `Apply-Bundle.ps1` in Downloads is not part of any bundle, so it
can only be as new as the last time you refreshed it. v102 shipped a fix for Unity's stray files and your
very next apply ran the *old* launcher: it warned `working tree not clean (105 entries)` and then printed
`bootstrap refreshed (the next apply uses this version)` — i.e. the fix arrived one apply too late. These
lines close that hole from the outside; from v103 on the script also closes it from the inside, by
replacing itself from the zip and re-running before it touches anything.

On a copy installed from a bundle, step 2 is what creates the upstream. If it says *"nothing to push"*,
that is success, not a failure.

---

## 2a. Pushing and merging to `main` (the whole procedure)

"Merge to main" is one operation in this repo, because **`main` is the only branch**: the push *is* the
merge. There is no pull request step and nothing else to approve.

```powershell
cd $env:USERPROFILE\HR-ZeroDay
.\scripts\Push.ps1                  # fetch -> refuse unless fast-forward -> push -> set upstream
```

Three possible endings, and what each means:

| The script says | Meaning | What to do |
|---|---|---|
| `origin/main is already contained in HEAD` then pushes `x..y main -> main` | normal fast-forward | nothing |
| `nothing to push` / *up to date* | your copy already matches GitHub | nothing |
| `behind` / refuses | GitHub has commits your copy does not | `git pull --no-rebase`, then push again |

**Never force.** `Push.ps1` refuses by design; if it ever says the histories diverged, that is a real
question, not a step to skip.

**Order matters on a bundle-installed copy:** push first, update from a bundle second. `Apply-Bundle -Clean`
rewrites `main` to the bundle's history, so a push *after* an update would need a force — and the script
will refuse.

### The team's side

```powershell
git pull --no-rebase
```

That is the entire instruction for BE1/BE2/backend. Their context is `AGENTS.md` (rules) and
`handoff/2026-11-27_agent-to-team_merge-ready.md` (what changed). The extra branch on GitHub,
`arena/c1faebb9-human-resources-zero-day`, is a historical snapshot and **nobody needs it**; leave it alone.

### If you ever do need a real branch

```powershell
cd $env:USERPROFILE\HR-ZeroDay
git switch -c feature/menu
# work, commit
.\scripts\Push.ps1 -Branch feature/menu      # pushes the branch and sets its upstream
# then, on GitHub, open the PR and merge it into main -- do not merge locally and force-push main
```

---

## 3. Unity: open the project (the path nobody wrote down)

**Add this project in Unity Hub, not the repo root:**

```powershell
start "" "$env:USERPROFILE\HR-ZeroDay\unity"
```

Hub → **Add** → **Add project from disk** → `C:\Users\<you>\HR-ZeroDay\unity` → editor **`6000.3.29f1`**
(Unity 6.3 LTS, Android Build Support ticked). Opening the repo root is the classic mistake: there is no
`ProjectSettings\` there, so Hub either refuses or creates a second project.

Then, in Unity, the whole ritual is **press Play**. `GameBoot` builds the desk when the scene is empty.
The `HR//0D` menu (**two** slashes) is only for when the art PNGs themselves changed:

| Menu item | When you actually need it |
|---|---|
| `HR//0D > Setup Project (one click)` | first open after an art change, or a broken Game view |
| `HR//0D > Publish Art To Resources` | `art/kit` changed and publish was not run |
| `HR//0D > Build Desk Scene` | `Assets/Scenes/Desk.unity` is missing or broken |
| `HR//0D > Reload Art Kit (open editor)` | reimport art under a running editor |

**Replaying after any update = Stop, then Play.** Editing while in Play mode is thrown away by Unity —
that is what makes people think the editor must be closed. It does not.

---

## 4. Gates, and the exact string that means "passed"

| Gate | Command | Passes when you see |
|---|---|---|
| Rules tests | `dotnet run --project tools\CoreTests` | `ALL TESTS PASSED` (v119: runs on every gate now) |
| Unity will compile | `dotnet build tools\UnityStubs\CompileCheck.csproj` | `Build succeeded.` |
| Art + palette + stubs + layout | `.\scripts\Test-ArtPipeline.ps1` | `ART PIPELINE OK` |
| PowerShell health | `.\scripts\Test-Scripts.ps1` | `All scripts healthy.` |
| **The commit works on a clean machine** | `python tools\verify_fresh_clone.py` | `PASS: a clean checkout of <sha> verifies itself.` |

The last one exports `HEAD` with `git archive` and runs every other check **inside** that export. It runs
**after** committing and **before** any bundle. Six boot failures came from a gate that checked my working
directory while you ran the commit.

---

### 4a. If `dotnet` is missing on this machine (v119)

The sandbox's build box had **no C# compiler at all** until v119, which is how an uncompilable v118 shipped.
If `dotnet` is not found, install it once — it takes about a minute:

```powershell
curl -sSL https://dot.net/v1/dotnet-install.sh | bash -s -- --channel 8.0 --install-dir $HOME/.dotnet --no-path
$env:DOTNET_ROOT="$HOME/.dotnet"; $env:PATH="$env:DOTNET_ROOT:$env:PATH"
```

Then run the two gates above **before** a bundle. A build error outranks every art change.

## 5. Art pipeline (only when the art changes)

```powershell
python tools\art\make_applicants.py            # regenerate the cast (28 files, 14 archetypes)
python tools\art\check_proportions.py          # structural gate: no floating heads
python tools\art\publish_to_unity.py           # copy the kit into unity\Assets\Resources\Art
python tools\art\make_sprite_manifest.py       # rewrite the manifest
.\scripts\Test-ArtPipeline.ps1                 # the gate that must then say ART PIPELINE OK
```

Rehearsal renders (no Unity, no editor) to check composition before touching the game:

```powershell
python tools\comp\preview_sweep.py             # the after-hours page, incl. the key band
python tools\comp\preview_callout.py           # the read-out panel over an applicant
```

---

## 6. Save file, when something looks stuck

**Where it is** — `Application.persistentDataPath` on Windows is
`%USERPROFILE%\AppData\LocalLow\<CompanyName>\<ProductName>\`, and this project sets
`companyName: NONOYJ`, `productName: HUMAN RESOURCES ZERO DAY`, so:

```powershell
$save = Join-Path $env:USERPROFILE 'AppData\LocalLow\NONOYJ\HUMAN RESOURCES ZERO DAY'
Get-ChildItem -LiteralPath $save -Filter 'hr0d_save*' | Select-Object Name, Length, LastWriteTime
```

**The files:** `hr0d_save.json` (current), `hr0d_save.json.bak` (the previous save, rotated on every
write), `hr0d_save.json.tmp` (only exists for the split second of a write).

**Deleting a save (start a fresh run):**

```powershell
Remove-Item -LiteralPath (Join-Path $env:USERPROFILE 'AppData\LocalLow\NONOYJ\HUMAN RESOURCES ZERO DAY') -Recurse -Force
```

**The `IOException at hr0d_save.json` warning on Stop** is a known open bug (D-5): the run is already
saved after every decision, so nothing is lost — but it should not print. If you see it at any OTHER time
(mid-shift, while playing), that is new and important: paste the full Console line.

---

## 7. When something goes wrong

| Symptom | Cause | Fix |
|---|---|---|
| `bundle not found` | you are in the repo, the zip is in Downloads | `cd $env:USERPROFILE\Downloads` first |
| `Open-Game.ps1 is not recognized` | ran it before applying the bundle | apply first; the script ships inside the zip |
| `The current branch main has no upstream branch` | bundle-installed copy, `.git` was rebuilt | `.\scripts\Push.ps1` (it sets upstream) |
| `Enter Safe Mode?` | a compile error | read the Console, then `.\scripts\Test-ArtPipeline.ps1` |
| apply fails on `.git` | a process holds it | `Apply-Bundle` retries; else close Unity and re-run |
| Game view is black | art not published to Resources | `HR//0D > Setup Project (one click)` |
| `Build-Bundle refuses a dirty tree` | uncommitted changes | `.\scripts\Push.ps1 -Message "..."` first |
| `still nested: art\art` etc. | the nesting check asks git now, not the filesystem (fixed in v90) | if seen again, the BUNDLE really is nested — tell the agent |
| `working tree not clean (474 entries)` right after an apply | `-Clean` now forces the tree to the bundle's commit (fixed in v90) | `git reset --hard` then `git clean -fd` |
| `cannot be loaded because running scripts is disabled` | PowerShell execution policy on a fresh machine | `Set-ExecutionPolicy -Scope CurrentUser RemoteSigned` then answer **Y**, re-run the script |

---

## 8. Repo hygiene, once per copy

```powershell
git config core.autocrlf false          # Apply-Bundle already sets this; harmless to repeat
git config user.name  "Your Name"       # so your commits are not "unknown"
git config user.email "you@example.com"
```

Rebuild a copy whose files and history disagree (`Apply-Bundle` says `working tree not clean`):

```powershell
git reset --hard
git clean -fd
```

`git clean -fd` deletes untracked files — that is the point, and it never touches `Library\` or `Temp\`
because those are ignored, not untracked. **Commit anything you want to keep first.**

---

## 9. Things that are deliberately absent

Google Docs handoffs (markdown in `handoff/` only) · Git LFS · force-pushing without
`-ReplaceRemoteHistory` · closing Unity to apply a bundle · storing a credential inside the repo.
