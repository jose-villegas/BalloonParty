# Configuration

All game data is split across focused ScriptableObjects. Each is registered as a singleton in `GameLifetimeScope` and injected wherever needed.

Source is organized into per-context subfolders: `Balloons/`, `Buffs/`, `Cinematics/`, `Effects/`, `GridActors/`, `Items/`, `Level/`, `Palette/`, `Ranges/`, and `Editor/` (drawers). The tables below note the subfolder where it isn't obvious.

## Contents

### ScriptableObjects

| Asset | Interface | What it holds |
|---|---|---|
| `GameConfiguration` | `IGameConfiguration` (in `Shared/`) | Projectile settings, slot grid dimensions, prediction trace params, score trail timing, score points scatter delay. (The points-per-level formula is **not** here — it lives in `LevelDifficultyResolver`/`ILevelThresholds`, `Game/Level/`.) |
| `BalloonsConfiguration` | `IBalloonsConfiguration` | Balloon-specific configuration — `BalloonPrefabEntry` entries with per-type weight/cap/nudge/VFX, default pop VFX, spawn line counts, spawn animation range, balance delay, global nudge defaults |
| `GridActorConfiguration` | `IGridActorConfiguration` | Grid actor entries with per-type weight, max-count cap, and `HitsToPop` for destructible actors (Gatekeeper). Used by `StaticActorSpawner` and the procedural `GridSpawner` |
| `GamePalette` | `IGamePalette` | Array of `PaletteEntry` (name + `Color`) — the single source for all balloon colors; `GetColor(name)` resolves name → `UnityEngine.Color` |
| `GameDisplayConfiguration` | `IGameDisplayConfiguration` | Reference world dimensions and `GetOrthogonalSize()` for camera sizing, plus the shared scene-capture tuning (`SceneCaptureDownscale`, `SceneCaptureFrameInterval`) consumed by `SceneCaptureService`. Drawn by the custom `GameDisplayConfigurationEditor` — the scene-capture fields are invisible without it |
| `ItemConfiguration` | `IItemConfiguration` | Per-item tuning — one `ItemSettings` entry per `ItemType`: activation frequency, weight, max cap, damage, and type-specific effect params |
| `BuffConfiguration` | `IBuffConfiguration` (`Buffs/`) | Lookup table from `ProjectileBuffId` to a tuning value — the single source `GetValue` resolves for whatever projectile buff a holder item grants |
| `LevelPacingConfiguration` | `ILevelPacingConfiguration` | The per-level-range difficulty ramp (`Configuration/Level/`) — ordered `LevelRangeEntry[]` (each a level band + `RangedLevelParameters`) plus the `ThresholdModifier` curve. Resolved into the live `LevelParameters` by `LevelDifficultyResolver` (`Game/Level/`); consumers read `IActiveLevelParameters.Current`, never this SO directly |
| `PuffCloudSettings` | `IPuffCloudSettings` | Puff cloud visual tuning — noise animation speed, visual padding, sorting layer/order, and the `CloudPrefab` reference |
| `BushSettings` | `IBushSettings` | Bush actor tuning — `BushPrefab`, baked `BushVariantData[]`, branch shader/gradient/shadow/AO params, leaf atlas + shadow/wind/rattle params, rustle VFX |
| `CinematicsSettings` | `ICinematicsSettings` | One `CinematicStateEntry` per `CinematicState` (indexed by ordinal): behavioural `CinematicTraits`, the camera-rig segment it plays, and optional capability blocks. Plus a top-level `LevelAscendSettings` — the Ascent is a transform-descent, not a camera move, so it has its own honest fields instead of borrowing a rig entry. All values are authored in the asset; the serialized types carry no code defaults |
| `OverflowSettings` | `IOverflowSettings` | Overflow-pile tuning — appear stagger, linger and pop pacing for rejected balloons below the grid |
| `ScoreTrailBehaviourConfiguration` | `IScoreTrailBehaviourConfiguration` | Score-trail choreography — the `ScoreTrailBehaviourId` → `MinPoints` table a group's total resolves against (highest-clearing entry wins), plus the shared `BigScoreFormationSettings` (radius, pen speed, coverage, spin) every decomposed shape draws from |
| `DisturbanceFieldSettings` | `IDisturbanceFieldSettings` | Shared disturbance field tuning — RT resolution (`TexelsPerUnit`), diffusion rate/reform speed/tick interval, wind speed/smoothing/decay, pressure, displacement amount/decay, performance thresholds, shader references, and per-source `StampProfile[]` with `StampSource` flags |
| `CloudFieldSettings` | `ICloudFieldSettings` (`Effects/`) | Shared cloud-noise field tuning — density blit material, RT resolution (`TexelsPerUnit`), and how much the Scenario Ascent/descent parallaxes the cloud roll |
| `SceneLightFieldSettings` | `ISceneLightFieldSettings` / `IScreenSpaceLightSettings` / `ISceneLightSettings` (`Effects/`) | One asset backing three interfaces: the scene light's direction/colour/intensity, the light-field RT (resolution, cadence, per-light accumulation), and the screen-space GI march (smear distance/downscale, mip spread, shadow/bounce strength, shader references) |
| `SpeckFieldSettings` | `ISpeckFieldSettings` | Ambient speck-field tuning — motion/disturbance response, per-speck look (size, trail, scale, fade, heat, color-lerp), and spawning/reduction: the spawn-all testing toggle, initial active count, per-source `SpeckProfile[]` (`SpeckSource` flags), and the reduction curve. The `SpeckField` component keeps only its own compute shader + material |
| `BushVariantData` | — | Pre-baked bush variant asset (branch map texture + leaf attachment slots), created by the Bush Baker editor window, loaded at runtime by `BushView` |

