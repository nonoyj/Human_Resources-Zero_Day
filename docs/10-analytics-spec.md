# Analytics & Research Instrument — Specification

**Purpose:** the game is an awareness tool, so it must also *produce evidence* about how
non-experts fail at AI/phishing detection. This spec defines what we record, what we derive, and
what we deliberately refuse to collect.

Owner: Dev A (client instrumentation) + backend team (storage/export) · Research lead: whoever
writes the event report.

---

## 1. Research questions (everything below exists to answer one of these)

| ID | Question | Why it matters |
|---|---|---|
| **RQ1** | Which tell categories do non-experts miss most, and which do they over-apply? | Tells us what to teach first in real awareness programmes |
| **RQ2** | Do players reject *humans* more as they get more suspicious (over-suspicion spiral)? | The Papers-Please failure mode; measures a real workplace harm |
| **RQ3** | Do verification tools *improve* accuracy, or create false confidence? | Tests the "a tool tells you what a record says, not what's true" lesson |
| **RQ4** | Does skill transfer happen within 5 shifts (learning curve), and does it persist across runs? | Evidence the game teaches rather than just entertains |
| **RQ5** | Which distractors cause the most wrongful rejections? | Red herrings are the real-world equivalent of profiling |
| **RQ6** | Where do players disengage (quit, skip, stop reading)? | Which screens are actually being played vs clicked through |

**Publishable output:** "In a sample of N players over 5 shifts, portrait-level synthetic tells were
caught 84% of the time while document arithmetic contradictions were caught 51%; false-rejection
rate rose 2.3× under time pressure; players who used the ID Registry *first* were 12 pp more likely
to accept a poisoned 'VALID' result." — that is the shape of the report this instrumentation must
make possible.

---

## 2. Privacy & ethics (non-negotiable — read before adding any field)

1. **No PII. Ever.** No names, emails, IPs (beyond a 24 h rate-limit counter), device models, or
   location. Identity is a random `deviceKey` UUID generated at first launch.
2. **The player's chosen character name is never transmitted.** If it's needed locally for the
   ending, it stays in the save file only.
3. **No free-text capture.** Reasons come from the fixed dropdown enum, never typed text.
4. **Consent screen** at first launch: one plain-language paragraph + `SHARE MY PLAY DATA` /
   `PLAY WITHOUT SHARING`. Both give the identical game. Declining only disables upload; local
   analytics stay on so the debrief/ending can still personalise.
5. **Research export is aggregated.** Backend exports only grouped counts (min cell size 5, so no
   single run can be re-identified).
6. **Opt-out is honoured retroactively**: the ops console can purge a `deviceKey`'s rows on request.

Consent copy (use verbatim):

> **HELP US STUDY THIS**
> This game is also research. If you allow it, we record what you decided and what you inspected —
> never your name, your device, or anything that identifies you. It helps us learn which scams
> people fall for.
> `[ SHARE ]`   `[ DON'T SHARE ]`

---

## 3. Event taxonomy (client → `POST /runs/{runId}/events`)

All events share: `t` (ISO-8601 UTC), `type`, `runId`, `day`, plus the fields below.
Payload budget: ≤ 256 KB per batch, ≤ 5 batches per run (batch on day-end and on app background).

### 3.1 Session & flow

| type | fields | research use |
|---|---|---|
| `app_open` | `build`, `firstLaunch`, `contentVersions`, `consent` | funnel start, version tracking |
| `consent` | `granted` (bool) | ethics audit |
| `screen_enter` | `screen` (`intro`,`menu`,`chargen`,`desk`,`manual`,`inbox`,`summary`,`ending`) | RQ6 drop-off |
| `screen_exit` | `screen`, `dwellMs` | RQ6 — screens clicked through vs read |
| `intro_skip` | `atMs` | is the intro too long? |
| `settings_change` | `key`, `from`, `to` | accessibility adoption (reduce motion, colour-blind, text speed) |
| `app_quit` | `reason` (`home`,`kill`,`crash`,`pause`) | crash-free rate, drop-off point |

### 3.2 The core instrument — one record per decision

