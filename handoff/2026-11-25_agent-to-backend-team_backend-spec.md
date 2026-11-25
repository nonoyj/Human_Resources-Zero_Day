# HANDOFF — Agent → **AI Backend Team**

**From:** Arena agent · **To:** the AI assisting the backend devs · **Date:** 2026-11-25
**Status:** spec ready for review · **Read in full before writing code**

---

## 1. What the backend actually is (read this twice)

`HR//0D` is a **single-player, offline-capable mobile game**. There is no login, no multiplayer,
no real-time service. So "backend" here means three narrow jobs, and it is critical you do not
build more than this:

1. **A read-only content delivery API** — so content (applicants, emails, attacks, field-manual
   entries) can be tuned *after* the APK is installed, and so the "daily attack briefing" can be
   pushed to event devices without a rebuild.
2. **A local run ledger sync** — anonymous telemetry needed for the educational evaluation
   (which tells players miss, where they fail) and for the event's leaderboard screen.
3. **A tiny ops console** for us: flip a content pack live, see run counts, revoke a pack.

**Explicitly out of scope:** accounts, auth beyond a device key, payments, cloud saves, chat,
anything that personalises content per player, and any "AI detection service". The AI in the game
is *authored content*; it never calls a model at runtime. If you find yourself designing a
database of real people's resumes — stop, that breaks the privacy story and the game's message.

### The reason this matters to the game's thesis
The game argues that AI systems become dangerous through *quiet, plausible, authorised access*.
Your API is the in-world "NEXUS content server" — keep it least-privilege, signed, versioned and
auditable. The team will point at it during the event as an example of doing it right.

## 2. Constraints (non-negotiable)

| Constraint | Value | Why |
|---|---|---|
| Client offline-first | Game is 100% playable with the server unreachable | Booth Wi-Fi will fail; judges will unplug the router |
| Max sync payload | ≤ 256 KB per request, ≤ 5 requests per run | Players are on mobile data |
| Auth | Bearer **content key** (public, in the APK) + per-install **device key** for writes | No PII, no accounts |
| PII | **Zero.** No names, no emails, no IP storage beyond a 24 h rate-limit counter | Ethics + GDPR/PDPA posture |
| Determinism | Content packs are immutable and **hash-pinned**; client refuses to load a pack whose hash mismatches | Reproducible runs, anti-tamper |
| Uptime need | Only during the event (± 1 week). No SLA after | Keeps cost near zero |
| Latency | p95 < 400 ms from PH/SG region | Feels instant on a booth phone |

## 3. Architecture (boring on purpose)

```
        ┌──────────────────────────┐
        │  Event devices (Android) │   client: Unity 6.3, offline JSON cache
        └────────────┬─────────────┘
                     │ HTTPS (content key + device key)
        ┌────────────▼─────────────┐
        │  API  — FastAPI (Python) │   or NestJS if the team prefers TS
        │  stateless, 2 workers    │
        └────┬──────────────┬──────┘
             │              │
   ┌─────────▼────┐  ┌──────▼──────────┐
   │ PostgreSQL 16│  │ Object storage  │  content packs (*.json.zst, hash-named)
   │ ledger+meta  │  │ (S3 / R2 / B2)  │
   └──────────────┘  └─────────────────┘
             │
   ┌─────────▼──────────────────┐
   │ Ops console (static HTML + │  flip packs, view counters, revoke
   │  2 read-only endpoints)    │
   └────────────────────────────┘
```

**Recommendation: FastAPI + PostgreSQL + S3-compatible storage.** Python because the content
validators already exist in Python (`prototype/generator_sim.py`), TypeScript/NestJS is acceptable
if the team is faster in it. Do **not** introduce Kubernetes, message queues, Redis, or microservices
for a 2-week event. A single container + managed Postgres is correct.

## 4. API contract (freeze this; client is being written against it now)

Base: `https://api.hrzeroday.example/v1` · All responses `application/json; charset=utf-8`
Errors: RFC 7807 `{ "type", "title", "status", "detail", "traceId" }`

