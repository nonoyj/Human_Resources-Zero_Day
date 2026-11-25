# Push the repo to GitHub — exact steps

Remote (confirmed by you as pushable):
`https://github.com/nonoyj/Human_Resources-Zero_Day.git`

You have **six commits of history** in `HR-ZeroDay-bundle-v2-with-git.zip` (the v1 bundle had no
`.git` folder — my mistake, corrected). Push that history so the commit log reads like real
engineering rather than one giant "initial commit".

---

## Path A — remote is empty (likely) → push my history as-is

```powershell
cd C:\users\user\HR-ZeroDay
git log --oneline                      # expect 7 commits, newest first
git remote -v                          # expect nothing, or the origin URL
git remote add origin https://github.com/nonoyj/Human_Resources-Zero_Day.git
git branch --show-current              # should print: main
git push -u origin main
```

Expected last line: `branch 'main' set up to track 'origin/main'.`

If git asks for credentials: use your GitHub username + a **Personal Access Token** as the password
(GitHub no longer accepts account passwords over HTTPS). Create one at
GitHub → Settings → Developer settings → Tokens (classic) → `repo` scope. To store it:
`git config --global credential.helper manager` (Git Credential Manager ships with Git for Windows).

## Path B — remote already has commits you want to keep

```powershell
cd C:\users\user\HR-ZeroDay
git remote add origin https://github.com/nonoyj/Human_Resources-Zero_Day.git
git fetch origin
git rebase origin/main        # replays my 7 commits on top of what's there
git push -u origin main
```
If `rebase` reports conflicts, stop and send me the output — do not force-push over their work.

## Path C — you only have the v1 bundle (no history) and don't want to re-download

```powershell
cd C:\users\user\HR-ZeroDay
git init -b main
git remote add origin https://github.com/nonoyj/Human_Resources-Zero_Day.git
git add -A
git commit -m "chore: import project docs, scripts and prototype"
git push -u origin main
```
This works fine; you just lose the granular commit history.

---

## After the first push — the workflow for all three devs

```powershell
git pull
git checkout -b feat/your-task-name
# work
git add -A
git commit -m "feat(desk): applicant panel layout"
git push -u origin feat/your-task-name
# open a Pull Request, one reviewer (a teammate or the agent via handoff)
```

### Rules (from `docs/04-collaboration-protocol.md`)
1. `main` must always open in Unity with zero console errors.
2. Never commit `Library/`, `Temp/`, `Logs/`, `*.apk`, or the keystore.
3. One owner per scene/prefab. Announce before touching someone else's.
4. Large binaries (art/audio) must be LFS-tracked — `.gitattributes` is already in the repo.
   On a fresh clone: `git lfs install` then `git lfs pull`.

---

## Verify the push landed correctly

```powershell
gh repo view nonoyj/Human_Resources-Zero_Day --web
```
or on the web: the repo should show `docs/`, `handoff/`, `prototype/`, `scripts/`, `.gitignore`,
`.gitattributes`, `README.md`, and **no** `Library/` folder ever.

Then give your two teammates:

```powershell
git clone https://github.com/nonoyj/Human_Resources-Zero_Day.git C:\users\user\HR-ZeroDay
cd C:\users\user\HR-ZeroDay
git lfs install
.\scripts\Setup-DevEnvironment.ps1
```
