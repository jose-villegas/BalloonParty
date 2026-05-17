# LevelUpTrailEffect — Plan

## Goal

When a balloon pop will complete a level-up, play a cinematic that follows the score trail from the balloon to the progress bar, keeping the player's attention on the moment that matters.

## Cinematic Timeline

| Phase | Time.timeScale | Camera | Trigger |
|---|---|---|---|
| **Normal play** | 1 | Base position, base zoom | — |
| **Balloon scored** (WillLevelUp = true) | 1 → 0.3 (tween, 0.15s unscaled) | Zoom in, start following the trail transform | `BalloonScoredMessage` |
| **Trail flying** | 0.3 | Smoothly tracks the actual trail orb from balloon to bar | `Update` reads `_trackedTrail.position` |
| **Trail arrives at bar** | 0.3 → 0 (set by CheckLevelUp) | Holds last trail position (near bar) | `ScoreTrailArrivedMessage` → `EndSlowMotion` |
| **Level-up popup visible** | 0 | Frozen, still focused near bar | `ScoreLevelUpMessage` → popup appears |
| **Continue pressed** | 0 → 1 (tween, 0.35s unscaled) | Smooth pan back to base + zoom out | `LevelUpDismissedMessage` → `Restore` |
| **Restore complete** | 1 | Base position, base zoom | `OnRestoreComplete` → `Navigation.TransitionTo(Game)` |
| **Back to play** | 1 | Base position, base zoom | Navigation → Game |

## Screen-Space UI Compensation (Option 2)

The progress bars live on a **Screen Space** Canvas — they don't move when the camera pans.
The trail flies toward a world-space position that was correct at the default camera view.
When the camera pans, that world position no longer visually lines up with the bar.

### How the compensation works

1. **`Update`** — DOTween writes the trail's interpolated position (heading toward the original fixed target).
   The camera lerps toward the trail to create the panning effect.
2. **`LateUpdate`** — we shift `_trackedTrail.position` by `cameraDelta`
   (`_camera.position − _basePosition`, z zeroed).

Because DOTween stores start/end internally and does **not** read the transform each frame,
our shift is naturally overwritten next `Update`. We re-apply it every `LateUpdate`, so each
rendered frame shows the compensated position.

For an orthographic camera that only translates (no rotation), the math is exact:
the bar's effective world position under the panned camera is `barOriginal + cameraDelta`,
and the shifted trail is at `trailDOTween + cameraDelta` — both offset by the same amount,
keeping the trail on a path that ends at the bar's visual position.

## Key Systems

- **ScoreController** — `WillLevelUp` predicts the level-up including in-flight pending points
- **ScoreTrailService** — stores last spawned trail per-color; `GetLastSpawnedTrail` lets the effect grab a direct `Transform` reference
- **LevelUpTrailEffect** — orchestrates slow-mo, camera follow, canvas scale, and restore
- **LevelUpPopUp** — publishes `LevelUpDismissedMessage` on Continue; no longer owns timeScale restore
- **OrthogonalSizeCameraController** — disabled during the cinematic so it doesn't overwrite the zoom tween; re-enabled on restore

## Messages

| Message | From | To | Purpose |
|---|---|---|---|
| `BalloonScoredMessage` | ScoreController | LevelUpTrailEffect, ScoreTrailService | Detect level-up, spawn trails |
| `ScoreTrailArrivedMessage` | ScoreTrailService | LevelUpTrailEffect, ScoreController | Trail reached bar — freeze camera, check level-up |
| `LevelUpDismissedMessage` | LevelUpPopUp | LevelUpTrailEffect | Continue pressed — restore camera + timeScale |

## Open / Tuning

- `_cameraPanWeight` (0.7) — how far the camera offsets toward the trail (0 = stay at base, 1 = center on trail)
- `_cameraFollowSpeed` (5) — how quickly the camera catches up to the trail each frame
- `_slowTimeScale` (0.3) — how slow the slow-mo feels
- `_slowDownDuration` (0.15s) — how fast slow-mo ramps in
- `_restoreDuration` (0.35s) — how fast camera + timeScale return to normal after Continue
- `_zoomAmount` (0.5) — orthographic size reduction during the cinematic
- Trail `ScorePointTraceDuration` is in scaled time — at timeScale 0.3 the trail flies ~3× slower in real time

### Canvas Scale

The progress bars live on a Screen Space - Camera canvas using the same orthographic camera.
Unity auto-sizes the canvas plane to fill the camera view regardless of `orthographicSize`,
so changing ortho size alone doesn't visually scale the UI. The effect manually tweens the
canvas `localScale` by the zoom ratio (`baseOrtho / zoomedOrtho`) so the UI scales in sync
with the world content. `OrthogonalSizeCameraController` is disabled during the cinematic
to prevent its `LateUpdate` from overwriting the zoom tween.

## Edge Cases

- **Subscription order**: MessagePipe doesn't guarantee order. `LevelUpTrailEffect` may process
  `BalloonScoredMessage` before `ScoreTrailService` spawns the trail. The effect lazily acquires
  the tracked trail in `Update` if `GetLastSpawnedTrail` returned `null` initially.
- **Non-tracked trails**: Other trails from the same burst or different colors are NOT compensated
  and may visually miss the bar. During slow-mo the player's focus is on the tracked trail, so
  the discrepancy is unlikely to be noticed.
