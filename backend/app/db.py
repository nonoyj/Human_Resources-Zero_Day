"""HR//0D backend storage.

Spec: handoff/2026-11-25_agent-to-backend-team_backend-spec.md section 5.
PostgreSQL 16 in production (docker-compose). For local dev/tests the same
code runs on SQLite via DATABASE_URL=sqlite:///... -- columns are plain
TEXT/INTEGER so the SQL is portable. No PII is ever stored: device_key is a
random per-install UUID, run_events payloads are validated before insert.
"""
from __future__ import annotations

import json
import os
import sqlite3
import time
import uuid
from contextlib import contextmanager

SCHEMA = """
CREATE TABLE IF NOT EXISTS content_packs (
  id TEXT NOT NULL,
  version TEXT NOT NULL,
  sha256 TEXT NOT NULL,
  bytes INTEGER NOT NULL,
  url TEXT NOT NULL,
  required INTEGER NOT NULL DEFAULT 1,
  min_client TEXT,
  channels TEXT NOT NULL DEFAULT '["demo"]',
  published_at TEXT NOT NULL DEFAULT '',
  revoked_at TEXT,
  PRIMARY KEY (id, version)
);
CREATE TABLE IF NOT EXISTS runs (
  run_id TEXT PRIMARY KEY,
  device_key TEXT NOT NULL,
  seed INTEGER NOT NULL,
  build TEXT NOT NULL,
  started_at TEXT NOT NULL,
  finished_at TEXT,
  outcome TEXT,
  days_completed INTEGER,
  synthetics_hired INTEGER,
  wrongful_rejections INTEGER,
  final_trust INTEGER,
  content_versions TEXT NOT NULL DEFAULT '{}'
);
CREATE TABLE IF NOT EXISTS run_events (
  id INTEGER PRIMARY KEY AUTOINCREMENT,
  run_id TEXT NOT NULL REFERENCES runs(run_id) ON DELETE CASCADE,
  t TEXT NOT NULL,
  type TEXT NOT NULL,
  payload TEXT NOT NULL
);
CREATE INDEX IF NOT EXISTS idx_run_events_run ON run_events (run_id, t);
CREATE INDEX IF NOT EXISTS idx_run_events_type ON run_events (type);
CREATE TABLE IF NOT EXISTS rate_limits (
  key TEXT PRIMARY KEY,
  window_start TEXT NOT NULL,
  count INTEGER NOT NULL,
  purge_after TEXT NOT NULL
);
"""

# Event types the client may send -- exactly the taxonomy in docs/10-analytics-spec.md section 3.
# Anything else is rejected per-item (partial accept) so a client bug cannot pollute the research
# table. If you add a type here, add it to the spec in the same commit.
KNOWN_EVENT_TYPES = {
    # 3.1 session & flow
    "app_open", "consent", "screen_enter", "screen_exit", "intro_skip",
    "settings_change", "app_quit",
    # 3.2 the core instrument
    "decision",
    # 3.3 inbox
    "email_action",
    # 3.4 hiring, firing, day & run
    "hire", "fire", "day_end", "manual_open", "run_end",
    # 3.5 learning-transfer probe
    "quiz_result",
}

# Keys that must never appear in an event payload (BE2 security rule).
PII_KEYS = {"name", "email", "phone", "address", "ip", "free_text", "note"}


def database_url() -> str:
    return os.environ.get("DATABASE_URL", "sqlite:///./hr0d.db")


def _connect(url: str):
    if url.startswith("sqlite"):
        # Accept both sqlite:///rel/path.db and sqlite:////abs/path.db
        path = url[len("sqlite:///"):] if url.startswith("sqlite:///") else url[len("sqlite://"):]
        path = path or "./hr0d.db"
        conn = sqlite3.connect(path)
        conn.row_factory = sqlite3.Row
        return conn
    import psycopg  # PostgreSQL in production

    return psycopg.connect(url)


@contextmanager
def connect(url: str | None = None):
    conn = _connect(url or database_url())
    try:
        yield conn
        conn.commit()
    finally:
        conn.close()


def init_db(url: str | None = None) -> None:
    with connect(url) as conn:
        for stmt in SCHEMA.split(";"):
            stmt = stmt.strip()
            if stmt:
                conn.execute(stmt)


def health(url: str | None = None) -> bool:
    try:
        with connect(url) as conn:
            conn.execute("SELECT 1")
        return True
    except Exception:
        return False


def now_iso() -> str:
    return time.strftime("%Y-%m-%dT%H:%M:%SZ", time.gmtime())


def new_uuid() -> str:
    return str(uuid.uuid4())


def is_uuid(value: str) -> bool:
    try:
        uuid.UUID(value)
        return True
    except (ValueError, AttributeError, TypeError):
        return False


def contains_pii(obj) -> bool:
    """True if any dict key (at any depth) looks like PII."""
    if isinstance(obj, dict):
        for k, v in obj.items():
            if str(k).lower() in PII_KEYS:
                return True
            if contains_pii(v):
                return True
    elif isinstance(obj, list):
        return any(contains_pii(v) for v in obj)
    return False


def dumps(obj) -> str:
    return json.dumps(obj, separators=(",", ":"), sort_keys=True)
