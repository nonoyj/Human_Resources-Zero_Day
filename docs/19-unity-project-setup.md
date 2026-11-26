# 19 — Creating the Unity project (you do this, ~25 minutes)

**Decided 2026-11-26:** the user creates the project rather than waiting for BE1, so the week is not
blocked. BE1 adopts it afterwards (see `handoff/2026-11-26_fe-to-BE1_unity-project-created.md`).

**Why this is a separate step at all:** the repo deliberately contains no `ProjectSettings/` or
`Packages/`. A Unity project cannot be opened without them, and hand-writing them is how a team ends up
with a project that cannot build. So we generate them once from the pinned editor and commit them.

---

## 0a. Refreshing the bootstrap (no dragging, no guessing)

`Apply-Bundle.ps1` in Downloads is a **copy** of `scripts\Apply-Bundle.ps1` in the repo, and the repo one
is updated by every bundle. So the repo is the source of truth and Downloads is a launcher:

```powershell
Copy-Item C:\users\user\HR-ZeroDay\scripts\Apply-Bundle.ps1 $env:USERPROFILE\Downloads\Apply-Bundle.ps1 -Force
```

After `scripts\setup-workspace.ps1` has run once, that same line is available as **`Refresh-Bootstrap`**
from any folder inside the repo (the helper walks up to find it).

`Test-Scripts.ps1` asserts the repo-root copy and the `scripts\` copy are byte-identical, so this can't
silently drift again.

### 0b. Pulling from GitHub for the first time (order matters)

A bundle-built repo has **no upstream** and knows **nothing about the remote**, so two things must happen
in this order:

```powershell
cd C:\users\user\HR-ZeroDay
git fetch origin                                  # 1. origin/main must EXIST before you can link it
git branch --set-upstream-to=origin/main main     # 2. now link
git pull --ff-only                                # 3. now it works
```

Doing 2 before 1 gives `fatal: the requested upstream branch 'origin/main' does not exist`, and then
`git pull` still says "no tracking information" -- both harmless, both fixable by running the three
lines in order. `scripts\setup-workspace.ps1` now does exactly this sequence.

## 0. What you need before you start

- Unity **Hub** installed.
- Unity **6.3 LTS**, editor version **`6000.3.29f1`**, with **Android Build Support** ticked
  (plus its SDK/NDK/JDK sub-items).
  If it is not installed: Hub → **Installs** → **Install Editor** → **6000.3.29f1** → tick *Android
  Build Support (with SDK, NDK, OpenJDK)*. It is a ~3 GB download; start it and do something else.
- Your working copy at `C:\users\user\HR-ZeroDay` on **`main`**, clean (`git status -s` prints nothing).

## 0c. The one-button version (`Adopt-UnityProject.ps1`)

Copying two folders by hand is where this step goes wrong, so the repo now does it:

```powershell
cd C:\users\user\HR-ZeroDay
.\scripts\Adopt-UnityProject.ps1 -Source C:\Users\User\Projects\HRZeroDayTemp
```

It proves the source is a Unity project, proves it is **not inside the repo**, proves the editor is
**6000.3.29f1**, copies only `ProjectSettings\` and `Packages\`, and tells you what it skipped.
If any check fails it stops before writing anything.

## 1. Create a throwaway project (outside the repo)

1. Unity Hub → **New project**.
2. Editor version dropdown: **`6000.3.29f1`** — check it, do not accept "latest".
3. Template: **Universal 2D** (this is URP 2D; we do not want 3D or HDRP).
4. Project name: **`HRZeroDayTemp`**.
5. Location: **`C:\Users\User\Projects`** — Unity Hub creates the folder if it does not exist, so you do
   not have to make it first. **Do not point it at `HR-Zero-Day`** and do not use `Select folder` to pick
   the repo: that generates a second `Assets/` inside the repo (the script in §0c refuses that case).
6. Create. Wait for the editor to open fully, then **File → Quit**. Wait until the process is gone
   (Task Manager: no `Unity.exe`). Copying files out of an open editor is how you get a half-written
   `ProjectSettings`.

## 2. Copy the two folders into the repo

PowerShell 5.1:

```powershell
$src = "C:\users\user\temp\HRZeroDayTemp"
$dst = "C:\users\user\HR-ZeroDay"

