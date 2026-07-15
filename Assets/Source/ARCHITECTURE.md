@page architecture BalloonParty — Architecture Diagrams

# BalloonParty — Architecture Diagrams

> Visual system maps, data flow diagrams, and scope hierarchy references.
> Each sub-page includes the diagram and guidance on how the system works and how to extend it.
> For code style rules see @ref style_guide. For per-feature details see each folder's `README.md`.

---

## Diagrams

| Page | What it covers |
|------|----------------|
| @subpage arch_mvc "MVC Pattern" | Layer separation, data flow rules, when to split View vs Controller |
| @subpage arch_scope_hierarchy "Scope Hierarchy" | VContainer scope tree, where to register new services, child scope rules |
| @subpage arch_turn_pipeline "Turn Pipeline" | Hit → Balance → Spawn → Post-Balance sequence and extension points |
| @subpage arch_balance_flow "Balance Flow" | Balance algorithm, transit slot tracking, `BalancePathHolder` |
| @subpage arch_spawner_coordination "Spawner Coordination" | Staged grid spawner pipeline, how to add a new spawner |
| @subpage arch_message_flow "Message Flow" | All pub/sub connections, when to use MessagePipe vs direct injection |
| @subpage arch_cinematics_architecture "Cinematics Architecture" | Settings SO → producers → runner → rig/director/TimeScaleService, traits, interplay |
| @subpage arch_score_cinematic "Score & Cinematic Pipeline" | Attribution → trails → cinematic intercept → level-up |
| @subpage arch_trail_composition "Trail Utility Composition" | `TrailFlightRegistry`, endpoint registration and trail interception, cinematic pause |
| @subpage arch_item_activation "Item Activation Pipeline" | Activation handoff, pool-return coordination, adding new item types |
| @subpage arch_static_state "Static State" | `Navigation` and `Cinematic` — cross-scene singleton state |
| @subpage arch_slot_actor "Slot Actor Abstraction" | Interface hierarchy, capability casting, adding new actor types |
| @subpage disturbance_field "Disturbance Field Service" | Screen-space RT field, stamp API, lerp vs instant routing, consumers |
| @subpage arch_bush_system "Bush System" | Bake pipeline, runtime rendering, GPU wind + rattle, disturbance field interaction |
| @subpage arch_screen_space_light "Screen-Space Light (2D GI)" | Shared capture → per-fragment field-directional cone march → soft shadow + bounce composite; mip-chain HSSVGI pattern |
| @subpage arch_light_field "Scene Light Field" | Reactive multi-light RT (point + area), 3-pass pipeline (fill/accumulate/gradient), palette colour, consumer include |

---

## Folder → Namespace Mapping

| Folder | Namespace | Layer |
|--------|-----------|-------|
| `Balloon/` | `BalloonParty.Balloon.{Model,View,Controller}` | MVC |
| `Projectile/` | `BalloonParty.Projectile.{Model,View}` | MVC |
| `Thrower/` | `BalloonParty.Thrower` | Controller + View |
| `Slots/Grid/` | `BalloonParty.Slots.Grid` | Model (grid state) |
| `Slots/Actor/` | `BalloonParty.Slots.Actor` | Model (actor contracts + static actor) |
| `Slots/Actor/Archetype/` | `BalloonParty.Slots.Actor.Archetype` | Concrete actors (Bush, Puff) + cluster views |
| `Slots/Actor/Cluster/` | `BalloonParty.Slots.Actor.Cluster` | Shared cluster infrastructure |
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
