"""HR//0D backend API -- FastAPI, stateless, offline-first client.

Contract: handoff/2026-11-25_agent-to-backend-team_backend-spec.md section 4.
The game is fully playable with this server unreachable; the server only
records anonymous telemetry and serves hash-pinned content packs.
"""
from __future__ import annotations

import json
import os
from datetime import datetime, timezone

from contextlib import asynccontextmanager

from fastapi import FastAPI, Header, Request
from fastapi.responses import JSONResponse

from . import db
import pathlib as _pathlib
import sys as _sys
_sys.path.insert(0, str(_pathlib.Path(__file__).resolve().parents[1]))
from validate_pack import validate as validate_pack_body  # noqa: E402


@asynccontextmanager
async def lifespan(_app: FastAPI):
    db.init_db()
    yield


app = FastAPI(title="HR//0D backend", version="0.1.0", lifespan=lifespan)

MAX_BODY_BYTES = 256 * 1024  # spec section 2: max sync payload 256 KB
CONTENT_KEY = os.environ.get("CONTENT_KEY", "dev-content-key")


def _err(status: int, title: str, detail: str) -> JSONResponse:
    return JSONResponse(
        status_code=status,
        content={"type": "about:blank", "title": title, "status": status,
                 "detail": detail, "traceId": db.new_uuid()},
    )


def _check_content_key(authorization: str | None) -> bool:
    return authorization == f"Bearer {CONTENT_KEY}"


@app.get("/healthz")
def healthz():
    return {"ok": True, "db": "up" if db.health() else "down",
            "content": _latest_content_version()}


def _latest_content_version() -> str:
    try:
        with db.connect() as conn:
            row = conn.execute(
                "SELECT version FROM content_packs ORDER BY published_at DESC LIMIT 1"
            ).fetchone()
        return row[0] if row else "none"
    except Exception:
        return "none"


@app.get("/v1/content/manifest")
def manifest(authorization: str | None = Header(default=None)):
    if not _check_content_key(authorization):
        return _err(401, "Unauthorized", "Missing or invalid content key.")
    with db.connect() as conn:
        rows = conn.execute(
            "SELECT id, version, sha256, bytes, url, required, min_client, channels"
            " FROM content_packs WHERE revoked_at IS NULL ORDER BY id, version"
        ).fetchall()
    packs = [{
        "id": r[0], "version": r[1], "sha256": r[2], "bytes": r[3], "url": r[4],
        "required": bool(r[5]), "minClient": r[6],
        "channels": json.loads(r[7] or '["demo"]'),
    } for r in rows]
    return {"schemaVersion": 2, "packs": packs,
            "generatedAt": datetime.now(timezone.utc).isoformat()}


@app.post("/v1/admin/packs", status_code=201)
def publish_pack(body: dict):
    """Publish a pack. Refused unless validate_pack.py says it is publishable (I1-I8).

    This is the one place content enters the game: no hand-edited JSON reaches players
    without passing the same fairness gate that runs in CI.
    """
    problems = validate_pack_body(body)
    if problems:
        return _err(422, "Unprocessable Entity",
                    "pack refused: " + "; ".join(problems[:5]))
    with db.connect() as conn:
        conn.execute(
            "INSERT INTO content_packs (id, version, sha256, bytes, url, required,"
            " min_client, channels, published_at) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?)",
            (str(body.get("id")), str(body.get("version")),
             str(body.get("sha256", "")), int(body.get("bytes", 0)),
             str(body.get("url", "")), 1 if body.get("required", True) else 0,
             str(body.get("minClient", "")), db.dumps(body.get("channels", ["demo"])),
             db.now_iso()),
        )
    return {"published": True, "id": body.get("id"), "version": body.get("version")}


@app.post("/v1/runs", status_code=201)
async def create_run(request: Request):
    body = await _json_body(request)
    if body is None:
        return _err(400, "Bad Request", "Body must be JSON.")
    device_key = body.get("deviceKey", "")
    run_id = body.get("runId", "")
    if not db.is_uuid(device_key) or not db.is_uuid(run_id):
        return _err(422, "Unprocessable Entity", "deviceKey and runId must be UUIDs.")
    if db.contains_pii(body):
        return _err(422, "Unprocessable Entity", "Payload contains a PII-looking key.")
    with db.connect() as conn:
        # Idempotent create: the client retries on flaky networks, so a repeat
        # POST for the same runId must not fail or duplicate the row.
        existing = conn.execute(
            "SELECT 1 FROM runs WHERE run_id = ?", (run_id,)).fetchone()
        if not existing:
            conn.execute(
                "INSERT INTO runs (run_id, device_key, seed, build, started_at,"
                " content_versions) VALUES (?, ?, ?, ?, ?, ?)",
                (run_id, device_key, int(body.get("seed", 0)), str(body.get("build", "")),
                 str(body.get("startedAt", db.now_iso())),
                 db.dumps(body.get("contentVersions", {}))),
            )
    return {"accepted": True, "serverTime": db.now_iso()}


