@page plan_editor_ui_library Editor UI Component Library

# Editor UI Component Library

Extract reusable IMGUI drawing components from editor windows into a
**standalone Unity package** (`com.balloonparty.editorui`), reducing
per-window LOC and eliminating repeated patterns (charting, tables, icon
helpers, style caching).

## Motivation

The editor tooling has 19 files totalling ~7 494 LOC.  Several windows exceed
500 LOC, mixing reusable drawing patterns with domain-specific logic.  A first
wave of extraction (`EditorUI/`) produced 9 well-factored components; this plan
defines the **second wave** (10 new components) and packages all eligible
components as a reusable UPM package.

---

## Anti-Patterns to Fix

| # | Pattern | Location | Severity |
|---|---------|----------|----------|
| 1 | `new GUIStyle(...)` per OnGUI frame (11+ sites) | LevelPacingWindow, CurvePanel | High |
| 2 | God method `DrawRow` (137 LOC, 15 columns) | LevelPacingWindow:722 | High |
| 3 | Triplicated balloon/item/actor cell code (~660 LOC) | LevelPacingWindow | High |
| 4 | Triplicated focus-group methods | LevelPacingWindow | Medium |
| 5 | Inline magic-number colours duplicated across files | Multiple | Medium |
| 6 | Three parallel `_selected*PerRow` int[] arrays | LevelPacingWindow | Medium |

---

## Proposed Components (prioritised)

### P0 — Highest Impact

**`StyleCache`** (~40 LOC)
Lazy-init `GUIStyle` factory with `Get(string key, Func<GUIStyle>)`.
Eliminates all per-frame `new GUIStyle()` allocations across 11 windows.

**`TypedEntrySelectorCell<TEnum>`** (~120 LOC)
Generic expand/collapse cell for an enum-typed weighted array.  Handles
dropdown with ✓/+ prefixes, add-on-select, per-entry sub-fields via a
`FieldSpec[]`, and remove button.  Eliminates ~660 LOC of triplicated code.

**`BarChart`** (~60 LOC)
Static method: `Draw(Rect, IReadOnlyList<float>, float max, in BarChartOptions)`
→ optional clicked index.  Selection highlight, padding.

**`PolylineOverlay`** (~30 LOC)
`Draw(Rect, IReadOnlyList<float>, float max, Color, float thickness)` —
normalised polyline via `Handles.DrawAAPolyLine`.

**`PlotGrid`** (~70 LOC)
Horizontal grid lines, Y-axis labels, X-axis labels.  Parameterised by
divisions and value range.

### P1

**`NavigationHeader`** (~35 LOC)
`Draw(string label, ref int value, int min)` → ◀ [Label] [IntField] ▶ row with
`GUIUtility.keyboardControl = 0` on change.

**`IconButtonHelper`** (~25 LOC)
`Get(string iconName, string fallbackGlyph, string tooltip) → GUIContent`.
Uses `EditorGUIUtility.FindTexture` with a static `Dictionary` cache.

**`IColorPalette` + `EditorAssetCache<T>` + `PaletteColorPicker`** (~80 LOC)
Generic palette interface + SO cache + colour picker widget.  Unlocks
`PaletteColorPicker` for the package.  Project implements `IColorPalette`
on `GamePalette`.

### P2

**`PlotLegend`** (~40 LOC)
`DrawEntry(ref float x, float y, Color, string label, bool isSwatch)` and
`DrawDiamondEntry(...)`.

**`PlotMarker`** (~25 LOC)
`DrawDiamond(float x, float y, Color, float size)` — reusable marker shapes.

**`FoldoutSection`** (~25 LOC)
`Draw(string prefKey, string label, Action drawContents, bool defaultOpen)` —
persistent EditorPrefs-backed foldout with `helpBox` styling.

---

## Per-Window Adoption Map

Every editor window and which components it would adopt.

