# Changes made -- 2026-11-26

**Author:** Agent (Arena) - **Branch:** `arena/c1faebb9-human-resources-zero-day`
**Commits:** `2bc441a` (interaction layer), `d0b0735` (this report), plus the merge/reconciliation
commit on this branch. **Scope:** client / gameplay interaction only. **No backend files changed.**

---

## 1. One-line summary

The desk went from a static scene to a playable loop: inspect nine art hotspots, get a 2x magnifier
ring for body tells or a text callout for face tells, use limited verification tools, flag a cover
letter, pick a rejection reason, stamp, see the consequence and move on. A browser twin makes the
same minute playable without Unity. Everything was reconciled with the newer `main` (80-sprite kit,
composition L, day/night room, wear overlays, five paperwork flags).

---

## 2. Files changed, by purpose

### New gameplay code -- `unity/Assets/Scripts/Game/Interaction/`

| File | What it does |
|---|---|
| `EvidenceMap.cs` | Maps each art artifact / catalog tell to a reveal style and day-gated surface; defines the 2/2/1 tool limits. Pure C#. |
| `EvidenceLedger.cs` | Records only what the player actually opened; art-only observations never become evidence; feeds `DecisionInput`. Pure C#. |
| `HotspotHitTest.cs` | Screen -> safe-area canvas -> applicant-local rect; padded targets; `mirror` vs timed `no_blink` overlap. Pure C#. |
| `ToolBudget.cs` | Per-day tool counts. Never decides whether a record is genuine. Pure C#. |
| `DeskSession.cs` | Owns `ShiftController` + ledger + tool budget + generated `Paperwork`; exposes truth-free view data. |
| `HotspotInteractor.cs` | `IPointerDownHandler` on each `HS_*`; Input-System-compatible. |
| `EvidencePresenter.cs` | Ledger result -> magnifier or callout; uses composition L `scaleFactor 0.96` and the layout's document rect. |
| `DeskEvidenceOverlay.cs` | World-space ring + callout using kit sprites and `TextMesh`. |
| `StampAnimator.cs` | Stamp pop with reduce-motion and no coroutine leak. |
| `DecisionInputController.cs` | Reason dropdown, stamp buttons, double-tap guard. |
| `DeskBootstrap.cs` | Drop-in wiring, seeded session, debrief and advance. |

### Modified engine-side files

| File | Change |
|---|---|
| `unity/Assets/Scripts/Core/Rules/ScoringService.cs` | Added `DecisionInput.PaperworkTrustDelta` (default 0) and folded it into `TrustDelta`. Only frozen-API change; needs Dev A sign-off. |
| `unity/Assets/Scripts/Editor/HRZeroDayMenu.cs` | Builds hotspots with interactors, pads colliders, creates EventSystem + 2D raycaster, ring/callout/stamp props and bootstrap components. Coexists with `ArtKitPublishMenu.cs`. |
| `unity/Assets/Scripts/Game/Desk/DeskSceneBuilder.cs` | Main's BUG-024 fix and `scaleFactor` handling preserved; our duplicate fix removed during merge. |
| `tools/UnityStubs/UnityStubs.cs` | Merged stub sets; added EventSystems, TextMesh, Quaternion, `Mathf.Max(float,float)`, built-in resources; removed duplicate `Mathf`/`SetActive` declarations. |
| `tools/CoreTests/CoreTests.csproj`, `Program.cs` | Added 10 interaction tests alongside main's expanded suite. |
| `tools/CoreTests/InteractionTests.cs` | 10 tests: reveal coverage, presentation map, honest humans, synthetic provability, day gating, dedupe, hit-test geometry, tool budget, full stamp loop, paperwork flag lock/trust. |

### Playable browser twin -- `prototype/web/`

