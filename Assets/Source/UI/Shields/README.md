# UI/Shields

The shield HUD: a ball-with-a-shield icon and the number of shields **every new ball starts with**.

Shields belong to each ball, and several balls can be in the air at once, so no single HUD number can
say how many shields "the ball" has. The HUD shows the one shield number that is the same for every
ball, its starting count; each ball shows its own shields itself, as layers around it
(`Projectile/View/ProjectileShieldView`). A shield gained in flight fades in a new layer and plays an
effect at the balloon that gave it; a shield spent on a wall bounce dissolves a layer and plays a shard
effect at the ball. Nothing flies to or from the HUD.

## Contents

| File | What it does |
|---|---|
| `ShieldUILifetimeScope` | VContainer child scope on the shield HUD root; registers the `ShieldCounterLabel[]` and `ShieldCounterAnimation` |
| `ShieldCounterLabel` | Marker subclass of `ReactiveCounterLabel`; rendering is delegated to a `RollingCounterDisplay` sibling. Bound once, at start, to `IShieldsPerBall.ShieldsPerBall` |
| `ShieldCounterAnimation` | Binds the labels and drives the Animator: `Ready` once at start, then `Gain` when the shields per ball go up (a shield card) and `Lost` when they go down (a fresh run drops the cards) |

## How it works

`IShieldsPerBall` (`Game/Upgrades/`) is implemented by `RunUpgrades`: the configured
`ProjectileStartingShields` plus every shield card held, including a retry's bonus card. It is a
reactive value recomputed whenever the cards change, so the HUD moves exactly when the next ball's
starting shields do. It never changes during a flight.

The counter's Animator still has a `Waiting` state from when it followed the ball in the thrower; the
script no longer triggers it.

## Interactions

- **IShieldsPerBall** (`RunUpgrades`): the value the labels show and the animation watches
- **ProjectileShieldView**: shows each ball's own shields, and their gains and losses, on the ball
- **ShieldGainedMessage / ShieldLostMessage**: still published per ball (heads-up text, audio, telemetry use them); the HUD does not
