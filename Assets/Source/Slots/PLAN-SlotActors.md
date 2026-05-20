# SlotGrid Actor Abstraction — Feature Plan

> Living plan for expanding the SlotGrid to support multiple actor types beyond balloons.
> Created from architecture discussion. Update as phases complete.

---

## Motivation

`SlotGrid` is hardcoded to `IWriteableBalloonModel` and `BalloonView`. Every method — `Place`, `Remove`, `At`, `GetNeighbors`, `CalculateWeight` — speaks directly in balloon types. This blocks introducing new slot occupants (static obstacles, spawner actors, etc.) without duplicating the grid or polluting the balloon model.

The goal is a **slot actor** abstraction so the grid operates on a common interface while each actor kind provides its own model and view.

---

## Key Design Decisions

### Mobility axis: `SlotActorKind`

The grid and balancer only care about one axis — **can this actor be relocated?**

```
SlotActorKind
├── Dynamic   — balancer can relocate, contributes weight
└── Static    — fixed in place, contributes weight, balancer skips
```

"Spawner" is **not** a kind. Spawning is a behavioral trait handled by the actor's controller. A spawner actor is either static or dynamic, and separately has spawning logic.

### Items and grid actors are separate hierarchies

Items and grid actors both modify the grid (`Place`, `Remove`, recolor), but they differ at the identity level:

| Concern | Items | Grid actors |
|---|---|---|
| **Slot ownership** | Hosted *on* a balloon — no slot of their own | Occupy their *own* slot |
| **Lifecycle** | Ephemeral — activate once, done | Persistent — live across turns |
| **Trigger** | Always `BalloonHitMessage → ItemActivator` | Varied (hit, turn, proximity, timer, etc.) |
| **Identity** | `ItemType` enum on a balloon model | Has its own model and view |

They share the `SlotGrid` API as their common operation surface — not a type hierarchy. Shared grid-operation helpers (e.g. recolor neighbors, area queries) should be extracted to `Shared/Extensions/` or `Slots/` when a second consumer appears.

---

## Interface Design

### Actor model interfaces (new, in `Slots/`)

```csharp
// Read-only — the minimum contract the grid needs from any occupant
public interface ISlotActor
{
    IReadOnlyReactiveProperty<Vector2Int> SlotIndex { get; }
    IReadOnlyReactiveProperty<bool> IsStable { get; }
    SlotActorKind Kind { get; }
}

// Writable counterpart — follows the existing read/write separation pattern
public interface IWriteableSlotActor : ISlotActor
{
    new ReactiveProperty<Vector2Int> SlotIndex { get; }
    new ReactiveProperty<bool> IsStable { get; }
}
```

**`Color` and `ScoreValue` are intentionally absent from `ISlotActor`.** Not all actors have a color or award score (e.g. a structural obstacle, a spawner block). Instead, these are expressed as **optional capability interfaces** that any actor model can adopt:

### Capability interfaces (new, in `Slots/`)

```csharp
public interface IHasColor
{
    IReadOnlyReactiveProperty<string> Color { get; }
}

public interface IHasWriteableColor : IHasColor
{
    new ReactiveProperty<string> Color { get; }
}

public interface IHasScore
{
    int ScoreValue { get; }
}

public interface IHasNudge
{
    NudgeOverride[] NudgeOverrides { get; }
}
```

Paintability is expressed purely through the type system: actors that implement `IHasWriteableColor` are paintable; actors that don't, aren't. No runtime flag needed.

This requires balloon variants that differ in paintability to use **separate model classes**:

- `BalloonModel : IWriteableBalloonModel, IHasWriteableColor` — normal balloons, paintable
- `ToughBalloonModel : IWriteableBalloonModel` — tough balloons, no `IHasWriteableColor`, not paintable

`IWriteableBalloonModel` itself does **not** inherit `IHasWriteableColor` — the concrete class decides. Controllers that need writable color at spawn still access it through `IWriteableBalloonModel.Color` (which is `ReactiveProperty<string>` regardless). `IsPaintable` is removed from `IBalloonModel`.

`IHasNudge` follows the same opt-in pattern. Actors without it are inert to nudge forces — not nudged when hit, not nudged as neighbors. Actors with `IHasNudge` but empty/null `NudgeOverrides` use global nudge defaults from config (existing behavior).