### 4.1 `GET /content/manifest`
Headers: `Authorization: Bearer <contentKey>`
```json
{
  "schemaVersion": 2,
  "packs": [
    { "id": "core-applicants", "version": "1.4.0",
      "url": "https://cdn.../core-applicants-1.4.0.json.zst",
      "sha256": "9f2c…", "bytes": 48211, "required": true,
      "minClient": "0.1.0", "channels": ["demo"] }
  ],
  "generatedAt": "2026-11-25T04:00:00Z"
}
```
- Client compares `version` + `sha256` with its cached copy; downloads only what changed.
- `required: false` packs may be skipped if the client is low on space.

### 4.2 `POST /runs` — start a run ledger
Body:
```json
{ "deviceKey": "uuid-v4", "runId": "uuid-v4", "seed": 882731,
  "build": "0.1.0-demo", "startedAt": "2026-11-25T05:00:00Z",
  "contentVersions": { "core-applicants": "1.4.0" } }
```
`201` → `{ "accepted": true, "serverTime": "..." }`. Idempotent on `runId`.

### 4.3 `POST /runs/{runId}/events` — batched, append-only
```json
{ "events": [
  { "t": "2026-11-25T05:03:12Z", "type": "decision",
    "applicantId": "d1_9f3a1c", "groundTruth": "synthetic",
    "decision": "approve", "reason": "SYNTHETIC_MEDIA",
    "evidenceSeen": ["portrait_asymmetry"], "toolUsed": "metadata_scan",
    "secondsOnCard": 41 },
  { "t": "…", "type": "tool_used", "tool": "id_registry", "result": "VALID" },
  { "t": "…", "type": "day_end", "day": 1, "quotaMet": true, "trust": 78 }
] }
```
`202` accepted. Server **must** reject payloads > 256 KB with `413` and unknown `type` with `422`
but still `202` the known ones (partial success is a field on the response:
`{ "accepted": 12, "rejected": [{"index":13,"why":"unknown type"}] }`).

### 4.4 `POST /runs/{runId}/finish`
```json
{ "outcome": "survived", "daysCompleted": 5, "syntheticsHired": 1,
  "wrongfulRejections": 2, "finalTrust": 61, "taughtTells": ["exp_vs_grad"] }
```
`200` → `{ "rank": 14, "totalRuns": 260 }` (rank is optional; client hides it if absent).

### 4.5 `GET /leaderboard?channel=demo&window=event`
Read-only, top 50, **no names** — only `{ rank, survivedDays, syntheticsCaught, wrongfulRejections, runHash }`.
The client displays these as badge-ID strings. Never accept a user-supplied display name (abuse vector).

### 4.6 `GET /healthz` → `{ "ok": true, "db": "up", "content": "1.4.0" }`

## 5. Data model (PostgreSQL)

```sql
create table content_packs (
  id             text not null,
  version        text not null,
  sha256         text not null,
  bytes          int  not null,
  url            text not null,
  required       boolean not null default true,
  min_client     text,
  channels       text[] not null default '{demo}',
  published_at   timestamptz not null default now(),
  revoked_at     timestamptz,
  primary key (id, version)
);

create table runs (
  run_id         uuid primary key,
  device_key     uuid not null,          -- random per install, not a person
  seed           bigint not null,
  build          text not null,
  started_at     timestamptz not null,
  finished_at    timestamptz,
  outcome        text,                   -- survived | defeated_*
  days_completed int,
  synthetics_hired int,
  wrongful_rejections int,
  final_trust    int,
  content_versions jsonb not null default '{}'
);

create table run_events (
  id bigserial primary key,
  run_id uuid not null references runs(run_id) on delete cascade,
  t timestamptz not null,
  type text not null,
  payload jsonb not null
);
create index on run_events (run_id, t);
create index on run_events (type);

create table rate_limits (            -- no IPs stored after 24h
  key text primary key,
  window_start timestamptz not null,
  count int not null,
  purge_after timestamptz not null
);
```
Retention: `run_events` 180 days, `rate_limits` purge 24 h (cron), `runs` kept (aggregate value).

## 6. Five endpoints' worth of security you must actually implement

1. **Content key is public — treat packs, not requests, as the trust boundary.** Sign each pack
   (`sha256` in the manifest) and have the client verify. A stolen content key must be worthless.
