# UI/Shields

Displays the projectile's remaining shields, animates state changes, and spawns visual trails when shields are gained.

## Contents

| File | What it does |
|---|---|
| `ShieldUILifetimeScope` | VContainer child scope on the shield HUD root; registers `ShieldCounterLabel[]`, `ShieldCounterAnimation`, trail prefab, the HUD anchor as the `Shield` trail endpoint, and `ShieldTrailController` entry point |
| `ShieldCounterLabel` | Shows the shield count; resets to "--" during balance passes |
| `ShieldCounterAnimation` | Drives Animator triggers (`Ready`, `Lost`, `Gain`, `Waiting`) based on `ShieldsRemaining` changes |
| `ShieldTrailController` | Plain C# `IStartable` — subscribes to `ShieldGainedMessage` (balloon → HUD) and `ShieldLostMessage` (HUD → wall bounce); composes a `TrailSpawner` to fly `FlyingTrail` orbs between the balloon/bounce point and the shield HUD endpoint |
| `SimplePoolChannel<FlyingTrail>` | shield-trail pool keyed by `ShieldTrail` |

## How it works

`ShieldCounterLabel` shows the starting shield count when a `ProjectileLoadedMessage` arrives and resets to "--" while the grid is balancing (indicated by `BalanceBalloonsMessage`).

`ShieldCounterAnimation` drives an Animator with three triggers: `"Ready"` on load, `"Lost"` when `ShieldsRemaining` decrements, `"Gain"` if shields are ever restored, and `"Waiting"` during a balance pass. It binds directly to a `ProjectileModel.ShieldsRemaining` reactive property — `ThrowerController` calls `BindProjectile(model)` after each reload so the animation always tracks the live projectile.

`ShieldTrailController` is a plain C# `IStartable` + `IDisposable` registered as an entry point in `ShieldUILifetimeScope`. It flies shield orbs in both directions between a world point and the `Shield` trail endpoint (the HUD anchor, resolved from the shared `TrailEndpointRegistry` — see `Shared/Pool`), reusing one composed `TrailSpawner`:

- **`ShieldGainedMessage`** (published by `ShieldItemHandler` when a shield item is activated): resolves the granting balloon's world position from its slot index via `SlotGrid.IndexToWorldPosition` and flies an orb *up to* the HUD.
- **`ShieldLostMessage`** (published by `ProjectileView` when a wall bounce spends a shield — i.e. `ShieldsRemaining` stays `>= 0` after the decrement; below that the projectile is destroyed instead): flies an orb *from* the HUD down to the bounce point carried in the message.

Both use `IGameConfiguration.ShieldTrailDuration`; the spawner handles pool return on completion.

## Interactions

- **ThrowerController** — calls `ShieldCounterAnimation.BindProjectile()` on each reload
- **ProjectileModel.ShieldsRemaining** — `ReactiveProperty<int>` subscribed by `ShieldCounterAnimation`
- **ProjectileLoadedMessage** — triggers "Ready" state and resets the label
- **ShieldGainedMessage** — triggers trail spawn from balloon slot to shield HUD
- **BalanceBalloonsMessage** — triggers "Waiting" state and suspends shield tracking
- **IGameConfiguration** — `ProjectileStartingShields` used by label for initial display; `ShieldTrailDuration` used by trail controller
- **SlotGrid** — resolves slot index to world position for trail origin
- **PoolManager** — `ShieldTrail` pool for `FlyingTrail`; consumer handles return
