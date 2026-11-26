# Changes made — 2026-11-26

**Author:** Agent (Arena) · **Branch:** `arena/c1faebb9-human-resources-zero-day`
**Commit:** `2bc441a feat(game): add desk interaction layer and playable twin`
**Scope:** client / gameplay interaction only. **No backend files were changed** — the backend is owned by the user.

---

## 1. One-line summary

The desk went from a static scene to a playable loop: the player can inspect nine art hotspots,
see a 2x magnifier ring for body tells or a text callout for face tells, use limited verification
tools, pick a rejection reason, stamp a decision, see the consequence and move to the next applicant.
A browser twin makes the same minute playable without Unity.

---

## 2. Files changed, by purpose

### New gameplay code — `unity/Assets/Scripts/Game/Interaction/`

| File | What it does |
|---|---|
| `EvidenceMap.cs` | Maps each art artifact / catalog tell to a reveal style and day-gated surface; defines the 2/2/1 tool limits. Pure C#. |
| `EvidenceLedger.cs` | Records only what the player actually opened; art-only observations never become evidence; feeds `DecisionInput`. Pure C#. |
| `HotspotHitTest.cs` | Screen -> safe-area canvas -> applicant-local rect; padded hit targets; `mirror` vs timed `no_blink` overlap resolution. Pure C#. |
| `ToolBudget.cs` | Per-day tool use counts. Never decides whether a record is genuine. Pure C#. |
| `DeskSession.cs` | Owns `ShiftController` + ledger + budget; exposes a truth-free `ApplicantViewData`; raises applicant/decision/day events. Pure C#. |
| `HotspotInteractor.cs` | `IPointerDownHandler` on each `HS_*` object; touch-friendly, Input-System-compatible. |
| `EvidencePresenter.cs` | Turns a ledger result into a magnifier or callout request; all wording comes from `TellCatalog` / layout labels / content fields. |
| `DeskEvidenceOverlay.cs` | World-space ring + callout using kit sprites and `TextMesh`; hides on `OnDisable`. |
| `StampAnimator.cs` | APPROVE / REJECT stamp pop with reduce-motion support and no coroutine leak. |
| `DecisionInputController.cs` | Reason dropdown, stamp buttons, one-shot double-tap guard, routes to pure scoring. |
| `DeskBootstrap.cs` | Drop-in wiring: seeded session, hotspot events, applicant sprite swap, debrief and advance. |

### Modified engine-side files

| File | Change |
|---|---|
| `unity/Assets/Scripts/Editor/HRZeroDayMenu.cs` | `Build Desk Scene` now adds `HotspotInteractor` to all nine hotspots, pads colliders, creates EventSystem + `Physics2DRaycaster`, ring/callout/stamp props, `DecisionInputController` and `DeskBootstrap`. |
| `unity/Assets/Scripts/Game/Desk/DeskSceneBuilder.cs` | BUG-024 fix: `Build(layout, null)` no longer dereferences a null parent. |
| `tools/UnityStubs/UnityStubs.cs` | Added stubs for EventSystems, `TextMesh`, `Quaternion`, `Mathf`, `GetComponentsInChildren`, built-in resources, etc., so the new Unity scripts type-check headlessly. |
| `tools/CoreTests/CoreTests.csproj`, `Program.cs` | Compile and run the five pure interaction files; added `InteractionTests`. |
| `tools/CoreTests/InteractionTests.cs` | 9 new tests: reveal coverage, locked presentation mapping, honest humans, synthetic provability, day gating, dedupe, hit-test geometry, tool budget, full stamp loop. |

### Playable browser twin — `prototype/web/`

| File | What it does |
|---|---|
| `index.html`, `style.css` | The 480x270 desk at 16:9 with monitor UI, evidence box, reason select, stamp buttons, notes panel and modal. |
| `app.js` | Renders shipped sprites from `desk_layout.json` + `unity_sprites.json`; handles examine mode, tabs, tools, magnifier crop, debrief/day/ending flow. |
| `rules.js` | DOM-free mirror of the C# interaction rules and scoring service. |
| `copy.json` | Temporary player-facing content file (move to BE2's `content/` later). |
| `make_fixture.py`, `fixtures/run-1000.json`, `fixtures/run-1000.key.json` | Deterministic applicant data generated from `prototype/generator_sim.py`; public fixture has no truth flag, key is used only by scoring/tests. |
| `test.mjs`, `package.json` | 8 dependency-free rule checks; `npm test` / `node test.mjs`. |

### Documentation

| File | Change |
|---|---|
| `docs/15-interaction-layer.md` | New: architecture, artifact/tell map, fairness contract, verification commands, known gaps. |
| `handoff/2026-11-26_agent-to-BE1_interaction-layer.md` | New: integration steps for BE1, content ask for BE2, risk register and evidence. |
| `handoff/2026-11-26_agent-to-team_changes-made.md` | This file. |
| `docs/90-bug-log.md` | BUG-024 logged before the fix, with symptom, root cause, fix and prevention rule. |
| `docs/NEXT-STEPS.md`, `README.md` | Links to the interaction layer and the browser twin. |

---

## 3. Bugs found and fixed

- **BUG-024** — `HR//0D > Build Desk Scene` threw `NullReferenceException` because
  `DeskSceneBuilder.Build(layout, null)` called `parent.Find(...)` on a null parent. Logged first,
  then fixed by skipping the rebuild lookup when no parent is supplied. Regression check: stub
  compile + the editor menu must log `built Assets/Scenes/Desk.unity`.

---

## 4. Verification actually run here

```
$ (cd prototype/web && node test.mjs)
8 interaction-twin checks passed

$ python3 tools/art/make_art_kit.py --check
PASS: every pixel is palette-conformant, no partial alpha

$ python3 tools/comp/twin_desk.py --verify
PASS: 4 layers + 9 hotspots resolve on a 480x270 canvas

$ tree-sitter C# syntax pass
31 C# files, 0 syntax errors

$ curl http://127.0.0.1:8765/prototype/web/
HTTP 200  (live preview: "HR//0D interaction twin")
```

**Not run here:** `dotnet build tools/UnityStubs/CompileCheck.csproj` and
`dotnet run --project tools/CoreTests`. This sandbox has no .NET SDK. BE1 must run both on the PC
before merging; 9 new C# tests are waiting there.

---

## 5. What is deliberately not done

- No Unity project skeleton (`ProjectSettings/`, `Packages/`, `.meta`) — still BE1 task 1.
- No backend, server, API or telemetry changes — owned by the user.
- No Field Manual / day-summary / endings screens — FE work.
- Hotspot touch targets use 6 px collider padding, not literal 48 dp; a full-width inspect list is
  the proper fix and is documented in `docs/15-interaction-layer.md`.
- The C# interaction code has not been opened in a real Unity Editor yet.

---

## 6. Definition of done for the next person

1. Create the Unity project skeleton.
2. Run both `dotnet` commands above; green = integration can start.
3. `HR//0D > Import Art Kit` -> `Verify Art Import` -> `Build Desk Scene`.
4. Play: applicant breathes; EXAMINE shows hotspots; body tap = ring, face tap = callout; stamp
   shows consequence and advances.
5. Build a throwaway APK before adding more UI.
