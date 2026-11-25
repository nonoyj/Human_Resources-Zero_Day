# Bundle workflow — how to take an update without reinstalling anything

**The problem with what we did before:** delete `HR-ZeroDay`, extract the zip, re-run setup. That
throws away nothing important (the repo is all files), but it is slow, it loses your uncommitted work,
and it makes you re-run Unity imports.

**The better way:** extract in place, overwrite, keep everything else. One command, safety-checked.

---

## Rule of thumb

| Situation | Command |
|---|---|
| **First time on a machine** | `-Target` (setup mode) |
| **You already have `C:\users\user\HR-ZeroDay`** | plain update mode |
| **You want to inspect a bundle without touching your copy** | `-Target D:\scratch\hr0d-check` |
| **You have a GitHub clone and the repo is pushed** | just `git pull` — no bundle needed |

Once you can push/pull from GitHub, **stop using bundles entirely**. Bundles exist only because the
agent cannot reach your remote yet.

---

## First-time setup (once per machine)

```powershell
cd C:\users\user\Downloads
# save HR-ZeroDay-bundle-vN.zip here first
cd C:\users\user\HR-ZeroDay      # if it does not exist yet, skip this line
```

Simplest, since the script lives inside the bundle: extract once manually, then use the script forever.

```powershell
# 1. extract the bundle anywhere temporary
Expand-Archive .\HR-ZeroDay-bundle-v7.zip -DestinationPath .\hr0d-tmp -Force

# 2. run the script from inside it, in setup mode
.\hr0d-tmp\HR-ZeroDay\scripts\Update-FromBundle.ps1 -BundlePath .\HR-ZeroDay-bundle-v7.zip -Target C:\users\user\HR-ZeroDay
```

After that, `C:\users\user\HR-ZeroDay\scripts\Update-FromBundle.ps1` exists and you never extract
manually again.

## Taking an update (every time after)

```powershell
cd C:\users\user\HR-ZeroDay
Set-ExecutionPolicy -Scope Process -ExecutionPolicy Bypass -Force
.\scripts\Update-FromBundle.ps1
```

It will:
1. find the newest `HR-ZeroDay-bundle-v*.zip` in `Downloads` (or use `-BundlePath`)
2. **refuse to run if `git status` shows uncommitted changes** — so you cannot lose work
3. extract to `%TEMP%`, verify it looks like the project, check it carries `.git`
4. copy over your working copy, **skipping** `Library\`, `Temp\`, `Logs\`, `obj\`, `UserSettings\`,
   `Build\`, `.tooling\`, `.cache\`, `bin\`
5. print HEAD before/after and whether the working tree is clean

Why copying `.git` matters: the bundle is a byte-for-byte snapshot of the repo, so replacing `.git`
keeps your history *consistent with the files*. Copying files without `.git` would make every file show
as modified.

### If it refuses because of uncommitted changes

```powershell
cd C:\users\user\HR-ZeroDay
git add -A
git commit -m "wip: my local changes before update"
.\scripts\Update-FromBundle.ps1
```

Or, if the changes are junk you do not want:

```powershell
git checkout -- .
.\scripts\Update-FromBundle.ps1
```

### If you deliberately want to overwrite

```powershell
.\scripts\Update-FromBundle.ps1 -Force
```

---

## Where bundles live

```
C:\Users\User\Downloads\HR-ZeroDay-bundle-v7.zip     <- keep the newest, delete the rest
C:\users\user\HR-ZeroDay\                             <- the working copy (never delete)
```

Delete old bundles freely — they are snapshots, not history. The history lives in `.git`.

---

## When GitHub is connected (the day the token arrives)

Stop downloading zips. From then on:

```powershell
cd C:\users\user\HR-ZeroDay
git pull
```

And when *you* change something: `git add -A; git commit -m "..."; git push`.
Bundles become an artifact only the agent uses to hand you work it cannot push itself.

---

## If an update leaves `folder/folder` duplicates (BUG-011 legacy, fixed in v10)

Older versions of the script nested folders (`docs/docs/`) and left your copy on the old commit. v10
fixes both. To repair a copy that already has the damage:

```powershell
cd C:\users\user\HR-ZeroDay
Set-ExecutionPolicy -Scope Process -ExecutionPolicy Bypass -Force