| File (LOC) | Already Uses | Would Adopt | Est. Saved |
|---|---|---|---|
| LevelPacingWindow (1 529) | TDH, PCD | SC, IBH, TES, NH, SR | ~1 050 |
| LevelPacingCurvePanel (550) | — | BC, PO, PG, PL, PM, NH, SC, FS | ~350 |
| BushBakerWindow (847) | — | FS, SC, IBH | ~60 |
| ShotSolverWindow (638) | — | BC, SC, IBH | ~58 |
| GameRenderMapsWindow (485) | — | SC, IBH | ~20 |
| ShadowBakerEditor (463) | — | FS | ~10 |
| PaintSplashPreview (417) | EAL, PCP | SC | ~5 |
| TextureAuditWindow (336) | SH, ST, AL, SR, SFT | SC | ~5 |
| ChainLightningPreview (305) | EAL, PCP | SC | ~5 |
| SpriteLayerCombinerEditor (296) | — | FS | ~8 |
| UnusedAssetsWindow (194) | — | ST, AL, SFT, SC | ~40 |
| PropertyDrawerHelper (171) | — | SC | ~5 |
| FrameDebuggerEventWalker (418) | — | — | 0 |
| LeafVenationSimulator (346) | — | — | 0 |
| FrameDebuggerEventReader (327) | — | — | 0 |
| GradientTextureDrawer (303) | — | — | 0 |
| CloudNoiseTextureGenerator (245) | — | — | 0 |
| BushLeafBaker (234) | — | — | 0 |
| BushBranchBaker (190) | — | — | 0 |
| **Total** | | | **~1 616** |

Component abbreviations: SC=StyleCache, IBH=IconButtonHelper, FS=FoldoutSection,
BC=BarChart, PO=PolylineOverlay, PG=PlotGrid, PL=PlotLegend, PM=PlotMarker,
NH=NavigationHeader, TES=TypedEntrySelectorCell, TDH=TableDrawHelper,
PCD=PropertyCellDrawer, SH=SortableHeader, ST=SelectionTracker, AL=AssetLinkLabel,
SR=StyledRow, SFT=SearchFilterToolbar, EAL=EditorAnimationLoop, PCP=PaletteColorPicker.

### Component Adoption Breadth

| Component | # Windows |
|-----------|-----------|
| StyleCache | 11 |
| IconButtonHelper | 4 |
| FoldoutSection | 4 |
| BarChart | 2 |
| NavigationHeader | 2 |
| PolylineOverlay, PlotGrid, PlotLegend, PlotMarker | 1 each |
| TypedEntrySelectorCell | 1 (but saves ~660 LOC) |

---

## Unity Package Design

### Package Eligibility

A component is **package-eligible** if it has zero `using BalloonParty.*`
imports and depends only on `UnityEditor`/`UnityEngine`.

| Component | Package? | Reason |
|-----------|----------|--------|
| SortableHeader | ✅ | Pure generic sort state + toolbar button |
| SelectionTracker | ✅ | Pure generic `ISelectable` interface |
| AssetLinkLabel | ✅ | Pure `EditorGUIUtility.PingObject` |
| StyledRow | ✅ | Pure background tint + bold label |
| SearchFilterToolbar | ✅ | Generic over `TEnum` |
| EditorAnimationLoop | ✅ | Pure editor update loop |
| TableDrawHelper | ✅ | Pure separators and cell insets |
| PropertyCellDrawer | ✅ | Pure `SerializedProperty` cell drawing |
| BarChart | ✅ | Pure rect + colour drawing |
| PolylineOverlay | ✅ | Pure `Handles.DrawAAPolyLine` |
| PlotGrid | ✅ | Pure grid lines + labels |
| PlotLegend | ✅ | Pure swatch + label |
| PlotMarker | ✅ | Pure marker shapes |
| NavigationHeader | ✅ | Pure ◀/▶ + IntField |
| IconButtonHelper | ✅ | Pure `FindTexture` + cache |
| StyleCache | ✅ | Pure lazy GUIStyle factory |
| FoldoutSection | ✅ | Pure EditorPrefs-backed foldout |
| PaletteColorPicker | ✅ | Moves to package once `IColorPalette` interface is introduced |
| TypedEntrySelectorCell | ✅ | Mechanism is generic; no BalloonParty deps in `FieldSpec[]` |

**19 package-eligible** (all components move to the package).

### Package Structure

