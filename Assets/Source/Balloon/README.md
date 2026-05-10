# Balloon

Represents balloons in the game — their state, appearance, spawning, and destruction.

## Folder structure

| Folder | What it owns |
|---|---|
| `Model/` | `IBalloonModel` (read-only interface), `IWriteableBalloonModel` (mutable interface), `BalloonModel` (concrete) — pure C# data with reactive properties (`Color`, `SlotIndex`, `IsStable`, `Item`). Consumers use the narrowest interface: views and messages receive `IBalloonModel`; controllers, spawners, and grid operations use `IWriteableBalloonModel` |
| `View/` | `BalloonView` — MonoBehaviour implementing `IPoolable` that binds to a model and renders color, shadow, sorting order, and pop VFX. Holds a `TweenTracker` reference for animation composition. Delegates item display to `ItemDisplayService` (in `Item/`) |
| `Controller/` | `BalloonController` — mediator that wires model to view and handles hit destruction; `BalloonBalancer` — rebalances the grid after projectile death; `BalloonNudgeHandler` — subscribes to `BalloonHitMessage` and nudges neighboring balloons with elastic push-out/return animations |
| `Spawner/` | `BalloonSpawner` — creates balloon lines at game start and after each projectile death; `BalloonSpawnerSettings` — holds the balloon prefab reference; `BalloonPoolChannel` — pool channel using VContainer `CreateChildFromPrefab` |
| `BalloonLifetimeScope` | Root scope on the balloon prefab — registers `BalloonView`; enables `CreateChildFromPrefab` instantiation for proper child scope building |

## Behaviour

A balloon knows its color, where it sits in the grid, whether it has settled into position, and whether it carries an item. When any of these change, the view updates automatically via UniRx subscriptions.

### Pooling

Balloon views are pooled via `PoolManager` / `BalloonPoolChannel`. A `CompositeDisposable` replaces `AddTo(this)` for reactive subscriptions — cleared on each `Bind()` and `OnDespawned()`. External subscribers (e.g. `BalloonController`'s hit subscription) register via `RegisterDisposeOnDespawn()` so they're cleaned up on pool return.

### Animation phases (turn-based)

Balloon animations are organized into sequential, non-overlapping phases per turn:

1. **Hit phase** — projectile bounces and pops balloons. Each pop nudges neighbors outward and back (local elastic animation). No rebalancing occurs during flight.
2. **Spawn phase** — after the projectile dies, new balloon lines spawn with a delay between each line. Spawn uses standalone `DOMove` + `DOScale` tweens (not tracked).
3. **Balance phase** — a single balance pass runs after all spawning completes. `BalloonBalancer` scans for unbalanced balloons and moves them upward along the optimal path using `DOPath(CatmullRom)`.

### Tween composition

A `TweenTracker` component on each balloon view manages position tween sequencing:

- **Nudge** calls `tracker.Replace(sequence)` — kills any existing tracked tween (previous nudge) and stores the new push-out → return sequence. Also kills standalone spawn tweens via `transform.DOKill()` so nudge cleanly takes over position. If the balloon was mid-scale-up, a parallel `DOScale` smoothly finishes scaling to full size.
- **Balance** calls `tracker.Append(tween)` — if a nudge is still playing, the balance path chains after it. If the tracker is idle, starts a new sequence.
- **Despawn** calls `tracker.Kill()` + `transform.DOKill()` — cleans up everything.

### Scale recovery

When nudge or balance interrupts a spawning balloon, `transform.DOKill()` kills the standalone scale tween mid-animation. Both nudge and balance capture the current scale beforehand and create a parallel `DOScale(Vector3.one, duration)` if the balloon hasn't reached full size — so the balloon smoothly finishes scaling alongside its movement.

## Interactions

- **SlotGrid** — balloons occupy positions in it; stores both models and views in parallel arrays. Spawner places, controller removes, balancer relocates. Systems that need the view for a model look it up via `_grid.ViewAt(slotIndex)`
- **BalloonBalancer** — moves balloons when gaps appear; fires once per turn after spawning
- **ProjectileView** — triggers destruction on collision via `BalloonHitMessage`
- **BalloonNudgeHandler** — subscribes to `BalloonHitMessage` and nudges neighboring balloon views (push-out → return) with scale recovery; uses `SlotGrid.ViewAt()` to reach views
- **ScoreController** — records each hit via `BalloonHitMessage`
- **PoolManager** — `BalloonView` instances are pooled via `BalloonPoolChannel`; pop VFX pooled via `VfxPoolChannel`
- **TweenTracker** — generic `MonoBehaviour` in `Shared/` that manages tween sequencing (append, replace, kill)
- **Item system** — balloons are one host for the game-wide item system (in `Item/`). `BalloonView` calls `ItemDisplayService.Bind()`/`Unbind()` to connect item visuals; the item system itself has no knowledge of balloons
- **IGameConfiguration** — balloon colors, spawn animation timing, balance timing, nudge distance/duration
