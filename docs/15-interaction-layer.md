# Interaction layer — the first playable minute

**Status:** implemented 2026-11-26, rebased onto the current `main` (80-sprite kit, composition L,
paperwork rules) · **Owner:** Agent (systems/art/docs) · **Consumers:** BE1 (Unity), BE2 (content)

This page describes the hands-on layer that turns the finished desk art and pure gameplay core into
a playable loop. It deliberately contains **no backend work**: the game stays offline-first and the
server is only a future content/telemetry sync.

---

## 1. What the player can now do

1. The desk appears from `unity/Assets/Content/desk_layout.json` — monitor left, applicant right,
   composition L at 480x270 with the applicant drawn at `scaleFactor 0.96` and the desk foreground
   sorting above him.
2. Resume, cover letter and three verification tools are readable on the monitor. The cover letter
   tab offers the five shipping paperwork flags (`docs/17-paperwork-mechanic.md`).
3. **EXAMINE** reveals the nine art hotspots. Tapping one:
   - body artifact (`seam`, `ear`, `hands`, `straight_badge`, `shadow`) -> **2x magnifier ring**,
   - face artifact (`mirror`, `glasses_merge`, `skin_band`, `no_blink`) -> **text callout**.
4. The player picks a rejection reason, stamps APPROVE / REJECT with a double-tap guard, sees the
   consequence and moves to the next applicant. Trust, strikes, day boundaries, the letter flags and
   the shift clock all come from the pure core.

A browser twin makes this minute playable without Unity:

```bash
python3 -m http.server 8765 --bind 0.0.0.0
# open /prototype/web/
(cd prototype/web && node test.mjs)      # 9 rule checks, no dependencies
```

The twin renders the shipped sprites from the **same** layout + manifest as Unity, applies the
applicant scale factor, switches the room L0/L1/L2 by shift, draws `fx/wear_*` for corruption, and
mirrors the paperwork flag scoring.

---

## 2. File map

### Pure C# (no UnityEngine; headless-testable)

| File | Responsibility |
|---|---|
| `unity/Assets/Scripts/Game/Interaction/EvidenceMap.cs` | artifact / tell -> reveal style and surface; tool limits |
| `EvidenceLedger.cs` | only tells the player actually opened are recorded; feeds `DecisionInput` |
| `HotspotHitTest.cs` | screen -> safe-area canvas -> applicant-local rect; padded target resolution |
| `ToolBudget.cs` | 2 / 2 / 1 uses per day; never decides truth |
| `DeskSession.cs` | owns `ShiftController` + ledger + budget + generated `Paperwork`; truth-free view DTO |

### Unity adapters (thin; compile against `tools/UnityStubs`)

| File | Responsibility |
|---|---|
| `HotspotInteractor.cs` | `IPointerDownHandler` on each `HS_*` object |
| `EvidencePresenter.cs` | ledger result -> magnifier or callout request; uses composition L scale and the layout's document rect |
| `DeskEvidenceOverlay.cs` | world-space ring / callout using kit sprites + `TextMesh` |
| `StampAnimator.cs` | stamp pop, cancel-safe, honours reduce-motion |
| `DecisionInputController.cs` | reason selection, stamp, one-shot lock |
| `DeskBootstrap.cs` | drop-in wiring of all the above to a seeded session |

`unity/Assets/Scripts/Editor/HRZeroDayMenu.cs` now adds the hotspot interactors, padded colliders,
EventSystem + `Physics2DRaycaster`, ring/callout/stamp props and the bootstrap components when it
builds `Desk.unity`. It coexists with the `Publish Art To Resources` menu in
`ArtKitPublishMenu.cs`; run publish/import first as `docs/19-unity-project-setup.md` says.

### Core API addition (needs Dev A sign-off)

`ScoringService.DecisionInput` gained one defaulted field, `PaperworkTrustDelta`, and `Evaluate`
folds it into `TrustDelta`. Existing callers are unaffected (default 0), and this is the only path
by which flag trust reaches `ShiftController.Submit`. It is the one deliberate change to the frozen
`Core/` API in this branch; Dev A should confirm the signature in review.

---

## 3. The fairness contract (do not "simplify" this)

