# HANDOFF — Agent → BE1 (with a content ask for BE2) — interaction layer

**Date:** 2026-11-26, updated after merging current `main` · **Branch:**
`arena/c1faebb9-human-resources-zero-day` · **Read first:** `docs/15-interaction-layer.md`

The desk now has hands. The pure rules, Unity adapters, scene wiring and browser twin are in the
repo, integrated with the 80-sprite art kit, composition L scale, day/night room, wear overlays and
the paperwork flag rules. **No backend work is included** — the user owns that stream.

---

## STATUS

On track, with one environment caveat: this sandbox has no `dotnet`, so the C# files were
syntax-checked (49 files, 0 errors) but **not yet compiled or run**. BE1's first action after
adopting the Unity project is to run the two .NET commands below.

## DONE (files)

- `unity/Assets/Scripts/Game/Interaction/` — 11 new files:
  `EvidenceMap`, `EvidenceLedger`, `HotspotHitTest`, `ToolBudget`, `DeskSession` (all pure C#),
  plus `HotspotInteractor`, `EvidencePresenter`, `DeskEvidenceOverlay`, `StampAnimator`,
  `DecisionInputController`, `DeskBootstrap` (Unity adapters).
- `DeskSession` now owns the generated `Paperwork` for each applicant: flags are exposed for the
  letter UI, locked at the stamp, and their trust delta is folded into the decision.
- `unity/Assets/Scripts/Core/Rules/ScoringService.cs` — **one deliberate frozen-API addition**:
  `DecisionInput.PaperworkTrustDelta` (default 0) is added to `TrustDelta` in `Evaluate`.
  Existing callers/tests are unchanged. Please sign this off in review.
- `unity/Assets/Scripts/Editor/HRZeroDayMenu.cs` — builds hotspots with `HotspotInteractor`,
  pads colliders, adds EventSystem + `Physics2DRaycaster`, ring/callout/stamp props and the bootstrap.
  It sits alongside `ArtKitPublishMenu.cs`; publish/import still come first.
- `unity/Assets/Scripts/Game/Desk/DeskSceneBuilder.cs` — main's BUG-024 fix (nullable parent,
  layer lookup by repo path, `scaleFactor`) is preserved; no duplicate fix remains.
- `tools/UnityStubs/UnityStubs.cs` — merged with main's stub work; added EventSystems, TextMesh,
  Quaternion, `Mathf.Max(float,float)`, built-in resources, etc. Duplicate `Mathf`/`SetActive`
  declarations from the two branches were removed.
- `tools/CoreTests/` — 10 new interaction tests, including paperwork flag lock/trust.
- `prototype/web/` — playable interaction twin: composition L scaling, L0/L1/L2 room, wear overlays,
  five paperwork flags, seeded fixtures from `prototype/generator_sim.py`, 9 dependency-free checks.
- `docs/15-interaction-layer.md` + this file — architecture, fairness contract, risks, evidence.

## NEXT (BE1, in order)

1. **Adopt the project the user already created** — `scripts/Adopt-UnityProject.ps1 -Source <temp>`;
   see `docs/19-unity-project-setup.md` and `handoff/2026-11-26_fe-to-BE1_unity-project-created.md`.
   Do **not** create a second Unity project or overwrite `unity/Assets`.
2. Run `dotnet build tools/UnityStubs/CompileCheck.csproj` and
   `dotnet run --project tools/CoreTests` on the PC. If either fails, send the exact output; do not
   paper over it by editing the core.
3. `HR//0D > Publish Art To Resources` -> `Import Art Kit` -> `Verify Art Import` ->
   `Build Desk Scene`.
4. Press Play. Expected: applicant breathes at composition-L scale; EXAMINE shows hotspots; a body
   tap opens the 2x ring, a face tap opens a callout; the letter tab offers five toggles; stamping
   shows the consequence and advances after `debriefSeconds`.
5. Fill `DeskBootstrap.watchCopy`, `correctHeading`, `wrongHeading` from content when BE2 lands it.
6. Build a throwaway APK before adding more UI — still the biggest schedule risk.

## CONTENT ASK FOR BE2

`prototype/web/copy.json` is the temporary content file. It now holds: `watchLonger`, debrief lines,
tool labels, the `VALID` result wording, three letter templates, the signature line and the five
flag labels. Please move these into `content/manual/`, `content/ui/` and `content/letters/` with
stable keys, then hand me the key list so BE1 can replace the inspector fields with a content lookup.
Do not change the tell ids or flag ids — both are frozen in `TellCatalog` / `LetterFlags`.

## BLOCKED ON

Unity project adoption, not creation → suggested default: run `Adopt-UnityProject.ps1` against the
project the user already made with 6000.3.29f1.

## RISK

- The interaction scripts have never been compiled by a real Unity C# compiler here. The biggest
  likely issue is an API spelling (`TextMesh`, `Physics2DRaycaster`, Input System UI module), not
  rules logic.
- `DeskBootstrap` auto-advances after the debrief. FE must set `autoAdvance = false` when the real
  day-summary/ending screens arrive, then call `Continue()` from that screen's button.
- The overlay uses the built-in font, not the new 5x7 monitor atlas; text will look less pixel-perfect
  until FE wires a proper font/material.
- Art hotspots are smaller than 48 dp. Padded colliders + smallest-rect resolution mitigate it;
  the real fix is an inspect-list UI with full-width rows.

## EVIDENCE

```
$ (cd prototype/web && node test.mjs)          -> 9/9 checks passed
$ python3 tools/art/make_sprite_manifest.py --verify -> PASS (80 sprites, 8 reference sheets)
$ python3 tools/art/publish_to_unity.py --verify     -> PASS (byte-identical)
$ python3 tools/comp/twin_desk.py --verify           -> PASS (6 layers + 9 hotspots)
$ python3 tools/art/make_art_kit.py --check          -> PASS (80 sprites)
$ python3 tools/art/make_font.py --check             -> PASS (61 glyphs)
$ tree-sitter C# syntax pass                        -> 49 files, 0 errors
$ curl http://127.0.0.1:8765/prototype/web/         -> HTTP 200
```

**Not run here:** both `dotnet` commands. No SDK in this sandbox.

## DON'T TOUCH

`Assets/Scripts/Core/**` except the reviewed `DecisionInput.PaperworkTrustDelta` addition ·
`Assets/Art/**` and `Assets/Resources/**` (generated) · any backend directory (the user's stream).
