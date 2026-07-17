# Prediction

Prediction trace system — draws a dotted line showing the projectile's predicted trajectory while the player is aiming.

## Architecture

### PredictionTraceCalculator

Pure C# class (no MonoBehaviour) that takes an origin, direction, and reusable `List<Vector3>`, then fills it with world-space trace points by stepping forward and reflecting off walls. Bounces off left, right, and top limits (from `LimitsClockwise`). A top-wall hit terminates further bounces.

### PredictionTraceView

MonoBehaviour with a `LineRenderer`. Call `SetTrace(points)` to update, `SetColor(color)` to set `startColor`/`endColor`, or `Clear()` to hide. Attach to the Thrower prefab alongside a `LineRenderer`. For the smoke + glitter look, use the `BalloonParty/Display/TraceGlitterLine` material shader (SightSmoke's drifting-noise alpha eat plus GlitterSwirl's orbiting specks in one pass) and set the LineRenderer's texture mode to **Tile** so the pattern density stays constant over any aim length — the config-driven trace colour reaches the shader through the renderer's start/end vertex colours.

### PredictionTraceProvider

Plain C# game-scope singleton (registered in `GameScopeRegistration.RegisterCoreServices`) that mirrors the same-frame trace for readers outside the Thrower's own view chain — the house pattern is `ProjectilePositionProvider` (`Projectile/`). `SetTrace(points)` copies into an internal preallocated `List<Vector3>` (never aliases the caller's mutable buffer), bumps an `int Version`, and sets `IsActive`; `Clear()` sets `IsActive` false and bumps `Version`. Readers poll `IsActive`/`Version`/`Points` instead of subscribing, so many pooled readers can cheaply skip work on frames where nothing changed.

### TraceHitMarker

MonoBehaviour view for a circular actor (e.g. a balloon) that shows a marker where the aim trace crosses its circle. Each `LateUpdate`, it reads `PredictionTraceProvider` and finds where the trace polyline FIRST pierces the actor's circle via `TraceHitGeometry.TryFindSurfaceHit` (line-circle intersection walked in travel order — the physical strike point, not the perpendicular-closest point, which sits ~90° off anywhere but a tangential graze; pure, allocation-free, edit-mode tested in `Assets/Tests/EditMode/Prediction/TraceHitGeometryTests.cs`). The marker shows only when an intersection exists; it's positioned at `origin + hitDirection * _markerOffset` with hitDirection pointing at that surface entry, translated only — never rotated or scaled. Optionally (assign `_markerRenderer`), the sprite's alpha scales with the crossing's **centrality** (1 = line through the centre, 0 = tangential one-touch graze): direct aims read strong, grazes fade toward `_minIntensity`, under the sprite's authored alpha as the ceiling; its RGB mirrors the trace line's configured colour. Work is skipped whenever the provider's `Version` is unchanged and the actor hasn't moved past a small epsilon since the last evaluation. Visibility toggles the marker GameObject only on change, not every frame. `OnEnable` force-hides and invalidates the cache, since pooled instances are reused by toggling the whole prefab's GameObject (`PoolChannel<T>.Get`/`Return`) rather than by any dedicated reset callback.

### Integration

`ThrowerController` owns a `PredictionTraceCalculator`; `ThrowerView` finds the `PredictionTraceView` via `GetComponentInChildren` in `Awake` and exposes `SetTrace`/`SetTraceColor`/`ClearTrace`. Each `Tick`, while the player is aiming and the projectile hasn't been fired, the controller calculates the trace, pushes it through the view, and mirrors it into `PredictionTraceProvider` for any `TraceHitMarker` readers. On fire, release, or reload, both the view and the provider are cleared. The line's color comes from `IGameConfiguration.PredictionTraceColor`, pushed once in `ThrowerController.Start`. The line deliberately casts NO scene-light — an aim telegraph relighting the actors it crosses read as noise (a light-field version was tried and removed; see branch backup/gi-normals-spherize for the era).

## Unity Setup

1. Add a child GameObject to the Thrower prefab
2. Add `LineRenderer` + `PredictionTraceView` components
3. Configure the `LineRenderer` material and width; color is driven at runtime from `IGameConfiguration.PredictionTraceColor`
4. For a hit marker on a circular actor prefab (e.g. a balloon): add a small child sprite (e.g. "HitMarker"), add `TraceHitMarker` to the actor, and assign `_marker` (the child sprite's `Transform`), `_circleRadius` (the actor's world-unit circle radius), and `_markerOffset` (distance from the actor origin the marker sits at)