| Rule | How it is enforced |
|---|---|
| Never tell the player who is an AI | `ApplicantViewData` has no truth/tell fields; `DeskBootstrap` passes only `SpriteKey` to the view. |
| Behaviour is never sufficient | Art-only artifacts (`ear`, `hands`, `straight_badge`, `shadow`, `glasses_merge`, `skin_band`, `no_blink`) return **observations**, not catalog tells. They cannot support a rejection reason. |
| No tell by colour alone | Face tells always carry `TellCatalog.Wording` text; the magnifier is paired with wording too. |
| Day-gated evidence (I1b) | `EvidenceLedger` checks `TellCatalog.IsUnlocked(day, surface)` before recording anything. |
| Imperfect tools (ADR-006) | `ToolBudget` only counts uses. A tool with no matching tell returns `VALID`; it may say `VALID` for a synthetic. |
| Paperwork is a guess under uncertainty | `Paperwork.For` derives flags from the letter text; `isReal` is never rendered during the shift. `DeskSession.Stamp` closes the letter and folds `Paperwork.TrustDelta` into the decision. |
| One finding, once | Repeat taps return `Changed = false` and never duplicate a note or decision input entry. |
| Deterministic | `DeskSession(seed)` rebuilds the same run, the same letters and the same flags; the browser twin reads a seeded fixture from `prototype/generator_sim.py`. |

### Artifact -> catalog tell

| Artifact | Style | Tell if present on this applicant |
|---|---|---|
| `seam` | magnifier | `portrait_blend_seam` |
| `mirror` | callout | `portrait_asymmetry` |
| `ear`, `hands`, `straight_badge`, `shadow` | magnifier | none (observation) |
| `glasses_merge`, `skin_band`, `no_blink` | callout | none (observation) |

`no_blink` additionally requires six seconds of observation; it is a prompt to look at the
documents, never a reason to reject.

---

## 4. Verification

On a machine with the .NET 8 SDK (**not available in the 2026-11-26 sandbox**):

```bash
dotnet build tools/UnityStubs/CompileCheck.csproj   # Unity-facing scripts type-check
dotnet run --project tools/CoreTests                # all existing tests + 10 interaction tests
dotnet run --project tools/CoreTests -- 2000        # generator soak
```

In this sandbox these commands were replaced by a tree-sitter C# syntax pass (49 files, 0 errors)
and the browser rule suite (9/9). This is **not** a substitute for the .NET run; BE1 must run the
two `dotnet` commands on the PC before merging.

Art/pipeline gates that did run here, all green:

```bash
python3 tools/art/make_sprite_manifest.py --verify     # 80 sprites, 8 reference sheets
python3 tools/art/publish_to_unity.py --verify         # byte-identical Unity copy
python3 tools/comp/twin_desk.py --verify               # 6 layers + 9 hotspots
python3 tools/art/make_art_kit.py --check              # 80 sprites palette-clean
python3 tools/art/make_applicants.py --check
python3 tools/art/make_wear_overlays.py --check
python3 tools/art/make_font.py --check
python3 tools/art/selfcheck_pngcheck.py
```

---

## 5. Known gaps / next actions

1. **`ProjectSettings/` + `Packages/` are not committed yet.** The user created the Unity project
   locally; BE1 should **adopt** it with `scripts/Adopt-UnityProject.ps1` per
   `docs/19-unity-project-setup.md` and `handoff/2026-11-26_fe-to-BE1_unity-project-created.md`.
   Do not create a second project.
2. **Touch-target size:** the art hotspot rects are small (some 16 px wide at 480 px reference).
   Colliders get 6 px padding and the smallest rect wins, but this is not a literal 48 dp target.
   The next UI pass should add an *inspect list* with full-width rows that select the same artifact
   ids; no rule changes are needed for that.
3. **Content strings:** `watchCopy`, `correctHeading`, `wrongHeading` and the debrief copy are
   supplied by fields/temporary JSON. BE2's `content/manual` and `content/endings` should become the
   source of truth; the browser twin uses `prototype/web/copy.json` for now.
4. **Tool result panel:** the C# layer budgets and reveals tool tells; FE still needs to build the
   tool result panel and connect it to `EvidencePresenter.Open(...)`.
5. **Field Manual / day summary / endings screens** are outside this minute and remain FE work.
6. The Unity overlay uses `TextMesh` with the built-in font; it does not yet use the new 5x7
   monitor font atlas. Wire it through `LetterView`/`MonitorScreenRaster` when FE owns the page.
