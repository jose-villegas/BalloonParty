# Cinematics

Orchestrates cinematic sequences during gameplay — slow-motion, camera zoom/pan, trail tracking, and restore transitions.

## Architecture

The system follows a **producer → director → scene** pattern:

| Role | Type | Responsibility |
|---|---|---|
| **Director** (`CinematicDirector`) | Plain C# `ITickable` + `ILateTickable` | Runs the active scene's tick callbacks, manages `Cinematic.Begin`/`End` lifecycle across the full session |
| **Scene** (`CinematicScene`) | Plain C# (sealed) | Value object holding `OnBegin`, `OnTick`, `OnLateTick`, `OnEnd` callbacks that define a single phase |
| **Producer** (`LevelUpTrailEffect`) | `MonoBehaviour` | Detects level-up conditions, creates scenes, and signals the director when to start/complete them |

The director does not know about level-ups, trails, or cameras. It only knows how to play scenes and manage the `Cinematic` static state. Producers define all domain-specific behaviour via scene callbacks.

## Level-Up Trail Flow

`LevelUpTrailEffect` produces two scenes per level-up:

### Scene 1 — Pan-In

| Step | What happens |
|---|---|
| **Trigger** | `ScorePointMessage` received, `ScoreController.WillLevelUp` returns true — checks projected progress for **all** colors so the cinematic registers even when multiple colors reach the threshold simultaneously. The tipping trail ID comes directly from the message's `Score` and `Level` |
| **Setup** | Builds `TrailId(color, score, level)` from the message and registers it with `ScoreTrailService.TrackTrail`. If the trail already spawned (subscription ordering — ScoreTrailService processes first), the retroactive path pauses it, switches its tweens to unscaled time, and fires the callback immediately |
| **Spawn callback** | `OnTippingTrailSpawned` begins the cinematic, calls `PauseTrailsAbove` for next-level in-flight trails, starts slow-motion + camera zoom, and resumes the tipping trail. Pre-tipping trails (any color) keep flying. Next-level spawns are gated by `Cinematic.IsPlaying && NextLevel` |
| **Tick** | Camera pans toward the tipping trail's world position each frame. Trail scale is driven by `_trackedTrailScaleCurve` evaluated over unscaled time |
| **End trigger** | `ScoreTrailArrivedMessage` matches tipping `TrailId`. `ScoreController` processes the arrival first — sets `_levelProgress` to the trail's score (which equals `requiredPoints`), triggering `CheckLevelUp` → `ScoreLevelUpMessage`. Then the effect completes the scene |
| **End callback** | No-op — the level-up popup appears independently from `ScoreLevelUpMessage` and pauses the game (`Time.timeScale = 0`) |

### Scene 2 — Restore

| Step | What happens |
|---|---|
| **Trigger** | `LevelUpDismissedMessage` received (player pressed Continue) |
| **Prepare** | Tweens `Time.timeScale` from 0 back to 1 and camera back to base position/size (all using unscaled time) |
| **End trigger** | Restore tween completes (via `OnComplete` callback) |
| **End callback** | Re-enables `OrthogonalSizeCameraController`, calls `director.EndCinematic` (resumes post-tipping trails via `OnCinematicEnd`, unblocks trail spawning), resets `_sessionActive`, transitions navigation to `Game` |

### Trail Identity

Each score trail is identified by a `TrailId(Color, Score, Level)`. Color is required because `_projectedProgress` is per-color, so two colors can produce the same numeric score. Level prevents post-reset collisions when progress restarts from 1. The tipping trail's identity is `TrailId(tippingColor, requiredPoints, currentLevel)`.

### Selective Pause

Only next-level trails are paused during the cinematic — **any trail** (regardless of color) with `Level > tippingLevel`. This prevents next-level trails from arriving and overwriting current-level `_levelProgress` values in `ScoreController`. Pre-tipping trails from any color keep flying so their progress bar arrivals complete naturally. `LevelUpTrailEffect` calls `PauseTrailsAbove(tippingTrailId)` explicitly after `BeginCinematic`; the generic `OnCinematicBegin` callback is a no-op. `OnCinematicEnd` resumes only the cinematically-paused trails. New next-level trail spawns are gated by the `NextLevel` flag on each `ScorePointMessage` — current-level trails from all colors spawn freely so `CheckLevelUp` can confirm every color's progress.

### Tipping Trail Lifecycle

1. `LevelUpTrailEffect` receives the tipping `ScorePointMessage` and calls `TrackTrail(id, callback)` on `ScoreTrailService`
2. Due to subscription ordering (`IStartable` before `MonoBehaviour`), the trail typically spawns before `TrackTrail` is called. The retroactive path in `TrackTrail` pauses the trail, switches its tweens to unscaled time via `DOTween.TweensByTarget`, and fires the callback immediately
3. If the trail is delayed (scatter `groupIndex > 0`), the forward path applies — trail is paused at spawn with `useUnscaledTime` set from creation
4. The callback starts the cinematic, selectively pauses post-tipping trails, and resumes the tracked trail
5. Pre-tipping trails keep flying; next-level spawns are gated by `Cinematic.IsPlaying && NextLevel`
6. On trail arrival, `ClearTrackedTrail` resets the tracking state

## Camera Reference

`LevelUpTrailEffect` holds the camera as a `[SerializeField]` — assigned in the Inspector. This avoids `Camera.main` (which is fragile on Android where the activity pause/resume cycle can destroy and recreate the main camera, leaving a stale destroyed-object reference that passes C# `?.` checks but throws on access).


## Interactions

- **`ScoreController`** — queried for `WillLevelUp` (all-projected check); on tipping trail arrival, sets `_levelProgress` to the trail's score (= `requiredPoints`), which triggers `CheckLevelUp` → level-up
- **`ScoreTrailService`** — owns `TrackTrail`/`ClearTrackedTrail`/`PauseTrailsAbove`/`ResumeTrail` API for the producer; retroactive `TrackTrail` switches tweens to unscaled time; resumes cinematically-paused trails on `OnCinematicEnd`; gates next-level spawns via `Cinematic.IsPlaying && NextLevel`
- **`Cinematic`** (static, `Shared/GameState/`) — the director calls `Begin`/`End` to broadcast state changes to all `ICinematicAware` services
- **`LevelUpPopUp`** — subscribes to `ScoreLevelUpMessage` independently; pauses the game (`Time.timeScale = 0`) on appear; publishes `LevelUpDismissedMessage` on Continue
- **`OrthogonalSizeCameraController`** — disabled during cinematic to prevent conflicting camera size changes