```
Packages/com.balloonparty.editorui/
├── package.json
├── CHANGELOG.md
├── LICENSE.md
├── README.md
├── Editor/
│   ├── com.balloonparty.editorui.Editor.asmdef
│   ├── Charts/
│   │   ├── BarChart.cs
│   │   ├── PolylineOverlay.cs
│   │   ├── PlotGrid.cs
│   │   ├── PlotLegend.cs
│   │   └── PlotMarker.cs
│   ├── Tables/
│   │   ├── ISelectable.cs
│   │   ├── SortableHeader.cs
│   │   ├── SelectionTracker.cs
│   │   ├── StyledRow.cs
│   │   ├── TableDrawHelper.cs
│   │   ├── PropertyCellDrawer.cs
│   │   └── TypedEntrySelectorCell.cs
│   ├── Layout/
│   │   ├── FoldoutSection.cs
│   │   ├── NavigationHeader.cs
│   │   └── SearchFilterToolbar.cs
│   ├── Palette/
│   │   ├── IColorPalette.cs
│   │   ├── EditorAssetCache.cs
│   │   └── PaletteColorPicker.cs
│   └── Utilities/
│       ├── StyleCache.cs
│       ├── IconButtonHelper.cs
│       ├── AssetLinkLabel.cs
│       └── EditorAnimationLoop.cs
├── Tests/
│   └── Editor/
│       ├── com.balloonparty.editorui.Tests.Editor.asmdef
│       ├── Charts/
│       │   ├── BarChartLogicTests.cs
│       │   ├── PolylineNormalizationTests.cs
│       │   └── PlotGridComputationTests.cs
│       ├── Tables/
│       │   ├── SortableHeaderTests.cs
│       │   └── SelectionTrackerTests.cs
│       ├── Layout/
│       │   └── NavigationHeaderTests.cs
│       ├── Palette/
│       │   └── PaletteColorPickerTests.cs
│       └── Utilities/
│           ├── StyleCacheTests.cs
│           └── IconButtonHelperTests.cs
└── Samples~/
    └── TableWindowExample/
        └── SampleTableWindow.cs
```

### Namespace Strategy

Root namespace: **`BalloonParty.EditorUI`** (collision-safe, matches package
name `com.balloonparty.editorui`).  Sub-namespaces mirror folders:
- `BalloonParty.EditorUI.Charts`
- `BalloonParty.EditorUI.Tables`
- `BalloonParty.EditorUI.Layout`
- `BalloonParty.EditorUI.Utilities`

### Assembly Definition

```json
{
  "name": "com.balloonparty.editorui.Editor",
  "rootNamespace": "BalloonParty.EditorUI",
  "references": [],
  "includePlatforms": ["Editor"],
  "allowUnsafeCode": false,
  "autoReferenced": false
}
```

`autoReferenced: false` — consumers must add an explicit reference.
`BalloonParty.Editor.asmdef` and `BalloonParty.Configuration.Editor.asmdef`
both add a reference to `com.balloonparty.editorui.Editor`.

### `package.json`

```json
{
  "name": "com.balloonparty.editorui",
  "version": "1.0.0",
  "displayName": "Editor UI Library",
  "description": "Reusable IMGUI building blocks for Unity editor windows.",
  "unity": "2022.3",
  "keywords": ["editor", "imgui", "tools", "charts", "tables"],
  "type": "tool"
}
```

### Development Workflow

1. **Embedded package** — develop in `Packages/com.balloonparty.editorui/`
   (Unity treats this as a local package automatically).
2. **BalloonParty.Editor.asmdef** adds a reference to
   `com.balloonparty.editorui.Editor`.
3. When stable, push to a standalone git repo; consume via UPM git URL:
   `"com.balloonparty.editorui": "https://github.com/jose-villegas/editorui.git"`
4. Versioning: SemVer — bump minor for new components, major for breaking
   API changes, patch for bug fixes.

### Migration Path

1. Create `Packages/com.balloonparty.editorui/` with `package.json` + asmdef.
2. **Move** (not copy) the 18 eligible `.cs` files **with their `.meta` files**
   to preserve GUIDs and avoid orphaned references.
3. Change namespace from `BalloonParty.Editor.EditorUI` to
   `BalloonParty.EditorUI.*` (sub-namespace per folder).
4. Update all consumers with new `using` statements — including
   `BalloonParty.Configuration.Editor` consumers (drawers that use
   `TableDrawHelper`, `PropertyCellDrawer`, etc.).
5. Add `com.balloonparty.editorui.Editor` reference to both
   `BalloonParty.Editor.asmdef` and `BalloonParty.Configuration.Editor.asmdef`.
6. `dotnet build` all `.csproj` + visual check in editor.

