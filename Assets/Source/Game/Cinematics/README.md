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
| **Trigger** | `BalloonScoredMessage` received, `ScoreController.WillLevelUp` returns true |
| **Setup** | Registers the tipping trail identity with `ScoreTrailService.TrackTrail`. No visual changes yet — slow-motion and camera effects start only when the tracked trail is actually spawned |
| **Spawn callback** | `ScoreTrailService` spawns trails sequentially. When the tipping trail (score = required points) spawns, it is paused immediately and `LevelUpTrailEffect.OnTippingTrailSpawned` fires. This begins the cinematic: pauses all other trails, starts slow-motion + camera zoom, resumes only the tipping trail (with unscaled time so it ignores timeScale). Remaining trail spawns are gated behind `Cinematic.IsPlaying` |
| **Tick** | Camera pans toward the tipping trail's world position each frame |
| **End trigger** | `ScoreTrailArrivedMessage` matches tipping score |
| **End callback** | No-op — the level-up popup appears independently from `ScoreController`'s `ScoreLevelUpMessage` |

### Scene 2 — Restore

| Step | What happens |
|---|---|
| **Trigger** | `LevelUpDismissedMessage` received (player pressed Continue) |
| **Prepare** | Tweens `Time.timeScale` back to 1 and camera back to base position/size |
| **End trigger** | Restore tween completes (via `OnComplete` callback) |
| **End callback** | Re-enables `OrthogonalSizeCameraController`, calls `director.EndCinematic` (resumes all paused trails, unblocks trail spawning), transitions navigation to `Game` |

### Trail Identity

Each score trail is identified by its `int score` value — the level progress value it represents within the current level. `ScoreController` maintains a single projected progress counter that advances immediately on balloon pop, so each trail from the same balloon gets a unique score even for multi-point pops. The tipping trail is simply the one with `score == requiredPoints`. `ScoreTrailService.GetTrailColor(score)` provides the reverse lookup when color is needed.

### Tipping Trail Lifecycle

1. `LevelUpTrailEffect` calls `TrackTrail(score, callback)` on `ScoreTrailService`
2. `ScoreTrailService` spawns trails sequentially — when the matching trail spawns, it pauses it and fires the callback
3. The callback starts the cinematic, which pauses all other trails and resumes only the tracked trail with unscaled time
4. `SpawnTrailsAsync` gates remaining spawns behind `Cinematic.IsPlaying`, so no new trails appear during the cinematic
5. On trail arrival, `ClearTrackedTrail` resets the tracking state

## Interactions

- **`ScoreController`** — queried for `WillLevelUp` and `GetRequiredPoints`; uses `_projectedProgress` for level-up threshold so the tipping trail's single arrival triggers level-up even when earlier trails from the same pop are still paused
- **`ScoreTrailService`** — owns `TrackTrail`/`ClearTrackedTrail` API for the producer; spawns trails with `useUnscaledTime` for the tracked trail; implements `ICinematicAware` to pause/resume all trails on `Cinematic.Begin`/`End`; gates `SpawnTrailsAsync` on `Cinematic.IsPlaying`
- **`Cinematic`** (static, `Shared/GameState/`) — the director calls `Begin`/`End` to broadcast pause/resume to all `ICinematicAware` services
- **`LevelUpPopUp`** — subscribes to `ScoreLevelUpMessage` independently; publishes `LevelUpDismissedMessage` on Continue
- **`OrthogonalSizeCameraController`** — disabled during cinematic to prevent conflicting camera size changes
