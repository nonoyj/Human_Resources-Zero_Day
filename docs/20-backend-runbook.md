# Backend -- what exists today, how to run it, what is still yours

**Status:** Day 1-4 of `handoff/2026-11-25_agent-to-backend-team_backend-spec.md` #7 are implemented
and tested. Days 5-9 (security hardening, ops console, deploy, load test, OpenAPI) are **not done** --
they need a real host, credentials, and a deploy decision only you can make.

Run it without Docker (SQLite, 30 seconds):

```powershell
cd backend
python -m venv .venv
.\.venv\Scripts\Activate.ps1
pip install -r requirements.txt
$env:DATABASE_URL="sqlite:///./hr0d.db"
uvicorn app.main:app --reload
# http://127.0.0.1:8000/healthz  ->  {"ok":true,"db":"up","content":"none"}
# http://127.0.0.1:8000/docs     ->  interactive OpenAPI
```

Run it with Postgres + object storage (the real topology):

```powershell
cd backend
copy .env.example .env
docker compose up --build
# api :8000 - postgres :5432 - minio :9000
```

Run the tests (no Docker, no network):

```powershell
cd backend
python -m pytest -q          # 22 tests
```

---

## What is built

| Endpoint | Behaviour | Test |
|---|---|---|
| `GET /healthz` | API + DB liveness, current content version | `test_healthz` |
| `GET /v1/content/manifest` | Bearer content key required; signed, hash-pinned pack list, revoked packs excluded | `test_manifest_requires_content_key` |
| `POST /v1/admin/packs` | **Refuses any pack that breaks I1-I8** -- the only door content enters through | `test_publish_refuses_unfair_pack`, `test_publish_accepts_the_real_day_01` |
| `POST /v1/runs` | Creates a run; UUID-validated; idempotent (retry-safe); refuses PII-looking keys | `test_run_create_is_idempotent` |
| `POST /v1/runs/{id}/events` | **Partial accept** (`202` + per-item rejects); 256 KB body cap; unknown run -> 404 | `test_events_partial_accept`, `test_events_body_cap_256kb` |
| `POST /v1/runs/{id}/finish` | Closes the run, returns rank | `test_finish_and_leaderboard` |
| `GET /v1/leaderboard` | Top 50, anonymous (no names, no device keys) | `test_finish_and_leaderboard` |
| `GET /v1/research/export` | Aggregated counts only, `minCell` respected | `test_research_export_is_aggregated` |

Event types accepted are exactly the taxonomy in `docs/10-analytics-spec.md` #3 -- if the client
sends a type that is not in the spec, the batch is still accepted but that item is reported back as
rejected. That is deliberate: a client bug must not silently corrupt the research table.

The 256 KB cap, the PII-key refusal, and the "reject unknown event types" rule are all in
`backend/app/db.py`; they are the three easiest things to accidentally remove.

## What is still yours (in order)

1. **Object storage wiring** -- packs currently store a `url` string; you must upload the file to
   MinIO/S3 under a hash-derived name and fill `sha256`/`bytes`. Nothing validates that the URL
   actually serves the hash yet.
2. **Rate limits** (60 / 10 min per device key, `rate_limits` table already created) -- not enforced.
3. **Ops console** -- pack list, revoke (`revoked_at` column exists), counters. Read-only HTML page.
4. **`DELETE /v1/devices/{key}`** purge, for the retroactive opt-out the ethics spec promises.
5. **Deploy** SG/PH + HTTPS + backup **restore drill** + the CI grep that fails on PII in logs.
6. **Load/soak** -- 20 devices x 20 runs, p95 < 400 ms.
7. **OpenAPI committed + `fixtures/`** -- real request/response pairs so anyone can build a client
   without running the server.

## Deploy gotcha found while building this

`docker-compose.yml` sets `DATABASE_URL=postgresql://...`, and `backend/app/db.py` opens Postgres
through `psycopg` with `?` placeholders -- that works on SQLite only. **Before deploying with
Postgres, switch the placeholders to `%s` or use `psycopg`'s parameter style**, which is the one
thing in this skeleton that is intentionally still unfinished. Keeping SQLite for dev is fine and
is what the tests use.

## Client contract notes (for whoever wires Unity/Android)

- The game is **offline-first**: every endpoint here is optional at play time. A dead API must
  never block a shift.
- Send `app_open` -> `decision`* -> `day_end`* -> `run_end`, batched at day end and on background.
- `deviceKey` is a random UUID generated at first launch; it is never a hardware id.
- Never send names, emails, free text, or the player's chosen character name.
