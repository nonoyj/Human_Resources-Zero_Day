# AGENTS.md — read this first if you are an AI agent working in this repo

**Repo:** *Human Resources: Zero Day* — a 480×270 Unity 6 game (HR screener vs synthetic applicants),
plus a FastAPI backend, a generated art/audio kit, and five gate scripts.

**Your owner is not me.** This file is written by the agent that built the client, for the agents that work
on the backend, content and art. It exists because the same five mistakes have already cost this project
several rounds. Read it once, properly, and you will not repeat them.

---

## 1. The five rules that matter more than any task

1. **Never break the gates.** Before you say a task is done, these must all pass. If one fails and the
   failure is not yours, say so explicitly rather than "fixing" another team's area.
   ```powershell
   dotnet run --project tools\CoreTests                       # expect: ALL TESTS PASSED
   dotnet build tools\UnityStubs\CompileCheck.csproj           # expect: Build succeeded.
   .\scripts\Test-ArtPipeline.ps1                              # expect: ART PIPELINE OK
   .\scripts\Test-Scripts.ps1                                  # expect: All scripts healthy.
   python tools\verify_fresh_clone.py                          # expect: PASS: a clean checkout of <sha> verifies itself
   ```
   Backend:
   ```powershell
   cd backend
   python -m pip install -r requirements.txt
   python -m pytest -q                                         # expect: 22 passed
   ```

2. **A generated file is never edited by hand.** Everything under `art/kit/` and
   `unity/Assets/Resources/Art/Kit/` is produced by scripts and hash-checked. To change an asset, *replace it
   by name*:
   ```powershell
   python tools\art\replace_assets.py --list                       # every replaceable name
   python tools\art\replace_assets.py --from tools\art\incoming --dry-run
   python tools\art\replace_assets.py --from tools\art\incoming    # replaces + republishes + re-gates
   ```
   Replaced files are **pinned** in `art/kit/.replacements.json`; the generators refuse to overwrite them.
   Editing a PNG in `Assets/Resources/...` directly is wasted work: the next publish overwrites it.

3. **The client never depends on the server.** The game is fully playable offline; the server records
   anonymous telemetry and serves hash-pinned packs. Any change that makes a network call sit in the
   *critical path* of starting a shift is a design error, not a feature.

4. **PII stays out of the backend, by construction.** No display names, no free text, no file paths from the
   player's machine. The `/v1/runs` endpoint refuses payloads containing PII keys — do not "relax" that
   check to make a test pass; fix the payload.

5. **Write down what you learned.** A fix without a line in `docs/90-bug-log.md` (with its *class* of bug)
   will be re-broken by the next agent. That log is the project's memory.

---

## 2. Where things are

| Path | What it is | Who owns it |
|---|---|---|
| `unity/Assets/Scripts/Core/` | pure C# rules and data, **no UnityEngine** — testable headless | shared; change carefully |
| `unity/Assets/Scripts/Game/` | views and wiring (MonoBehaviours) | client |
| `tools/CoreTests/` | the headless test runner (152 checks) | shared |
| `tools/art/`, `tools/audio/` | generators, publishers, `replace_assets.py` | art/audio |
| `art/kit/` | **generated** art + audio + manifests + `reference/` sheets | art/audio |
| `content/` | authored JSON: days, emails, letters, **intro script** | content |
| `backend/` | FastAPI service, SQLite/Postgres, pytest contract tests | backend |
| `docs/` | specs and the bug log. `docs/21-content-pack-format.md` is the content contract | shared |
| `handoff/` | dated one-way notes. **Archive — may be out of date** | — |

**The current state of truth** is `HANDOFF.md`, `docs/83-phase-tracker.md` and `docs/90-bug-log.md`.
If anything else disagrees with them, they win.

---

## 3. The content pack contract (you will need this)

The client reads JSON packs through `ContentLoader`, hash-pinned by `art/kit/unity_sprites.json`-style
manifests. Validate any pack before shipping it — the admin endpoint refuses packs that break the fairness
invariants I1–I8:

