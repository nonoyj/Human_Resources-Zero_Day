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
| 3 | **Open the game** | `.\scripts\Open-Game.ps1` | repo root |
| 4 | **Build a bundle** | `pwsh -File scripts\Build-Bundle.ps1 -Version NN` | repo root |
| 5 | **All gates** | `.\scripts\Test-ArtPipeline.ps1` ; `.\scripts\Test-Scripts.ps1` ; `dotnet run --project tools\CoreTests` | repo root |

**Order that never breaks:** push **first**, install a bundle **second**. `Apply-Bundle` rebuilds `.git`
from the bundle's history, so a push after an install needs a force, and `Push.ps1` refuses to ever force.

---

## 2. The block every reply ends with (copy-paste)

```powershell
# 1) install this round's bundle (the zip is in Downloads)
cd $env:USERPROFILE\Downloads
.\Apply-Bundle.ps1 -BundlePath .\HR-ZeroDay-bundle-vNN.zip

# 2) get the repo back on its feet and set the upstream branch (first push only needs the message once)
cd $env:USERPROFILE\HR-ZeroDay
.\scripts\Push.ps1

# 3) open the game -- Unity does NOT need to be closed; Stop, then Play
.\scripts\Open-Game.ps1
```

On a copy installed from a bundle, step 2 is what creates the upstream. If it says *"nothing to push"*,
that is success, not a failure.

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
| Rules tests | `dotnet run --project tools\CoreTests` | `ALL TESTS PASSED` |
| Unity will compile | `dotnet build tools\UnityStubs\CompileCheck.csproj` | `Build succeeded.` |
| Art + palette + stubs + layout | `.\scripts\Test-ArtPipeline.ps1` | `ART PIPELINE OK` |
| PowerShell health | `.\scripts\Test-Scripts.ps1` | `All scripts healthy.` |
| **The commit works on a clean machine** | `python tools\verify_fresh_clone.py` | `PASS: a clean checkout of <sha> verifies itself.` |

The last one exports `HEAD` with `git archive` and runs every other check **inside** that export. It runs
**after** committing and **before** any bundle. Six boot failures came from a gate that checked my working
directory while you ran the commit.

---

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

The game writes `hr0d_save.json` next to the save folder; a `.bak` is rotated on every save.

```powershell
Get-ChildItem -Recurse -Filter hr0d_save* -Path $env:USERPROFILE\AppData\LocalLow | Select-Object FullName, Length, LastWriteTime
```

If the Console still shows an `IOException` naming that file, paste it — that is an open bug, not your fault.

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
| `still nested: art\art` etc. | **cosmetic** false warning from the nesting check | ignore it; queued for cleanup (LATER-LIST entry L-1) |

---

## 8. Things that are deliberately absent

Google Docs handoffs (markdown in `handoff/` only) · Git LFS · force-pushing without
`-ReplaceRemoteHistory` · closing Unity to apply a bundle · storing a credential inside the repo.
