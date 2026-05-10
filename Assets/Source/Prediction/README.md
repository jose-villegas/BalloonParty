# Prediction

Prediction trace system — draws a dotted line showing the projectile's predicted trajectory while the player is aiming.

## Architecture

### PredictionTraceCalculator

Pure C# class (no MonoBehaviour) that takes an origin, direction, and reusable `List<Vector3>`, then fills it with world-space trace points by stepping forward and reflecting off walls. Bounces off left, right, and top limits (from `LimitsClockwise`). A top-wall hit terminates further bounces.

### PredictionTraceView

MonoBehaviour with a `LineRenderer`. Call `SetTrace(points)` to update or `Clear()` to hide. Attach to the Thrower prefab alongside a `LineRenderer`.

### Integration

`ThrowerController` owns a `PredictionTraceCalculator` and finds a `PredictionTraceView` via `GetComponentInChildren`. During `Update`, while the player holds the mouse button and the projectile hasn't been fired, it calculates the trace and pushes it to the view. On fire or release, the line is cleared.

## Unity Setup

1. Add a child GameObject to the Thrower prefab
2. Add `LineRenderer` + `PredictionTraceView` components
3. Configure the `LineRenderer` material, width, and color to match the desired visual style