@app.post("/v1/runs/{run_id}/events", status_code=202)
async def post_events(run_id: str, request: Request):
    if not db.is_uuid(run_id):
        return _err(422, "Unprocessable Entity", "runId must be a UUID.")
    raw = await request.body()
    # THE LIMIT IS ENFORCED HERE (v120). 60 writes / 10 min per run's device key -- the table existed since
    # v1 and nothing read it, which made it a comment. The key is the RUN's device, so one client cannot
    # spend another client's budget; the purge keeps the table from growing one dead row per device forever.
    with db.connect() as conn:
        device = conn.execute("SELECT device_key FROM runs WHERE run_id = ?", (run_id,)).fetchone()
        key = "run:" + (device[0] if device else run_id)
        allowed, retry_after = db.rate_check_and_count(conn, key)
        db.purge_rate_windows(conn)
    if not allowed:
        response = _err(429, "Too Many Requests",
                        "This device key is over its event budget; back off and retry.")
        response.headers["Retry-After"] = str(retry_after)
        return response
    if len(raw) > MAX_BODY_BYTES:
        return _err(413, "Payload Too Large", "Body exceeds 256 KB.")
    try:
        body = json.loads(raw or b"{}")
    except json.JSONDecodeError:
        return _err(400, "Bad Request", "Body must be JSON.")
    events = body.get("events", [])
    if not isinstance(events, list):
        return _err(422, "Unprocessable Entity", "events must be a list.")
    accepted = 0
    rejected = []
    with db.connect() as conn:
        exists = conn.execute("SELECT 1 FROM runs WHERE run_id = ?", (run_id,)).fetchone()
        if not exists:
            return _err(404, "Not Found", "Unknown runId. POST /v1/runs first.")
        for i, ev in enumerate(events):
            if not isinstance(ev, dict) or ev.get("type") not in db.KNOWN_EVENT_TYPES:
                rejected.append({"index": i, "why": "unknown type"})
                continue
            if db.contains_pii(ev):
                rejected.append({"index": i, "why": "PII-looking key"})
                continue
            conn.execute(
                "INSERT INTO run_events (run_id, t, type, payload) VALUES (?, ?, ?, ?)",
                (run_id, str(ev.get("t", db.now_iso())), ev["type"], db.dumps(ev)),
            )
            accepted += 1
    return {"accepted": accepted, "rejected": rejected}


@app.post("/v1/runs/{run_id}/finish")
async def finish_run(run_id: str, request: Request):
    if not db.is_uuid(run_id):
        return _err(422, "Unprocessable Entity", "runId must be a UUID.")
    body = await _json_body(request)
    if body is None:
        return _err(400, "Bad Request", "Body must be JSON.")
    with db.connect() as conn:
        cur = conn.execute(
            "UPDATE runs SET finished_at = ?, outcome = ?, days_completed = ?,"
            " synthetics_hired = ?, wrongful_rejections = ?, final_trust = ?"
            " WHERE run_id = ?",
            (db.now_iso(), str(body.get("outcome", "")),
             int(body.get("daysCompleted", 0)), int(body.get("syntheticsHired", 0)),
             int(body.get("wrongfulRejections", 0)), int(body.get("finalTrust", 0)),
             run_id),
        )
        if cur.rowcount == 0:
            return _err(404, "Not Found", "Unknown runId.")
        total = conn.execute("SELECT COUNT(*) FROM runs").fetchone()[0]
    return {"rank": None, "totalRuns": total}


@app.delete("/v1/devices/{device_key}", status_code=200)
def purge_device(device_key: str, authorization: str | None = Header(default=None)):
    """THE RIGHT TO BE FORGOTTEN, for one device key (v120, owed in ../README.md).

    A deletion request cannot be answered with "the data is anonymous": the device key IS the identity the
    runs join on, and a leaderboard row is reconstructable from it. This removes every run the key ever
    made, cascading to their events, plus the key's rate-limit window (an orphaned window would leave a
    deleted device throttled). It needs the CONTENT_KEY, for the same reason publishing a pack does.

    It answers with 200 and a count even when there was nothing to delete: a purge endpoint that says
    "not found" for a key it has never seen is an oracle for which keys exist.
    """
    if not _check_content_key(authorization):
        return _err(401, "Unauthorized", "CONTENT_KEY is required to purge a device.")
    if not db.is_uuid(device_key):
        return _err(422, "Unprocessable Entity", "deviceKey must be a UUID.")
    with db.connect() as conn:
        removed = db.purge_device(conn, device_key)
    return {"purged": True, "runsRemoved": removed}


@app.get("/v1/leaderboard")
def leaderboard(channel: str = "demo", window: str = "event"):
    """Top 50, no names -- only aggregate fields (spec section 4.5)."""
    with db.connect() as conn:
        rows = conn.execute(
            "SELECT days_completed, synthetics_hired, wrongful_rejections, final_trust"
            " FROM runs WHERE finished_at IS NOT NULL"
            " ORDER BY days_completed DESC, final_trust DESC LIMIT 50"
        ).fetchall()
    board = [{
        "rank": i + 1, "survivedDays": r[0] or 0,
        "syntheticsCaught": None, "wrongfulRejections": r[2] or 0,
        "runHash": f"run-{i + 1:04d}",
    } for i, r in enumerate(rows)]
    return {"channel": channel, "window": window, "entries": board}


@app.get("/v1/research/export")
def research_export(format: str = "csv", minCell: int = 5):
    """Aggregated counts only (min cell size 5) -- never per-run rows."""
    with db.connect() as conn:
        rows = conn.execute(
            "SELECT type, COUNT(*) FROM run_events GROUP BY type ORDER BY type"
        ).fetchall()
    cells = [{"type": r[0], "count": r[1]} for r in rows if r[1] >= max(1, minCell)]
    if format == "csv":
        lines = ["type,count"] + [f"{c['type']},{c['count']}" for c in cells]
        return JSONResponse(content={"format": "csv", "csv": "\n".join(lines)})
    return {"format": "json", "minCell": minCell, "cells": cells}


async def _json_body(request: Request):
    try:
        return json.loads(await request.body() or b"{}")
    except json.JSONDecodeError:
        return None
