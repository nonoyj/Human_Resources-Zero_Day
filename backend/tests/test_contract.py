"""Backend acceptance tests -- the client contract, executable.

Run: cd backend && python3 -m pytest -q
Uses SQLite so it needs no Docker. Every test here maps to a rule in
handoff/2026-11-25_agent-to-backend-team_backend-spec.md section 4.
"""
import os
import pathlib
import sys
import uuid

sys.path.insert(0, str(pathlib.Path(__file__).resolve().parents[1]))
os.environ.setdefault("CONTENT_KEY", "test-key")

DB = pathlib.Path(__file__).parent / "test_hr0d.db"
if DB.exists():
    DB.unlink()
os.environ["DATABASE_URL"] = f"sqlite:///{DB}"

import pytest
from fastapi.testclient import TestClient

from app import db, main

AUTH = {"Authorization": "Bearer test-key"}


@pytest.fixture(scope="module")
def client():
    db.init_db()
    with TestClient(main.app) as c:
        yield c


def _run(client):
    rid = str(uuid.uuid4())
    r = client.post("/v1/runs", json={
        "deviceKey": str(uuid.uuid4()), "runId": rid, "seed": 7, "build": "0.1.0-demo",
    })
    assert r.status_code == 201, r.text
    return rid


def test_healthz(client):
    r = client.get("/healthz")
    assert r.status_code == 200
    assert r.json()["ok"] is True
    assert r.json()["db"] == "up"


def test_manifest_requires_content_key(client):
    assert client.get("/v1/content/manifest").status_code == 401
    r = client.get("/v1/content/manifest", headers=AUTH)
    assert r.status_code == 200
    assert r.json()["schemaVersion"] == 2
    assert isinstance(r.json()["packs"], list)


def test_run_requires_uuid_device_key(client):
    r = client.post("/v1/runs", json={"deviceKey": "not-a-uuid",
                                      "runId": str(uuid.uuid4())})
    assert r.status_code == 422


def test_run_create_is_idempotent(client):
    rid = _run(client)
    r = client.post("/v1/runs", json={"deviceKey": str(uuid.uuid4()), "runId": rid,
                                      "seed": 7, "build": "0.1.0-demo"})
    assert r.status_code == 201
    with db.connect() as conn:
        n = conn.execute("SELECT COUNT(*) FROM runs WHERE run_id = ?", (rid,)).fetchone()[0]
    assert n == 1


def test_events_partial_accept(client):
    rid = _run(client)
    r = client.post(f"/v1/runs/{rid}/events", json={"events": [
        {"t": "2026-11-26T10:00:00Z", "type": "decision", "day": 1, "decision": "reject"},
        {"type": "not_a_real_event"},
        {"type": "decision", "name": "Alice"},          # PII key -> rejected
        "garbage",                                       # malformed -> rejected
    ]})
    assert r.status_code == 202, r.text
    body = r.json()
    assert body["accepted"] == 1
    assert len(body["rejected"]) == 3
    assert body["rejected"][0]["why"] == "unknown type"


def test_events_unknown_run_is_404(client):
    r = client.post(f"/v1/runs/{uuid.uuid4()}/events", json={"events": []})
    assert r.status_code == 404


def test_events_body_cap_256kb(client):
    rid = _run(client)
    big = {"events": [{"type": "decision", "blob": "x" * 300_000}]}
    r = client.post(f"/v1/runs/{rid}/events", json=big)
    assert r.status_code == 413


def test_finish_and_leaderboard(client):
    rid = _run(client)
    r = client.post(f"/v1/runs/{rid}/finish", json={
        "outcome": "win", "daysCompleted": 5, "syntheticsHired": 0,
        "wrongfulRejections": 2, "finalTrust": 88,
    })
    assert r.status_code == 200, r.text
    assert "rank" in r.json()
    board = client.get("/v1/leaderboard").json()
    assert board["entries"]
    # Anonymous: no device key, name, or email may appear in a board row.
    assert not any(k in str(board) for k in ("deviceKey", "name", "email"))


def test_publish_refuses_unfair_pack(client):
    bad = {"schemaVersion": 1, "id": "core", "version": "1.0.0",
           "days": [{"day": 1, "applicants": [
               {"id": "x", "archetype": "synthetic_polished",
                "fullName": "No Tell", "idNumber": "PH-1", "tells": []}]}]}
    r = client.post("/v1/admin/packs", json=bad)
    assert r.status_code == 422
    assert "I1" in r.json()["detail"] or "I4" in r.json()["detail"]


