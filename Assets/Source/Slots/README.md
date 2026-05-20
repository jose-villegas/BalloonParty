# Slots

The grid that holds all actors in play. Actors are anything that can occupy a slot — balloons today, static obstacles and spawners in the future.

## Actor abstraction

Every grid occupant is an `ISlotActor`. The grid never speaks directly in balloon types.

### Core interfaces

| Interface | Purpose |
|---|---|
| `ISlotActor` | Read-only grid contract — `SlotIndex`, `IsStable`, `Kind` |
| `IWriteableSlotActor` | Writable counterpart — adds `ReactiveProperty` versions of `SlotIndex` and `IsStable` |
| `ISlotActorView` | View contract — `transform`, `TweenTracker`, `ActorKind` |
| `SlotActorKind` | Enum — `Dynamic` (balancer can relocate) or `Static` (fixed) |

### Capability interfaces

Optional traits actors can advertise to consumers:

| Interface | Meaning |
|---|---|
| `IHasColor` | Actor has a read-only color |
| `IHasWriteableColor` | Actor has a writable color — the type system flag for paintability |
| `IHasScore` | Actor awards score when destroyed |
| `IHasNudge` | Actor participates in the nudge force system |
| `IPassThrough` | Actor's slot can be crossed by animation paths (spawn entry, balance moves). Actors that do NOT implement this block traversal; Phase 9 introduces rerouting for blocking actors. |

Paintability is expressed purely through types: a `BalloonModel` implements `IHasWriteableColor`; a `ToughBalloonModel` does not — no runtime flag needed.

## Contents

| File | What it does |
|---|---|
| `SlotGrid` | Core data structure — parallel 2D arrays of `IWriteableSlotActor` and `ISlotActorView`; `Place`, `Remove`, `At`, `ViewAt`, `ActorAt<T>`, `ActorViewAt<T>`, `IsEmpty`, `IsKind`, `IsUnbalanced`, `OptimalNextEmptySlot`, `BottomEmptySlotPerColumn`, `HexNeighborIndices` (static), `GetNeighbors`, `IndexToWorldPosition` |
| `SlotGridChangedEvent` | Struct fired on every `Place` or `Remove` — carries the affected index and change type |
| `SlotGridView` | MonoBehaviour — draws gizmo spheres in `OnDrawGizmos` to visualise occupied/empty slots in the Editor |
| `ISlotActor.cs` | Read-only actor interface |
| `IWriteableSlotActor.cs` | Writable actor interface |
| `ISlotActorView.cs` | View-side actor interface |
| `SlotActorKind.cs` | Mobility enum |
| `StaticActorModel.cs` | Minimal `IWriteableSlotActor`, `Kind = Static` — no color, no score, no durability |
| `StaticActorView.cs` | Placeholder `ISlotActorView` MonoBehaviour — pooled, no animations |
| `StaticActorPoolChannel.cs` | `InjectingPoolChannel<StaticActorView>` |
| `StaticActorSettings.cs` | Lightweight prefab carrier injected into `StaticActorSpawner` |
| `StaticActorSpawner.cs` | `IStartable` — picks N random bottom-empty slots and places static actors at game start |
| `IHasColor.cs` / `IHasWriteableColor.cs` | Color capability |
| `IHasScore.cs` | Score capability |
| `IHasNudge.cs` | Nudge override capability |

## How it works

The grid is a two-dimensional space of slots arranged in a staggered pattern (odd rows offset by half a column). Each slot is either empty or occupied by an actor model and its view. The grid knows how to convert a slot coordinate into a world position, which slots are unbalanced (missing support from below), and what the best empty slot is for an actor to move into.

`OptimalNextEmptySlot` uses a recursive weight algorithm to decide between two candidate slots (directly above and diagonally above). The weight of a candidate = count of occupied slots in the tree above it. Higher weight = more support = preferred. On tie, the diagonal candidate wins (via `>=` comparison).

`Place` guards against double-occupation — if a slot is already occupied, the placement is rejected with an exception.

`BottomEmptySlotPerColumn` returns the lowest empty row index per column — used by `BalloonSpawner` when finding spawn targets for a new line.

## Interactions

- **BalloonSpawner** — calls `Place` for each new balloon; skips already-occupied slots in `PopulateInitialGrid`
- **StaticActorSpawner** — calls `Place` for each static actor at game start using `BottomEmptySlotPerColumn`
- **BalloonController** — calls `Remove` when a balloon is popped; subscribes to `ActorHitMessage`
- **BalloonBalancer** — reads occupancy to find gaps; skips `Static` actors; calls `Remove` + `Place` to relocate dynamic actors; uses `ViewAt` to reach views for animation
- **NudgeService** — uses `GetNeighbors` and `IndexToWorldPosition` to direct nudge animations; filters by `IHasNudge`
- **PaintItemHandler** — uses `HexNeighborIndices`; casts `At()` result to `IHasWriteableColor` for painting
- **IGameConfiguration** — provides `SlotsSize`, `SlotSeparation`, `SlotsOffset` for grid construction and position calculations
