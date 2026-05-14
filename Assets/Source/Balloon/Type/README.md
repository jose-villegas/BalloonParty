# Balloon/Type

Balloon types define hit capacity, color selection, and per-type Inspector configuration.

## Contents

| File | What it does |
|---|---|
| `BalloonType` | Enum — `Simple`, `Tough`, `Unbreakable` |
| `IBalloonTypeConfiguration` | Interface on balloon prefab root MonoBehaviours — `TypeName`, `Initialize(IWriteableBalloonModel)`. Hit count, item eligibility, and other per-type data are owned by `BalloonPrefabEntry` and written by `BalloonSpawner` before `Initialize()` is called |
| `IBalloonViewBinding` | Interface for MonoBehaviours on a balloon prefab that need to react to model binding. `BalloonView` discovers all components implementing this interface via `GetComponentsInChildren` and calls `Bind(IBalloonModel, CompositeDisposable)` automatically — keeping `BalloonView` agnostic of specific balloon types |
| `ColorableBalloonType` | Abstract `MonoBehaviour` — picks a random color from `GamePalette` filtered by `_allowedColorsMask`; sets `TypeName` and `Color` on the model via `Initialize()` |
| `SimpleBalloonType` | Extends `ColorableBalloonType` — one-hit colored balloon; no additional behavior |
| `ToughBalloonType` | `MonoBehaviour` implementing both `IBalloonTypeConfiguration` and `IBalloonViewBinding` — not colorable; sets only `TypeName` in `Initialize()`. On `Bind()` subscribes to `HitsRemaining` and animates `_DamageProgress` on the `ToughBalloon` shader via `MaterialPropertyBlock` using a DOTween tween |

## How it works

During spawning, `BalloonSpawner` writes `HitsRemaining`, `CanHoldItem` directly onto the model from `BalloonPrefabEntry` before calling `IBalloonTypeConfiguration.Initialize(model)`. `Initialize()` is then responsible only for type-specific data: `TypeName` and (for colored types) `Color`. This keeps per-type balance values centralized in the configuration asset rather than scattered across MonoBehaviours.

Then `BalloonView.Bind(model)` calls `Bind()` on all `IBalloonViewBinding` components found in the hierarchy. `ToughBalloonType` uses this to subscribe to `HitsRemaining` and drive shader damage visuals.

The `_allowedColorsMask` on `ColorableBalloonType` is a bitmask over `GamePalette.Colors` shown in the Inspector as per-color checkboxes via `PaletteColorMaskAttribute`. `PickColor()` builds a list of allowed color names from the bitmask and picks uniformly at random.

`BalloonController` reads `HitsRemaining` on each `BalloonHitMessage`:

- **`-1`** (Unbreakable) → deflect without decrementing
- **`> 1`** (Tough) → decrement and deflect
- **`≤ 1`** → pop

Each deflect publishes `BalloonDeflectedMessage` (carries the balloon model, its world position, and the projectile direction) and `BalloonNudgeMessage(NudgeType.Deflect)` to push the balloon away from the projectile direction. The nudge is handled by `NudgeService` in `Nudge/`.

## Tough balloon shader

`ToughBalloonType` drives the `Sprites/ToughBalloon` shader via `MaterialPropertyBlock` (no material instance allocation — safe for pooled objects):

- **`_DamageProgress`** (`0` pristine → `1` critical) — animated via `DOVirtual.Float` over `_crackAnimDuration` seconds using `Ease.OutCubic`. When a second hit arrives before the tween completes, the new tween starts from the current animated value so transitions chain smoothly.
- **`_VoronoiSeed`** — set to a random `Vector2` at bind time so each balloon instance has a unique crack pattern.

The shader produces procedural damage visuals: a subsurface rim fringe that thins with damage, and spherically-projected Voronoi crack lines that grow from invisible hairlines to full splits as `_DamageProgress` increases.

## Interactions

- **BalloonSpawner** — writes `HitsRemaining` and `CanHoldItem` from `BalloonPrefabEntry` before calling `Initialize()`; calls `IBalloonViewBinding.Bind()` indirectly via `BalloonView`
- **BalloonView** — auto-discovers and calls `IBalloonViewBinding.Bind()` on all components in the hierarchy
- **BalloonController** — reads `HitsRemaining` to route hit/deflect/pop
- **GamePalette** — injected into `ColorableBalloonType` to resolve allowed color names
- **PaletteColorMaskAttribute** — drives the Inspector bitmask drawer for color filtering
- **`BalloonParty/Balloon/ToughBalloon` shader** — receives `_DamageProgress` and `_VoronoiSeed` per-instance via `MaterialPropertyBlock`