Copy-Item -Path (Join-Path $src 'ProjectSettings') -Destination $dst -Recurse -Force
Copy-Item -Path (Join-Path $src 'Packages')        -Destination $dst -Recurse -Force

# proof: the pinned version is in the file Unity reads
Get-Content (Join-Path $dst 'ProjectSettings\ProjectVersion.txt')
```

**Expected output:** a line containing `m_EditorVersion: 6000.3.29f1`.
If it says anything else, stop — the wrong editor was used and the project must be recreated.

**Do not copy** `Assets/`, `Library/`, `Temp/`, `Logs/`, `UserSettings/`, or `.csproj` files. The repo
already has `Assets/`; the rest are per-machine junk.

## 3. Open the repo as a project

1. Unity Hub → **Add** → **Add project from disk** → select **`C:\users\user\HR-Zero-Day`**
   (the folder that *contains* `Assets/` and now `ProjectSettings/`).
2. Open it. **If Hub offers to upgrade the project, decline.** It will warn that upgrading is
   recommended — it is not; the pin is deliberate (top risk in `docs/01`).
3. First import takes a few minutes (it compiles the 56-test core, the stubs, and imports 80 sprites).

## 4. What "healthy" looks like

| Check | Expected |
|---|---|
| Console | no errors. Warnings about a missing scene or "no cameras rendering" are fine |
| Project window | `Assets/Art/Kit` with `desk/ backgrounds/ applicants/ ...`, `Assets/Resources` with `desk_layout.json`, `unity_sprites.json`, `palette.json`, `fonts/` |
| Import settings | select any sprite → *Filter Mode* = **Point**, *Compression* = **None**. If not, run `HR//0D → Reimport Art Kit` from the menu |
| Play mode | a black/grey game view. Nothing is built yet — that is correct |

## 5. Commit it (this is the moment BE1 unblocks)

```powershell
cd C:\users\user\HR-Zero-Day
git status -s
git add ProjectSettings Packages
git commit -m "feat(unity): project created on 6000.3.29f1, URP 2D, Android support"
```

`git status -s` before the add will also show `Library/`, `Temp/`, `Logs/` etc. — those must already be
ignored by `.gitignore`. **If they appear and are not ignored, stop and tell me**; committing `Library/`
is a 2 GB mistake.

Optional but recommended, so the whole team lands on the same settings:

```powershell
git add ProjectSettings/ProjectSettings.asset
git commit -m "chore(unity): pin text serialization + visible meta files for merge-safe scenes"
```

(Edit → Project Settings → Editor → *Asset Serialization* = **Force Text**, *Version Control* =
**Visible Meta Files** — required for a 3-person team to merge scenes at all.)

## 6. Then hand it over

Send BE1 the note `handoff/2026-11-26_fe-to-BE1_unity-project-created.md` (or just tell them: *"the
project exists at 6000.3.29f1, pull, do not create one"*). **They were told task 1 was theirs; they need
to hear it is done or they will duplicate it.**

## 7. If something goes wrong

| Symptom | Cause | Fix |
|---|---|---|
| Hub says "project is not valid" | `ProjectSettings/` missing or empty | redo §2; verify `ProjectSettings\ProjectVersion.txt` exists |
| Version upgraded on its own | a newer editor opened it | `git checkout -- ProjectSettings/`, then open with `6000.3.29f1` only |
| Sprites look blurry | import settings lost | run `HR//0D → Reimport Art Kit` |
| `Assets/Scripts` has compile errors | stubs/asmdef mismatch | paste the first error; do not edit asmdefs |
