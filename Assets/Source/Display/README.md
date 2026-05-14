# Display

Camera and screen-adaptation logic.

## Contents

| File | What it provides |
|---|---|
| `OrthogonalSizeCameraController` | Plain C# `IStartable` — sets the camera's orthographic size at startup based on the device's aspect ratio. Reads `GameDisplayConfiguration` which maps aspect ratios to optimal orthographic sizes. If no matching ratio is found, the camera keeps its scene-authored size. |

## How it works

`GameDisplayConfiguration` (in `Configuration/`) holds a list of aspect-ratio → orthographic-size pairs. At `Start()`, the controller rounds the current screen ratio, finds the closest match, and applies the corresponding size. This ensures the play field is framed correctly across different devices without letterboxing or clipping.

The controller is registered via `RegisterEntryPoint<OrthogonalSizeCameraController>()` in `GameLifetimeScope`. It receives `GameDisplayConfiguration` through constructor injection and uses `Camera.main` to apply the size.