```powershell
python backend\validate_pack.py <pack.json>     # same code path the server uses
```

**The intro is the content team's**, not the backend's: `content/intro/intro_01.json`
(`{id, version, beats:[{speaker,text,hold}]}`, 1–8 beats, ≤90 ASCII atlas chars, hold 0.5–8 s, total ≤25 s).
A beat that cannot be painted is dropped at runtime, with a reason, and the desk starts.
Read `handoff/2026-11-27_agent-to-BE2_intro-slot-ready.md` before touching it. The spoken-intro clips are
**v107** and are additive: `audio` per beat, optional, silent-safe (ADR-009 in `docs/06-decisions.md`).

---

## 4. Audio, specifically (added v105)

Sounds are generated (`tools/audio/make_audio.py`) **and** replaceable by name. The mix is arithmetic in
`unity/Assets/Scripts/Core/Rules/AudioRules.cs` and is covered by 6 tests:

- event cues clear **6 dB** over the room bed; texture cues (steps, keys) clear **4 dB**
- `muted` means **exactly 0.0 gain**, never "very quiet"
- corruption 0 is bit-exactly neutral; corruption 3 flattens the bed 1.5 semitones
- the same cue inside 40 ms is one sound, keyed by **sample + pitch**

If you replace a sound, run `replace_assets.py` (it re-measures and updates the manifest). If you need a
*different level*, change the number in `AudioRules.cs` and update `AudioTests.cs` in the same commit —
never by editing a WAV's peak and hoping.

---

## 5. Backend: what is built, what is owed

Built and tested (22 pytest checks, `backend/tests/`): `/healthz`, `/v1/content/manifest` (bearer +
hash-pinned), `/v1/admin/packs` (validates), `/v1/runs` (idempotent, PII-key refusal),
`/v1/runs/{id}/events` (202 partial accept, 256 KB cap), `/v1/runs/{id}/finish`, `/v1/leaderboard`,
`/v1/research/export`.

Still owed, in priority order — **this is your work queue**:

1. **Object storage.** Upload packs to MinIO/S3 under their hash name and fill `sha256`/`bytes`. Nothing yet
   proves the URL serves the hash it claims.
2. **Rate limits** — 60 requests / 10 min per device key. The `rate_limits` table exists; nothing enforces it.
3. **Ops console** — pack list, revoke, counters.
4. **`DELETE /v1/devices/{key}`** — purge for retroactive opt-out (the research-ethics commitment).
5. **Deploy** (SG/PH) + HTTPS + a backup *restore drill* + a CI grep that fails if PII appears in logs.
6. **Load/soak**: 20 devices × 20 runs, p95 < 400 ms.
7. **OpenAPI** committed + `fixtures/` for the client's contract tests.

**Deploy gotcha, already known:** `backend/app/db.py` is SQLite-only (`?` placeholders). Before Postgres,
switch to `%s` and test both — the `DATABASE_URL` plumbing already supports both.

---

## 6. Conventions that are not negotiable

- **Windows PowerShell** for all commands; the command book is `docs/80-COMMAND-BOOK.md`. Never invent a
  command — if you need a new one, add it to that file.
- **Commit messages**: `type(scope): what changed and why it matters`, imperative, with the *class* of bug
  if it fixes one. Look at `git log --oneline -20` for the shape.
- **Never force-push.** `.\scripts\Push.ps1` refuses by design and tells you to pull.
- **`git pull --no-rebase`** — this repo's history is linear on purpose.
- **No new dependency without a reason**; the client is a Unity project on Windows and the backend must
  install from `requirements.txt` alone.
- **Tests are the contract.** A number with no test is a rumour (see BUG-065 for how that cost a round).

---

## 7. If you are stuck

1. `.\scripts\Where-Am-I.ps1` — prints the live state of the repo and the next command.
2. `docs/90-bug-log.md` — search for your symptom; the *class* of bug is usually there.
3. `docs/00-open-questions.md` — decisions not yet made.
4. Ask for a decision rather than guessing at a design that another team owns.
