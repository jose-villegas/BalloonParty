# BalloonParty — Architecture Diagram

> Single-page map of systems, data flow, and scope hierarchy.
> For style rules see `Assets/Source/README.md`. For per-feature details see each folder's `README.md`.

---

## MVC Pattern

@image html mvc_architecture.svg "Model–View–Controller with MessagePipe"

---

## Scope Hierarchy

@image html scope_hierarchy.svg "VContainer Scope Hierarchy"

---

## Turn Pipeline

@image html turn_pipeline.svg "Turn Pipeline — Hit → Balance → Spawn → Post-Balance"

---

## Balance Flow

@image html balance_flow.svg "Balance Algorithm — Per-Actor Transit Tracking"

---

## System Map

@image html spawner_coordination.svg "Spawner Coordination — Staged GridSpawner Pipeline"

---

## Message Flow

@image html message_flow.svg "MessagePipe Pub/Sub Flow"

---

## Score & Cinematic Pipeline

@image html cinematic_flow.svg "Score & Cinematic Pipeline"

---

## Trail Utility Composition

@image html trail_composition.svg "Trail Utility Composition"

---

## Item Activation

@image html item_activation.svg "Item Activation Pipeline"

---

## Static State

@image html static_state.svg "Static State — Navigation & Cinematic"


## Slot Actor Abstraction

`SlotGrid` owns two parallel 2D arrays — `IWriteableSlotActor[,]` (model side) and `ISlotActorView[,]` (view side). Every grid occupant is referred to through these interfaces rather than balloon-specific types.

@image html actor_hierarchy.svg "Slot Actor Interface Hierarchy"

**Capability interfaces** — optional traits subscribers cast for at their call site:

| Interface | Meaning | Implemented by |
|---|---|---|
| `IHasColor` | Read-only color | `IBalloonModel` (all balloon types) |
| `IHasWriteableColor` | Paintable — writable color | `BalloonModel` only (not `ToughBalloonModel`) |
| `IHasScore` | Awards score on pop | `IBalloonModel` (all balloon types) |
| `IHasNudge` | Participates in nudge system | `IBalloonModel` (all balloon types) |
| `IPassThrough` | Slot can be crossed by animation paths | `StaticActorModel` |

Subscribers that previously cast to `IBalloonModel` now cast to the narrowest capability interface needed (`IHasColor`, `IHasScore`, `IHasNudge`). `BalloonController` is the only subscriber that still casts to `IBalloonModel` — it needs `EvaluateHit()` which is balloon-specific. See `Slots/README.md` for full detail.

---

## Folder → Namespace Mapping

| Folder | Namespace | Layer |
|--------|-----------|-------|
| `Balloon/` | `BalloonParty.Balloon.{Model,View,Controller}` | MVC |
| `Projectile/` | `BalloonParty.Projectile.{Model,View}` | MVC |
| `Thrower/` | `BalloonParty.Thrower` | Controller + View |
| `Slots/Grid/` | `BalloonParty.Slots.Grid` | Model (grid state) |
| `Slots/Actor/` | `BalloonParty.Slots.Actor` | Model (actor contracts + static actor) |
| `Slots/Capabilities/` | `BalloonParty.Slots.Capabilities` | Model (capability interfaces) |
| `Slots/Spawner/` | `BalloonParty.Slots.Spawner` | Controller (spawn coordination) |
| `Game/` | `BalloonParty.Game` | Composition root |
| `Game/Score/` | `BalloonParty.Game.Score` | Controller |
| `Game/Cinematics/` | `BalloonParty.Game.Cinematics` | Controller + View |
| `Item/` | `BalloonParty.Item` | Handlers + Views |
| `Nudge/` | `BalloonParty.Nudge` | Service |
| `UI/` | `BalloonParty.UI.*` | Views |
| `Shared/` | `BalloonParty.Shared.*` | Utilities |
| `Configuration/` | `BalloonParty.Configuration` | SO definitions |
| `Display/` | `BalloonParty.Display` | Camera + rendering |
| `Prediction/` | `BalloonParty.Prediction` | Trajectory math |
| `Cheats/` | `BalloonParty.Cheats` | Dev-only |