```jsonc
{
  "t": "2026-11-25T05:03:12.111Z", "type": "decision", "runId": "...", "day": 3,
  "applicantId": "d3_9f3a1c",
  "archetype": "synthetic_cloned",      // ground truth archetype (never shown in game)
  "groundTruth": "synthetic",           // human | human_bait | synthetic
  "decision": "approve",                // approve | reject | fire
  "reason": "DOC_INCONSISTENCY",        // dropdown enum, or null on approve
  "confidence": 2,                      // 1-3 dot self-rating, nullable if disabled
  "tellsPresent": ["identity_clone","ref_unreachable"],
  "tellsInspected": ["identity_clone"], // player opened the surface that reveals it
  "surfacesOpened": ["resume_row","id_tool","cover_letter"],
  "toolsUsed": [{"id":"id_registry","result":"VALID","msAfter":8200}],
  "distractorsPresent": ["career_gap"],
  "distractorResolved": false,          // did they open the field that clears it?
  "secondsOnCard": 41,
  "stampsUndone": 0,                    // hesitation signal
  "hintShown": false,
  "correct": false                      // computed server-side too, for integrity
}
```

This single record answers RQ1, RQ2, RQ3, RQ5 and most of RQ4. Treat it as the primary table.

### 3.3 Inbox / email attacks

```jsonc
{ "t": "...", "type": "email_action", "day": 4, "emailId": "d4_e02",
  "attackType": "lookalike_domain", "isMalicious": true,
  "action": "report",            // open_link | reply | report | ignore | grant_access | open_attachment
  "riskClicked": false,          // did they open the malicious link/attachment?
  "senderCheckPerformed": true,  // did they expand the full sender address?
  "secondsOnEmail": 63 }
```

### 3.4 Hiring, firing, day & run

```jsonc
{ "type": "hire",    "applicantId": "...", "day": 1, "wasSynthetic": false }
{ "type": "fire",    "applicantId": "...", "day": 3, "wasSynthetic": true, "fireWasCorrect": true }
{ "type": "day_end", "day": 1, "quotaMet": true, "approved": 3, "rejected": 1,
  "wrongfulRejections": 1, "syntheticsHired": 0, "trust": 78, "corruptionLevel": 0,
  "shiftMs": 421000 }
{ "type": "manual_open", "entryId": "manual_doc_contradiction", "contextSurface": "resume_row",
  "secondsReading": 22 }
{ "type": "run_end", "outcome": "survived", "daysCompleted": 5, "syntheticsHired": 1,
  "syntheticsCaught": 7, "wrongfulRejections": 2, "finalTrust": 61,
  "ending": "survived_clean",
  "seed": 882731, "contentVersions": {"core-applicants":"1.4.0"} }
```

### 3.5 The learning-transfer probe (new — this is what makes it research, not telemetry)

After the run ends, 4 questions, ~20 seconds, shown once per device (skippable):

| # | Item | Measures |
|---|---|---|
| Q1 | "A resume says 9 years of experience; the applicant is 24 and graduated in 2023. Suspicious?" | transfer of the arithmetic tell |
| Q2 | "An email from `hr.dept@nexus-sec.gov.co` claims to be from `nexus-sec.gov`. What's wrong?" | lookalike-domain transfer |
| Q3 | "Your ID tool says the number is VALID. Is the applicant confirmed human?" | tool-blindness transfer |
| Q4 | Rank: which 2 of 5 statements are AI tells? | discrimination |

Emit `{ "type":"quiz_result", "item":1, "correct":true, "msShown":4300 }` each.
**This is the evidence that the game teaches** — RQ4 cannot be answered without it.

---

## 4. Derived metrics (computed in the backend / export script, not the client)

| Metric | Definition | Answers |
|---|---|---|
| **Recall by tell** | caught synthetics / synthetics where tell was `tellsPresent` | RQ1 |
| **Miss matrix** | `tellsPresent` × `tellsInspected` × `decision` | RQ1 — *didn't look* vs *looked and still missed* |
| **Over-suspicion index** | humans rejected / humans seen | RQ2 |
| **Over-suspicion slope** | OSI(day5) − OSI(day1); also OSI vs `secondsOnCard` | RQ2 |
| **Reason accuracy** | correct rejects with correct reason / correct rejects | teaches "right for the wrong reason" |
| **Tool reliance index** | tools used per applicant; accuracy split by `result` of the tool | RQ3 |
| **Post-tool bias** | P(approve \| tool said VALID, truth = synthetic) − P(approve \| no tool) | RQ3 — the killer stat |
| **Evidence coverage** | `tellsInspected` / `tellsPresent` before stamping | RQ1 |
| **Speed–accuracy** | `secondsOnCard` quartiles vs correctness, split by ground truth | RQ2/RQ3 |
| **Distractor cost** | wrongful rejections where `distractorsPresent` and `!distractorResolved` | RQ5 |
| **Learning curve** | accuracy by day index; and across successive runs | RQ4 |
| **Confidence calibration** | correctness at confidence 1/2/3 (Brier-style) | overconfidence |
| **Manual effect** | accuracy of decisions made after `manual_open` vs before | teaching value |
| **Quiz delta** | quiz score at first run vs 3rd run | RQ4 transfer |
| **Quit funnel** | % reaching day 1/2/3/4/5/ending | RQ6 |

