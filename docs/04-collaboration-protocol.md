# Collaboration Protocol (read this before you push)

## 1. Git in 60 seconds

```powershell
# one-time
git clone <remote> hr-zero-day
cd hr-zero-day
git config user.name  "Your Name"
git config user.email "you@example.com"

# every task
git checkout main
git pull
git checkout -b feat/inbox-screen        # branch per task, kebab-case
# ...work...
git add -A
git commit -m "feat(inbox): malicious-link assessment screen"
git push -u origin feat/inbox-screen     # then open a PR
```

**Branch prefixes:** `feat/`, `fix/`, `art/`, `content/`, `docs/`, `chore/`.
**Commit subjects:** imperative, <= 72 chars, conventional prefixes. One logical change per commit.

### Never commit
`Library/`, `Temp/`, `Logs/`, `*.apk`, `*.aab`, keystores, `.env`, `UserSettings/`.
The `.gitignore` here already covers this -- **verify `git status` before your first push**.

### Merge rules
1. `main` must always open without console errors.
2. PRs need **one reviewer** (any other teammate or me).
3. **One owner per scene/prefab.** If you must edit someone else's, announce it in the group first.
4. Force **text serialization** (`Edit -> Project Settings -> Editor -> Asset Serialization -> Force Text`)
   and LFS for `*.png *.psd *.wav *.mp3 *.ttf *.otf *.fbx`.
5. If `main` breaks: the person who merged fixes it *before* any new work. That's the rule.

## 2. Handoff files (how we talk without talking)

Every task that crosses a boundary (art -> code, content -> code, code -> QA) gets a file in
`handoff/` named `YYYY-MM-DD_<from>-to-<to>_<topic>.md`:

```markdown
# HANDOFF -- Content -> Systems -- Day 02 emails
**From:** C   **To:** A   **Date:** 2026-11-28   **Branch:** content/day-02-emails

## What is done
- 6 emails authored in `content/emails/day_02.json`, schema v1.2

## What you must do
- [ ] Load `trueSender` into the header UI (currently only showing `from`)
- [ ] Wire `actions[]` to the action bar; `report` should trigger `TrustService.ReportPhish`

## Contract changes
| Field | Before | Now | Breaking? |
|---|---|---|---|
| `trueSender` | -- | string | No (additive) |

## Known issues / do not touch
- `d2_e03` body text is placeholder, do not balance difficulty around it

## How to verify
1. Open `04_Desk`, press Play, set `DayManager.DebugDay = 2`
2. Open Inbox: 6 emails, first one shows the mismatched sender in amber
```

**Rule:** if it isn't in a handoff file, it didn't happen. Chat messages are ephemeral; the repo is not.

## 3. Bug log discipline -- `docs/90-bug-log.md`

Every non-trivial bug gets an entry **before** the fix:

```
### BUG-014 -- Save file corrupts when app is backgrounded mid-shift
- Symptom / Repro / Root cause / Fix / Regression test / Prevention rule
```

The **Prevention rule** line is the important one -- it's how we stop repeating problems.
Examples of prevention rules we'll accumulate:
- "Never write directly to the save path; always write `.tmp` then `File.Replace`."
- "Never `FindObjectOfType` in `Update`."
- "Never hardcode an applicant's name in a prefab; it comes from JSON."
- "Every new screen ships with a back button + safe-area anchor or it doesn't merge."

## 4. Definition of Done (per task)

- [ ] Runs in-editor with **zero** console warnings introduced
- [ ] Tested on a real Android device (or at minimum in Device Simulator at 1080x1920)
- [ ] Works with save -> quit -> relaunch -> resume
- [ ] All user-facing strings externalised (no hardcoded English in prefabs/scripts)
- [ ] Touch targets >= 48 dp, works one-handed
- [ ] Handoff file written if it crosses a boundary
- [ ] Bug log updated if anything surprised you

## 5. Escalation

1. Stuck > 30 min -> post in the group with: what you expected, what happened, the console log.
2. Disagreement on design -> the open-questions doc decides; if it's not there, I decide and document it.
3. Anything that touches *the game's teaching goal* (what the player learns) -> escalate to the whole
   team, not a solo call.

## 6. Working with me (the agent)

- Give me the **error text + the file/function** and I'll root-cause it.
- I can't open your Unity editor, so I need: console output, the script file, and what you clicked.
- I write code/content here in the workspace as reviewable files with commit messages; you copy
  or apply them, then commit. I keep `docs/90-bug-log.md` and handoffs current.
- I will **ask before guessing** on anything that changes the design or costs more than ~30 min.