# 1. update to v10 (this also replaces .git properly, so HEAD finally moves)
.\scripts\Update-FromBundle.ps1 -BundlePath C:\Users\User\Downloads\HR-ZeroDay-bundle-v10.zip -Force -CleanNesting

# 2. confirm
git log --oneline -3          # newest commit should be the v10 bundle's HEAD
git status --short            # should print nothing
dir docs                      # must NOT contain a "docs" folder inside it
```

`-CleanNesting` deletes only the known duplicate paths (`art/art`, `docs/docs`, ...). They were copies,
never source, so nothing is lost. If you prefer to do it by hand:

```powershell
Remove-Item .\docs\docs, .\art\art, .\scripts\scripts, .\tools\tools, .\unity\unity, .\handoff\handoff, .\prototype\prototype -Recurse -Force -ErrorAction SilentlyContinue
```

## Check an update actually worked

An update is only successful if **all four** are true:

```powershell
git rev-parse --short HEAD     # must equal the bundle's newest commit
git status --short             # must be empty
dir <project folder>           # no folder-inside-itself
dotnet run --project tools\CoreTests   # ALL TESTS PASSED
```

---

## Building a bundle (never by hand -- BUG-014 / BUG-015)

```powershell
cd C:\users\user\HR-ZeroDay
.\scripts\Build-Bundle.ps1 -Version 17
```

It refuses to ship if: the bootstrap is missing at the zip root or inside `HR-ZeroDay\`, `.git` is
absent, a tooling cache (`.local`, `.nuget`, `.dotnet`, `node_modules`, `Library`) leaked in, or any
entry path contains a character Windows cannot write (`: * ? " < > |`). It prints size + sha256 to
paste into the handoff note.

Then verify from a clean folder before handing it over:

```powershell
Expand-Archive .\HR-ZeroDay-bundle-v17.zip -DestinationPath .\hr0d-check -Force
cd .\hr0d-check\HR-ZeroDay
git log --oneline -1 ; git status --short
python tools\art\make_art_kit.py --check
python tools\art\make_applicants.py --check
```

---

## Retiring bundles: the first push to GitHub

Bundles exist only because there was no remote. Once the repo is on GitHub, `git pull` / `git push`
replace them.

### Bundle anatomy (since v26)

| File | Purpose |
|---|---|
| `HR-ZeroDay/history.bundle` | the entire git history, verified by `git bundle verify` |
| `HR-ZeroDay/history.branch` | which branch to check out (`main`) |
| `Apply-Bundle.ps1` (zip root and `HR-ZeroDay/`) | bootstrap; rebuilds `.git` from the bundle, keeps your `origin` |

A `.git` folder is **never** shipped -- copying one recursively is what broke v22 (BUG-021).

```powershell
cd C:\users\user\HR-ZeroDay
.\scripts\Connect-Remote.ps1                     # dry run: connect, fetch, report. changes nothing.
.\scripts\Connect-Remote.ps1 -Push               # push when the remote is empty or behind you
.\scripts\Connect-Remote.ps1 -Push -ReplaceRemoteHistory   # only if GitHub made a README commit
```

### Why a plain `git push` can be rejected even though you have access

Being a collaborator grants **permission**; git still has to **authenticate** (prove who you are) on
every push, and the histories must be related:

| Symptom | Real cause | Fix |
|---|---|---|
| `could not read Username` / `Authentication failed` | no credential yet | just push: Git Credential Manager opens a browser; sign in once (no token needed) |
| `Updates were rejected because the remote contains work you do not have` | GitHub auto-created a README/initial commit, so the two histories share no ancestor | `-Push -ReplaceRemoteHistory`, then set the default branch in Settings if it is `master` |
| `Permission denied` / `protected branch` | you are a member but not a writer, or `main` is protected | ask the owner; do not force |

### Credentials: which one do you need?

- **Browser sign-in (default, recommended).** Git for Windows installs Git Credential Manager; the
  first push opens a window, you authorise once, and the token is cached in Windows Credential Manager.
- **Personal Access Token** only when there is no browser (headless/CI). Fine-grained token,
  Contents = Read and write. Paste it when git asks for a *password*.
- **SSH** for a permanent key-based setup: `-UseSsh` after uploading `~/.ssh/id_ed25519.pub`.

Never put a token in a file inside the repo, and never paste it into a handoff doc.
