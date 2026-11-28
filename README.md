# Project Codename: HUMAN RESOURCES: ZERO DAY

## Read these first (added 2026-11-27)

| File | Why |
|---|---|
| `docs/80-COMMAND-BOOK.md` | **every command, verbatim** -- bundle, push, Unity, gates, art pipeline |
| `docs/82-replenish-phase.md` | the scheduled polish phase; cosmetic notes are parked there, not chased |
| `HANDOFF.md` | current state in one page |
| `.\scripts\Where-Am-I.ps1` | prints all of the above plus the live state of your copy |


A mobile cybersecurity-awareness game for Android (Unity 6.3 LTS), inspired by the
*document-inspection* genre (*Papers, Please*, *That's Not My Neighbor*) -- but built to teach
**real, transferable AI/phishing detection skills** to non-technical players.

> **One-line pitch:** You are the hiring gatekeeper at the company that holds the world's
> security. Every applicant might be a person. Some of them are DaleGPT.

---

## Start here

**[`HANDOFF.md`](HANDOFF.md)** -- current state, the five things that have cost the most time (all guarded
now), what is next, and where everything lives. One page, no history.

## Repository map

| Path | Purpose |
|---|---|
| `HANDOFF.md` | **current working state -- read this first** |
| `docs/` | Source of truth -- **see `docs/README.md` for the index** |
| `docs/16-the-later-list.md` | Everything still owed, ordered and owner-tagged |
| `docs/06-decisions.md` | Every locked decision, with dates |
| `docs/15-art-bible.md` | Palette, composition and art rules |
| `docs/15-interaction-layer.md` | Hotspot -> evidence -> stamp loop; artifact/tell map and verification |
| `prototype/web/` | Playable browser twin of the first minute (no Unity needed) |
| `unity/` | The Unity project (Assets + Content; no Library) |
| `art/kit/` | Shipped pixel art. `art/kit/reference/` holds the renders you look at |
| `art/concepts/` | Only the two approved mood references + the 5x window crop |
| `tools/` | Generators and tests (`tools/art/`, `tools/comp/`, `tools/CoreTests`) |
| `scripts/` | PowerShell 5.1 tooling: gates, bundle build, git push |
| `handoff/` | Dated agent->team handoff markdown |
| `prototype/` | Applicant-generator fairness simulation (run before content changes) |

> **Not in the repo, on purpose:** `content/` does not exist yet -- JSON content is a later-list item
> (4.1). Builds (`*.zip`, `*.apk`) and the local toolchain (`/tmp`, `.tooling/`) are never committed.

## Status

- [x] Repository skeleton + foundational docs
- [x] Randomised-run generator proven fair (0 violations / 1,000 runs)
- [x] Analytics & research instrument specified
- [x] Windows setup scripts + troubleshooting
- [ ] Decisions locked (see `docs/00-open-questions.md`)
- [ ] Unity project scaffold
- [ ] Vertical slice (1 playable shift)
- [ ] Full 5-day demo + Android build

## Ground rules

1. **Nothing enters `main` without passing the bug checklist** (`docs/04-collaboration-protocol.md`).
2. **Every surprise gets logged** -- no fix without an entry in `docs/90-bug-log.md`.
3. **Content is data, not code.** Writers edit JSON; programmers edit systems.
4. **Every mechanic must map to a real-world lesson** listed in the Field Manual. If it
   doesn't teach anything, it gets cut.
