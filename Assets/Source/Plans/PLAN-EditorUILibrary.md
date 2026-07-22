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

**`TypedEntrySelectorCell<TEnum>`** (~120 LOC)
Generic expand/collapse cell for an enum-typed weighted array.  Handles
dropdown with ✓/+ prefixes, add-on-select, per-entry sub-fields via a
`FieldSpec[]`, and remove button.  Eliminates ~660 LOC of triplicated code.

**`BarChart`** (~60 LOC)
Static method: `Draw(Rect, float[], float max, BarChartOptions)` → optional
clicked index.  Selection highlight, padding.  Used by CurvePanel and
ShotSolverWindow.

**`PolylineOverlay`** (~30 LOC)
`Draw(Rect, float[], float max, Color, float thickness)` — normalised polyline
via `Handles.DrawAAPolyLine`.

**`PlotGrid`** (~70 LOC)
Horizontal grid lines, Y-axis labels, X-axis labels.  Parameterised by
divisions and value range.

### P1

**`StyleCache`** (~40 LOC)
Lazy-init `GUIStyle` factory with `Get(string key, Func<GUIStyle>)`.
Eliminates all per-frame `new GUIStyle()` allocations across 11 windows.

**`NavigationHeader`** (~35 LOC)
`Draw(string label, ref int value, int min)` → ◀ [Label] [IntField] ▶ row with
`GUIUtility.keyboardControl = 0` on change.

**`IconButtonHelper`** (~25 LOC)
`Get(string iconName, string fallbackGlyph, string tooltip) → GUIContent`.
Uses `EditorGUIUtility.FindTexture` with a static `Dictionary` cache.

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
| PaletteColorPicker | ❌ | Imports `BalloonParty.Configuration.Palette` |
| TypedEntrySelectorCell | ⚠️ | Mechanism is generic; keep project-specific initially |

**17 package-eligible, 2 project-specific.**

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
│   │   └── PropertyCellDrawer.cs
│   ├── Layout/
│   │   ├── FoldoutSection.cs
│   │   ├── NavigationHeader.cs
│   │   └── SearchFilterToolbar.cs
│   └── Utilities/
│       ├── StyleCache.cs
│       ├── IconButtonHelper.cs
│       ├── AssetLinkLabel.cs
│       └── EditorAnimationLoop.cs
└── Samples~/
    └── TableWindowExample/
        └── SampleTableWindow.cs
```

### Assembly Definition

```json
{
  "name": "com.balloonparty.editorui.Editor",
  "rootNamespace": "EditorUI",
  "references": [],
  "includePlatforms": ["Editor"],
  "allowUnsafeCode": false,
  "autoReferenced": true
}
```

**Key:** zero assembly references — the package depends only on
`UnityEditor` and `UnityEngine` (implicit).

### Namespace Strategy

- Package root: `EditorUI` (not `BalloonParty.Editor.EditorUI`)
- Sub-namespaces mirror folders: `EditorUI.Charts`, `EditorUI.Tables`,
  `EditorUI.Layout`, `EditorUI.Utilities`
- Project code: `BalloonParty.Editor` — adds `using EditorUI.Charts;` etc.

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
2. Move the 17 eligible `.cs` files from `Assets/Source/Editor/EditorUI/`.
3. Change namespace from `BalloonParty.Editor.EditorUI` to `EditorUI.*`.
4. Update all consumers with new `using` statements.
5. Keep `PaletteColorPicker` and `TypedEntrySelectorCell` in
   `Assets/Source/Editor/EditorUI/` (project-specific layer).
6. `dotnet build` + visual check in editor.

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

### Phase 1 — Package Scaffold + Charting (P0 charts + P1 StyleCache)
Create the embedded package at `Packages/com.balloonparty.editorui/`.
Move existing eligible components.  Create `BarChart`, `PolylineOverlay`,
`PlotGrid`, `StyleCache`.  Refactor `LevelPacingCurvePanel` to use them.

### Phase 2 — Helpers (P1 remaining + P2)
Create `NavigationHeader`, `IconButtonHelper`, `PlotLegend`, `PlotMarker`,
`FoldoutSection`.  Finish refactoring `LevelPacingCurvePanel` to ~200 LOC.
Adopt `StyleCache` in all 11 eligible windows.

### Phase 3 — Generic Cell (P0 TypedEntrySelectorCell)
Create `TypedEntrySelectorCell<TEnum>` (project-specific).  Refactor the
three balloon/item/actor expanded + collapsed cell methods in
`LevelPacingWindow`.  Collapse triplicated focus methods.

### Phase 4 — Broad Adoption
Adopt `FoldoutSection` in BushBakerWindow, ShadowBakerEditor,
SpriteLayerCombinerEditor.  Adopt `BarChart` in ShotSolverWindow.
Adopt `ST`/`AL`/`SFT` in UnusedAssetsWindow.  Adopt `IBH` in
GameRenderMapsWindow.

### Phase 5 — Cleanup + Publish
Remove dead private methods from refactored windows.  Update
`EditorUI/README.md` and package `README.md`.  Push package to standalone
git repo.

---

## Design Decisions

**Composition over inheritance** — all new types are static utility classes
(matching the existing `EditorUI/` pattern), not a base class.  Windows
compose them freely; no rigid lifecycle imposed.

**UI Toolkit deferred** — these windows rely heavily on absolute-positioned
`Rect` layout and `Handles` drawing which have no UI Toolkit equivalents.
The IMGUI extraction is the right move now; UI Toolkit can come later as a
separate initiative.

**No VContainer / DI** — editor utilities are stateless statics; no
registration needed.

**Package-first** — build directly in `Packages/` from the start so the
namespace and asmdef are correct from day one.  No intermediate step of
building in `Assets/` and then moving.
