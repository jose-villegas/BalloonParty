# Diagrams

Doxygen `@page` documentation describing the project's architecture at a system
level. Each `arch_*.md` is a standalone page that embeds an SVG (`@image html`)
and explains, in prose, what the diagram shows and which contracts it enforces.

These pages are the conceptual companion to the per-folder `README.md` files: a
folder README explains one feature in isolation, while a diagram here shows how
features compose across the whole game.

## Pages

| Page | Subject |
|---|---|
| `arch_mvc` | The Model / View / Controller split that governs every feature |
| `arch_scope_hierarchy` | VContainer `LifetimeScope` tree and registration inheritance |
| `arch_message_flow` | MessagePipe publisher/subscriber routing across systems |
| `arch_static_state` | Static `Navigation` / `Cinematic` reactive state machines |
| `arch_turn_pipeline` | End-to-end flow of a single thrown projectile turn |
| `arch_balance_flow` | Balloon balancing pass and path animation |
| `arch_spawner_coordination` | `GridSpawnerCoordinator` staging of `IGridSpawner` runs |
| `arch_slot_actor` | Slot grid actor model and archetype rendering |
| `arch_item_activation` | Item activation frequency, weighting, and handler dispatch |
| `arch_cinematics_architecture` | The cinematics pipeline: settings SO, producers, runner, traits, TimeScaleService |
| `arch_score_cinematic` | Score thresholds driving the level-up cinematic |
| `arch_trail_composition` | Score / glow / flying trail composition during level-up |
| `arch_bush_system` | Bush cluster generation, baking, and instanced rendering |

## Building

Pages are rendered by Doxygen using the project `Doxyfile`. The embedded SVGs are
authored separately and referenced by filename — keep the `@image html` reference
in sync when a diagram asset is renamed.