**Consumer migration (Phase 3):** `PaintItemHandler` migrates from `IWriteableBalloonModel` to `IHasWriteableColor`:

```csharp
var actor = _grid.At(idx);
if (actor is IHasWriteableColor colorable
    && colorable.Color.Value != paintColor)
{
    paintTargets[i] = colorable;
}
```

Paint naturally works on any future colored actor that implements `IHasWriteableColor`. No type checks, no runtime flags.

### Actor view interface (new, in `Slots/`)

```csharp
public interface ISlotActorView
{
    Transform transform { get; }
    TweenTracker TweenTracker { get; }
    SlotActorKind ActorKind { get; }
}
```

### Actor hit message (replaces `BalloonHitMessage`, in `Shared/Messages/`)

```csharp
public readonly struct ActorHitMessage
{
    public readonly ISlotActor Actor;
    public readonly Vector3 WorldPosition;
    public readonly Vector3 ProjectileDirection;
    public readonly int Damage;

    public ActorHitMessage(
        ISlotActor actor,
        Vector3 worldPosition,
        Vector3 projectileDirection,
        int damage = 1) { ... }
}
```

**`BalloonHitMessage` is removed.** `ActorHitMessage` is the single hit message for all actor types. Subscribers that need balloon-specific data downcast at their call site — the same pattern applied to `SlotGrid.At()` consumers:

```csharp
_hitSubscriber.Subscribe(msg =>
{
    if (msg.Actor is IBalloonModel balloon)
    {
        OnBalloonHit(balloon, msg);
    }
});
```

**Current subscribers migration:**

| Subscriber | Needs balloon-specific data? | Migration |
|---|---|---|
| `BalloonController` | Yes — `EvaluateHit`, `HitsRemaining`, item type | Filter: `msg.Actor is IBalloonModel` |
| `ItemActivator` | Yes — `Item.Value`, `IBalloonModel` for `Setup()` | Filter: `msg.Actor is IBalloonModel` |
| `ScoreController` | Needs color + score | Migrate to `ActorHitMessage`; filter `msg.Actor is IHasColor and IHasScore` — any scored colored actor participates in streaks automatically |
| `NudgeService` | Needs nudge overrides | Filter: `msg.Actor is IHasNudge` — any nudgeable actor participates; neighbors filtered via `IHasNudge` too |
| `BalloonSpawner` | No — only cares that something was hit | No filter needed — works with all actor types immediately |

### Existing types conform — no new functionality, just widen

```
IBalloonModel : ISlotActor, IHasColor, IHasScore, IHasNudge
    └── existing members satisfy contracts:
        Color → IHasColor
        ScoreValue → IHasScore
        NudgeOverrides → IHasNudge
        SlotIndex, IsStable → ISlotActor
    └── adds HitsRemaining, TypeName, Item, CanHoldItem, EvaluateHit()
    └── IsPaintable removed

IWriteableBalloonModel : IWriteableSlotActor, IBalloonModel
    └── does NOT inherit IHasWriteableColor — concrete class decides

BalloonModel : IWriteableBalloonModel, IHasWriteableColor
    └── normal paintable balloons
    └── IsPaintable field removed — paintability expressed by implementing IHasWriteableColor

ToughBalloonModel : IWriteableBalloonModel
    └── tough non-paintable balloons — does NOT implement IHasWriteableColor
    └── new class, extracted from BalloonModel

BalloonView : MonoBehaviour, ISlotActorView
    └── already has Transform and TweenTracker; adds ActorKind => SlotActorKind.Dynamic
```

---

## Phases

### Phase 1 — Define actor interfaces

**New files in `Assets/Source/Slots/`:**

| File | Purpose |
|---|---|
| `ISlotActor.cs` | Read-only interface for any grid occupant |
| `IWriteableSlotActor.cs` | Writable counterpart |
| `ISlotActorView.cs` | View-side interface |
| `SlotActorKind.cs` | Enum: `Dynamic`, `Static` |
| `IHasColor.cs` | Read-only color capability |
| `IHasWriteableColor.cs` | Writable color capability + `IsPaintable` |
| `IHasScore.cs` | Score capability |
| `IHasNudge.cs` | Nudge capability — `NudgeOverrides` |

