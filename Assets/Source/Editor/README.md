# Editor

Shared editor tooling used across all `PropertyDrawer` implementations in the project. Nothing in this folder is referenced at runtime — it compiles only in Editor builds.

## Contents

| File | What it does |
|---|---|
| `PropertyDrawerHelper` | `internal static` utility class — shared constants (`LineHeight`, `Spacing`) and drawing primitives used by every custom drawer. `DrawCommonFields` iterates a serialized type's direct children via `SerializedProperty`, skipping a caller-supplied exclusion set, and renders each with a nicified display name. `DrawNamedField` and `DrawSectionHeader` handle individual named fields and bold group labels. `CountCommonFields` mirrors `DrawCommonFields` for height calculation |
| `SceneDrawingHelper` | `public static` utility class — shared Scene-view drawing primitives via `Handles`. `DrawWorldRect` draws an axis-aligned rectangle from center/width/height. `DrawWorldRectFromLimits` draws from explicit top/right/bottom/left edges (clockwise `Vector4` convention). Used by `GameDisplayConfigurationEditor` and `MapLimitsSceneOverlay` |
| `AutoFieldPropertyDrawer` | Abstract base class for `PropertyDrawer`s that want common fields rendered automatically. Seals `GetPropertyHeight` and `OnGUI` and exposes four override points |
| `ScriptReferenceRemap` | `internal static` — context menu tool for remapping script GUIDs in prefab/scene files after class renames. Available from Inspector component "⋮" menu and Project window right-click on `.cs` files |
| `ScriptSearchPopup` | `internal sealed EditorWindow` — searchable popup for picking a `MonoScript`, similar to Unity's "Add Component" search. Used by `ScriptReferenceRemap` |
| `EditorUI/` | Reusable UI building blocks for editor windows — `SortableHeader` (sortable column with ▲/▼), `SelectionTracker` (`ISelectable` + toggle-all + per-row checkbox + count + get-selected), `AssetLinkLabel` (clickable ping-to-select), `StyledRow` (bold label + highlighted row), `SearchFilterToolbar` (search + enum filter + refresh). See `EditorUI/README.md` |

## AutoFieldPropertyDrawer

Subclasses declare one required property and up to three optional overrides:

| Member | Required | What to provide |
|---|---|---|
| `ExcludedFields` | Yes | `HashSet<string>` of serialized field names to skip in the auto pass. These must be handled in `DrawPinnedFields` or `DrawSpecialFields` |
| `DrawPinnedFields` / `GetPinnedFieldsHeight` | No | Fields drawn **above** the auto section — intended for discriminator enums that should always appear at the top (e.g. `_appliesTo` in `NudgeOverride`, `_type` in `ItemSettings` when ordering demands it) |
| `DrawSpecialFields` / `GetSpecialFieldsHeight` | Yes | Fields drawn **below** the auto section — variable-height arrays, conditional fields, and anything needing custom controls |
| `BuildFoldoutLabel` | No | Override to append contextual text to the foldout header (e.g. `Element 0  [Deflect]`, `Element 2  [Bomb]`) |

### Draw order when expanded

```
1. Foldout header        ← BuildFoldoutLabel
2. Pinned fields         ← DrawPinnedFields   (default: nothing)
3. Auto common fields    ← DrawCommonFields via PropertyDrawerHelper
4. Special fields        ← DrawSpecialFields
```

### Adding new fields to a type

- **New common field** — add it to the data class. It appears in the Inspector automatically with a nicified name derived from the field name. No drawer changes needed.
- **New field needing custom layout** — add its serialized name to `ExcludedFields` and handle it in `DrawPinnedFields` or `DrawSpecialFields` (with the matching height method).

## Current drawers using this base

| Drawer | Location | What it customises |
|---|---|---|
| `ItemSettingsDrawer` | `Configuration/Editor/` | `BuildFoldoutLabel` appends `[ItemType]`; special section for Bomb/Laser/Lightning/Paint type-specific fields with section headers. Paint section draws flight duration, arc curve, scale curve, shadow scale curve, sprite scale curve, and spin speed. `_damage` is drawn only for damaging types (hidden for Paint and Shield) |
| `BalloonPrefabEntryDrawer` | `Configuration/Editor/` | Special section handles `_nudgeOverrides` (variable height), `_overridePopVfx` toggle, and conditional `_popVfxPrefab` |
| `NudgeOverrideDrawer` | `Nudge/Editor/` | `BuildFoldoutLabel` appends `[NudgeType]`; `_appliesTo` pinned to top via `DrawPinnedFields` using `EnumFlagsField`; `_falloff` conditional on Shockwave flag |