---

## 5. Implementation on the client (Dev A)

```
AnalyticsService (singleton, boot scene)
  .Track(AnalyticsEvent e)         // never throws, never blocks gameplay
  ├─ buffer in memory, flush to persistentQueue.jsonl every 15 s or on screen change
  ├─ outbox: batches of <=64 events, sent by IBackend.SendEventsAsync
  ├─ dropped after 30 days offline (documented, not silent)
  └─ hard cap 5 MB of local queue (oldest evicted, counted)
```

Rules:
- **Never call analytics from `Update`.** Compose the record at the point of decision, dispatch once.
- **Every field is optional-safe.** A missing field must not break deserialisation (use
  `[JsonProperty]` + defaults; never `[Serializable]` with non-null assumptions).
- **Analytics failure is invisible.** Wrap in try/catch, log to `analytics_errors.log`, never surface
  to the player, never affect the save or the score.
- **`AnalyticsService` must be testable offline**: `FakeBackend` writes JSONL to disk so an EditMode
  test can assert the exact event sequence of a scripted run.
- Determinism link: every run stores its `seed`, so any analytical anomaly can be replayed locally.

### EditMode tests to write
- `Decision_EmitsExactlyOneEvent` (double-tap protection)
- `Analytics_DoesNotThrow_WhenBackendFails`
- `Queue_PersistsAcrossRestart`
- `OptOut_StopsUploads_ButKeepsLocalDebrief`
- `NoPii_InPayload` (assert the payload contains no name/email patterns — mirror of the backend CI grep)
- `EventSchemaVersion_IsPresent`

---

## 6. What the backend must add to the earlier spec

1. `POST /runs/{id}/events` already accepts arbitrary `type` + `payload` — extend the **allowlist** to
   the types in §3 and reject unknowns (`422` per-item, partial accept).
2. `GET /research/export?format=csv&from=&to=&minCell=5` — aggregated, anonymity-guarded export.
3. `GET /research/tell-matrix` — the miss matrix as JSON, for the dashboards.
4. Retention: raw events 180 days; aggregates kept indefinitely.
5. Ops console: a single page with (a) quit funnel, (b) recall by tell, (c) over-suspicion by day,
   (d) post-tool bias. Read-only; no per-player drill-down (deliberate: prevents re-identification).

### CSV schema for researchers (one row per decision)
`run_hash,day,archetype,ground_truth,decision,reason,confidence,tells_present,tells_inspected,
surfaces_opened,tools_used,seconds_on_card,distractors_present,distractor_resolved,correct`

`run_hash` = first 12 chars of SHA-256 of the `deviceKey`+`runId` — lets a researcher follow one run
without being able to invert it to a person.

---

## 7. Guardrails (things we will NOT do)

| Won't | Because |
|---|---|
| Score teachers/players and report "you are bad at security" | Shame kills learning and is an ethics problem |
| Record typed text, clipboard, or anything outside the game | Not needed, and it's spyware behaviour |
| Sell, share, or publish per-player data | The whole point of the game is distrust of quiet data collection |
| Use analytics to dynamically make the game harder | Fixed seed = fair research; adaptive difficulty confounds RQ4 |
| Transmit before consent | One-tap opt-out, honoured immediately |
| Keep IPs to "detect abuse" | 24 h rate-limit counter only, then purged |

---

## 8. Minimum viable slice for the event (Day 10 cut-line)

If time runs short, ship in this order and stop:
1. `decision` events (with `tellsPresent`/`tellsInspected`/`toolsUsed`/`secondsOnCard`)
2. `run_end` + `day_end`
3. local JSONL export button in Settings (*"Export my play data"* → share sheet) — works with **zero**
   backend, so research can happen even if the server is down
4. `quiz_result`

The **local export button is the safety net**: even with no backend at all, the event team collects
research data by having players tap *Export* and AirDropping/emailing the file to a research laptop.
