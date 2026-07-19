# Diagnostics

Runtime diagnostic tools for performance monitoring and device configuration.

## Contents

| File | What it does |
|---|---|
| `FPSCounter` | MonoBehaviour that displays average FPS and lowest frame rate per 0.5s interval as an `OnGUI` overlay. Color-coded: green (\f$\ge\f$ warn threshold), yellow (\f$\ge\f$ bad threshold), red (\f$<\f$ bad). Font size, colors, and thresholds configurable via Inspector. **Dev-only** — the whole class is guarded by `UNITY_EDITOR \|\| DEVELOPMENT_BUILD`, so it compiles out of release builds (the scene component and its serialized settings drop with it) |
| `FrameRateSettings` | MonoBehaviour that sets `Application.targetFrameRate` on `Awake`. Disables VSync (`vSyncCount = 0`) so the target takes effect. Three modes: **Default60** (fixed 60), **MatchDisplay** (device-dependent — see below), **Custom** (exposes a frame rate field, shown conditionally via `[ShowIf]`). Default mode is `MatchDisplay`. Ships in **all builds** — it's the only code setting the target frame rate, so release needs it |

## Usage

Add both components to a persistent GameObject in the scene. `FrameRateSettings` should run on the same object or earlier than any gameplay code — it only needs to execute once in `Awake()`.

In the Editor, changing values in Play mode applies immediately via `OnValidate` (`FPSCounter` updates live; `FrameRateSettings` re-applies the target frame rate). In the Editor, `MatchDisplay` leaves the target uncapped (`-1`) — the Game View reports its own refresh rate, not the physical display, so matching it would falsely cap the editor.

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

