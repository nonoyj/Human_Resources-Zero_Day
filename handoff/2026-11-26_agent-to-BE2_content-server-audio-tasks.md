# HANDOFF -- Agent -> **BE2 (Server, content, audio, build)** -- your task list

**Date:** 2026-11-26 - **Read first:** `docs/09-backend-spec.md`,
`handoff/2026-11-25_agent-to-backend-team_backend-spec.md` (full API contract + addendum),
`docs/10-analytics-spec.md`, `docs/13-team-plan.md` #4 (audio contract).

You own **three** areas: the server, all game content (JSON), and the audio, plus the Android build.

---

## A. Server (days 1-8)

1. `backend/` skeleton: FastAPI + Docker Compose (api + postgres + minio); `GET /healthz` -> 200.
2. Migrations: `content_packs`, `runs`, `run_events`, `decisions`, `quiz_results`, `rate_limits`.
3. `GET /v1/content/manifest` + signed immutable packs (`sha256` pinned, hash-named in object storage).
4. `POST /v1/runs`, `POST /v1/runs/{id}/events` (partial accept: `202` with per-item rejects), `/finish`.
5. `validate_pack.py`: refuse a pack that breaks fairness invariants I1-I8 (port the checks from
   `prototype/generator_sim.py`; they are already validated at 46,000 applicants).
6. Research: `GET /v1/research/export?format=csv&minCell=5`, `/tell-matrix`, `/funnel`.
7. Security: rate limits (60/10 min per device), 256 KB body cap, **reject payloads containing
   PII-looking keys** (`name|email|phone|address|ip|free_text|note`), `DELETE /v1/devices/{key}` purge,
   log redaction + a CI grep that fails the build if logs contain PII patterns.
8. Deploy to staging (SG/PH region), HTTPS, backup + **restore drill**, OpenAPI committed, `fixtures/`
   with real request/response pairs so the client can be built without you.

**Fallback that must exist:** the client works fully offline. If the server is down at the event, the
game still plays and research still happens via the in-app *"Export my play data"* button.

## B. Content (days 2-10) -- all JSON, no code

| File | Contents |
|---|---|
| `content/schema/*.json` | Formal schemas for applicant, email, warning, manual entry |
| `content/days/day_01..05.json` | Day plans: applicant count, synthetic count, tell pools, distractor pools |
| `content/emails/` | 12+ templates: lookalike domain, urgency, authority, unexpected attachment, wrong thread, credential request, grant-access, fast-track referral -- each with a legit twin |
| `content/letters/` | Cover letters per archetype: human (warm, specific), bait (pleading, deadline), synthetic (flat, mirrors the job posting) |
| `content/manual/` | Field Manual: 5 day entries + 12 tell entries (wording already written in `docs/07-randomization-spec.md` #5) |
| `content/dialogue/` | 0-2 short lines per archetype (never a confession) |
| `content/endings/` | 1 win + 4 defeat texts, each ending with "what you could have done" |
| `content/intro/` | Skippable intro story script (5 beats) |

**Rule:** every string the player reads lives in these files. No hardcoded English in prefabs or scripts.

## C. Audio (days 4-10) -- you produce, FE plays

Deliver into `unity/Assets/Audio/` with **these exact names** (the FE wires playback):

```
sfx_stamp_approve.ogg  sfx_stamp_reject.ogg  sfx_page_turn.ogg   sfx_terminal_beep.ogg
sfx_error.ogg          sfx_alarm.ogg         sfx_inbox.ogg
music_desk_calm.ogg    music_desk_tense.ogg
amb_office.ogg         amb_crt.ogg
```
OGG Vorbis, 44.1 kHz, mono SFX / stereo music, SFX peak <= -3 dBFS, music <= -14 LUFS, no leading
silence, seamless loops for music/ambience. **Total <= 12 MB.** Missing file = silence, never a crash.

## D. Android build + QA (days 11-14)

Signed APK `0.1.0-demo`, arm64 + armv7, API 24+/35, IL2CPP. Test matrix: 720x1280, 1080x2400, tablet;
save -> quit -> relaunch; background kill mid-shift; offline; low storage; rapid double-tap on stamps.
Sideload instructions + `scrcpy` booth setup for the event laptop.

---

## Don't touch
`Assets/Scenes`, `Assets/Prefabs`, `Assets/Scripts/UI` (FE dev's) - `Assets/Scripts/Core` (systems,
frozen API) - never commit keystores, `Library/`, or `*.apk`.

## Report back in this format
```
STATUS / DONE (files) / NEXT / BLOCKED ON + suggested default / RISK / EVIDENCE (curl output, test log)
```
