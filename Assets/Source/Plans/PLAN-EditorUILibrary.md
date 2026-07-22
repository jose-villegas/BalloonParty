@page plan_editor_ui_library Editor UI Component Library

# Editor UI Component Library

Extract reusable IMGUI drawing components from large editor windows into
`Assets/Source/Editor/EditorUI/`, reducing per-window LOC and eliminating
repeated patterns (charting, tables, icon helpers, style caching).

## Motivation

`LevelPacingWindow.cs` (1 529 LOC) and `LevelPacingCurvePanel.cs` (550 LOC)
together hold ~2 080 LOC that mixes reusable drawing patterns with
domain-specific logic.  Several patterns recur in `ShotSolverWindow`,
`GameRenderMapsWindow`, `TextureAuditWindow`, and `BushBakerWindow`.  A first
wave of extraction (`EditorUI/`) already exists and is well-structured; this
plan defines the **second wave**.

---

## Anti-Patterns to Fix

| # | Pattern | Location | Severity |
|---|---------|----------|----------|
| 1 | `new GUIStyle(...)` per OnGUI frame (11+ sites) | LevelPacingWindow, CurvePanel | High |
| 2 | God method `DrawRow` (137 LOC, 15 columns) | LevelPacingWindow:722 | High |
| 3 | Triplicated balloon/item/actor cell code (~660 LOC) | LevelPacingWindow | High |
| 4 | Triplicated focus-group methods | LevelPacingWindow | Medium |
| 5 | Inline magic-number colours duplicated across files | Both | Medium |
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
Lazy-init `GUIStyle` factory.  Exposes named styles
(`CenteredBoldLabel`, `GrayMiniLabel`, `GrayMiniLabelRight`, etc.)  Eliminates
all per-frame `new GUIStyle()` allocations.

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

## Folder Layout

```
Assets/Source/Editor/EditorUI/
├── AssetLinkLabel.cs           (existing)
├── BarChart.cs                 ← NEW
├── EditorAnimationLoop.cs      (existing)
├── FoldoutSection.cs           ← NEW
├── IconButtonHelper.cs         ← NEW
├── NavigationHeader.cs         ← NEW
├── PaletteColorPicker.cs       (existing)
├── PlotGrid.cs                 ← NEW
├── PlotLegend.cs               ← NEW
├── PlotMarker.cs               ← NEW
├── PolylineOverlay.cs          ← NEW
├── PropertyCellDrawer.cs       (existing)
├── README.md                   (existing — update)
├── SearchFilterToolbar.cs      (existing)
├── SelectionTracker.cs         (existing)
├── SortableHeader.cs           (existing)
├── StyleCache.cs               ← NEW
├── StyledRow.cs                (existing)
├── TableDrawHelper.cs          (existing)
└── TypedEntrySelectorCell.cs   ← NEW
```

10 new files, ~470 LOC total library code.

---

## Estimated Impact

| File | Before | After | Savings |
|------|--------|-------|---------|
| LevelPacingWindow.cs | 1 529 | ~480 | ~1 050 (69 %) |
| LevelPacingCurvePanel.cs | 550 | ~200 | ~350 (64 %) |
| ShotSolverWindow.cs | 638 | ~580 | ~58 (9 %) |
| **New library code** | 0 | ~470 | — |
| **Net total** | 2 717 | ~1 730 | **~987 (36 %)** |

---

## Implementation Phases

### Phase 1 — Charting + Style (P0 charts + P1 StyleCache)
Create `BarChart`, `PolylineOverlay`, `PlotGrid`, `StyleCache`.
Refactor `LevelPacingCurvePanel` to use them.  Validate no visual diff.

### Phase 2 — Helpers (P1 remaining + P2)
Create `NavigationHeader`, `IconButtonHelper`, `PlotLegend`, `PlotMarker`,
`FoldoutSection`.  Finish refactoring `LevelPacingCurvePanel` to ~200 LOC.

### Phase 3 — Generic Cell (P0 TypedEntrySelectorCell)
Create `TypedEntrySelectorCell<TEnum>`.  Refactor the three
balloon/item/actor expanded + collapsed cell methods in
`LevelPacingWindow`.  Collapse `FocusBalloonTypeInAllRows` /
`FocusItemTypeInAllRows` / `FocusActorTypeInAllRows` into a generic version.
Remove the three parallel `_selected*PerRow` arrays.

### Phase 4 — Cleanup
Remove dead private methods from `LevelPacingWindow`.
Update `EditorUI/README.md` with new component docs.

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
