# Balloon/Spawner

Responsible for introducing balloons into the grid — both at game start and during play.

## Contents

| File | What it does |
|---|---|
| `BalloonSpawner` | `IStartable` + `IGridSpawner` — creates and places balloons, manages per-type pool registration and active-count caps. `SpawnPriority` is `SpawnStage.BalloonActors` (100). `Start()` registers pools and kicks off pre-warm asynchronously; `SpawnAsync()` awaits the pre-warm then populates the initial grid |
| `BalloonPoolChannel` | `InjectingPoolChannel<BalloonView>` — creates balloon instances via `IObjectResolver.Instantiate()`, injecting all `[Inject]` fields from the parent container without creating child scopes |

## Behaviour

`BalloonSpawner` implements `IGridSpawner` with `SpawnPriority = SpawnStage.BalloonActors`. `GridSpawnerCoordinator` calls `SpawnAsync()` after the Navigation gate opens and all lower-priority spawners (e.g. `StaticActorSpawner`) have completed. `SpawnAsync` awaits the pre-warm task started in `Start()` — so pool pre-warming and the navigation/static-actor wait overlap rather than serialize.

At game start `BalloonSpawner` spawns the initial grid rows from `BalloonsConfiguration.GameStartedBalloonLines`. For each empty slot it picks a balloon type via weighted random selection from `BalloonsConfiguration.Entries`, respecting each entry's `MaxCount` cap. All model configuration — `TypeName`, `ScoreValue`, `HitsToPop`, and `NudgeOverrides` — is bundled into a `BalloonModelConfig` struct and passed to the model constructor. The model class is chosen from `entry.BalloonType`:

| `BalloonType` | Model class | Notes |
|---|---|---|
| `Simple` | `BalloonModel` | Paintable, item-capable |
| `BubbleCluster` | `BubbleClusterModel` | Not paintable, not item-capable; `HitsToPop = 5` maps hits → bubble count via `SoapBubbleClusterVariant`; scores scatter across random palette colors |
| `Tough` | `ToughBalloonModel` | Not paintable, not item-capable |
| `Unbreakable` | `UnbreakableBalloonModel` | No `HitsRemaining`; only Piercing destroys |

The variant's `Initialize(model)` handles color (for colorable types). Spawn animation follows the path returned by `SlotGrid.ComputePath`, driving a `DOPath(CatmullRom)` from the entry-row offset down to the target slot.

After each projectile death (starting from the second turn) the spawner fires `BalloonsConfiguration.NewProjectileBalloonLines` new lines with `BalloonsConfiguration.NewBalloonLinesTimeInterval` delay between lines. Multi-line spawning uses `async UniTaskVoid` with a `CancellationTokenSource`, avoiding any coroutine runner dependency. The first projectile death is skipped because game-start lines are seeded separately — `SceneTransition` publishes `SpawnBalloonLineMessage` on scene load.

After the final line in each spawn batch, the spawner publishes `ItemCheckMessage` (so `ItemAssigner` can assign items to newly spawned balloons) and `BalanceBalloonsMessage` (so `BalloonBalancer` settles the grid).

## Interactions

- **SlotGrid** — queried for empty slots; all placed balloons registered here
- **BalloonBalancer** — notified via `BalanceBalloonsMessage` after each spawn batch
- **ItemAssigner** — notified via `ItemCheckMessage` carrying the list of newly spawned balloon models
- **SpawnBalloonLineMessage** — triggers a single-line spawn (used by `SceneTransition` at game start and by cheats)
- **ProjectileDestroyedMessage** — triggers multi-line spawning after each turn
- **BalloonsConfiguration** — `Entries`, `GameStartedBalloonLines`, `NewProjectileBalloonLines`, `NewBalloonLinesTimeInterval`, `BalloonSpawnAnimationDurationRange`
- **DisturbanceFieldService** — stamps `Stamp()` along each spawn path using the `BalloonPath` stamp profile from `DisturbanceFieldSettings`; creates visible wakes through Puff clouds during spawn animations
