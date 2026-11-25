# Backend Specification (summary — full brief in `handoff/2026-11-25_agent-to-backend-team_backend-spec.md`)

The backend exists for exactly three jobs. Anything beyond these is scope creep and gets rejected.

| # | Job | Endpoint family |
|---|---|---|
| 1 | Serve **signed, immutable content packs** so content can be tuned after install | `GET /v1/content/manifest` |
| 2 | Record **anonymous run telemetry** for the educational evaluation and leaderboard | `POST /v1/runs`, `/runs/{id}/events`, `/runs/{id}/finish` |
| 3 | Give us a **tiny ops console** (publish / revoke packs, counters) | admin UI + `GET /v1/healthz` |

**Not in scope:** accounts, cloud saves, chat, per-player content, any runtime model calls,
storing real people's data.

## Architecture

```
Android client (offline-first, JSON cache, hash-pinned packs)
        │ HTTPS — public content key + per-install device key
        ▼
FastAPI (Python 3.12) · stateless · 2 workers
        ├── PostgreSQL 16   (content_packs, runs, run_events, rate_limits)
        └── S3-compatible object storage (packs: <id>-<version>.json.zst, hash-named)
        └── static ops console (read-only + publish/revoke)
```
Deploy: one container + managed Postgres, region SG or PH. No k8s, no queues, no Redis.

## Client contract rules (these protect us)

1. **Offline-first is absolute.** The game must be fully playable with the server unreachable, using
   the last cached pack. If the network layer prevents play, the design is wrong.
2. **Packs are immutable and hash-pinned.** Client refuses a pack whose `sha256` doesn't match the
   manifest. A leaked public content key must be worthless.
3. **Publishing a pack runs the fairness validator** (`validate_pack.py`, lifting invariants I1–I8 from
   `prototype/generator_sim.py`). A pack that can produce an unprovable AI is refused. This is the
   product's integrity guarantee.
4. **No PII.** Device UUID only. IPs kept ≤ 24 h for rate limiting. No display names on the leaderboard.
5. **Fake backend first.** Dev A codes against `IBackend` with `FakeBackend` (fixtures committed in
   `backend/fixtures/`). The game is never blocked by backend progress.

## Telemetry payload (what we ask the client to send)

Only what we need to answer *did the game teach?*:
`decision` (applicant id, ground truth, decision, chosen reason, evidence seen, tool used, seconds on card),
`tool_used`, `day_end` (quota, trust), `run_finish` (outcome, synthetics hired, wrongful rejections).

Retention 180 days, aggregate-only exports for the event report.

## Interface the client codes against (C#)

```csharp
public interface IBackend {
    Task<ContentManifest> GetManifestAsync(CancellationToken ct);
    Task DownloadPackAsync(PackRef pack, string destPath, CancellationToken ct);
    Task<bool> StartRunAsync(RunLedger ledger, CancellationToken ct);
    Task<bool> SendEventsAsync(string runId, IReadOnlyList<RunEvent> events, CancellationToken ct);
    Task<RunResult?> FinishRunAsync(string runId, RunSummary summary, CancellationToken ct);
}
```
Both `HttpBackend` and `FakeBackend` implement this. All calls are fire-and-forget with a local
outbox queue persisted in the save file, flushed on next launch — **so a failed sync never affects
gameplay or the save**.

## Backend dev deliverables (10 days, part-time)

See the handoff §7 for the day-by-day table. Milestones that matter to the game team:

| Milestone | Needed by | Unblocks |
|---|---|---|
| `GET /content/manifest` + fixtures | Day 3 | Dev A's ContentLoader integration |
| `POST /runs` + `/events` + fixtures | Day 4 | telemetry, leaderboard screen |
| `validate_pack.py` gate | Day 5 | content publishing safety |
| Staging deploy + OpenAPI | Day 7 | end-to-end test on a real phone |
| Soak test 20 devices × 20 runs | Day 8 | go/no-go for the leaderboard feature |

## Fallback (decided now, so nobody panics on Day 10)

If the backend is not ready, we ship `FakeBackend` and cut the leaderboard screen (`docs/91-backlog.md`).
The demo remains fully playable, offline, and teachable. **Nothing in the game depends on the server.**

## Analytics layer (see `docs/10-analytics-spec.md`)

The research instrument adds a `decisions` table (one row per hiring decision, with tells present /
inspected / tools used / seconds on card), a `quiz_results` table, five research endpoints, a PII-key
rejection rule, an opt-out purge endpoint, and four aggregate dashboards. It changes nothing about
offline play: analytics is fire-and-forget with a local outbox, and the client ships an **"Export my
play data"** button so research survives a dead server.