### Data types

| File | What it does |
|---|---|
| `BalloonPrefabEntry` | Serializable entry in `BalloonsConfiguration` — holds a `BalloonView` prefab reference, spawn weight, optional max-count cap, `HitsToPop` (how many hits before popping; -1 = unbreakable), `ScoreValue` (points awarded on pop), per-type `NudgeOverride[]`, and per-outcome `HitVfxOverride[]`. `BalloonType` drives which model class is created (`Simple` → `BalloonModel`, `Tough` → `ToughBalloonModel`). Item eligibility is determined by whether the model implements `IHasWriteableItemSlot` — not a flag. Pool key is derived from the prefab's GameObject name. |
| `GridActorPrefabEntry` | Serializable entry in `GridActorConfiguration` — holds a `GridActorView` prefab reference, `GridActorType`, `SlotPlacementMode` (`Random` or `Cluster`), and `HitsToPop` (relevant only for `Gatekeeper`). Per-level spawn count and cluster cap come from the level pacing gate (`IActiveLevelParameters.TryGetGridActorGate` → `ResolvedGridActorGate`), not the entry. Pool key is derived from the prefab's GameObject name. |
| `IWeightedEntry` | Shared interface for weighted random selection — `Weight`, `MaxCount`, `PoolKey`. Implemented by the resolver's resolved pick entries (`ResolvedBalloonEntry`, `ResolvedItemEntry`), which carry the active range's weight/cap; the raw catalog entries no longer implement it. |
| `LevelRangeEntry` | Serializable entry in `LevelPacingConfiguration` (`Level/`) — a level band (min/max) plus the `RangedLevelParameters` authored for it; `Contains(level)`/`PositionOf(level)` locate a level within the band. The last entry is the open-ended tail |
| `RangedLevelParameters` | Serializable authored side of a range (`Level/`) — spawn-line count, `FirstSpawnTurn` grace, per-type `BalloonTypeWeight[]`/`ItemTypeWeight[]` gates, item cadence/count weights, allowed-colours mask, all as `RangedValue`s. `Resolve(position, rng)` produces a concrete `LevelParameters` |
| `LevelParameters` / `ILevelParameters` | The **resolved** live difficulty mix for the current level (`Level/`) — concrete pick-lists, `AllowedColors`/`AllowedColorsMask`, spawn/cadence values. Produced by `LevelDifficultyResolver`; read via `IActiveLevelParameters.Current` (`Game/Level/`) |
| `RangedInt` / `RangedFloat` / `RangeMode` | Range primitives (`Ranges/`) — a value that `Resolve(positionInRange, rng)`s from a min/max via a `RangeMode` (fixed / lerped-across-the-range / random), letting difficulty scale within a band |
| `ItemSettings` | Per-item tuning data for `ItemConfiguration` — common fields (type, frequency, weight, cap, visuals, activation effect prefab, `Damage`, `DamageFlags`) plus nested per-type blocks (`BombSettings`, `LaserSettings`, `LightningSettings`, `PaintSettings`, `SnipeSettings`). `Damage` controls how many hit-points a single activation removes from each affected balloon; defaults to 1 |
| `HitVfxOverride` | Serializable `HitOutcome` → `ParticleSystem` pair in `BalloonPrefabEntry` — per-outcome hit VFX (null prefab = no VFX for that outcome) |
| `CameraRigCinematicSettings` | Serializable camera-rig segment — a timeScale curve (whose last key doubles as the segment duration) plus camera framing; the uniform shape every cinematic state plays |
| `CinematicStateEntry` | Serializable per-state declaration in `CinematicsSettings` — `CinematicTraits` + the camera-rig segment + optional capability blocks (`TrackedTrailSettings`) |
| `TrackedTrailSettings` | Serializable tuning for a trail a cinematic tracks/puppets during flight (used by the level-up's tipping trail) |
| `PaletteEntry` | Serializable name + `Color` pair used in `GamePalette` |
| `ItemType` | Enum — `None`, `Shield`, `Bomb`, `Laser`, `Lightning`, `Paint`, `Snipe` |
| `PaletteColorMaskAttribute` | `PropertyAttribute` that marks an `int` field as a bitmask over `GamePalette.Colors` — rendered in the Inspector as per-color checkboxes via `PaletteColorMaskDrawer` |
| `PaletteColorNameAttribute` | `PropertyAttribute` that marks a `string` field as a palette color name — rendered in the Inspector as a popup dropdown with color swatch via `PaletteColorNameDrawer` |
| `StampProfile` | Serializable struct in `DisturbanceFieldSettings` — `StampSource` flags, `Radius`, `Strength`, `Duration`. Defines per-source disturbance parameters |
| `StampSource` | `[Flags]` enum — `Projectile`, `BalloonPath`, `BalloonPop`, `Bomb`, `Laser`, `Paint`. Identifies which game system a `StampProfile` applies to |

### Editor

All custom `PropertyDrawer` implementations in this folder extend `AutoFieldPropertyDrawer` (in `Source/Editor/`) unless they need fully custom rendering. Common fields are discovered automatically via serialized-property iteration and rendered using nicified names — drawers only need to declare which fields require special layout in `ExcludedFields` and handle those manually.

| File | What it does |
|---|---|
| `BalloonPrefabEntryDrawer` | Extends `AutoFieldPropertyDrawer` — auto-draws common fields; manually handles `_nudgeOverrides` and `_hitVfxOverrides` (variable-height arrays) |
| `ItemSettingsDrawer` | Extends `AutoFieldPropertyDrawer` — auto-draws common fields (including `Damage`); manually handles type-specific sections (Bomb, Laser, Lightning, Paint, Snipe) with section headers. Paint section includes: Blob Flight Duration, Blob Arc Curve, Blob Scale Curve, Blob Shadow Scale Curve, Blob Sprite Scale Curve, Blob Spin Speed. Snipe section splits into a base block (Speed Buff Multiplier) and a Rainbow sub-section (charge-per-hit, bloom radius/cap, colour cycles). `_damage` is drawn only for damaging types (hidden for Paint, Shield, and Snipe); overrides `BuildFoldoutLabel` to append `[ItemType]` to the header |
| `HitVfxOverrideDrawer` | Extends `AutoFieldPropertyDrawer` — pins `_appliesTo` to the top; `BuildFoldoutLabel` appends `[HitOutcome]` |
| `GameDisplayConfigurationEditor` | Custom `Editor` for the `GameDisplayConfiguration` SO (file: `GameDisplayConfigurationDrawer.cs`) — draws the reference world dimensions, the **Scene Capture** section (`Downscale`, `Frame Interval` — hidden without this editor), an ortho-size preview per common device aspect ratio, and a Scene-view overlay of the reference box + device frames |
| `GameConfigurationEditor` | Custom `Editor` for `GameConfiguration` — adds the "Show Limits In Scene" toggle that controls `MapLimitsSceneOverlay` (see `Source/Editor/README.md`) |
| `PaletteColorMaskDrawer` | Custom `PropertyDrawer` for `PaletteColorMaskAttribute` — renders a bitmask int as labeled checkboxes matching the current `GamePalette` entries |
| `PaletteMaskDrawer` | `MaterialPropertyDrawer` for a shader's `[PaletteMask]` Float property — same bitmask idea as `PaletteColorMaskDrawer`, but for a material Inspector field a shader reads directly (e.g. `SceneLight.cginc`'s masked-light helpers) |
| `PaletteColorNameDrawer` | Custom `PropertyDrawer` for `PaletteColorNameAttribute` — renders a string field as a popup listing all `GamePalette` color names with a color swatch beside the selected entry. Uses `ConfigAssetCache<GamePalette>` for lazy-cached palette lookup |
| `StampProfileDrawer` | Custom `PropertyDrawer` for `StampProfile` — foldout header shows `StampSource` flags label; expanded view shows Sources, Radius, Strength, Duration fields |

