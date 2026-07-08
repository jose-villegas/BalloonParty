# Diagnostics

Runtime diagnostic tools for performance monitoring and device configuration.

## Contents

| File | What it does |
|---|---|
| `FPSCounter` | MonoBehaviour that displays average FPS and lowest frame rate per 0.5s interval as an `OnGUI` overlay. Color-coded: green (≥ warn threshold), yellow (≥ bad threshold), red (< bad). Font size, colors, and thresholds configurable via Inspector. **Dev-only** — the whole class is guarded by `UNITY_EDITOR \|\| DEVELOPMENT_BUILD`, so it compiles out of release builds (the scene component and its serialized settings drop with it) |
| `FrameRateSettings` | MonoBehaviour that sets `Application.targetFrameRate` on `Awake`. Disables VSync (`vSyncCount = 0`) so the target takes effect. Three modes: **Default60** (fixed 60), **MatchDisplay** (reads native device refresh rate — 60/90/120 Hz), **Custom** (exposes a frame rate field, shown conditionally via `[ShowIf]`). Default mode is `MatchDisplay`. Ships in **all builds** — it's the only code setting the target frame rate, so release needs it |

## Usage

Add both components to a persistent GameObject in the scene. `FrameRateSettings` should run on the same object or earlier than any gameplay code — it only needs to execute once in `Awake()`.

In the Editor, changing values in Play mode applies immediately via `OnValidate` (`FPSCounter` updates live; `FrameRateSettings` re-applies the target frame rate).