**New file in `Assets/Source/Shared/Messages/`:**

| File | Purpose |
|---|---|
| `ActorHitMessage.cs` | Grid-level hit message carrying `ISlotActor` — replaces `BalloonHitMessage` |

**Modify existing:**

| File | Change |
|---|---|
| `IBalloonModel` | Add `: ISlotActor, IHasColor, IHasScore, IHasNudge` — existing `Color`, `ScoreValue`, `NudgeOverrides`, `SlotIndex`, `IsStable` satisfy the contracts; remove `IsPaintable` |
| `IWriteableBalloonModel` | Add `: IWriteableSlotActor` — does NOT inherit `IHasWriteableColor` |
| `BalloonModel` | Add `: IHasWriteableColor`; remove `IsPaintable` field |
| `ToughBalloonModel` | **New class** — extracted from `BalloonModel`, does NOT implement `IHasWriteableColor` |
| `BalloonView` | Add `: ISlotActorView`, implement `ActorKind` |
| `GameLifetimeScope` | Replace `BalloonHitMessage` broker with `ActorHitMessage` |

### Phase 2 — Refactor `SlotGrid` to use actor interfaces

**Change internal arrays:**

- `IWriteableBalloonModel[,] _slots` → `IWriteableSlotActor[,] _slots`
- `BalloonView[,] _views` → `ISlotActorView[,] _views`

**Method signature changes:**

| Method | Before | After |
|---|---|---|
| `Place` | `(IWriteableBalloonModel, BalloonView, Vector2Int)` | `(IWriteableSlotActor, ISlotActorView, Vector2Int)` |
| `At` | returns `IWriteableBalloonModel` | returns `IWriteableSlotActor` |
| `ViewAt` | returns `BalloonView` | returns `ISlotActorView` |
| `GetNeighbors` | returns `List<IWriteableBalloonModel>` | returns `List<IWriteableSlotActor>` |

**New convenience methods:**

```csharp
T ActorAt<T>(Vector2Int index) where T : class, IWriteableSlotActor;
T ActorViewAt<T>(Vector2Int index) where T : class, ISlotActorView;
bool IsKind(int col, int row, SlotActorKind kind);
```

### Phase 3 — Update consumers

Each consumer that currently uses `IWriteableBalloonModel` / `BalloonView` from the grid downcasts to the specific type at its call site. They already know they're operating on balloons.

**Affected consumers:**

| Consumer | Change |
|---|---|
| `BalloonBalancer` | Skip actors where `Kind == Static`; downcast to `IWriteableBalloonModel` for balloon-specific fields |
| `BalloonSpawner` | Migrate to `ActorHitMessage` — no downcast needed, works with all actor types |
| `BalloonController` | Migrate to `ActorHitMessage`; filter `msg.Actor is IBalloonModel` |
| `ItemActivator` | Migrate to `ActorHitMessage`; filter `msg.Actor is IBalloonModel` |
| `ScoreController` | Migrate to `ActorHitMessage`; filter `msg.Actor is IHasColor and IHasScore` for color/streak |
| `NudgeService` | Migrate to `ActorHitMessage`; filter `msg.Actor is IHasNudge` for nudge overrides; filter neighbors via `IHasNudge` too |
| `PaintItemHandler` | Migrate from `IWriteableBalloonModel` to `IHasWriteableColor` — `is IHasWriteableColor` is the only check needed; no runtime flag |
| `LightningItemHandler` | Same — downcast for balloon-specific queries |
| `BalloonRemoverCheat` | Downcast `SlotGrid.At()` result; publish `ActorHitMessage` instead of `BalloonHitMessage` |
| `SlotGridView` | No change — gizmo drawing is slot-based |

**Balancer-specific changes:**

| Concern | How |
|---|---|
| Static actors never relocate | Balance loop: `if (actor.Kind == SlotActorKind.Static) continue;` |
| Static actors count as support | `CalculateWeight` / `IsEmpty` unchanged — static actors occupy slots like any other |
| Static actors block movement targets | `OptimalNextEmptySlot` already skips non-empty slots |

### Phase 4 — Update tests

