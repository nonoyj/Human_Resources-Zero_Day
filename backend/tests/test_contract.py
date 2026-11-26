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