def test_publish_accepts_the_real_day_01(client):
    import json, pathlib as p
    day = json.loads((p.Path(__file__).resolve().parents[2]
                      / "content/days/day_01.json").read_text())
    r = client.post("/v1/admin/packs", json=day)
    assert r.status_code == 201, r.text
    manifest = client.get("/v1/content/manifest", headers=AUTH).json()
    assert any(pk["id"] == "day_01" for pk in manifest["packs"])


def test_finish_unknown_run_is_404(client):
    r = client.post(f"/v1/runs/{uuid.uuid4()}/finish", json={"outcome": "win"})
    assert r.status_code == 404


def test_research_export_is_aggregated(client):
    rid = _run(client)
    client.post(f"/v1/runs/{rid}/events", json={"events": [
        {"type": "decision"}] * 6})
    r = client.get("/v1/research/export?format=json&minCell=1")
    assert r.status_code == 200
    assert all(c["count"] >= 1 for c in r.json()["cells"])


def test_pii_key_detection_is_recursive():
    assert db.contains_pii({"a": [{"b": {"email": "x"}}]}) is True
    assert db.contains_pii({"a": [{"b": {"decision": "reject"}}]}) is False


# --------------------------------------------------------------------------- v120: the owed two
# Both of these were listed in ../README.md as "owed". A limit that is only a table and a purge that is
# only a promise are the same bug: the code says one thing and the schema says another.

def _new_run(client, device_key=None):
    device_key = device_key or str(uuid.uuid4())
    run_id = str(uuid.uuid4())
    r = client.post("/v1/runs", json={"deviceKey": device_key, "runId": run_id,
                                      "seed": 7, "build": "v120"})
    assert r.status_code == 201, r.text
    return device_key, run_id


def test_events_are_rate_limited_per_device(client):
    """60 writes per 10 minutes, then 429 with a Retry-After the client can obey."""
    _device, run_id = _new_run(client)
    event = {"t": "0.0", "type": "app_open", "payload": {}}
    accepted = 0
    for _ in range(db.RATE_MAX + 2):
        r = client.post(f"/v1/runs/{run_id}/events", json={"events": [event]})
        if r.status_code == 202:
            accepted += 1
        else:
            assert r.status_code == 429, r.text
            assert int(r.headers["Retry-After"]) > 0
            break
    assert accepted == db.RATE_MAX, f"accepted {accepted}, the budget is {db.RATE_MAX}"


def test_the_window_turns_over(client):
    """At the end of the window the same key is allowed again -- the limit must not be a lifetime ban."""
    with db.connect() as conn:
        conn.execute("DELETE FROM rate_limits")
        assert db.rate_check_and_count(conn, "run:window-test", "2026-01-01T00:00:00+00:00")[0]
        for _ in range(db.RATE_MAX - 1):
            db.rate_check_and_count(conn, "run:window-test", "2026-01-01T00:01:00+00:00")
        allowed, _retry = db.rate_check_and_count(conn, "run:window-test", "2026-01-01T00:02:00+00:00")
        assert not allowed, "the 61st write inside the window was allowed"
        allowed, _retry = db.rate_check_and_count(conn, "run:window-test", "2026-01-01T00:20:00+00:00")
        assert allowed, "the window never turns over: this key is banned for life"


def test_purge_removes_every_run_and_its_events(client):
    device, run_id = _new_run(client)
    client.post(f"/v1/runs/{run_id}/events",
                json={"events": [{"t": "1.0", "type": "app_open", "payload": {}}]})
    r = client.delete(f"/v1/devices/{device}", headers=AUTH)
    assert r.status_code == 200, r.text
    assert r.json()["runsRemoved"] >= 1
    with db.connect() as conn:
        assert conn.execute("SELECT 1 FROM runs WHERE device_key = ?", (device,)).fetchone() is None
        assert conn.execute("SELECT 1 FROM run_events WHERE run_id = ?", (run_id,)).fetchone() is None
        assert conn.execute("SELECT 1 FROM rate_limits WHERE key = ?", ("run:" + device,)).fetchone() is None


def test_purge_needs_the_key_and_says_nothing_about_unknown_devices(client):
    """No key -> 401. Unknown device -> 200 with 0 removed, NOT 404: a 404 is an oracle for which keys exist."""
    assert client.delete(f"/v1/devices/{uuid.uuid4()}").status_code == 401
    r = client.delete(f"/v1/devices/{uuid.uuid4()}", headers=AUTH)
    assert r.status_code == 200 and r.json()["runsRemoved"] == 0
    assert client.delete("/v1/devices/not-a-uuid", headers=AUTH).status_code == 422
