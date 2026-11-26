# HANDOFF — Agent → BE1 (with a content ask for BE2) — interaction layer

**Date:** 2026-11-26 · **Branch:** `arena/c1faebb9-human-resources-zero-day`
**Read first:** `docs/15-interaction-layer.md` · **Playable proof:** `/prototype/web/index.html`

The desk now has hands. The pure rules, the Unity adapters, the scene wiring and a browser twin
are in the repo. **Nothing here touches the backend contract** — the user owns that stream.

---

## STATUS

On track, with one environment caveat: this sandbox has no `dotnet`, so the C# files were
syntax-checked (31 files, 0 errors) but **not yet compiled or run**. BE1's first action after
opening the Unity project is to run the two .NET commands below.

## DONE (files)

- `unity/Assets/Scripts/Game/Interaction/` — 9 new files:
  `EvidenceMap`, `EvidenceLedger`, `HotspotHitTest`, `ToolBudget`, `DeskSession` (all pure C#),
  plus `HotspotInteractor`, `EvidencePresenter`, `DeskEvidenceOverlay`, `StampAnimator`,
  `DecisionInputController`, `DeskBootstrap` (Unity adapters).
- `unity/Assets/Scripts/Editor/HRZeroDayMenu.cs` — builds hotspots with `HotspotInteractor`, pads
  colliders, adds EventSystem + `Physics2DRaycaster`, ring/callout/stamp props and the bootstrap.
- `unity/Assets/Scripts/Game/Desk/DeskSceneBuilder.cs` — BUG-024 fix: `Build(layout, null)` no
  longer dereferences a null parent. Bug logged in `docs/90-bug-log.md` **before** the fix.
- `tools/UnityStubs/UnityStubs.cs` — stubs extended for EventSystems, TextMesh, Quaternion, etc.
- `tools/CoreTests/` — 9 interaction tests wired into the headless runner.
- `prototype/web/` — playable interaction twin, seeded fixture from `prototype/generator_sim.py`,
  `copy.json`, and 8 dependency-free rule checks.
- `docs/15-interaction-layer.md` — architecture + fairness contract + known gaps.

## NEXT (BE1, in order)

1. Create the Unity project skeleton (`docs/NEXT-STEPS.md` Step BE1 task 1). Do **not** overwrite
   `unity/Assets`.
2. Run `dotnet build tools/UnityStubs/CompileCheck.csproj` and
   `dotnet run --project tools/CoreTests` on the PC. If either fails, send me the exact output;
   do not paper over it by editing the core.
3. `HR//0D > Import Art Kit` → `Verify Art Import` → `Build Desk Scene`.
4. Press Play. Expected: applicant breathes; pressing EXAMINE (bootstrap auto-wires it) shows
   hotspot outlines; tapping a body artifact opens the 2x ring, a face artifact opens a callout;
   stamping shows the consequence and advances after `debriefSeconds`.
5. Fill `DeskBootstrap.watchCopy`, `correctHeading`, `wrongHeading` from content when BE2 lands it.
6. Build a throwaway APK before adding more UI — this is risk #4 in the day-1 status.

## CONTENT ASK FOR BE2 (no code)

`prototype/web/copy.json` is the temporary content file. It contains the strings this minute needs:
`watchLonger`, three debrief headings/lines, tool labels and the `VALID` result wording. Please move
these into `content/manual/` and `content/ui/` with stable keys, then hand me the key list so BE1
can replace the inspector fields with a content lookup. Do not change the tell ids — they are
frozen in `TellCatalog`.

## BLOCKED ON

Unity project skeleton (BE1 task 1) → suggested default: create a URP 2D project outside the repo
and copy only `ProjectSettings/` + `Packages/` into `unity/`, as the existing handoff says.

## RISK

- The interaction scripts have never been compiled by a real Unity C# compiler here. The biggest
  likely issue is an API spelling (`TextMesh`, `Physics2DRaycaster`, Input System UI module), not
  rules logic. The stub pass catches names we stubbed, not names Unity actually ships.
- Art hotspots are smaller than 48 dp. Padded colliders + smallest-rect resolution mitigate it;
  the real fix is an inspect-list UI with full-width rows (documented in `docs/15-interaction-layer.md`).
- `DeskBootstrap` auto-advances after the debrief. FE must set `autoAdvance = false` when the real
  day-summary/ending screens arrive so they can own that transition.

## EVIDENCE

```
$ python3 prototype/web/make_fixture.py --seed 1000
wrote fixtures/run-1000.json and fixtures/run-1000.key.json

$ (cd prototype/web && node test.mjs)
PASS  fixture has no truth flag in the public record
PASS  every synthetic is provable on its day (I1b)
PASS  honest humans never produce proven evidence (I5)
PASS  body tell opens a magnifier; face tell opens a callout
PASS  no_blink needs a longer look and never becomes catalog evidence
PASS  locked surfaces stay locked
PASS  tools are capped 2/2/1 and can say VALID for an AI
PASS  first playable minute: inspect, reject with reason, next applicant
8 interaction-twin checks passed

$ python3 tools/art/make_art_kit.py --check
PASS: every pixel is palette-conformant, no partial alpha

$ python3 tools/comp/twin_desk.py --verify
PASS: 4 layers + 9 hotspots resolve on a 480x270 canvas; monitor left of x=240, applicant right of it

$ tree-sitter C# syntax pass
checked 31 C# files; syntax errors: 0

$ curl -s -o /dev/null -w '%{http_code}' http://127.0.0.1:8765/prototype/web/
200
```

The live preview is running at `http://127.0.0.1:8765/prototype/web/` in this sandbox.

## DON'T TOUCH

`Assets/Scripts/Core/**` (frozen API) · `Assets/Art/Kit/**` (generated) ·
`Assets/Resources/*.json` (generated) · any backend directory (the user's stream).
