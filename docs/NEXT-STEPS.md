# What to do next — the only page you need this week

**Art kit v2 is in the repo (14 applicant archetypes + 9 AI artifacts).** Nothing to wait for.

---

## Step 1 — Take the bundle (2 commands)

Download **`HR-ZeroDay-bundle-v16.zip`** and **`Apply-Bundle.ps1`** into `C:\Users\User\Downloads`, then:

```powershell
cd C:\Users\User\Downloads
Set-ExecutionPolicy -Scope Process -ExecutionPolicy Bypass -Force
.\Apply-Bundle.ps1 -BundlePath .\HR-ZeroDay-bundle-v16.zip -Clean
```

Confirm it worked:

```powershell
cd C:\users\user\HR-ZeroDay
git log --oneline -1                       # eed990f  (32 commits)
git status --short                         # should print nothing
python tools\art\make_art_kit.py --check   # PASS: every pixel is palette-conformant
python tools\art\make_applicants.py --check # PASS: palette-conformant, no partial alpha
```

## Step 2 — Look at your art (5 minutes)

Open `art\kit\reference\applicants_sheet.png` (14 archetypes), `artifact_legend.png`
(human vs AI, side by side) and `character_spread.png` (36 humans + 18 synthetics proves the variety). Import rules are in `art\kit\README.md`:
Point filter, no compression, PPU 16, 9-slice border 8 px, Full Rect mesh.

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
