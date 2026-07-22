# Editor UI Library

Reusable IMGUI building blocks for Unity editor windows. Eliminates repeated
charting, table, icon, and style-caching patterns across editor tooling.

## Installation

Add as a local package in your project's `Packages/` folder, or via git URL:

```json
"com.balloonparty.editorui": "https://github.com/jose-villegas/editorui.git"
```

## Components

### Charts
- **BarChart** — Clickable bar chart with selection highlight.
- **PolylineOverlay** — Normalized polyline via `Handles.DrawAAPolyLine`.
- **PlotGrid** — Horizontal grid lines with Y-axis and X-axis labels.
- **PlotLegend** — Swatch + label entries for chart legends.
- **PlotMarker** — Diamond and circle marker shapes.

### Tables
- **SortableHeader** — Toolbar button that toggles sort direction.
- **SelectionTracker** — Checkbox-based row selection with select-all.
- **StyledRow** — Highlighted rows and bold labels.
- **TableDrawHelper** — Separators, group backgrounds, cell insets.
- **PropertyCellDrawer** — SerializedProperty fields in table cells.
- **TypedEntrySelectorCell** — Generic expand/collapse cell for enum-typed weighted arrays.

### Layout
- **FoldoutSection** — EditorPrefs-backed persistent foldout.
- **NavigationHeader** — ◀ [Label] [IntField] ▶ navigation row.
- **SearchFilterToolbar** — Search field + enum popup filter + refresh.

### Palette
- **IColorPalette** — Minimal read-only color palette interface.
- **EditorAssetCache\<T\>** — Lazy ScriptableObject finder with invalidation.
- **PaletteColorPicker** — Color popup + swatch for any `IColorPalette`.

### Utilities
- **StyleCache** — Lazy-init GUIStyle factory (eliminates per-frame allocations).
- **IconButtonHelper** — Cached icon lookup with fallback glyph.
- **AssetLinkLabel** — Clickable label that pings assets in the Project view.
- **EditorAnimationLoop** — Play/pause/stop animation loop via EditorApplication.update.

## Usage

Add a reference to `com.balloonparty.editorui.Editor` in your editor asmdef:

```json
{
  "references": ["com.balloonparty.editorui.Editor"]
}
```

Then import the namespace:

```csharp
using BalloonParty.EditorUI;
using BalloonParty.EditorUI.Charts;
using BalloonParty.EditorUI.Tables;
```

## Requirements

- Unity 2022.3+
- Editor-only (no runtime dependency)
