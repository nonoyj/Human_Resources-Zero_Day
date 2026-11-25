# Project Codename: HUMAN RESOURCES: ZERO DAY

A mobile cybersecurity-awareness game for Android (Unity 6.3 LTS), inspired by the
*document-inspection* genre (*Papers, Please*, *That's Not My Neighbor*) — but built to teach
**real, transferable AI/phishing detection skills** to non-technical players.

> **One-line pitch:** You are the hiring gatekeeper at the company that holds the world's
> security. Every applicant might be a person. Some of them are DaleGPT.

---

## Repository map

| Path | Purpose |
|---|---|
| `docs/` | Source of truth: decisions, roadmap, tech stack, protocols |
| `docs/00-open-questions.md` | Every unresolved decision + recommendation + status |
| `docs/01-roadmap-14-days.md` | The 14-day phase plan and task assignments |
| `docs/02-tech-stack.md` | Unity project spec, folder layout, data & save formats |
| `docs/03-title-shortlist.md` | Name candidates and recommendation |
| `docs/04-collaboration-protocol.md` | Git workflow, handoff MD format, bug log discipline |
| `unity/` | The Unity project (created once the editor version is confirmed) |
| `art/` | Generated pixel art (sprites, backgrounds, UI, portraits) |
| `content/` | JSON content: applicants, emails, warnings, field-manual entries |
| `prototype/` | Optional browser playable slice for fast design validation |

## Status

- [x] Repository skeleton + foundational docs
- [ ] Decisions locked (see `docs/00-open-questions.md`)
- [ ] Unity project scaffold
- [ ] Vertical slice (1 playable shift)
- [ ] Full 5-day demo + Android build

## Ground rules

1. **Nothing enters `main` without passing the bug checklist** (`docs/04-collaboration-protocol.md`).
2. **Every surprise gets logged** — no fix without an entry in `docs/90-bug-log.md`.
3. **Content is data, not code.** Writers edit JSON; programmers edit systems.
4. **Every mechanic must map to a real-world lesson** listed in the Field Manual. If it
   doesn't teach anything, it gets cut.
