@page arch_slot_actor Slot Actor Abstraction

# Slot Actor Abstraction

@image html actor_hierarchy.svg "Slot Actor Interface Hierarchy"

## What this diagram shows

How `SlotGrid` stays decoupled from concrete balloon types by operating exclusively
through interface contracts. Every grid occupant is an `ISlotActor`; everything else
is an optional capability discovered at the call site via a cast.

**Core interfaces:**
- `ISlotActor` — read-only: `SlotIndex`, `Kind` (`Dynamic` or `Static`)
- `IWriteableSlotActor` — adds writable `SlotIndex`; used by grid mutators
- `IDynamicSlotActor` — redefines `SlotIndex` as reactive, adds `IsStable` reactive property; balloons implement this
- `ISlotActorView` — view contract: `transform`, `TweenTracker`, `ActorKind`, `RotationPivot`

**Capability interfaces** — optional traits discovered by casting at the subscriber's
call site:

| Interface | Meaning | Who implements |
|-----------|---------|----------------|
| `IHasColor` | Read-only reactive color | `BalloonModel` only (the bubble cluster is colorless) |
| `IPaintable` | Color is writable (paint item target) | `BalloonModel` only |
| `IHasScore` | Fixed `ScoreValue` on pop | `BalloonModel`, `ToughBalloonModel`, `UnbreakableBalloonModel` |
| `IHasScoreColor` | Score attribution strategy | All balloon models |
| `IHasNudge` | Participates in nudge animations | All balloon models |
| `IHasItemSlot` | Can host an item (extends `IHasColor`) | `BalloonModel` only |
| `IPressureMovable` | Yields its slot when shoved by a pressure cascade (`PushResponse`) | All balloon models (via `BalloonModelBase`) |
| `IHitable` | Responds to `EvaluateHit` | Balloons + Deflector + Absorber + Gatekeeper |
| `IHasDurability` | Tracks `HitsRemaining` | Most balloons + Gatekeeper |
| `IPassThrough` | Slot traversable by animation paths | `PuffObstacleModel`, `StaticActorModel` |

## Guidance

**Adding a new actor type:**
1. Create a plain C# model implementing `IWriteableSlotActor` at minimum
2. Add capability interfaces only for traits the actor actually has — do not implement
   `IHitable` if the actor has no hit response; do not implement `IHasScore` if it
   awards no points
3. Register a prefab entry in `GridActorConfiguration` (or `BalloonsConfiguration`
   for balloon types)
4. `GridActorHitController` and `ScoreController` discover capabilities via casting —
   no changes needed in those systems unless you add a new capability interface

**Capability casting is intentional:**
Subscribers that previously cast broadly to `IBalloonModel` now cast to the narrowest
interface they need (`IHasColor`, `IHasScore`, `IHasNudge`). This is not defensive
programming — it is the extension point. A new actor type can implement `IHasNudge`
and automatically receive nudge animations without touching `NudgeService`.

**`IHasDurability` vs `IHitable`:**
- `IHitable` only — actor responds to hits but has no health pool (Deflector, Absorber)
- `IHasDurability` — extends `IHitable`; actor tracks `HitsRemaining` and pops at zero
- `Piercing` damage flag on `DamageContext` bypasses `HitsRemaining` and forces `Pop`
  regardless of remaining health — used by item damage to destroy tough actors

**Read/write separation:**
Consumers receive models as `ISlotActor` (read-only). Only systems that legitimately
mutate state (spawner, balancer, hit controller) hold `IWriteableSlotActor` references.
Messages carry `ISlotActor` — subscribers downcast to `IWriteableSlotActor` only if
they need to mutate, and they do so explicitly and intentionally.

