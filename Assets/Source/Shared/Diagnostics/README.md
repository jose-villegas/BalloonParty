# Diagnostics

Runtime diagnostic tools for performance monitoring and device configuration.

## Contents

| File | What it does |
|---|---|
| `Log` | Static logging facade — call `Log.Info(tag, msg)`, `Log.Warn(tag, msg)`, `Log.Error(tag, msg)`, or `Log.Assert(condition, tag, msg)` from anywhere. `Info` and `Assert` are decorated with `[Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]`, so they compile out of release builds automatically — no `#if` guards at call sites. `Warn` and `Error` remain in all builds. Each tag gets a deterministic color in the Unity Console via rich text. Static class, no DI needed |
| `FPSCounter` | MonoBehaviour that displays average FPS and lowest frame rate per 0.5s interval as an `OnGUI` overlay. Color-coded: green (\f$\ge\f$ warn threshold), yellow (\f$\ge\f$ bad threshold), red (\f$<\f$ bad). Colors and thresholds are read live from the Inspector fields every `OnGUI`; font size is baked into the `GUIStyle` the first time it's built and never rebuilt after, so a font-size change needs a domain reload (entering/exiting Play mode) to take effect. **Dev-only** — the whole class is guarded by `UNITY_EDITOR \|\| DEVELOPMENT_BUILD`, so it compiles out of release builds (the scene component and its serialized settings drop with it) |
| `FrameRateSettings` | MonoBehaviour that sets `Application.targetFrameRate` on `Awake`. Disables VSync (`vSyncCount = 0`) so the target takes effect. Three modes: **Default60** (fixed 60), **MatchDisplay** (device-dependent — see below), **Custom** (exposes a frame rate field, shown conditionally via `[ShowIf]`). Default mode is `MatchDisplay`. Ships in **all builds** — it's the only code setting the target frame rate, so release needs it |

## Log — tagged logging

Use `Log` instead of raw `Debug.Log` / `Debug.LogWarning` / `Debug.LogError` throughout the runtime codebase. The tag string groups related messages and gets a per-tag color in the Console.

```csharp
Log.Info("Spawner", $"Spawned {count} actors");   // stripped from release builds
Log.Warn("Grid",    "Row overflow — clamping");    // survives in all builds
Log.Error("Score",  "Negative attribution");       // survives in all builds
Log.Assert(hp > 0,  "Health", "HP went negative"); // stripped from release builds
```

`Info` and `Assert` auto-strip via `[Conditional]` — callers don't need `#if` guards and the string interpolation arguments are also eliminated by the compiler. `Warn` and `Error` are unconditional because warnings and errors should be visible in release diagnostics. `Warn` and `Error` accept an optional `UnityEngine.Object context` parameter to highlight the source object in the Hierarchy on click.

## Usage

Add both components to a persistent GameObject in the scene. `FrameRateSettings` should run on the same object or earlier than any gameplay code — it only needs to execute once in `Awake()`.

In the Editor, changing `FrameRateSettings`' Inspector fields in Play mode re-applies the target frame rate immediately via `OnValidate`. `FPSCounter` has no such hook — its colors and thresholds are read live in `OnGUI` regardless, but a font-size change needs a domain reload to show (see above). In the Editor, `MatchDisplay` leaves the target uncapped (`-1`) — the Game View reports its own refresh rate, not the physical display, so matching it would falsely cap the editor.

## MatchDisplay on adaptive-refresh Android

On-device, `MatchDisplay` votes for the **highest refresh rate advertised across
`Screen.resolutions`** — not whatever `Screen.currentResolution` currently reports.
That's deliberate, not an oversight: on adaptive-refresh-rate (ARR) panels (Pixel-class
Android), there's no discrete display-mode switch to make — the panel already runs a
high-Hz mode and `Screen.currentResolution` reports the *per-app arbitrated* rate,
which itself echoes back whatever `Application.targetFrameRate` this app last voted
for. Matching that reading is an echo loop that self-pins the target at 60 the first
time it runs. Shopping `Screen.resolutions` for the best refresh rate and requesting it
via `Screen.SetResolution` sidesteps the loop; it's a no-op on true ARR panels (the
mode doesn't change) and a real switch on older devices that idle in a 60 Hz mode.

The `Screen.resolutions` entries are **full-panel sizes**, while the app's actual
rendering surface is inset by the display cutout/nav area — so width/height will
(by design) never exactly match a listed mode. `MatchDisplay` only shops those entries
for refresh rate and keeps the current surface size via `Screen.SetResolution`.

Diagnose refresh-rate issues from the `[FrameRateSettings]` log lines it prints at
startup (current resolution + refresh, the full `Screen.resolutions` list, the
requested mode, and the granted mode a few frames later) — read them with `adb logcat`
on a real device; this is the whole diagnosis loop. Related device-level settings:
Swappy / Optimized Frame Pacing is enabled (`androidUseSwappy` in Player Settings), and
the Android manifest declares `android:appCategory="game"` — both feed into how the
platform paces and schedules this app's frames.

