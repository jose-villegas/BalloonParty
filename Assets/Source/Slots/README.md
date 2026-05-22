# Slots

The grid that holds all actors in play. Actors are anything that can occupy a slot — balloons today, static obstacles and spawners in the future.

## Actor abstraction

Every grid occupant is an `ISlotActor`. The grid never speaks directly in balloon types.

### Core interfaces

| Interface | Purpose |
|---|---|
| `ISlotActor` | Read-only grid contract — `SlotIndex : Vector2Int`, `Kind : SlotActorKind` |
| `IWriteableSlotActor` | Writable counterpart — adds `new Vector2Int SlotIndex { get; set; }` |
| `IDynamicSlotActor` | Extends `ISlotActor` — adds `IReadOnlyReactiveProperty<bool> IsStable`. Actors that can move or be in a transitioning state implement this. Static actors do NOT. |
| `IWriteableDynamicSlotActor` | Extends both `IDynamicSlotActor` and `IWriteableSlotActor` — adds writable `ReactiveProperty<bool> IsStable` |
| `ISlotActorView` | View contract — `transform`, `TweenTracker`, `ActorKind` |
| `SlotActorKind` | Enum — `Dynamic` (balancer can relocate, contributes weight) or `Static` (fixed, contributes weight, balancer skips) |

### Capability interfaces

Optional traits actors can advertise to consumers:

| Interface | Meaning |
|---|---|
| `IHasColor` | Actor has a read-only color |
| `IHasWriteableColor` | Actor has a writable color — the type system flag for paintability |
| `IHasScore` | Actor awards score when destroyed |
| `IHasNudge` | Actor participates in the nudge force system |
| `IHitable` | Actor participates in the hit system — `EvaluateHit(int damage)` returns a `HitOutcome` and is responsible for mutating any internal state (e.g. decrementing health) |
| `IHasDurability` | Extends `IHitable` — actor also tracks `HitsRemaining`. Removal is determined by `HitsRemaining.Value <= 0` after `EvaluateHit` returns |
| `IPassThrough` | Actor's slot can be crossed by animation paths (spawn entry, balance moves). Actors that do NOT implement this block traversal; rerouting is deferred to a future phase. |

Paintability is expressed purely through types: a `BalloonModel` implements `IHasWriteableColor`; a `ToughBalloonModel` does not — no runtime flag needed.

`IHitable` vs `IHasDurability`:

| Actor | Implements | `EvaluateHit` behaviour |
|---|---|---|
| `BalloonModel` (soft) | `IHasDurability` | `PassThrough` on survival, `Pop` on death; decrements `HitsRemaining` |
| `ToughBalloonModel` | `IHasDurability` | `Deflect` on survival, `Pop` on death; decrements `HitsRemaining` |
| Unbreakable balloon *(Phase 7.5)* | `IHitable` only | Always `Deflect`; no `HitsRemaining` |
| Absorbing wall *(future)* | `IHitable` only | Always `Absorb`; projectile is killed |
| `StaticActorModel` | neither | No collider — not part of the hit pipeline |

## Contents

| File / Folder | What it does |
|---|---|
| `Grid/` | `SlotGrid`, `SlotGridChangedEvent`, `SlotGridView` — core grid data structure (namespace `BalloonParty.Slots.Grid`) |
| `Actor/` | Core actor interfaces, identity enum, and static actor implementation — `ISlotActor`, `IWriteableSlotActor`, `IDynamicSlotActor`, `IWriteableDynamicSlotActor`, `ISlotActorView`, `SlotActorKind`, `StaticActorModel`, `StaticActorView`, `StaticActorPoolChannel`, `StaticActorSettings`, `StaticActorSpawner` (namespace `BalloonParty.Slots.Actor`) |
| `Capabilities/` | Optional capability interfaces — `IHasColor`, `IHasWriteableColor`, `IHasScore`, `IHasNudge`, `IHasItemSlot`, `IHitable`, `IHasDurability`, `IPassThrough`, `HitOutcome` (namespace `BalloonParty.Slots.Capabilities`) |
| `Spawner/` | Spawner coordination — `IGridSpawner`, `SpawnStage`, `GridSpawnerCoordinator` (namespace `BalloonParty.Slots.Spawner`) |