## Standalone drawers

These drawers extend `PropertyDrawer` directly and handle their own rendering without the auto-field pattern:

| Drawer | Location | What it customises |
|---|---|---|
| `GameDisplayConfigurationDrawer` | `Configuration/Editor/` | Inline aspect-ratio / orthographic-size pair editing |
| `PaletteColorMaskDrawer` | `Configuration/Editor/` | Renders a bitmask `int` as labeled per-color checkboxes from `GamePalette` |
| `PaletteColorNameDrawer` | `Configuration/Editor/` | Renders a `string` field as a popup of `GamePalette` color names with a color swatch |

## Custom editors

| Editor | Target | What it does |
|---|---|---|
| `PaintSplashViewEditor` | `PaintSplashView` | Adds an editor-time preview panel below the default inspector. Generates radial flight paths around the object using the hex diagonal neighbor distance from `GameConfiguration.SlotSeparation`. Animates `ColorableRenderer` blobs via `EditorApplication.update` using arc, scale, shadow scale, sprite scale, and spin speed curves — all read from `ItemConfiguration`. Each blob gets a unique `sortingOrder` to prevent transparent sprite flickering. All MPB values (`_TimeOffset`, `_ShadowScale`, `_SpriteScale`) are set every frame on a fresh `MaterialPropertyBlock`. Blob child particle systems are manually simulated via `ParticleSystem.Simulate()` during preview since `Play()` doesn't work in edit mode. Private fields are read via cached `System.Reflection.FieldInfo`. Tint uses `GamePalette` color names via popup. Controls: Play/Pause (single toggle), Stop, Playback Speed slider |
| `GameConfigurationEditor` | `GameConfiguration` | Adds a "Show Limits In Scene" toggle below the default inspector that controls `MapLimitsSceneOverlay` |

## Scene overlays

| Overlay | What it draws |
|---|---|
| `MapLimitsSceneOverlay` | `[InitializeOnLoad]` static class — draws the `GameConfiguration.LimitsClockwise` map boundary as an orange rectangle with edge-value labels in the Scene view. Active in both edit and play mode regardless of inspector selection. Uses `ConfigAssetCache<GameConfiguration>` to locate the asset. Toggle persisted via `EditorPrefs` and controllable from the `GameConfigurationEditor` inspector |

## Context menu tools

| Tool | Menu path | What it does |
|---|---|---|
| `SetMobileTextureSize` | `Assets > Texture > Set Mobile Max Size > {size}` | Sets platform-specific max texture size overrides for iPhone and Android on all selected textures. Sizes: 64, 128, 256, 512, 1024, 2048. "Reset to Default" removes the mobile overrides. Only appears when the selection contains `Texture2D` assets |
| `TextureAuditWindow` | `Assets > Texture > Texture Audit Window` | Opens an editor window pre-populated with all textures from the selected folder(s) or individual texture selection. Shows a sortable table (Name, Source size, Default/iPhone/Android max, Override status). Filters: All, No Override, With Override. Text search by name. Rows without mobile overrides are highlighted orange. Click a name to ping it in the Project view. Select rows → choose a size → Apply/Reset from the bottom bar |
| `ScriptReferenceRemap` | `CONTEXT/MonoBehaviour/Remap Script References`, `Assets > Remap Script References` | Remaps script GUID references in prefab/scene files. From the Inspector "⋮" menu: parses the asset file, finds broken script GUIDs, opens `ScriptSearchPopup` to pick the replacement, and replaces the GUID across all affected assets. From the Project window: right-click a `.cs` file to remap its GUID to another script. Designed for class renames that break serialized references |
| `ScriptSearchPopup` | (internal — opened by `ScriptReferenceRemap`) | Searchable dropdown popup similar to Unity's "Add Component" search. Filters all `MonoScript` assets by class name and namespace. Supports keyboard navigation (↑/↓/Enter/Escape) and multi-term search |