**GUID note:** moving `.meta` files alongside `.cs` preserves Unity's
internal references.  No asset re-import needed.

---

## Estimated Impact (all windows)

| Scope | Before | After | Savings |
|-------|--------|-------|---------|
| LevelPacingWindow | 1 529 | ~480 | ~1 050 (69 %) |
| LevelPacingCurvePanel | 550 | ~200 | ~350 (64 %) |
| Other windows (combined) | 5 415 | ~5 199 | ~216 (4 %) |
| **New library code** | 0 | ~470 | — |
| **Net total** | 7 494 | ~6 349 | **~1 616 saved, net ~1 146 (15 %)** |

The biggest wins are in LevelPacingWindow + CurvePanel.  Other windows see
smaller but consistent improvements through `StyleCache`, `FoldoutSection`,
and `IconButtonHelper`.

---

## Implementation Phases

### Phase 1 — Package Scaffold + P0 Core
Create the embedded package at `Packages/com.balloonparty.editorui/`.
Move existing eligible components (with `.meta` files).  Create `StyleCache`,
`BarChart`, `PolylineOverlay`, `PlotGrid`.  Refactor `LevelPacingCurvePanel`
to use them.  Write tests for computation logic.

### Phase 2 — Helpers + Palette (P1 + P2)
Create `NavigationHeader`, `IconButtonHelper`, `PlotLegend`, `PlotMarker`,
`FoldoutSection`.  Implement `IColorPalette` + `EditorAssetCache<T>` +
migrate `PaletteColorPicker`.  Add `: IColorPalette` to `GamePalette`.
Finish refactoring `LevelPacingCurvePanel` to ~200 LOC.
Adopt `StyleCache` in all 11 eligible windows.

### Phase 3 — Generic Cell (P0 TypedEntrySelectorCell) *(parallelisable with Phase 2)*
Create `TypedEntrySelectorCell<TEnum>` in the package.  Refactor the
three balloon/item/actor expanded + collapsed cell methods in
`LevelPacingWindow`.  Collapse triplicated focus methods.

### Phase 4 — Broad Adoption
Adopt `FoldoutSection` in BushBakerWindow, ShadowBakerEditor,
SpriteLayerCombinerEditor.  Adopt `BarChart` in ShotSolverWindow.
Adopt `ST`/`AL`/`SFT` in UnusedAssetsWindow.  Adopt `IBH` in
GameRenderMapsWindow.

### Phase 5 — Cleanup + Publish
Remove dead private methods from refactored windows.  Update package
`README.md`.  Push package to standalone git repo.  Tag `v1.0.0`.

---

## Design Decisions

**Composition over inheritance** — most new types are static utility classes;
some are stateful instances (`EditorAnimationLoop`, `EditorAssetCache<T>`,
`PaletteColorPicker`).  No base class imposed; windows compose freely.

**UI Toolkit deferred** — these windows rely heavily on absolute-positioned
`Rect` layout and `Handles` drawing which have no UI Toolkit equivalents.
The IMGUI extraction is the right move now; UI Toolkit can come later — the
`IColorPalette` interface and computed-rect separation make future migration
straightforward.

**No VContainer / DI** — editor utilities need no DI registration.

**Package-first** — build directly in `Packages/` from the start so the
namespace and asmdef are correct from day one.  No intermediate step of
building in `Assets/` and then moving.

**`autoReferenced: false`** — consuming assemblies must explicitly reference
the package, making dependencies visible (matches VContainer/UniRx pattern).

---

## Palette Generalization — `IColorPalette`

### Problem

`PaletteColorPicker` was previously marked project-specific because it
depends on `GamePalette` (a concrete `ScriptableObject`).

### Solution

Introduce a 3-member interface in the package:

```csharp
namespace BalloonParty.EditorUI.Palette
{
    public interface IColorPalette
    {
        int Count { get; }
        string GetName(int index);
        Color GetColor(int index);
    }
}
```

**Package provides:**
- `IColorPalette` — minimal read-only contract
- `EditorAssetCache<T>` — finds any SO by type via `AssetDatabase`
- `PaletteColorPicker` — draws any `IColorPalette` implementation

**Project provides:**
- `GamePalette : ScriptableObject, IGamePalette, IColorPalette`
  (3-line explicit interface implementation)

