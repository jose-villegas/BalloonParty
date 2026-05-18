# UI/Shields

Displays the projectile's remaining shields, animates state changes, and spawns visual trails when shields are gained.

## Contents

| File | What it does |
|---|---|
| `ShieldUILifetimeScope` | VContainer child scope on the shield HUD root; registers `ShieldCounterLabel[]`, `ShieldCounterAnimation`, and `ShieldTrailController` |
| `ShieldCounterLabel` | Shows the shield count; resets to "--" during balance passes |
| `ShieldCounterAnimation` | Drives Animator triggers (`Ready`, `Lost`, `Gain`, `Waiting`) based on `ShieldsRemaining` changes |
| `ShieldTrailController` | Subscribes to `ShieldGainedMessage`; spawns a pooled `FlyingTrail` orb that flies from the balloon's slot position to the shield HUD |
| `ShieldTrailPoolChannel` | `PoolChannel<FlyingTrail>` — pool keyed by `ShieldTrail` |

## How it works

`ShieldCounterLabel` shows the starting shield count when a `ProjectileLoadedMessage` arrives and resets to "--" while the grid is balancing (indicated by `BalanceBalloonsMessage`).

`ShieldCounterAnimation` drives an Animator with three triggers: `"Ready"` on load, `"Lost"` when `ShieldsRemaining` decrements, `"Gain"` if shields are ever restored, and `"Waiting"` during a balance pass. It binds directly to a `ProjectileModel.ShieldsRemaining` reactive property — `ThrowerController` calls `BindProjectile(model)` after each reload so the animation always tracks the live projectile.

`ShieldTrailController` listens for `ShieldGainedMessage` (published by `ShieldItemHandler` when a shield item is activated). It resolves the balloon's world position from the slot index via `SlotGrid.IndexToWorldPosition`, then spawns a pooled `FlyingTrail` that tweens from that position to the shield HUD's transform. The trail duration is read from `IGameConfiguration.ShieldTrailDuration`. On completion the trail is returned to the pool.

## Interactions

- **ThrowerController** — calls `ShieldCounterAnimation.BindProjectile()` on each reload
- **ProjectileModel.ShieldsRemaining** — `ReactiveProperty<int>` subscribed by `ShieldCounterAnimation`
- **ProjectileLoadedMessage** — triggers "Ready" state and resets the label
- **ShieldGainedMessage** — triggers trail spawn from balloon slot to shield HUD
- **BalanceBalloonsMessage** — triggers "Waiting" state and suspends shield tracking
- **IGameConfiguration** — `ProjectileStartingShields` used by label for initial display; `ShieldTrailDuration` used by trail controller
- **SlotGrid** — resolves slot index to world position for trail origin
- **PoolManager** — `ShieldTrail` pool for `FlyingTrail`; consumer handles return
