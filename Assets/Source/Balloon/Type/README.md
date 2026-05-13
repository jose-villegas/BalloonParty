# Balloon/Type

Balloon types define hit capacity, color selection, and per-type Inspector configuration.

## Contents

| File | What it does |
|---|---|
| `BalloonType` | Enum — `Simple`, `Tough`, `Unbreakable` |
| `IBalloonTypeConfiguration` | Interface on balloon prefab root MonoBehaviours — `TypeName`, `HitsToPop`, `Initialize(IWriteableBalloonModel)` |
| `ColorableBalloonType` | Abstract `MonoBehaviour` — picks a random color from `GamePalette` filtered by `_allowedColorsMask`; sets `TypeName` and `HitsRemaining` on the model via `Initialize()` |
| `SimpleBalloonType` | Extends `ColorableBalloonType` — one-hit colored balloon; no additional behavior |
| `ToughBalloonType` | `MonoBehaviour` implementing `IBalloonTypeConfiguration` — configurable `_hitsToPop` (default 2); not colorable |

## How it works

During spawning, `BalloonSpawner` calls `IBalloonTypeConfiguration.Initialize(model)` on the balloon prefab's root component. This writes `TypeName`, `HitsRemaining`, and (for colored types) `Color` directly onto the model.

The `_allowedColorsMask` on `ColorableBalloonType` is a bitmask over `GamePalette.Colors` shown in the Inspector as per-color checkboxes via `PaletteColorMaskAttribute`. `PickColor()` builds a list of allowed color names from the bitmask and picks uniformly at random.

`BalloonController` reads `HitsRemaining` on each `BalloonHitMessage`:

- **`-1`** (Unbreakable) → deflect without decrementing
- **`> 1`** (Tough) → decrement and deflect
- **`≤ 1`** → pop

Each deflect publishes `BalloonDeflectedMessage` (carries the balloon model, its world position, and the projectile direction) and `BalloonNudgeMessage(NudgeType.Deflect)` to push the balloon away from the projectile direction. The nudge is handled by `NudgeService` in `Nudge/`.

## Interactions

- **BalloonSpawner** — calls `Initialize()` after getting a view from the pool
- **BalloonController** — reads `HitsRemaining` to route hit/deflect/pop
- **GamePalette** — injected into `ColorableBalloonType` to resolve allowed color names
- **PaletteColorMaskAttribute** — drives the Inspector bitmask drawer for color filtering