## Design rules

- **Never hardcode** values that exist in a configuration asset.
- **Never duplicate** configuration data via `[SerializeField]` on individual systems. If a system needs a value, it injects the relevant configuration object.
- **Always inject the read-only interface**, not the concrete SO type. Every configuration SO has a corresponding `I`-prefixed interface (`IBalloonsConfiguration`, `IGamePalette`, etc.) that exposes only read-only properties. `GameLifetimeScope` registers each SO via `RegisterInstance<IInterface>(so)`. This prevents accidental mutation of SO fields at runtime and decouples consumers from the SO class.
- New configuration fields are added to the appropriate asset type. When a domain of settings grows large enough to stand alone, extract it into its own ScriptableObject rather than bloating an existing one.
- `IGameConfiguration` lives in `Shared/` (not `Configuration/`) because it is consumed by assemblies that do not reference the `Configuration` namespace. All other configuration interfaces live alongside their SO in `Configuration/`.
- New common fields added to `ItemSettings` or `BalloonPrefabEntry` appear in the Inspector automatically — no drawer changes required. Only fields that need custom layout (variable height, conditional visibility, custom controls) must be added to the drawer's `ExcludedFields` set.
- Collection properties on interfaces use `IReadOnlyList<T>` to prevent element assignment and mutation of the collection structure. The backing `T[]` or `List<T>` in the SO satisfies this implicitly.
- **Editor config lookups** must use `ConfigAssetCache<T>` (in `Shared/`) instead of inline `AssetDatabase.FindAssets` + `LoadAssetAtPath`. This caches the result after the first lookup and avoids repeated asset-database queries. The class is guarded by `#if UNITY_EDITOR` so it compiles away in builds, but is available to both runtime `OnValidate` blocks and editor code. The `config-asset-cache` audit rule enforces this in editor files and `#if UNITY_EDITOR` blocks everywhere.