2. **Rate limit** per `deviceKey` (60 req/10 min) and per IP (600 req/10 min, IP not persisted).
3. **Body size cap** 256 KB, `Content-Type` enforced, JSON schema validation, no `eval`-ish parsing.
4. **No PII in logs.** Redact headers; log `runId`/`traceId` only. Add a CI check that greps logs for
   email/phone patterns and fails the build.
5. **Content review gate.** Publishing a pack runs `validate_pack.py` (schema + fairness invariants
   I1–I8 lifted from `prototype/generator_sim.py`). A pack that produces an unprovable AI is
   **rejected** — this is the product's integrity guarantee, not a nice-to-have.

## 7. Deliverables & order (2 weeks, one backend person part-time)

| Day | Deliverable | Done when |
|---|---|---|
| 1 | Repo `backend/` skeleton, Docker Compose (api + postgres + minio), `.env.example` | `docker compose up` gives `/healthz` 200 |
| 2 | Migrations + `content_packs` + manifest endpoint serving a static pack from minio | Game's ContentLoader can fetch a pack by URL+hash |
| 3 | `runs` + `run_events` + idempotency + partial-accept semantics | 1,000 synthetic events replay with no duplicates |
| 4 | `validate_pack.py` (schema + I1–I8) wired into a `POST /admin/packs` publish path | Bad pack is refused with a readable reason |
| 5 | Rate limits, body caps, log redaction, CI PII grep | Load test 50 rps green |
| 6 | Ops console (static page): pack list, revoke, run counters | Non-technical teammate can revoke a pack |
| 7 | Deploy to a managed host in SG/PH region, HTTPS, daily backup + restore drill | Restore drill passes, documented |
| 8 | **Load/soak**: 20 devices × 20 runs against staging | Zero 5xx, p95 < 400 ms |
| 9 | Handoff to client: OpenAPI spec + Postman collection + `docs/backend-client-contract.md` | Dev A integrates in one sitting |
| 10+ | On-call for the event; freeze content 48 h before | — |

## 8. Contract hygiene (how we avoid integration pain)

- Publish **OpenAPI 3.1** at `/openapi.json` and commit the generated `openapi.yaml` to the repo.
- **Version the contract**: additive fields only in `v1`; breaking changes go to `/v2`.
- Commit a **`fixtures/`** folder with real request/response pairs — the client tests against fixtures,
  not against a running server, so the two teams never block each other.
- The client must have a **`FakeBackend`** implementation behind the same interface so the game is
  never blocked by the backend. If the backend team slips, we ship with `FakeBackend` and note it.
- Any change to §4 must arrive as a handoff file: `handoff/YYYY-MM-DD_backend-to-systems_api-change.md`.

## 9. Things you should push back on

- If someone asks for **user accounts** or **cloud saves** → no, out of scope, ADR-004.
- If someone asks the backend to **score** the player → no, scoring stays client-side and
  deterministic; the server only records.
- If someone wants the server to **generate applicants at runtime** → no. Randomised generation is
  client-side from a seed; otherwise the game stops working offline.
- If someone wants **real resumes / real people's data** as fixtures → absolutely no. All fixture
  data is synthetic and clearly fictional (`docs/08-behaviour-model.md` has the naming pools).

## 10. Feedback format for your human

```
STATUS / DONE (files) / NEXT / BLOCKED ON + suggested default / RISK / EVIDENCE (curl output, test log)
```

Surface decisions the game team must ratify as ADRs (`docs/06-decisions.md`): hosting region and
cost, telemetry retention, whether the leaderboard ships at all, and whether content packs may be
updated during the event (I recommend: no — freeze 48 h before).

## 11. Local dev quickstart you should write on day 1

```powershell
git clone https://github.com/nonoyj/Human_Resources-Zero_Day
cd Human_Resources-Zero_Day
# backend/ to be created here
cp backend/.env.example backend/.env
docker compose -f backend/docker-compose.yml up --build
# -> http://localhost:8000/healthz
# seed a pack:
python backend/tools/seed_pack.py --pack content/packs/core-applicants-1.0.0.json
curl -H "Authorization: Bearer dev-content-key" http://localhost:8000/v1/content/manifest
```

---
*Repo is the source of truth. If this spec and the repo disagree, the repo wins — and tell me.*
