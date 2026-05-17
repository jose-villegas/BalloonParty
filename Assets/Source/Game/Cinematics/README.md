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
| **Prepare** | Slow-motion tween starts (`Time.timeScale` → configured slow value), camera zooms in |
| **Tick** | Polls `ScoreTrailService` for the tipping trail (score = required points). Once found, calls `director.BeginCinematic` (pauses all other trails) and resumes just the tipping trail. Camera pans toward the trail's world position each frame |
| **End trigger** | `ScoreTrailArrivedMessage` matches tipping color + score |
| **End callback** | No-op — the level-up popup appears independently from `ScoreController`'s `ScoreLevelUpMessage` |

### Scene 2 — Restore

| Step | What happens |
|---|---|
| **Trigger** | `LevelUpDismissedMessage` received (player pressed Continue) |
| **Prepare** | Tweens `Time.timeScale` back to 1 and camera back to base position/size |
| **End trigger** | Restore tween completes (via `OnComplete` callback) |
| **End callback** | Re-enables `OrthogonalSizeCameraController`, calls `director.EndCinematic` (resumes all trails), transitions navigation to `Game` |

### Trail Identity

Each score trail is identified by a `(colorName, score)` tuple — the score value it represents within the current level. `ScoreController` maintains a projected progress counter that advances immediately on balloon pop, so each trail from the same balloon gets a unique score even for multi-point pops. The tipping trail is simply the one with `score == requiredPoints`.

## Interactions

- **`ScoreController`** — queried for `WillLevelUp` and `GetRequiredPoints`; independently handles `CheckLevelUp` and popup triggering via `ScoreLevelUpMessage`
- **`ScoreTrailService`** — queried for trail transforms (`GetTrailTransform`), told to resume specific trails (`ResumeTrail`); implements `ICinematicAware` to pause/resume all trails on `Cinematic.Begin`/`End`
- **`Cinematic`** (static, `Shared/GameState/`) — the director calls `Begin`/`End` to broadcast pause/resume to all `ICinematicAware` services
- **`LevelUpPopUp`** — subscribes to `ScoreLevelUpMessage` independently; publishes `LevelUpDismissedMessage` on Continue
- **`OrthogonalSizeCameraController`** — disabled during cinematic to prevent conflicting camera size changes

