# UI/Shields

Displays the projectile's remaining shields and animates state changes as the projectile bounces or is replaced.

## Contents

| File | What it does |
|---|---|
| `ShieldUILifetimeScope` | VContainer child scope on the shield HUD root; registers `ShieldCounterLabel[]` and `ShieldCounterAnimation` |
| `ShieldCounterLabel` | Shows the shield count; resets to "--" during balance passes |
| `ShieldCounterAnimation` | Drives Animator triggers (`Ready`, `Lost`, `Gain`, `Waiting`) based on `ShieldsRemaining` changes |

## How it works

`ShieldCounterLabel` shows the starting shield count when a `ProjectileLoadedMessage` arrives and resets to "--" while the grid is balancing (indicated by `BalanceBalloonsMessage`).

`ShieldCounterAnimation` drives an Animator with three triggers: `"Ready"` on load, `"Lost"` when `ShieldsRemaining` decrements, `"Gain"` if shields are ever restored, and `"Waiting"` during a balance pass. It binds directly to a `ProjectileModel.ShieldsRemaining` reactive property — `ThrowerController` calls `BindProjectile(model)` after each reload so the animation always tracks the live projectile.

## Interactions

- **ThrowerController** — calls `ShieldCounterAnimation.BindProjectile()` on each reload
- **ProjectileModel.ShieldsRemaining** — `ReactiveProperty<int>` subscribed by `ShieldCounterAnimation`
- **ProjectileLoadedMessage** — triggers "Ready" state and resets the label
- **BalanceBalloonsMessage** — triggers "Waiting" state and suspends shield tracking
- **IGameConfiguration** — `ProjectileStartingShields` used by label for initial display

