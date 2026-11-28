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
        # SQLITE DOES NOT ENFORCE FOREIGN KEYS UNLESS ASKED, PER CONNECTION. The schema says
        # `ON DELETE CASCADE` on run_events, and without this line that clause does nothing at all -- which
        # was invisible until a purge test asked "did the events go too?" and they had not. Postgres enforces
        # it by default, so this is the one place the two engines genuinely differ at runtime.
        conn.execute("PRAGMA foreign_keys = ON")
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


# --------------------------------------------------------------------------- rate limits (v120)
# The table has existed since v1 and NOTHING enforced it. A limit that is only a table is a comment.
# Fixed window, 60 writes per 10 minutes per device key, which is what ../README.md promises.
RATE_WINDOW_SECONDS = 600
RATE_MAX = 60


def rate_check_and_count(conn, key: str, now: str | None = None) -> tuple[bool, int]:
    """Count one request against `key`'s window. Returns (allowed, seconds_until_reset).

    Fixed-window, not sliding: the client retries on flaky networks, and a sliding window makes a retry
    storm behave differently from a burst of new work, which is a distinction that would only ever hurt
    an honest player on a bad connection. The reset is returned so the client can back off properly.
    """
    from datetime import datetime, timedelta, timezone

    now_dt = datetime.fromisoformat(now) if now else datetime.now(timezone.utc)
    stamp = now_dt.isoformat()
    row = conn.execute(
        "SELECT window_start, count FROM rate_limits WHERE key = ?", (key,)).fetchone()
    if row is None:
        conn.execute(
            "INSERT INTO rate_limits (key, window_start, count, purge_after) VALUES (?, ?, ?, ?)",
            (key, stamp, 1,
             (now_dt + timedelta(seconds=RATE_WINDOW_SECONDS)).isoformat()))
        return True, RATE_WINDOW_SECONDS
    start = datetime.fromisoformat(row[0])
    elapsed = (now_dt - start).total_seconds()
    if elapsed >= RATE_WINDOW_SECONDS:                     # the window turned over: start a new one
        conn.execute(
            "UPDATE rate_limits SET window_start = ?, count = 1, purge_after = ? WHERE key = ?",
            (stamp, (now_dt + timedelta(seconds=RATE_WINDOW_SECONDS)).isoformat(), key))
        return True, RATE_WINDOW_SECONDS
    count = int(row[1]) + 1
    conn.execute("UPDATE rate_limits SET count = ? WHERE key = ?", (count, key))
    return count <= RATE_MAX, int(RATE_WINDOW_SECONDS - elapsed)


def purge_rate_windows(conn, now: str | None = None) -> int:
    """Drop every window that has expired. Called on write paths, so the table cannot grow without bound."""
    from datetime import datetime, timezone

    now_dt = datetime.fromisoformat(now) if now else datetime.now(timezone.utc)
    cur = conn.execute("DELETE FROM rate_limits WHERE purge_after < ?", (now_dt.isoformat(),))
    return cur.rowcount if cur.rowcount and cur.rowcount > 0 else 0


def purge_device(conn, device_key: str) -> int:
    """THE RIGHT TO BE FORGOTTEN, for one device (v120). Returns how many runs were removed.

    A player who asks to be deleted cannot be answered with "we anonymised it": the key IS the identity the
    runs are joined on. Deleting the runs cascades to their events (`ON DELETE CASCADE` + the pragma in
    connect()), and the rate-limit window for that key goes too, because an orphaned window would keep a
    deleted device throttled after it no longer exists.
    """
    # EVENTS FIRST, EXPLICITLY. The cascade is on, and this is still written out: a purge is the one
    # operation where "the database should have done it" is not an acceptable answer, and this SQL also runs
    # unchanged on Postgres, where the cascade is enforced by the server rather than by a pragma.
    conn.execute(
        "DELETE FROM run_events WHERE run_id IN (SELECT run_id FROM runs WHERE device_key = ?)",
        (device_key,))
    cur = conn.execute("DELETE FROM runs WHERE device_key = ?", (device_key,))
    removed = cur.rowcount if cur.rowcount and cur.rowcount > 0 else 0
    conn.execute("DELETE FROM rate_limits WHERE key = ?", ("run:" + device_key,))
    return removed