`SlotGridTests` creates mock `IWriteableSlotActor` instead of `BalloonModel` for grid-level tests. Balloon-specific tests keep using `BalloonModel`.

### Phase 5 — Update documentation

- `Slots/README.md` — describe actor abstraction, `SlotActorKind`, interface hierarchy
- `Balloon/README.md` — note that `IBalloonModel` extends `ISlotActor`
- `ARCHITECTURE.md` — add actor interfaces to the system map

### Phase 6 (future iteration) — Static actor implementation

| File | Layer | Purpose |
|---|---|---|
| `IStaticActorModel : ISlotActor` | Model | Read-only — may add `Durability`, `BlocksProjectile`, etc. |
| `IWriteableStaticActorModel : IWriteableSlotActor, IStaticActorModel` | Model | Writable counterpart |
| `StaticActorModel` | Model | Concrete implementation |
| `StaticActorView : MonoBehaviour, ISlotActorView` | View | Renders the obstacle, pooling hooks |
| `StaticActorSpawner : IStartable` | Controller | Places static actors via `SlotGrid.Place` |
| New config SO | Configuration | Per-type tuning for static actors |

### Phase 7 (future iteration) — Spawner actor behavior

Actors that place/remove/move/recolor other actors in the grid. The spawning/modifying behavior is pure controller composition:

```
SpawnerActor (Kind = Static or Dynamic)
├── Model:  implements IWriteableSlotActor (+ spawner-specific state)
├── View:   implements ISlotActorView (renders block, spawn animations)
└── Controller: IStartable
       subscribes to game triggers (turn events, neighbor pops, etc.)
       queries SlotGrid for valid targets (HexNeighborIndices + IsEmpty)
       calls SlotGrid.Place/Remove/At to modify the grid
```

The grid doesn't know about spawning — it only provides the `Place`/`Remove`/`At` API that these controllers call.

### Future consideration — Actor identity

The grid currently has no way to query *what type* of actor occupies a slot without downcasting. Capability interfaces (`IHasColor`, `IHasScore`, `IHasNudge`) cover behavioral queries, but identity queries ("is this an obstacle? a spawner?") may be needed for future systems (e.g. level design tools, analytics, serialization). Options when the need arises:

- `string ActorId` on `ISlotActor` — flexible, config-driven, but stringly-typed
- `SlotActorType` enum on `ISlotActor` — compile-time safe, coarse-grained
- Additional capability interfaces for the specific trait being queried

Deferred until a concrete consumer surfaces.

---

## What NOT to change

- **No new DI registrations** until static actors exist (Phase 6)
- **No config SO changes** until static actors need tuning
- **`IBalloonModel` keeps all its fields** — only the shared subset (`SlotIndex`, `IsStable`, `Kind`) lifts to `ISlotActor`. `Color` and `ScoreValue` lift to capability interfaces (`IHasColor`, `IHasScore`), not `ISlotActor` itself.
- **`BalloonHitMessage` is replaced by `ActorHitMessage`** — single hit message for all actor types. Balloon-specific subscribers downcast `msg.Actor` to `IBalloonModel` at their call site.
- **Item system stays separate** — items are passengers on actors, not actors themselves
- **No `StartCoroutine`** — all async via UniTask

---

## Migration Path (non-breaking)

1. Introduce interfaces (`ISlotActor`, `IWriteableSlotActor`, `ISlotActorView`, `SlotActorKind`)
2. Introduce `ActorHitMessage`, replace `BalloonHitMessage` broker in `GameLifetimeScope`
3. Make existing balloon types conform (inheritance, `ActorKind` property)
4. Migrate all `BalloonHitMessage` publishers and subscribers to `ActorHitMessage`
5. Widen `SlotGrid` arrays and method signatures
6. Update each consumer to downcast at its call site
7. Guard balancer against static actors
8. Delete `BalloonHitMessage.cs`
9. Update tests
10. Update READMEs

Each step compiles and passes tests independently.

---

## Current State

| Phase | Status |
|---|---|
| 1 — Define actor interfaces | Not started |
| 2 — Refactor SlotGrid | Not started |
| 3 — Update consumers | Not started |
| 4 — Update tests | Not started |
| 5 — Update documentation | Not started |
| 6 — Static actor implementation | Future |
| 7 — Spawner actor behavior | Future |

