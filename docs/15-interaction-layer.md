# Interaction layer — the first playable minute

**Status:** implemented 2026-11-26 · **Owner:** Agent (systems/art/docs) · **Consumers:** BE1 (Unity project), BE2 (content strings)

This page describes the hands-on layer that turns the finished desk art and pure gameplay core into
a playable loop. It deliberately contains **no backend work**: the game stays offline-first and the
server is only a future content/telemetry sync.

---

## 1. What the player can now do

1. The desk appears (monitor left, applicant right) from `unity/Assets/Content/desk_layout.json`.
2. Resume, cover letter and three verification tools are readable on the monitor.
3. **EXAMINE** reveals the nine art hotspots. Tapping one:
   - body artifact (`seam`, `ear`, `hands`, `straight_badge`, `shadow`) -> **2x magnifier ring**,
   - face artifact (`mirror`, `glasses_merge`, `skin_band`, `no_blink`) -> **text callout**.
4. The player picks a rejection reason, stamps APPROVE / REJECT with a double-tap guard, sees the
   consequence, and moves to the next applicant. Trust, strikes and day boundaries come from the
   existing `ShiftController`.

A browser twin makes this minute playable without Unity:

```bash
python3 -m http.server 8765 --bind 0.0.0.0
# open /prototype/web/
node prototype/web/test.mjs          # 8 rule checks, no dependencies
```

---

## 2. File map

### Pure C# (no UnityEngine; headless-testable)

| File | Responsibility |
|---|---|
| `unity/Assets/Scripts/Game/Interaction/EvidenceMap.cs` | artifact / tell -> reveal style and surface; tool limits |
| `EvidenceLedger.cs` | only tells the player actually opened are recorded; feeds `DecisionInput` |
| `HotspotHitTest.cs` | screen -> safe-area canvas -> applicant-local rect; padded target resolution |
| `ToolBudget.cs` | 2 / 2 / 1 uses per day; never decides truth |
| `DeskSession.cs` | owns `ShiftController` + ledger + budget; exposes a truth-free `ApplicantViewData` |

### Unity adapters (thin; compile against `tools/UnityStubs`)

| File | Responsibility |
|---|---|
| `HotspotInteractor.cs` | `IPointerDownHandler` on each `HS_*` object |
| `EvidencePresenter.cs` | turns a ledger result into a magnifier or callout request |
| `DeskEvidenceOverlay.cs` | world-space ring / callout using kit sprites + `TextMesh` |
| `StampAnimator.cs` | stamp pop, cancel-safe, honours reduce-motion |
| `DecisionInputController.cs` | reason selection, stamp, one-shot lock |
| `DeskBootstrap.cs` | drop-in wiring of all the above to a seeded session |

`unity/Assets/Scripts/Editor/HRZeroDayMenu.cs` now adds the hotspot interactors, padded colliders,
EventSystem + `Physics2DRaycaster`, ring/callout/stamp props and the bootstrap components when it
builds `Desk.unity`.

---

## 3. The fairness contract (do not "simplify" this)

| Rule | How it is enforced |
|---|---|
| Never tell the player who is an AI | `ApplicantViewData` has no truth/tell fields; `DeskBootstrap` passes only `SpriteKey` to the view. |
| Behaviour is never sufficient | Art-only artifacts (`ear`, `hands`, `straight_badge`, `shadow`, `glasses_merge`, `skin_band`, `no_blink`) return **observations**, not catalog tells. They cannot support a rejection reason. |
| No tell by colour alone | Face tells always carry `TellCatalog.Wording` text; the magnifier is paired with wording too. |
| Day-gated evidence (I1b) | `EvidenceLedger` checks `TellCatalog.IsUnlocked(day, surface)` before recording anything. Resume/ID/tool tells cannot appear on day 1. |
| Imperfect tools (ADR-006) | `ToolBudget` only counts uses. A tool with no matching tell returns `VALID`; it may say `VALID` for a synthetic. |
| One finding, once | Repeat taps return `Changed = false` and never duplicate a note or a decision input entry. |
| Deterministic | `DeskSession(seed)` rebuilds the same run; the browser twin reads a seeded fixture from the reference generator. |

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
dotnet run --project tools/CoreTests                # 21 existing + 9 interaction tests
dotnet run --project tools/CoreTests -- 2000        # generator soak
```

In this sandbox these commands were replaced by a tree-sitter C# syntax pass (31 files, 0 errors)
and the browser rule suite (8/8). This is **not** a substitute for the .NET run; BE1 must run the
two `dotnet` commands on the PC before merging.

Art/pipeline checks that did run here:

```bash
python3 tools/art/make_art_kit.py --check      # PASS: 73 sprites
python3 tools/comp/twin_desk.py --verify       # PASS: 4 layers + 9 hotspots
```

---

## 5. Known gaps / next actions

1. **Unity project skeleton still does not exist** (`ProjectSettings/`, `Packages/`, `.meta`).
   `HR//0D > Build Desk Scene` cannot be executed until BE1 creates it. BUG-024 (null parent in
   `DeskSceneBuilder.Build`) was found and fixed during this work.
2. **Touch-target size:** the art hotspot rects are small (some 16 px wide at 480 px reference).
   Colliders get 6 px padding and the smallest rect wins, but this is not a literal 48 dp target.
   The next UI pass should add an *inspect list* with full-width rows that select the same artifact
   ids; no rule changes are needed for that.
3. **Content strings:** `watchCopy`, `correctHeading`, `wrongHeading` and the debrief copy are
   supplied by content/fields. BE2's `content/manual` and `content/endings` should become the source
   of truth; the browser twin uses `prototype/web/copy.json` as its temporary content file.
4. **Tool result presentation:** the C# layer budgets and reveals tool tells; BE1 still needs to
   build the tool result panel and connect it to `EvidencePresenter.Open(...)`.
5. **Field Manual / day summary / endings screens** are outside this minute and remain FE work.