| File | What it does |
|---|---|
| `index.html`, `style.css` | 480x270 desk, monitor panel aligned to composition L, evidence box, reason select, stamps, flags panel, notes, modal. |
| `app.js` | Renders shipped sprites from layout/manifest; applicant scale, L0/L1/L2 room by shift, wear overlays for corruption, examine mode, tools, magnifier crop, five letter flags, debrief flow. |
| `rules.js` | DOM-free mirror of C# interaction rules, scoring and paperwork flags. |
| `copy.json` | Temporary player-facing content: debrief, tools, letters, signature, five flag labels. |
| `make_fixture.py`, `fixtures/run-1000*.json` | Deterministic data from `prototype/generator_sim.py`; public fixture has no truth flag. |
| `test.mjs`, `package.json` | 9 dependency-free rule checks. |

### Documentation

| File | Change |
|---|---|
| `docs/15-interaction-layer.md` | Architecture, fairness contract, composition L / paperwork integration, verification, gaps. |
| `handoff/2026-11-26_agent-to-BE1_interaction-layer.md` | Integration steps for BE1 (adopt, not create), content ask for BE2, risk register, evidence. |
| `handoff/2026-11-26_agent-to-team_changes-made.md` | This file. |
| `docs/90-bug-log.md` | BUG-024 logged before the fix, positioned between BUG-023 and BUG-025. |
| `README.md`, `docs/README.md` | Links to the interaction layer and the browser twin. |
| `docs/NEXT-STEPS.md` | Deleted on current `main` (superseded by `docs/16-the-later-list.md`); our link migrated to `docs/README.md`. |

---

## 3. Bugs and integration defects fixed

- **BUG-024** -- `Build(layout, null)` dereferenced a null parent. Logged first; main later fixed it
  independently. The duplicate fix was removed in the merge; the log entry is kept.
- **Merge-defects:** duplicate `Mathf` type and duplicate `GameObject.SetActive` signature in the
  combined `UnityStubs.cs`; stale hardcoded monitor/document rect and unscaled applicant hotspot
  geometry after composition L; stale `NEXT-STEPS.md` references. All corrected.
- **Core API:** paperwork trust had no path to `ShiftController.Submit`; added a defaulted field to
  `DecisionInput` rather than a second trust mutation point.

---

## 4. Verification actually run here

```
node prototype/web/test.mjs                         -> 9/9 pass
python3 tools/art/make_sprite_manifest.py --verify   -> PASS (80 sprites, 8 reference sheets)
python3 tools/art/publish_to_unity.py --verify       -> PASS (byte-identical Unity copy)
python3 tools/comp/twin_desk.py --verify             -> PASS (6 layers + 9 hotspots)
python3 tools/art/make_art_kit.py --check            -> PASS (80 sprites)
python3 tools/art/make_applicants.py --check         -> PASS (28 sprites)
python3 tools/art/make_wear_overlays.py --check      -> PASS (2 overlays)
python3 tools/art/make_font.py --check               -> PASS (61 glyphs)
python3 tools/art/selfcheck_pngcheck.py              -> RESULT: PASS
tree-sitter C# syntax pass                           -> 49 files, 0 errors
curl /prototype/web/                                 -> HTTP 200 (live preview)
```

**Not run:** `dotnet build tools/UnityStubs/CompileCheck.csproj` and
`dotnet run --project tools/CoreTests` -- no .NET SDK in this sandbox. BE1 must run both on the PC.

---

## 5. What is deliberately not done

- No `ProjectSettings/` / `Packages/` in the repo yet -- the user created the Unity project locally;
  BE1 adopts it with `scripts/Adopt-UnityProject.ps1` (`docs/19-unity-project-setup.md`).
- No backend, server, API or telemetry changes -- owned by the user.
- No Field Manual / day-summary / endings screens -- FE work.
- Hotspots use 6 px collider padding, not literal 48 dp; an inspect-list UI is the proper fix.
- Unity overlay text uses the built-in font, not the new 5x7 monitor atlas.

---

## 6. Definition of done for the next person

1. Adopt the Unity project (`Adopt-UnityProject.ps1`), do not create a second one.
2. Run both `dotnet` commands above; green = integration can start.
3. `Publish Art To Resources` -> `Import Art Kit` -> `Verify Art Import` -> `Build Desk Scene`.
4. Play: applicant breathes at composition-L scale; EXAMINE shows hotspots; body tap = ring, face tap
   = callout; the letter offers five flags; stamp shows consequence and advances.
5. Build a throwaway APK before adding more UI.