**Benefits:**
- Picker becomes fully generic — any project implements `IColorPalette`
- `GamePalette`'s rich API (`IGamePalette` — masks, rainbow, progress) is
  unaffected
- Consumer code unchanged (`DrawLayout`, `SelectedColor`)
- `TypedEntrySelectorCell` also unlocks for the package (no remaining deps)

---

## Testability Strategy

### Architecture: Separate Computation from Drawing

Each component that does IMGUI drawing should expose its **pure logic** in
testable methods.  The actual `EditorGUI.*` / `Handles.*` calls remain thin
untested wrappers.

```
BarChart.ComputeBarRects(Rect, float[], float, BarChartOptions) → Rect[]  ← TESTABLE
BarChart.Draw(Rect, float[], float, BarChartOptions) → int?               ← wrapper
```

### What IS testable (pure logic)

| Component | Testable seam |
|-----------|---------------|
| BarChart | Rect computation, normalization, click-index-from-x |
| PolylineOverlay | Point normalization to plot rect |
| PlotGrid | Grid line Y positions, label values |
| StyleCache | Cache-hit semantics (returns same instance) |
| IconButtonHelper | Dictionary caching, fallback when icon missing |
| SortableHeader | `ApplySort` sorting logic |
| SelectionTracker | `GetSelected` filtering logic |
| NavigationHeader | Value clamping, change detection |
| EditorAnimationLoop | State machine (start/pause/stop/tick) |
| PaletteColorPicker | Index clamping, `IColorPalette` stub driving |
| EditorAssetCache | Lazy-init, invalidation |

### What is NOT testable

Pixel-level rendering, visual appearance, actual IMGUI draw calls.
These are validated by in-editor visual inspection.

### Test Assembly

```json
{
  "name": "com.balloonparty.editorui.Tests.Editor",
  "rootNamespace": "BalloonParty.EditorUI.Tests",
  "references": ["com.balloonparty.editorui.Editor"],
  "includePlatforms": ["Editor"],
  "overrideReferences": true,
  "precompiledReferences": ["nunit.framework.dll"],
  "autoReferenced": false,
  "defineConstraints": ["UNITY_INCLUDE_TESTS"]
}
```

Tests live at `Packages/com.balloonparty.editorui/Tests/Editor/` —
discovered automatically by Unity Test Runner.

### Test Files

```
Tests/Editor/
├── Charts/
│   ├── BarChartLogicTests.cs       — rect computation, normalization, click index
│   ├── PolylineNormalizationTests.cs — point mapping, empty/single-value edge cases
│   └── PlotGridComputationTests.cs   — grid Y positions, label formatting
├── Tables/
│   ├── SortableHeaderTests.cs      — ApplySort ascending/descending, stable sort
│   └── SelectionTrackerTests.cs    — GetSelected, toggle, select-all
├── Layout/
│   └── NavigationHeaderTests.cs    — clamping, boundary (min=1)
├── Palette/
│   └── PaletteColorPickerTests.cs  — mock IColorPalette, index bounds
└── Utilities/
    ├── StyleCacheTests.cs          — lazy-init, same-instance guarantee
    └── IconButtonHelperTests.cs    — cache hit/miss, fallback glyph
```

---

## Reviewer Fixes Incorporated

| # | Finding | Resolution |
|---|---------|------------|
| 1 | `EditorAnimationLoop` is stateful | Acknowledged in Design Decisions — not all are statics |
| 2 | Cross-asmdef breakage | Migration path now requires both asmdefs to add reference |
| 3 | GUID orphaning | Migration path: move `.meta` files alongside `.cs` |
| 5 | StyleCache priority mismatch | Promoted to P0 (it's in Phase 1 anyway) |
| 6 | `BarChart` per-frame alloc | Use `IReadOnlyList<float>` + `in BarChartOptions` |
| 7 | Namespace collision | Changed to `BalloonParty.EditorUI` |
| 8 | No test strategy | Added full testability section above |
| 9 | `autoReferenced: true` | Changed to `false` with explicit refs |
| 10 | TypedEntrySelectorCell eligibility | Moved to package-eligible (no domain deps) |
| 12 | Phase ordering | Phase 3 (TES) can run parallel with Phase 2 |
| 13 | Samples need `package.json` entry | Already present in `package.json` above |
| 14 | Min Unity version | Matches project version (2022.3) |
