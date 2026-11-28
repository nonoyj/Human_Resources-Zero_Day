# MERGE-READY -- pull `main`, your work queue is below

**From:** the client agent
**To:** BE1 / BE2 / backend / art / audio -- and your AI agents
**Date:** 2026-11-27
**Commit:** `46d9206` on `origin/main`, bundle `v106`

---

## Pull first, read second

```powershell
git pull --no-rebase
```

Then, and only then, read **`AGENTS.md`** at the repo root. It is written for an AI agent working in this
repo: the five gates that must pass before anything is called done, the rule about generated files, the
content pack contract, the audio mix, the backend work queue, and the conventions. **If your agent reads one
file, that is the file.** This note is the summary; `AGENTS.md` is the authority.

## The state you are pulling

- Client: playable start to finish (five days, three endings, intro, after-hours walk, sound).
- **All five client gates green**, and the backend's **22 contract tests green in the same tree** -- I ran
  both before writing this, in this commit.
- There is no half-applied work, no stashed branch, no "will fix tomorrow". `git status` is clean.

## What changed since you last pulled (the short version)

| Version | What it means for you |
|---|---|
| v101 | five-day arc + three endings; `RunLedger` records a run |
| v102 | **the intro slot** -- `content/intro/intro_01.json`, format in `docs/21`, placeholder words, DISPATCH's to replace |
| v103-v104 | install/CLI repairs. Nothing you use changed, except that the installer now fixes itself |
| v105 | **audio exists**: 9 generated sounds, the room bed, 7 cues wired. Mix rules in `AudioRules.cs` + 6 tests |
| v106 | this handover. `AGENTS.md`, `backend/README.md`, `tools/art/replace_assets.py`, `docs/85-rest-stop.md` |

## Backend: your queue, in priority order

Full detail in `AGENTS.md` #5 and `backend/README.md`. Short form:

1. **Object storage** -- upload packs under their hash name, fill `sha256`/`bytes`, and *prove* the URL
   serves the hash it claims. Nothing checks that today.
2. **Rate limits** -- 60 / 10 min per device key. The table exists; nothing enforces it.
3. **Ops console** -- pack list, revoke, counters.
4. **`DELETE /v1/devices/{key}`** -- the retroactive-opt-out purge. This one is an ethics commitment, not a
   nice-to-have.
5. **Deploy** (SG/PH) + HTTPS + a backup **restore drill** + a CI grep that fails on PII in logs.
6. **Soak** -- 20 devices x 20 runs, p95 < 400 ms.
7. **OpenAPI** committed + `fixtures/` for the client's contract tests.

**Known deploy gotcha:** `backend/app/db.py` uses SQLite `?` placeholders. Switch to `%s` before Postgres and
run the tests against both.

## Content: what is yours

- `content/intro/intro_01.json` -- the five beats are a **placeholder I wrote to prove the path**. Yours to
  replace, shape unchanged. Rules and a preview command are in
  `handoff/2026-11-27_agent-to-BE2_intro-slot-ready.md`.
- The **spoken** intro is v107: one clip per beat, optional per beat, silent-safe. Decision recorded as
  ADR-009 in `docs/06-decisions.md`. Add `audio` only when the clip actually exists.
- The 7 sweep trace strings are still placeholders; the 5 ending strings exist.

## Art / audio: replace anything, by name

```powershell
python tools\art\replace_assets.py --list
python tools\art\replace_assets.py --from tools\art\incoming --dry-run
python tools\art\replace_assets.py --from tools\art\incoming
```

Drop files into `tools/art/incoming/` mirroring `art/kit/`. It republishes the manifest, the Unity mirrors
and the import settings, then re-verifies. **Your replacements are pinned** in `art/kit/.replacements.json`,
so running a generator later will not revert your work (use `--force` if you ever want it back).

It refuses, deliberately: wrong-size PNGs (sprites are placed in pixels at 16 ppu), fully transparent PNGs,
WAVs that are not 24 kHz mono 16-bit, and names that are not shipped assets. Each refusal is a mistake that
otherwise looks like success.

## What I would like from you

- **Do not edit generated files by hand.** `art/kit/**` and `unity/Assets/Resources/**` are produced; a
  hand edit is overwritten by the next publish and shows up as a red gate for the next person.
- **Add to `docs/90-bug-log.md`** when you fix something non-obvious, with its *class* of bug. That log is
  how this project has avoided repeating itself.
- **Ask before changing a Core rule.** `unity/Assets/Scripts/Core/` is shared and covered by 152 checks;
  a change there is a change to the game's contract, not a local fix.

## Where the work stops, and how it restarts

`docs/85-rest-stop.md` -- the stopping rule, the restart order, and the first three moves of the next
session. If you are picking this up cold, that page plus `AGENTS.md` is all the context you need.
