# What to do next — the only page you need this week

**Art kit v2 is in the repo (14 applicant archetypes + 9 AI artifacts).** Nothing to wait for.

---

## Step 1 — Repair the working copy (one command)

Your PC currently has **newer files on an older `.git`** (the bundle transport bug, BUG-021). The
files are all there; only the history pointer is stale. This fixes it without touching your art,
your docs or your GitHub remote:

```powershell
cd C:\Users\User\Downloads
Set-ExecutionPolicy -Scope Process -ExecutionPolicy Bypass -Force
.\Fix-Repo.ps1 -BundlePath .\HR-ZeroDay-bundle-v31.zip
```

`Fix-Repo.ps1` reads the `.zip` **directly** (no extraction, nothing to go stale), rebuilds `.git` from
the verified `history.bundle`, restores `origin`, and ends with the only line that matters:

```
[ok]   working tree clean -- files match HEAD exactly
REPO OK
```

If that line is missing, paste the output — do not commit or push anything yet.

## Step 1b — Push the real commit to GitHub

```powershell
cd C:\users\user\HR-ZeroDay
git log --oneline -1                      # newest message, and NOT dc55e59
git status --short                        # must print nothing
.\scripts\Connect-Remote.ps1 -Push        # fast-forward; no force needed
```

After this, bundles are retired: `git pull` / `git push` only.

## Step 1c — Verify before sending anything to the team

```powershell
.\scripts\Test-Scripts.ps1                # All scripts healthy
.\scripts\Test-ArtPipeline.ps1            # ART PIPELINE OK
dotnet run --project tools\CoreTests       # ALL TESTS PASSED
```

## Step 1d — Hand the team the day-1 status
Send `handoff\2026-11-26_agent-to-team_day1-status-and-next.md`. BE1 starts with Unity project
skeleton (task 1 — it blocks everyone); BE2 starts with content schemas.

## Step 2 — Look at your art (5 minutes)

Open, in this order:
1. `art\kit\reference\composition_preview.png` — the desk as it will look on the phone
2. `art\kit\reference\artifact_legend.png` — human vs AI, same seed, one tell each
3. `art\kit\reference\character_spread.png` — 36 humans + 18 synthetics (the variety proof)
4. `art\kit\reference\applicants_sheet.png` — all 14 archetypes, frame 1 of 4
5. `art\kit\reference\twin_desk_human_solid_a_f0_L0.png` — the engine-accurate desk render (hotspots outlined)

Import rules are in `art\kit\README.md`; nobody clicks them by hand any more — the Editor menu
`HR//0D > Import Art Kit` applies them from `unity_sprites.json`.

## Step 3 — Send these two files to the others (this is the handoff)

| Give to | File | Their first action |
|---|---|---|
| **BE1 (systems)** | `handoff\2026-11-26_agent-to-BE1_systems-tasks.md` | Create the Unity project skeleton (task 1) — **blocks everyone** |
| **BE2 (server/content/audio)** | `handoff\2026-11-26_agent-to-BE2_content-server-audio-tasks.md` | FastAPI skeleton + start content JSON + audio list |

Both files are written so an AI assistant can act on them directly: tasks are numbered, commands are
copy-pasteable, and "don't touch" sections protect the file boundaries.

Tell BE1 explicitly: **"send me the three interface stubs by end of day 2"** (`IDeskDataSource`,
`ISaveService`, `IAudioService`). Without them you cannot build the desk screen.

## Step 4 — Your own work (FE) starts now, art-first

You can begin **before** the Unity project exists, because the art and layout are decided:

1. Sketch the desk layout on `art\kit\backgrounds\office_L0.png` at 480×270 (use any image editor;
   the contact sheet + `art\concepts\desk_concept_A.png` are the reference).
2. Once BE1's project + stubs land: build the **desk screen** — monitor-left / applicant-right,
   document sheet, tool row, stamp tray, reason dropdown. Placeholder applicant = coloured rectangle.
3. Then: main menu → settings → character creation → inbox → field manual → day summary → endings.

Your screens, in the order that unblocks the demo fastest, are listed in `docs\13-team-plan.md` §6.

## Step 5 — Still pending, only you can do it

```powershell
# a) long paths (elevated PowerShell, once) -- Unity asset paths break without it
Set-ItemProperty "HKLM:\SYSTEM\CurrentControlSet\Control\FileSystem" LongPathsEnabled 1

# b) push when the token is ready -- this retires bundles forever
cd C:\users\user\HR-ZeroDay
gh auth login
git remote add origin https://github.com/nonoyj/Human_Resources-Zero_Day.git
git push -u origin main
```

---

## Short answers to what you actually asked

**"Do I need to do anything with the art list?"**
Yes — one thing: **send the two handoff files** (step 3) and **keep the art in the repo**. Nobody needs
to install or convert anything. The kit is plain PNGs already committed; BE1/FE just imports them with
the settings in `art\kit\README.md`.

**"Is it part of the next zip?"**
It was already in the last one (`art/kit/`). v14 is only necessary because it adds the two handoff
files and this page — nothing about the art changed.

**"What am I supposed to be doing day to day now?"**
You are the frontend: scenes, prefabs, UI, animation, transitions, audio playback. Everything else
(systems, server, content, audio files, build) is the other two. The moment BE1 lands the Unity project
and the stubs, your desk-screen work starts and the demo starts looking real.
