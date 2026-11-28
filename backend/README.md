# backend/ -- HR//0D service

FastAPI + SQLite/Postgres. **The game never needs it**: it records anonymous telemetry and serves
hash-pinned content packs, and the client plays fully offline.

## Run it

```powershell
cd backend
python -m pip install -r requirements.txt
python -m uvicorn app.main:app --reload --port 8000
# http://127.0.0.1:8000/docs   (auto OpenAPI, dev only)
```

```powershell
python -m pytest -q          # 26 contract tests, SQLite, no Docker
docker compose up --build    # the same thing in a container
```

Environment: `DATABASE_URL` (default `sqlite:///./hr0d.db`), `CONTENT_KEY` (bearer key for pack
endpoints; **must** be set in any non-local environment).

## Endpoints

| Method | Path | Notes |
|---|---|---|
| GET | `/healthz` | liveness |
| GET | `/v1/content/manifest` | bearer `CONTENT_KEY`; hash-pinned asset list |
| POST | `/v1/admin/packs` | validates a pack with `validate_pack.py`; refuses I1-I8 violations |
| POST | `/v1/runs` | idempotent; **refuses payloads with PII keys** |
| POST | `/v1/runs/{id}/events` | 202 partial accept, 256 KB body cap |
| POST | `/v1/runs/{id}/finish` | closes a run |
| GET | `/v1/leaderboard` | anonymous aggregate |
| GET | `/v1/research/export` | the analytics export |
| DELETE | `/v1/devices/{key}` | **purges every run the device ever made** (+ its events, + its rate window) |

## Status (2026-11-25, v120)

Built and tested: everything above, **26 pytest checks** in `tests/`.

**Done in v120** (two of the three that mattered before a public build):

* **rate limits are ENFORCED** -- 60 event writes per 10 minutes per device key, fixed window, `429` with a
  `Retry-After` the client can obey, expired windows purged on every write so the table cannot grow one dead
  row per device forever. The table had existed since v1 with nothing reading it.
* **`DELETE /v1/devices/{key}` exists** -- deletes the runs, their events (explicitly, and the cascade is now
  actually on: SQLite ignores `ON DELETE CASCADE` unless `PRAGMA foreign_keys = ON` is set **per
  connection**, which is the one place SQLite and Postgres differ at runtime) and the key's rate window.
  Answers `200 {"purged": true, "runsRemoved": n}` even for a key it has never seen -- a `404` there would be
  an oracle for which device keys exist.

**Owed, in priority order:** object storage under hash names (and a check that the URL serves the hash) -
ops console - deploy + HTTPS + backup restore drill + a PII-in-logs CI grep - a 20 devices x 20 runs soak
with p95 < 400 ms - committed OpenAPI + `fixtures/`.

**Nothing in this round changes the contract the game uses**: the client never calls the service, so a
player on a plane sees exactly the same game. The two additions are for the operator, not the player.

**Before Postgres:** `app/db.py` uses `?` placeholders (SQLite). Switch to `%s` and run the tests against
both.

See `../AGENTS.md` for the rules an agent must follow in this repo, and `../docs/20-backend-runbook.md`
for the operational detail.
