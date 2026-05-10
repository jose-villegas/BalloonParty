# Display

Camera and screen-adaptation logic.

## Contents

| File | What it provides |
|---|---|
| `OrthogonalSizeCameraController` | Sets the camera's orthographic size at startup based on the device's aspect ratio. Reads `IGameConfiguration.DisplayConfiguration` which maps aspect ratios to optimal orthographic sizes. If no matching ratio is found, the camera keeps its scene-authored size. |

## How It Works

`GameDisplayConfiguration` (in `Configuration/`) holds a list of aspect-ratio → orthographic-size pairs. At `Start()`, the controller rounds the current screen ratio, finds the closest match, and applies the corresponding size. This ensures the play field is framed correctly across different devices without letterboxing or clipping.

The controller is registered via `RegisterComponentInHierarchy` in `GameLifetimeScope` and must be placed on the Main Camera GameObject in the scene.

