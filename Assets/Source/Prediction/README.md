# Prediction

Prediction trace system — draws a dotted line showing the projectile's predicted trajectory while the player is aiming.

## Architecture

### PredictionTraceCalculator

Pure C# class (no MonoBehaviour) that takes an origin, direction, and reusable `List<Vector3>`, then fills it with world-space trace points by stepping forward and reflecting off walls. Bounces off left, right, and top limits (from `LimitsClockwise`). A top-wall hit terminates further bounces.

### PredictionTraceView

MonoBehaviour with a `LineRenderer`. Call `SetTrace(points)` to update or `Clear()` to hide. Attach to the Thrower prefab alongside a `LineRenderer`.

### PredictionTraceLights

Pure C# class owned by `ThrowerController` that mirrors the prediction line with capsule (segment) lights from the scene-light field — one per trace leg (the calculator emits corner points only: launch, each wall bounce, the top-wall tip), so the glow bends exactly where the line does. Intensity is tunable from launch to tip via `PredictionLightFadeCurve`, sampled at each leg's arc-length midpoint; the beam half-width is likewise tunable via `PredictionLightWidthCurve` (wide→thin or any combo), sampled at each leg's endpoints so the taper stays continuous across bounces. Lights register when the trace appears, are mutated in place every aim update (rebuilding only when the leg count changes — a rotating aim crossing a bounce threshold), and are disposed when the trace clears, on fire, on run reset, and on controller teardown. A degenerate (empty or zero-length) trace disposes the lights instead of leaving ghosts. Knobs live in `GameConfiguration` under the Trace header (half-width, intensity, falloff power, fade curve). At most three stamps (two bounces) against the field's 32-stamp cap shared with the projectile, laser telegraph, and item flashes.

### Integration

`ThrowerController` owns a `PredictionTraceCalculator` and a `PredictionTraceLights`; `ThrowerView` finds the `PredictionTraceView` via `GetComponentInChildren` in `Awake` and exposes `SetTrace`/`ClearTrace`. Each `Tick`, while the player is aiming and the projectile hasn't been fired, the controller calculates the trace and pushes it through the view and the lights. On fire or release, the line and its lights are cleared. The lights are tinted by the presentation-only `Prediction` palette entry (`GamePalette.PredictionColorId`, threaded through `ThrowerSettings`) — author the entry on the palette asset to tune the glow's colour; a missing entry falls back to the key light.

## Unity Setup

1. Add a child GameObject to the Thrower prefab
2. Add `LineRenderer` + `PredictionTraceView` components
3. Configure the `LineRenderer` material, width, and color to match the desired visual style

