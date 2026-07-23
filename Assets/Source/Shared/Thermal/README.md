# Thermal

Thermal-aware frame-rate governance. On the target device (Pixel 9, Tensor G4) the game runs
locked and cool at 120 fps but throttles purely thermally after a couple of minutes of play —
frame time tracks thermal status, with no ARR latch, memory leak, or CPU hot spot (see
`Plans/PLAN-PerformanceRecovery.md`, Step 0 findings). A deliberate, stable step down to the
panel's native **80 Hz** mode under sustained heat looks smoother than a juddery, half-missed 120.

## Contents

| File | What it does |
|---|---|
| `IThermalSource` | Plain-C# port exposing the latest polled `Headroom` (Android `getThermalHeadroom`: `1.0` = severe-throttle threshold, lower = cooler) and `Status` (`getCurrentThermalStatus`, `-1` if unavailable). |
| `AndroidThermalSource` | JNI adapter over `PowerManager`. Rate-limited/cached polling (headroom must not be queried more than ~1 Hz and returns `NaN` when unavailable → mapped to `0`, i.e. cool). Compiled only under `UNITY_ANDROID && !UNITY_EDITOR`. |
| `StubThermalSource` | Always-cool source for the editor and non-Android platforms; pins the governor to its top rung. |
| `ThermalFrameRateGovernor` | `IStartable + ITickable` state machine over a rate ladder (default `{120, 80, 60}`). |

## How the governor works

Ladder rungs are ordered fastest-first; index 0 is the top rate. Every `PollIntervalSeconds`
(~1 s) it re-reads the source and advances hysteresis timers:

- **Step down** (to a slower rung) when the device stays hot — `Headroom >= DownHeadroom`
  (default `0.85`) or `Status >= 1` — for `DownSustainSeconds` (~10 s). Short, so throttling is
  met quickly (the Tensor G4 starts pulling clocks at status 1, ~2.5 min in).
- **Step up** (to a faster rung) when the device stays cool — `Headroom <= UpHeadroom`
  (default `0.65`) and `Status <= 0` — for `UpSustainSeconds` (~60 s) **and** at least
  `MinDwellSeconds` (~90 s) have elapsed at the current rung. The asymmetry (fast down, slow up,
  plus the dwell floor) is what prevents oscillation around the throttle point.

Rate changes are applied through `FrameRateSettings.ApplyGovernedRate(int)` — the single seam
that clears VSync and sets `Application.targetFrameRate`. **This is not the ARR echo-loop** the
`MatchDisplay` vote path guards against: the governor decides the rung purely from thermal
signals and never reads back a refresh rate, so there is nothing to re-pin.

Time is fed in as accumulated deltas via `Advance(float dt)` (called from `Tick` with unscaled
delta time), so the state machine has no `Time.*` dependency and EditMode tests can drive it.

## Configuration & wiring

Tuning lives in `ThermalGovernorSettings` (`Configuration/`, interface
`IThermalGovernorSettings`): the ladder, thresholds, windows, poll cadence, and an `Enabled`
flag. `GameLifetimeScope` registers it with a null-safe fallback — an unassigned asset degrades
to a default instance carrying the same defaults, so the governor works without any scene wiring;
author an asset only to tune. The source is platform-switched in
`GameScopeRegistration.RegisterCoreServices` (`AndroidThermalSource` on device, `StubThermalSource`
otherwise) and the governor is registered as an entry point in the same scope, which persists for
the gameplay session (where the heat builds).

Note: `FrameRateSettings` itself is a boot-only component in the Launcher scene (that scene is
unloaded on the transition to Game), so the seam is a `static` method — the governor never needs
a live `FrameRateSettings` instance.
