# Balloon/Spawner

Responsible for introducing balloons into the grid — both at game start and during play.

## Contents

| File | What it does |
|---|---|
| `BalloonSpawner` | `IStartable` + `IGridSpawner` — creates and places balloons, manages per-type pool registration and active-count caps, and decides where each line's balloon goes. Before each spawn wave it computes the deficit via `WaveDeficitCalculator`, publishes `WaveDamageMessage` if hearts are lost, and spawns only the effective (non-deficit) lines — doomed lines are still spawned visually via `RejectedBalloonEffect`. `SpawnPriority` is `SpawnStage.BalloonActors` (100). `Start()` registers pools and kicks off pre-warm asynchronously; `SpawnAsync()` awaits the pre-warm then populates the initial grid |
| `RejectedBalloonEffect` | **Purely visual** overflow pile for balloons that couldn't be placed — HP drain is handled at the wave level by `WaveDamageMessage`, not per-balloon. `Play` queues a would-be balloon into the overflow rows below the grid. `ITickable` runs the pile as a **per-column queue**; balloons compact upward when one pops. During spawning, `BalloonSpawner` brackets doomed-line balloons with `BeginDoomedLine(lineIndex)` / `EndDoomedLine()` — those balloons are **not** auto-popped by the heart-drain interval. Instead, `PopDoomedLine(int)` bulk-pops every balloon tagged with a given line index (triggered by `HeartTrailController` when its strikethrough sequence completes). Non-doomed overflow balloons still drain one at a time (front-most first, serialized via `PopIntervalSeconds`). `IRunResettable` returns any transients on restart. Engages a `PauseService.Pause(Overflow)` thrower-lock while the pile is active. Timing/motion come from `IOverflowSettings` |
| `BalloonFactory` | Assembles a single balloon: pulls a view from the pool, builds the model (via `BalloonModelFactory`) and its `BalloonController`, places it on the grid, and plays the entry animation. The spawner owns scheduling, per-type caps, and path computation; the factory owns the object-graph wiring |
| `BalloonPlacementResolver` | Decides which slot a line's balloon takes for a given column — own entry, re-home, or pressure-open (see "Placement under pressure") |
| `BalloonPoolChannel` | `InjectingPoolChannel<BalloonView>` — creates balloon instances via `IObjectResolver.Instantiate()`, injecting all `[Inject]` fields from the parent container without creating child scopes |

## Behaviour

`BalloonSpawner` implements `IGridSpawner` with `SpawnPriority = SpawnStage.BalloonActors`. `GridSpawnerCoordinator` calls `SpawnAsync()` after the Navigation gate opens and all lower-priority spawners (e.g. `StaticActorSpawner`) have completed. `SpawnAsync` awaits the pre-warm task started in `Start()` — so pool pre-warming and the navigation/static-actor wait overlap rather than serialize.

