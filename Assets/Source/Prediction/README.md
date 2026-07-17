# Prediction

Prediction trace system — draws a dotted line showing the projectile's predicted trajectory while the player is aiming.

## Architecture

### PredictionTraceCalculator

Pure C# class (no MonoBehaviour) that takes an origin, direction, and reusable `List<Vector3>`, then fills it with world-space trace points by stepping forward and reflecting off walls. Bounces off left, right, and top limits (from `LimitsClockwise`). A top-wall hit terminates further bounces.

### PredictionTraceView

MonoBehaviour with a `LineRenderer`. Call `SetTrace(points)` to update or `Clear()` to hide. Attach to the Thrower prefab alongside a `LineRenderer`.

### Integration

`ThrowerController` owns a `PredictionTraceCalculator`; `ThrowerView` finds the `PredictionTraceView` via `GetComponentInChildren` in `Awake` and exposes `SetTrace`/`ClearTrace`. Each `Tick`, while the player is aiming and the projectile hasn't been fired, the controller calculates the trace and pushes it through the view. On fire or release, the line is cleared. The line deliberately casts NO scene-light — an aim telegraph relighting the actors it crosses read as noise (a light-field version was tried and removed; see branch backup/gi-normals-spherize for the era).

## Unity Setup

1. Add a child GameObject to the Thrower prefab
2. Add `LineRenderer` + `PredictionTraceView` components
3. Configure the `LineRenderer` material, width, and color to match the desired visual style