## How it works

The grid is a two-dimensional space of slots arranged in a staggered pattern (odd rows offset by half a column). Each slot is either empty or occupied by an actor model and its view. The grid knows how to convert a slot coordinate into a world position, which slots are unbalanced (missing support from below), and what the best empty slot is for an actor to move into.

`OptimalNextEmptySlot` uses a recursive weight algorithm to decide between two candidate slots (directly above and diagonally above). The weight of a candidate = count of occupied slots in the tree above it. Higher weight = more support = preferred. On tie, the diagonal candidate wins (via `>=` comparison).

`Place` guards against double-occupation — if a slot is already occupied, the placement is rejected with an exception.

`BottomEmptySlotPerColumn` returns the lowest empty row index per column — used by `BalloonSpawner` when finding spawn targets for a new line.

`AllEmptySlots` yields every empty slot across the full grid — used by `StaticActorSpawner`.

`IsTraversable(col, row)` returns true if the slot is empty or the occupant implements `IPassThrough` — used by `ComputePath` to build spawn animation waypoints.

`ComputePath(source, target)` returns world-space waypoints along the straight-line grid path between two slot indices. Either endpoint may be outside grid bounds. Non-traversable in-bounds slots emit a warning; rerouting is deferred.

## Spawner Coordination

`GridSpawnerCoordinator` owns the sequencing of all grid population. Individual spawners (`StaticActorSpawner`, `BalloonSpawner`) implement `IGridSpawner` and declare their priority via `SpawnStage`. The coordinator:

1. **Waits** on `IReadyGate` (a `NavigationReadyGate(Game)`) — nothing spawns until the game scene is fully active.
2. **Groups** all registered spawners by `SpawnStage`, sorted ascending.
3. **Runs** each group with `UniTask.WhenAll` (parallel within a stage) before advancing to the next stage.

```
NavigationReadyGate opens
        │
        ▼
SpawnStage.StaticActors (0)  ──→  StaticActorSpawner.SpawnAsync
        │
        ▼
SpawnStage.DynamicActors (50) ──→  (future GridSpawner)
        │
        ▼
SpawnStage.BalloonActors (100) ──→  BalloonSpawner.SpawnAsync
```

Spawners are injected as `IEnumerable<IGridSpawner>` via VContainer's collection injection — each registered with `.As<IGridSpawner>()` in `GameLifetimeScope`.

`StaticActorSpawner.Start()` registers the pool immediately (synchronous, runs at scene start before the gate opens). `BalloonSpawner.Start()` starts pre-warming pools asynchronously and stores the task; `SpawnAsync` awaits it before populating the grid — so pre-warm and navigation wait overlap rather than serialize.

## Interactions

- **BalloonSpawner** — calls `Place` for each new balloon; uses `ComputePath` for spawn animation waypoints; skips already-occupied slots in `PopulateInitialGrid`
- **StaticActorSpawner** — calls `Place` for each static actor at game start using `AllEmptySlots`
- **BalloonController** — calls `Remove` when a balloon is popped; subscribes to `ActorHitMessage`
- **BalloonBalancer** — reads occupancy to find gaps; skips `Static` actors (or actors that are not `IDynamicSlotActor`); calls `Remove` + `Place` to relocate dynamic actors; uses `ViewAt` to reach views for animation
- **NudgeService** — uses `GetNeighbors` and `IndexToWorldPosition` to direct nudge animations; filters by `IHasNudge`
- **PaintItemHandler** — uses `HexNeighborIndices`; casts `At()` result to `IHasWriteableColor` for painting
- **IGameConfiguration** — provides `SlotsSize`, `SlotSeparation`, `SlotsOffset` for grid construction and position calculations
