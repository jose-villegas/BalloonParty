# Disturbance

Shared screen-space disturbance field that any game system can stamp into. Puff cloud shaders and future effects sample from it.

## Contents

| File | What it does |
|---|---|
| `DisturbanceFieldService` | Plain C# `IStartable` + `ITickable` + `IDisposable` — owns a camera-sized `ARGBHalf` RT pair (density in R, displacement XY in GB). Runs one diffusion blit per tick (spatial blur + reform toward equilibrium + wind advection + pressure fill + displacement decay). Exposes `Stamp()` for instant and lerp stamps. Pending stamps are batched (up to 16 per blit pass) via `DisturbanceStampBatched.shader`. Pushes `_DisturbanceTex`, `_FieldBoundsMin`, `_FieldBoundsSize` as global shader properties each tick so all consumers (cloud views, future effects) read the field without per-instance setup. Registered as a singleton in `GameLifetimeScope` |

## How it works

### RT layout

Two `RenderTexture` instances (`_fieldA`, `_fieldB`) ping-pong as read/write targets. Format is `ARGBHalf` with equilibrium clear color `(1.0, 0.5, 0.5, 1.0)`:

- **R** = density (1.0 = full cloud, 0.0 = cleared)
- **G** = displacement X (0.5 = zero, biased ±0.5 range)
- **B** = displacement Y (0.5 = zero, biased ±0.5 range)

Resolution is derived from `GameDisplayConfiguration.GetOrthogonalSize()` × `DisturbanceFieldSettings.TexelsPerUnit`.

### Stamp API

- **`Stamp(worldPos, radius, strength, direction, duration = 0)`** — queues a disturbance at `worldPos`. World coordinates are converted to field UV space internally. When `duration` is zero (default), the stamp is applied instantly — stamps accumulate in a pending list and are flushed in batches each frame. When `duration` is greater than zero, queues a lerp stamp that ramps from 0 to full strength over that many seconds, spreading the effect smoothly across multiple frames. Useful for pop bursts, bomb detonations, and paint splashes.

### Diffusion tick

Runs at a configurable interval (`DiffusionTickInterval`). Each tick:
1. Semi-Lagrangian wind advection shifts the sample origin
2. Pressure gradient pushes density from high to low, filling holes directionally
3. Displacement channels decay toward 0.5 (neutral)
4. Density trends back toward 1.0 (reform)

Wind direction is set dynamically from stamp directions (opposite to the disturbance velocity), smoothed and decaying — so the reform flows from behind the moving object.

## Consumers

| System | When it stamps | `StampSource` | Notes |
|---|---|---|---|
| `ProjectileView` | Each `FixedUpdate` in `MoveAndBounce()` | `Projectile` | Creates visible wakes through Puff clouds |
| `BalloonSpawner` | Each frame during spawn path DOTween `OnUpdate` | `BalloonPath` | Spawn animations disturb clouds they pass through |
| `BalloonBalancer` | Each frame during balance path DOTween `OnUpdate` | `BalloonPath` | Balance animations disturb clouds |
| `BalloonController` | On balloon pop | `BalloonPop` | Pop burst shockwave |
| `BombItemHandler` | On detonation | `Bomb` | Large-radius burst |
| `LaserItemHandler` | Along each beam segment | `Laser` | Linear disturbance along beams |
| `PaintItemHandler` | On neighbor hit and splash landing | `Paint` | Splash disturbances |
| `DisturbanceStampCheat` | Mouse drag (debug only) | — | Direct `Stamp()` call for testing |

All consumers read their radius/strength/duration from `DisturbanceFieldSettings.GetProfile(StampSource)`.

## Configuration

All tuning lives on `DisturbanceFieldSettings` SO (in `Configuration/`). See `Configuration/README.md` for field details.

## Interactions

- **`PuffCloudView`** — reads `_DisturbanceTex`, `_FieldBoundsMin`, `_FieldBoundsSize` from global shader properties (set by the service each tick). Only pushes `_TimeOffset` per-instance via `MaterialPropertyBlock`. No direct reference to the service
- **`GameLifetimeScope`** — registers the service as singleton with `AsImplementedInterfaces().AsSelf()`
- **`GameDisplayConfiguration`** — provides orthographic size for RT bounds computation
- **`DisturbanceFieldSettings`** — provides all tuning knobs and shader references

