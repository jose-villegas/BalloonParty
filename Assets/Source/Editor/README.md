# Editor

Shared editor tooling — drawer infrastructure, custom inspectors, bake pipelines, and menu tools. Nothing in this folder is referenced at runtime — it compiles only in Editor builds.

## Contents

| File | What it does |
|---|---|
| `PropertyDrawerHelper` | `internal static` utility class — shared constants (`LineHeight`, `Spacing`) and drawing primitives used by every custom drawer. `DrawCommonFields` iterates a serialized type's direct children via `SerializedProperty`, skipping a caller-supplied exclusion set, and renders each with a nicified display name. `DrawNamedField` and `DrawSectionHeader` handle individual named fields and bold group labels. `CountCommonFields` mirrors `DrawCommonFields` for height calculation |
| `SceneDrawingHelper` | `public static` utility class — shared Scene-view drawing primitives via `Handles`. `DrawWorldRect` draws an axis-aligned rectangle from center/width/height. `DrawWorldRectFromLimits` draws from explicit top/right/bottom/left edges (clockwise `Vector4` convention). Used by `GameDisplayConfigurationEditor` and `MapLimitsSceneOverlay` |
| `AutoFieldPropertyDrawer` | Abstract base class for `PropertyDrawer`s that want common fields rendered automatically. Seals `GetPropertyHeight` and `OnGUI` and exposes four override points |
| `EnumIndexedDrawer` | Drawer for `[EnumIndexed(typeof(SomeEnum))]` (attribute in `Shared/`) — for an array indexed by an enum's ordinal, labels each element with the enum value's name ("Default", "Return", …) instead of "Element 0, 1, …". Reads the element index from the property path and relabels the default field draw |
| `SortingLayerNameDrawer` | Drawer for `[SortingLayerName]` string fields — renders a popup of the project's sorting layers instead of a plain text field |
| `UnitCircleDrawer` | Drawer for `[UnitCircle]` `Vector2` fields — click-or-drag disc picker for aiming a normalized direction, with an editable numeric row above for exact values |
| `LevelRangeEntryDrawer` | Drawer for `LevelRangeEntry` array elements — labels each element "Level N", "Level N–M", or "Fallback" instead of "Element 0, 1, …" |
| `LevelThresholdOverrideDrawer` | Drawer for `LevelThresholdOverride` array elements — labels each element "Level N" or "Level N–M" instead of "Element 0, 1, …" |
| `ScriptReferenceRemap` | `internal static` — context menu tool for remapping script GUIDs in prefab/scene files after class renames. Available from the Inspector component "⋮" menu (`CONTEXT/MonoBehaviour`) |
| `ScriptSearchPopup` | `internal sealed EditorWindow` — searchable popup for picking a `MonoScript`, similar to Unity's "Add Component" search. Used by `ScriptReferenceRemap` |
| `TexturePreviewBox` | Reusable texture preview box for editor windows — HelpBox container with a toolbar (background toggle + custom actions) and a centred preview over a selectable background (checkerboard/black/white). Used by `BushBakerWindow` |
| `GradientTextureDrawer` | `MaterialPropertyDrawer` for `[GradientTexture]` shader properties — replaces the texture slot with Unity's gradient picker and bakes the gradient to a `Texture2D` sub-asset embedded in the material (gradient definition persisted in the importer's `userData`) |
| `CloudNoiseTextureGenerator` | Menu tool (**Tools > BalloonParty > Generate Cloud Noise Texture**) — regenerates the tileable noise texture `PuffCloud.shader` samples: periodic Perlin (seamless by construction), histogram-matched to the original procedural simplex, written as a 16-bit PNG so the smoothstepped cloud gradients can't band. The period must match the material's `_NoisePeriod` |
| `UnusedAssetsWindow` | Menu tool (**Tools > BalloonParty > Unused Assets**) — lists assets no root reaches (candidates, not verdicts), sorted by size, click-to-ping + copy-paths. Roots: every scene, `Resources/`, `Assets/Settings/` (ProjectSettings references aren't walkable), preloaded assets. Scripts/docs/editor/plugins excluded — their usage isn't asset-reference shaped. Anything code-loaded by name (`Shader.Find` fallbacks etc.) can be a false positive; review before deleting |
| `LevelPacingWindow` | Menu tool (**Tools > BalloonParty > Level Pacing**) — spreadsheet-style view of `LevelPacingConfiguration`: each level range is a row, each parameter a column. Easier to compare across levels than the default vertical Inspector |
| `SceneCaptureServiceEditor` | Custom inspector for `SceneCaptureService` — live play-mode preview of the shared scene-capture RT, for judging the downscale by eye |
| `ScreenSpaceLightServiceEditor` | Custom inspector for `ScreenSpaceLightService` — live play-mode preview of the light buffer in two views (RGB = bounce color, A = shadow amount) |
| `ColorableBalloonVariantEditor` | Custom inspector for `ColorableBalloonVariant` — adds a palette color picker (via `EditorUI/PaletteColorPicker`) to preview the variant in any palette color without entering Play mode |
| `LaserViewEditor` | Custom inspector for `LaserView` — adds an `EffectViewPreviewPlayer` (via `EffectPreview/LaserPreviewModule`) to preview the laser animation clip frame-by-frame in edit mode |
| `RainbowBalloonVariantEditor` | Custom inspector for `RainbowBalloonVariant` — replaces the inherited single-colour preview with an allowed-colours mask preview: pick a set of palette colors and push them into the banded shader the way a level's `AllowedColorsMask` would |
| `EditorUI/` | Reusable UI building blocks for editor windows — `SortableHeader` (sortable column with ▲/▼), `SelectionTracker` (`ISelectable` + toggle-all + per-row checkbox + count + get-selected), `AssetLinkLabel` (clickable ping-to-select), `StyledRow` (bold label + highlighted row), `SearchFilterToolbar` (search + enum filter + refresh), `EditorAnimationLoop` (play/pause/stop loop driven by `EditorApplication.update`), `PaletteColorPicker` (palette dropdown + swatches over `ConfigAssetCache<GamePalette>`). See `EditorUI/README.md` |
| `EffectPreview/` | Reusable editor-time preview system for `EffectView` subclasses — `IEffectPreviewModule` (interface for pluggable rendering logic), `EffectViewPreviewPlayer` (animation loop, color picker, config caches, inspector GUI), `EditorGridHelper` (slot grid position math without a runtime `SlotGrid`), `PaintSplashPreviewModule` (blob flight preview), `ChainLightningPreviewModule` (line renderer bolt preview), `LaserPreviewModule` (animation-clip frame preview). See `EffectPreview/README.md` |
| `Bush/` | Bush Baker tooling — fractal branch-map generation, Gielis leaf baking, leaf atlas packing, `BushVariantData` export, all behind the **Tools > Bush Baker** window. See `Bush/README.md` |
| `ShadowBake/` | `SpriteShadowBakerEditor` — bakes a prefab's blurred union sprite silhouette into a single shadow sprite, replacing per-frame shader blur. See `ShadowBake/README.md` |
| `SpriteCombine/` | `SpriteLayerCombinerEditor` — flattens a prefab's rigid sprite layers into one baked sprite to cut draws/overdraw. See `SpriteCombine/README.md` |
| `Maps/` | `GameRenderMapsWindow` — unified play-mode preview of the project's shared render-target "maps" (disturbance field, GI light buffer, …) with per-channel RGBA isolation, behind **Tools > BalloonParty > Game Render Maps**. See `Maps/README.md` |
| `FrameDump/` | Dumps the Frame Debugger's captured event list to a diff-friendly text file, for comparing batch composition before/after a rendering change. Two menu items under **Tools > BalloonParty**. See `FrameDump/README.md` |
| `ShotSolver/` | `ShotSolverWindow` — play-mode-only tool that sweeps aim angle against the live board and reports which windows reach a target score, behind **Tools > BalloonParty > Shot Solver**. See `ShotSolver/README.md` |
| `TestRunner/` | `EditModeTestRunner` — runs the EditMode suite inside the open editor (no batchmode project-lock) via **Tools > BalloonParty > Run EditMode Tests**, writing the same `Tools/last-test-run.md` the headless runner produces. No README |

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
| `BalloonPrefabEntryDrawer` | `Configuration/Editor/` | Special section handles `_nudgeOverrides` and `_hitVfxOverrides` (variable-height arrays) |
| `HitVfxOverrideDrawer` | `Configuration/Editor/` | `BuildFoldoutLabel` appends `[HitOutcome]`; `_appliesTo` pinned to top |
| `NudgeOverrideDrawer` | `Nudge/Editor/` | `BuildFoldoutLabel` appends `[NudgeType]`; `_appliesTo` pinned to top via `DrawPinnedFields` using `EnumFlagsField`; `_falloff` conditional on Shockwave flag |

## Standalone drawers

These drawers extend `PropertyDrawer` directly and handle their own rendering without the auto-field pattern:

| Drawer | Location | What it customises |
|---|---|---|
| `PaletteColorMaskDrawer` | `Configuration/Editor/` | Renders a bitmask `int` as labeled per-color checkboxes from `GamePalette` |
| `PaletteColorNameDrawer` | `Configuration/Editor/` | Renders a `string` field as a popup of `GamePalette` color names with a color swatch |
| `SortingLayerNameDrawer` | `Editor/` | Renders a `[SortingLayerName]` string field as a popup of the project's sorting layers |
| `UnitCircleDrawer` | `Editor/` | Renders a `[UnitCircle]` `Vector2` field as a click-or-drag disc for aiming a normalized direction |
| `LevelRangeEntryDrawer` | `Editor/` | Relabels `LevelRangeEntry` array elements as "Level N" / "Level N–M" / "Fallback" |
| `LevelThresholdOverrideDrawer` | `Editor/` | Relabels `LevelThresholdOverride` array elements as "Level N" / "Level N–M" |

## Custom editors

| Editor | Target | What it does |
|---|---|---|
| `PaintSplashViewEditor` | `PaintSplashView` | Constructs an `EffectViewPreviewPlayer` with a `PaintSplashPreviewModule`. The module handles radial blob flights, arc/scale/shadow/sprite curves, spin, MPB updates, blob particle simulation, and splash particle spawning (prefab-stage-aware). The player manages the animation loop, palette color picker, and config caches. All preview logic is in `EffectPreview/PaintSplashPreviewModule` |
| `ChainLightningViewEditor` | `ChainLightningView` | Constructs an `EffectViewPreviewPlayer` with a `ChainLightningPreviewModule`. The module generates random slot positions via `EditorGridHelper`, fills jagged bolt segments into the view's `LineRenderer`s, and animates forward growth + retraction via a delta-time state machine. All preview logic is in `EffectPreview/ChainLightningPreviewModule` |
| `GameConfigurationEditor` | `GameConfiguration` | Adds a "Show Limits In Scene" toggle below the default inspector that controls `MapLimitsSceneOverlay` |
| `GameDisplayConfigurationEditor` | `GameDisplayConfiguration` | (in `Configuration/Editor/GameDisplayConfigurationDrawer.cs`) Draws reference world dimensions, the Scene Capture fields, per-aspect-ratio ortho-size previews, and a Scene-view overlay of the reference box + device frames |
| `SceneCaptureServiceEditor` | `SceneCaptureService` | Live play-mode preview of the shared scene-capture RT |
| `ScreenSpaceLightServiceEditor` | `ScreenSpaceLightService` | Live play-mode preview of the light buffer (RGB = bounce, A = shadow) |
| `ColorableBalloonVariantEditor` | `ColorableBalloonVariant` | Palette color picker to preview variant coloring outside Play mode |
| `LaserViewEditor` | `LaserView` | Constructs an `EffectViewPreviewPlayer` with a `LaserPreviewModule`. The module samples the view's animation clip frame-by-frame (`AnimationClip.SampleAnimation`, since `Animator.Update` is unreliable outside Play mode) and re-applies the picked tint after each sample |
| `RainbowBalloonVariantEditor` | `RainbowBalloonVariant` | Adds an allowed-colors mask picker (`EditorGUILayout.MaskField` over `GamePalette`) and an "Apply Bands Preview" button that pushes the mask into the banded shader the way a level's `AllowedColorsMask` would |
| `SpriteShadowBakerEditor` | `SpriteShadowBaker` | Bake button + full shadow-bake pipeline — see `ShadowBake/README.md` |
| `SpriteLayerCombinerEditor` | `SpriteLayerCombiner` | Bake button + sprite-layer flattening pipeline — see `SpriteCombine/README.md` |

## Scene overlays

| Overlay | What it draws |
|---|---|
| `MapLimitsSceneOverlay` | `[InitializeOnLoad]` static class — draws the `GameConfiguration.LimitsClockwise` map boundary as an orange rectangle with edge-value labels in the Scene view. Active in both edit and play mode regardless of inspector selection. Uses `ConfigAssetCache<GameConfiguration>` to locate the asset. Toggle persisted via `EditorPrefs` and controllable from the `GameConfigurationEditor` inspector |

## Menu tools

| Tool | Menu path | What it does |
|---|---|---|
| `CloudNoiseTextureGenerator` | `Tools > BalloonParty > Generate Cloud Noise Texture` | Regenerates `Assets/Textures/Grid/CloudNoiseTileable.png` — the tileable 16-bit noise texture `PuffCloud.shader` fetches per octave |
| `BushBakerWindow` | `Tools > Bush Baker` | Bush branch-map + leaf-atlas baking window (see `Bush/README.md`) |
| `SetMobileTextureSize` | `Assets > Texture > Set Mobile Max Size > {size}` | Sets platform-specific max texture size overrides for iPhone and Android on all selected textures. Sizes: 64, 128, 256, 512, 1024, 2048. "Reset to Default" removes the mobile overrides. Only appears when the selection contains `Texture2D` assets |
| `TextureAuditWindow` | `Assets > Texture > Texture Audit Window` | Opens an editor window pre-populated with all textures from the selected folder(s) or individual texture selection. Shows a sortable table (Name, Source size, Default/iPhone/Android max, Override status). Filters: All, No Override, With Override. Text search by name. Rows without mobile overrides are highlighted orange. Click a name to ping it in the Project view. Select rows → choose a size → Apply/Reset from the bottom bar |
| `ScriptReferenceRemap` | `CONTEXT/MonoBehaviour/Remap Script References` | Remaps script GUID references in a prefab/scene file. From the Inspector "⋮" menu: parses the selected asset's file, finds broken script GUIDs, opens `ScriptSearchPopup` to pick the replacement, and replaces the GUID within that asset file. Designed for class renames that break serialized references |
| `ScriptSearchPopup` | (internal — opened by `ScriptReferenceRemap`) | Searchable dropdown popup similar to Unity's "Add Component" search. Filters all `MonoScript` assets by class name and namespace. Supports keyboard navigation (↑/↓/Enter/Escape) and multi-term search |
| `LevelPacingWindow` | `Tools > BalloonParty > Level Pacing` | Spreadsheet-style view of `LevelPacingConfiguration` — one row per level range, one column per parameter |
| `GameRenderMapsWindow` | `Tools > BalloonParty > Game Render Maps` | Unified play-mode preview of the project's shared render-target "maps" with per-channel RGBA isolation — see `Maps/README.md` |
| `FrameDebuggerDumper` | `Tools > BalloonParty > Dump Frame Debugger`, `Dump Frame Debugger With Step Screenshots` | Dumps the Frame Debugger's captured event list to a diff-friendly text file — see `FrameDump/README.md` |
| `ShotSolverWindow` | `Tools > BalloonParty > Shot Solver` | Play-mode-only tool that sweeps aim angle against the live board and reports which windows reach a target score — see `ShotSolver/README.md` |

