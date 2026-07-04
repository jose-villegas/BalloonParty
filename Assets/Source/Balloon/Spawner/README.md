# Balloon/Spawner

Responsible for introducing balloons into the grid — both at game start and during play.

## Contents

| File | What it does |
|---|---|
| `BalloonSpawner` | `IStartable` + `IGridSpawner` — creates and places balloons, manages per-type pool registration and active-count caps, and decides where each line's balloon goes. `SpawnPriority` is `SpawnStage.BalloonActors` (100). `Start()` registers pools and kicks off pre-warm asynchronously; `SpawnAsync()` awaits the pre-warm then populates the initial grid |
| `RejectedBalloonEffect` | The feedback when a balloon can't be placed: a pooled would-be balloon rises into the **overflow rows below the grid** and lingers as a visible pile. Draining is **heart-driven** — when a balloon is ready it publishes `OverflowHeartRequestedMessage` **and `SpawnBlockedMessage` right behind it**, so the hit point and camera shake land the moment the heart launches from the UI; the balloon itself only **pops when that heart lands** (`OnHeartArrived`) — the landing is purely the visual burst. Requests are **serialized** (one per interval, front-most first) so hearts drain in sequence. `ITickable` runs the pile as a **per-column queue** — a balloon's target row is its live index, so when one pops the balloons below slide up (compact) to fill the gap; the in-flight heart **homes on the balloon's live position** (`TryGetLivePosition`) so it still lands on it as it compacts. `IRunResettable` returns any transients on restart (they have no grid slot, so the board-clear broadcast can't reach them). Exposes `IPendingHealthCharges.PendingCharges` (queued, unlaunched balloons — each will unconditionally cost one HP), which `LossForecast` reads to know a loss is certain at reject-queue time. Engages a `PauseService.Pause(Overflow)` thrower-lock for the duration of the pile. Timing/motion (linger, request interval, stagger, ease, arrival radius) come from `Configuration/OverflowSettings` (`IOverflowSettings`) |
| `BalloonPoolChannel` | `InjectingPoolChannel<BalloonView>` — creates balloon instances via `IObjectResolver.Instantiate()`, injecting all `[Inject]` fields from the parent container without creating child scopes |

## Behaviour

`BalloonSpawner` implements `IGridSpawner` with `SpawnPriority = SpawnStage.BalloonActors`. `GridSpawnerCoordinator` calls `SpawnAsync()` after the Navigation gate opens and all lower-priority spawners (e.g. `StaticActorSpawner`) have completed. `SpawnAsync` awaits the pre-warm task started in `Start()` — so pool pre-warming and the navigation/static-actor wait overlap rather than serialize.

At game start `BalloonSpawner` spawns the initial grid rows from `BalloonsConfiguration.GameStartedBalloonLines`. For each empty slot it picks a balloon type via weighted random selection from `BalloonsConfiguration.Entries`, respecting each entry's `MaxCount` cap. All model configuration — `TypeName`, `ScoreValue`, `HitsToPop`, and `NudgeOverrides` — is bundled into a `BalloonModelConfig` struct and passed to the model constructor. `BalloonModelFactory.Create` (in `Balloon/Model/`) picks the model class from `entry.BalloonType` — shared with `RejectedBalloonEffect` so the switch lives in one place:

| `BalloonType` | Model class | Notes |
|---|---|---|
| `Simple` | `BalloonModel` | Paintable, item-capable |
| `BubbleCluster` | `BubbleClusterModel` | Not paintable, not item-capable; `HitsToPop = 5` maps hits → bubble count via `SoapBubbleClusterVariant`; scores scatter across random palette colors |
| `Tough` | `ToughBalloonModel` | Not paintable, not item-capable |
| `Unbreakable` | `UnbreakableBalloonModel` | No `HitsRemaining`; only Piercing destroys |

The variant's `Initialize(model)` handles color (for colorable types). Spawn animation follows the path returned by `SlotGrid.ComputePath`, driving a `DOPath(CatmullRom)` from the entry-row offset down to the target slot.

After each projectile death (starting from the second turn) the spawner fires `BalloonsConfiguration.NewProjectileBalloonLines` new lines with `BalloonsConfiguration.NewBalloonLinesTimeInterval` delay between lines. Multi-line spawning uses `async UniTaskVoid` with a `CancellationTokenSource`, avoiding any coroutine runner dependency. The first projectile death is skipped because game-start lines are seeded separately — `SceneTransition` publishes `SpawnBalloonLineMessage` on scene load.

After the final line in each spawn batch, the spawner publishes `ItemCheckMessage` (so `ItemAssigner` can assign items to newly spawned balloons) and `BalanceBalloonsMessage` (so `BalloonBalancer` settles the grid).

### Placement under pressure (turn spawns)

A blocked column isn't lost immediately. `TrySpawnForColumn` resolves a slot in order, scanning columns nearest-first (`TryNearestColumn`):

1. **Own column** — `FindFirstReachableEmptyRow` (the topmost slot reachable by rising from the entry).
2. **Re-home** — the nearest *other* column the balloon can rise straight into (`ResolveOpenEntry`); a line may over-fill a column.
3. **Pressure-open** — the nearest column `BalloonBalancer.TryRelievePressure` can shove open by pulling a balloon into a gap *anywhere* on the board (`ResolvePressureOpen`), including interior pockets no entry reaches directly.
4. **Reject** — only when nothing frees a slot: `RejectedBalloonEffect.Play` queues the would-be balloon into the overflow pile below the grid; it lingers, then a heart trail is sent to it — the hit point is charged at the launch, and the balloon pops when the heart lands.

The initial grid fill never saturates, so it skips steps 2–4 (passes `allowReject: false`).

## Interactions

- **SlotGrid** — queried for empty slots; all placed balloons registered here
- **BalloonBalancer** — notified via `BalanceBalloonsMessage` after each spawn batch
- **ItemAssigner** — notified via `ItemCheckMessage` carrying the list of newly spawned balloon models
- **SpawnBalloonLineMessage** — triggers a single-line spawn (used by `SceneTransition` at game start and by cheats)
- **ProjectileDestroyedMessage** — triggers multi-line spawning after each turn
- **BalloonsConfiguration** — `Entries`, `GameStartedBalloonLines`, `NewProjectileBalloonLines`, `NewBalloonLinesTimeInterval`, `BalloonSpawnAnimationDurationRange`
- **DisturbanceFieldService** — stamps `Stamp()` along each spawn path using the `BalloonPath` stamp profile from `DisturbanceFieldSettings`; creates visible wakes through Puff clouds during spawn animations
