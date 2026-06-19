# Display

Camera and screen-adaptation logic.

## Contents

| File | What it provides |
|---|---|
| `OrthogonalSizeCameraController` | MonoBehaviour — sets the camera's orthographic size based on the device's aspect ratio. Reads `GameDisplayConfiguration` (injected) which maps aspect ratios to optimal orthographic sizes. Has a serialized `Camera` reference to avoid `Camera.main` lookup issues during additive scene loading. When `_continuous` is enabled, updates every `LateUpdate` to react to resolution or orientation changes; otherwise applies once on `Start` |
| `CameraShakeService` | MonoBehaviour — punches the gameplay camera (`DOShakePosition`) on every `SpawnBlockedMessage` (a rejected balloon), then restores it. Captures the rest position once per burst so overlapping shakes don't drift, and **skips entirely while `ICinematicState.IsPlaying`** so it never fights the level-up cinematic's camera control. Serialized `Camera` reference plus inspector-tunable duration / strength / vibrato. Registered via `RegisterComponentInHierarchy<CameraShakeService>()`; place it on the camera GameObject |

## How it works

`GameDisplayConfiguration` (in `Configuration/`) holds a list of aspect-ratio → orthographic-size pairs. The controller rounds the current screen ratio, finds the closest match, and applies the corresponding size. This ensures the play field is framed correctly across different devices without letterboxing or clipping.

The controller is registered via `RegisterComponentInHierarchy<OrthogonalSizeCameraController>()` in `GameLifetimeScope` and `LaunchLifetimeScope`. Place it on the camera GameObject in each scene with the camera reference wired in the Inspector.
