# UI/Shields

Displays the projectile's remaining shields, animates state changes, and spawns visual trails when shields are gained.

## Contents

| File | What it does |
|---|---|
| `ShieldUILifetimeScope` | VContainer child scope on the shield HUD root; registers `ShieldCounterLabel[]`, `ShieldCounterAnimation`, trail prefab, the HUD anchor as the `Shield` trail endpoint, and `ShieldTrailController` entry point |
| `ShieldCounterLabel` | `ReactiveCounterLabel` subclass showing the shield count — "--" until bound and between turns; bound to the live projectile's `ShieldsRemaining` by `ShieldCounterAnimation` on each load |
| `ShieldCounterAnimation` | Drives Animator triggers (`Ready`, `Lost`, `Gain`, `Waiting`) based on `ShieldsRemaining` changes; binds/unbinds the labels on projectile load/destroy |
| `ShieldTrailController` | Plain C# `IStartable` — subscribes to `ShieldGainedMessage` (balloon → HUD) and `ShieldLostMessage` (HUD → wall bounce); composes a `TrailSpawner` to fly `FlyingTrail` orbs between the balloon/bounce point and the shield HUD endpoint |
| `SimplePoolChannel<FlyingTrail>` | shield-trail pool keyed by `ShieldTrail` |

## How it works

`ShieldCounterAnimation` subscribes to `ProjectileLoadedMessage` / `ProjectileDestroyedMessage` and owns the labels' binding: on load it binds every `ShieldCounterLabel` to the new projectile's `ShieldsRemaining` (the labels show "--" until then); on destroy it unbinds them, returning the display to "--" for the between-turns wait.

It also drives an Animator: `"Ready"` on load, `"Lost"` when `ShieldsRemaining` decrements, `"Gain"` when it increments (streak shield awards), and `"Waiting"` from projectile death until the next load. It binds directly to the live projectile's `ShieldsRemaining` reactive property carried in `ProjectileLoadedMessage`.

`ShieldTrailController` is a plain C# `IStartable` + `IDisposable` registered as an entry point in `ShieldUILifetimeScope`. It flies shield orbs in both directions between a world point and the `Shield` trail endpoint (the HUD anchor, resolved from the shared `TrailEndpointRegistry` — see `Shared/Pool`), reusing one composed `TrailSpawner`:

- **`ShieldGainedMessage`** (published by `ShieldItemHandler` when a shield item is activated): resolves the granting balloon's world position from its slot index via `SlotGrid.IndexToWorldPosition` and flies an orb *up to* the HUD.
- **`ShieldLostMessage`** (published by `ProjectileView` when a wall bounce spends a shield — i.e. `ShieldsRemaining` stays `>= 0` after the decrement; below that the projectile is destroyed instead): flies an orb *from* the HUD down to the bounce point carried in the message.

Both use `IGameConfiguration.ShieldTrailDuration`; the spawner handles pool return on completion.

## Interactions

- **ProjectileModel.ShieldsRemaining** — `ReactiveProperty<int>` subscribed by `ShieldCounterAnimation` and the labels
- **ProjectileLoadedMessage** — triggers "Ready" state and binds the labels to the new projectile
- **ProjectileDestroyedMessage** — triggers "Waiting" state and unbinds the labels
- **ShieldGainedMessage** — triggers trail spawn from balloon slot to shield HUD
- **IGameConfiguration** — `ShieldTrailDuration` used by trail controller
- **SlotGrid** — resolves slot index to world position for trail origin
- **PoolManager** — `ShieldTrail` pool for `FlyingTrail`; consumer handles return
