# backend/ — HR//0D service

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
python -m pytest -q          # 22 contract tests, SQLite, no Docker
docker compose up --build    # the same thing in a container
```

Environment: `DATABASE_URL` (default `sqlite:///./hr0d.db`), `CONTENT_KEY` (bearer key for pack
endpoints; **must** be set in any non-local environment).

## Endpoints

| Method | Path | Notes |
|---|---|---|
| GET | `/healthz` | liveness |
| GET | `/v1/content/manifest` | bearer `CONTENT_KEY`; hash-pinned asset list |
| POST | `/v1/admin/packs` | validates a pack with `validate_pack.py`; refuses I1–I8 violations |
| POST | `/v1/runs` | idempotent; **refuses payloads with PII keys** |
| POST | `/v1/runs/{id}/events` | 202 partial accept, 256 KB body cap |
| POST | `/v1/runs/{id}/finish` | closes a run |
| GET | `/v1/leaderboard` | anonymous aggregate |
| GET | `/v1/research/export` | the analytics export |

## Status (2026-11-27)

Built and tested: everything above, 22 pytest checks in `tests/`.

**Owed, in priority order:** object storage under hash names (and a check that the URL serves the hash) ·
rate limits 60/10 min per device key (the table exists, nothing enforces it) · ops console · a
`DELETE /v1/devices/{key}` purge · deploy + HTTPS + backup restore drill + a PII-in-logs CI grep · a
20 devices × 20 runs soak with p95 < 400 ms · committed OpenAPI + `fixtures/`.

**Before Postgres:** `app/db.py` uses `?` placeholders (SQLite). Switch to `%s` and run the tests against
both.

See `../AGENTS.md` for the rules an agent must follow in this repo, and `../docs/20-backend-runbook.md`
for the operational detail.
