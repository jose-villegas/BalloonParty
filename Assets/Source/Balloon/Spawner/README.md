# Balloon/Spawner

Responsible for introducing balloons into the grid — both at game start and during play.

## Contents

| File | What it does |
|---|---|
| `BalloonSpawner` | `IStartable` — creates and places balloons, manages per-type pool registration and active-count caps |
| `BalloonPoolChannel` | `PoolChannel<BalloonView>` — creates balloon instances via `parentScope.CreateChildFromPrefab()` so each balloon's `BalloonLifetimeScope` resolves its dependencies correctly |

## Behaviour

At game start `BalloonSpawner` spawns the initial grid rows from `BalloonsConfiguration.GameStartedBalloonLines`. For each empty slot it picks a balloon type via weighted random selection from `BalloonsConfiguration.Entries`, respecting each entry's `MaxCount` cap. After getting a view from the pool it calls `IBalloonTypeConfiguration.Initialize(model)` on the prefab root — this sets the balloon's type, hit count, and color. `model.CanHoldItem` is then set from the entry's `CanHoldItem` flag — this is the authoritative source, separate from the type component.

After each projectile death (starting from the second turn) the spawner fires `BalloonsConfiguration.NewProjectileBalloonLines` new lines with `BalloonsConfiguration.NewBalloonLinesTimeInterval` delay between lines. Multi-line spawning uses `async UniTaskVoid` with a `CancellationTokenSource`, avoiding any coroutine runner dependency. The first projectile death is skipped because game-start lines are seeded separately — `SceneTransition` publishes `SpawnBalloonLineMessage` on scene load.

After the final line in each spawn batch, the spawner publishes `ItemCheckMessage` (so `ItemAssigner` can assign items to newly spawned balloons) and `BalanceBalloonsMessage` (so `BalloonBalancer` settles the grid).

## Interactions

- **SlotGrid** — queried for empty slots; all placed balloons registered here
- **BalloonBalancer** — notified via `BalanceBalloonsMessage` after each spawn batch
- **ItemAssigner** — notified via `ItemCheckMessage` carrying the list of newly spawned balloon models
- **SpawnBalloonLineMessage** — triggers a single-line spawn (used by `SceneTransition` at game start and by cheats)
- **ProjectileDestroyedMessage** — triggers multi-line spawning after each turn
- **BalloonsConfiguration** — `Entries`, `GameStartedBalloonLines`, `NewProjectileBalloonLines`, `NewBalloonLinesTimeInterval`, `BalloonSpawnAnimationDurationRange`