At game start `BalloonSpawner` spawns the initial grid rows — the row count is the active level's resolved `IActiveLevelParameters.Current.BoardLines`. For each empty slot it picks a balloon type via the level's resolved weighted pick (`IActiveLevelParameters.Current.PickBalloonEntry`, which gates types by the active range and respects each type's resolved max-count cap), then hands the entry to `BalloonFactory.Create`. Model configuration such as `TypeName`, `ScoreValue`, `HitsToPop`, `NudgeOverrides`, and `ItemActivationWeight` is bundled into a `BalloonModelConfig` struct and passed to the model constructor. `BalloonModelFactory.Create` (in `Balloon/Model/`) picks the model class from `entry.BalloonType` — shared with `RejectedBalloonEffect` so the switch lives in one place:

| `BalloonType` | Model class | Notes |
|---|---|---|
| `Simple` | `BalloonModel` | Paintable, item-capable |
| `BubbleCluster` | `BubbleClusterModel` | Not paintable, not item-capable; `HitsToPop = 5` maps hits → bubble count via `SoapBubbleClusterVariant`; scores scatter across the level's still-incomplete color bars |
| `Tough` | `ToughBalloonModel` | Not paintable, not item-capable |
| `Unbreakable` | `UnbreakableBalloonModel` | No `HitsRemaining`; only Piercing destroys |

The variant's `Initialize(model, levelAllowedColorsMask)` handles color (for colorable types) — the mask is the active level's color gate, intersected with the variant's own allowed-color mask so a level can withhold specific colors. Spawn animation follows the path returned by `SlotGrid.ComputePath`, driving a `DOPath(CatmullRom)` from the entry-row offset down to the target slot (run by `BalloonFactory.AnimateSpawn`).

After each projectile death the spawner fires `IActiveLevelParameters.Current.SpawnLines` new lines with `BalloonsConfiguration.NewBalloonLinesTimeInterval` delay between lines, but only once the turn count reaches the level's `FirstSpawnTurn` — a per-level grace period (reset to zero on every level-up) that lets a fresh range play a few turns before its own lines start landing. Multi-line spawning uses `async UniTaskVoid` with a `CancellationTokenSource`, avoiding any coroutine runner dependency. Game-start lines are seeded separately — `SceneTransition` publishes `SpawnBalloonLineMessage` on scene load.

After the final line in each spawn batch, the spawner publishes `ItemCheckMessage` (so `ItemAssigner` can assign items to newly spawned balloons) and `BalanceBalloonsMessage` (so `BalloonBalancer` settles the grid).

### Placement under pressure (turn spawns)

A blocked column isn't lost immediately. `BalloonPlacementResolver.Resolve` picks a slot in order, scanning columns nearest-first (`TryNearestColumn`):

1. **Own column** — `FindFirstReachableEmptyRow` (the topmost slot reachable by rising from the entry).
2. **Re-home** — the nearest *other* column the balloon can rise straight into (`ResolveOpenEntry`); a line may over-fill a column.
3. **Pressure-open** — the nearest column `BalloonBalancer.TryRelievePressure` can shove open by pulling a balloon into a gap *anywhere* on the board (`ResolvePressureOpen`), including interior pockets no entry reaches directly.
4. **Reject** — only when nothing frees a slot: `RejectedBalloonEffect.Play` queues the would-be balloon into the visual overflow pile below the grid (purely decorative — actual HP loss is computed at the wave level by `WaveDeficitCalculator`).

Placement reach is a `PlacementReach` argument to `Resolve`:

- **Turn spawns** pass `PlacementReach.Pressure` — all four steps above.
- **Initial grid fill** passes `PlacementReach.Rehome` — steps 1–2 only. On tight boards (static actors occupying cells) a column can be blocked at level start, so rehoming redistributes its allotment into columns that still have reachable room, keeping the total at `BoardLines × columns` whenever the board's reachable capacity allows. It never shoves (no step 3) or overflows below the grid (no step 4). `LogInitialFillDiagnostics` logs expected/achievable/spawned per column in editor and development builds.
- **Pop-spawn extras** (`SpawnLooseBalloons`) pass `PlacementReach.OwnColumn` — step 1 only; a blocked column is skipped.

### Spawn depth & tough layering

`BalloonPrefabEntry.SpawnWeight` controls *depth*, never *which* types spawn or *how many* (those come from the range pick weights and the `InitialCountWeights`/`WaveCountWeights` quota curves). Each wave's picked batch is dealt into the topmost open rows first, so the deal order is the vertical order. `PrepareSpawnBatch` sorts the batch **lightest-first** (`BySpawnWeightAscending`), so heavier types (Tough, weight `+3`) settle below lighter ones — one bottom-heavy gradient.

The **initial fill** overrides that with vertical layering (`ArrangeInitialLayers` + the pure `InitialLayerPlan.HeavyPerLine`). The board is split into segments `BalloonsConfiguration.ToughLayerSpacing` lines tall; the bottom line of each segment collects the heavy (positive-weight) types, and lighter types fill the rest. With spacing `3` that reads as *two light lines, one part-tough line, repeating*. Heavies are round-robined onto the layer lines deepest-first (an uneven count settles lower), capped at one board line each; any surplus beyond the layers' capacity sinks to the bottom as the plain gradient would place it. This is **arrangement-only** — the tough *count* is unchanged, just its depth. Spacing `0`/`1`, no heavies, or a board too short for a full segment all fall back to the plain gradient. The mapping is exact on an empty board (one pass ≈ one row) and approximate on tight boards where static actors offset a column's entry row.

## Interactions

- **SlotGrid** — queried for empty slots (`CountEmpty()`); all placed balloons registered here
- **WaveDeficitCalculator** — called before each wave to determine hearts lost (pure static function in `Game/Health/`)
- **WaveDamageMessage** — published after doomed lines are spawned, carrying `HeartsLost`
- **BalloonBalancer** — notified via `BalanceBalloonsMessage` after each spawn batch
- **ItemAssigner** — notified via `ItemCheckMessage` carrying the list of newly spawned balloon models
- **SpawnBalloonLineMessage** — triggers a single-line spawn (used by `SceneTransition` at game start and by cheats)
- **ProjectileDestroyedMessage** — triggers multi-line spawning after each turn
- **BalloonsConfiguration** — `Entries`, `NewBalloonLinesTimeInterval`, `DefaultBalloonMoveSpeed`, `SpawnEntryRowOffset`. Spawn entry travels at a constant speed (per-type `BalloonPrefabEntry.MoveSpeed`, or this fallback): duration = path length ÷ speed, so a balloon entering from farther down takes proportionally longer instead of racing a fixed duration.
- **IActiveLevelParameters** — `BoardLines`, `SpawnLines`, `PickBalloonEntry` (the per-level resolved spawn counts + weighted balloon pick)
- **DisturbanceFieldService** — stamps `Stamp()` along each spawn path using the `BalloonPath` stamp profile from `DisturbanceFieldSettings`; creates visible wakes through Puff clouds during spawn animations
