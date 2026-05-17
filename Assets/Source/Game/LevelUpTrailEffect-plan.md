# LevelUpTrailEffect — Cinematic Plan

## Goal

When a score trail will tip a level-up, play a cinematic: slow time, zoom the camera, and follow that single trail to the progress bar.

## Current Problems

The effect relies on `GetLastSpawnedTrail` to find the tipping trail by color name. This is fragile:

- **Multi-point balloons** spawn trails with a stagger delay. The tipping trail may not exist yet when the cinematic wants to capture it. A deferred `_pendingCinematicStart` flag works around this but adds complexity.
- **Race with completion** — `_lastSpawnedTrails.Remove` in the trail's completion callback can delete a newer trail's entry. Partial fix (identity check) is in place but the dictionary-per-color model is fundamentally 1:1.
- **Non-pop score sources** — items (bomb, laser, lightning) publish `BalloonHitMessage` which becomes `BalloonScoredMessage`. Trails from these hits work identically, but `WillLevelUp` / `PointsNeededForLevelUp` count arrivals by color without knowing which specific trail tips the level-up. Multi-hit items can trigger many trails for the same color in quick succession.
- **Counting arrivals** — `_arrivalsBeforeTip` counts per-color arrivals to guess which trail is the tipper. This breaks when trails from different pops (or items) for the same color interleave.

## Proposed Fix — Individual Trail Identity

### Core idea

Each score trail gets a unique ID at spawn time. The trail carries its color and a single-point score. `ScoreTrailArrivedMessage` includes the trail ID. This lets any consumer identify exactly which trail arrived and what score it carried.

### Trail identity

```
TrailId — int, auto-incremented per spawn in ScoreTrailService
```

Each trail maps to:
- `colorName` — which color bar it targets
- `score` — always 1 (one trail = one point)
- `Transform` — the flying orb

`ScoreTrailService` maintains a `Dictionary<int, TrailInfo>` of in-flight trails. On arrival, the trail publishes its ID and is removed from the dictionary.

### Updated messages

| Message | Fields | Change |
|---|---|---|
| `ScoreTrailArrivedMessage` | `int TrailId, string ColorName, Vector3 WorldPosition` | Add `TrailId` |

### How WillLevelUp identifies the tipping trail

When `WillLevelUp` returns true for a `BalloonScoredMessage` with N points:

1. Query `PointsNeededForLevelUp(colorName)` → returns M (based on actual `_levelProgress`).
2. The tipping trail is the **M-th** trail spawned for this color from this pop.
3. `ScoreTrailService` returns the trail IDs it spawned for this message. `LevelUpTrailEffect` stores the ID at index M-1 as `_tippingTrailId`.
4. No need to count arrivals — just wait for `ScoreTrailArrivedMessage` with that specific ID.

If the M-th trail hasn't spawned yet (stagger delay), the effect enters slow-mo and waits. `ScoreTrailService` notifies via a callback or the effect polls by ID.

### Simplified flow

| Step | What happens |
|---|---|
| Balloon/item scores | `BalloonScoredMessage` published. `ScoreTrailService` spawns N trails, returns their IDs. |
| `LevelUpTrailEffect` receives message | `WillLevelUp` → true. Compute tipping index M-1. Store `_tippingTrailId`. |
| Tipping trail spawned | `LevelUpTrailEffect` grabs its Transform by ID. Begins cinematic (slow-mo + zoom). |
| Tipping trail arrives | `ScoreTrailArrivedMessage` carries matching ID. `EndSlowMotion`. `CheckLevelUp` triggers naturally. |
| Popup dismissed | `Restore` tweens camera + timeScale back. `Cinematic.End` resumes paused trails. `Navigation → Game`. |

### What this eliminates

**LevelUpTrailEffect:**
- `_arrivalsBeforeTip` counting
- `_waitingForTip` state
- `_pendingCinematicStart` deferred flag

**ScoreTrailService:**
- `_lastSpawnedTrails` per-color dictionary (replaced by per-ID lookup)
- `_lastSpawnedTrails.Remove` race condition
- `GetLastSpawnedTrail` method

**ScoreController:**
- `_pendingPoints` dictionary and all its bookkeeping
- `PointsNeededForLevelUp` method
- Pending projection logic in `WillLevelUp` — with trail IDs, the effect knows exactly which trail tips the level-up; no need to project pending counts at all. `WillLevelUp` simplifies to: "are all other colors complete, and would this color reach required with the added points?"

## Cinematic Timeline

| Phase | timeScale | Camera | Trigger |
|---|---|---|---|
| Normal play | 1 | Base | — |
| Tipping trail identified | 1 → slow | Zoom in, follow trail | `BalloonScoredMessage` + `WillLevelUp` |
| Trail flying | slow | Tracks trail orb | `Update` |
| Trail arrives | slow → 0 | Holds near bar | `ScoreTrailArrivedMessage` (matching ID) |
| Popup visible | 0 | Frozen | `ScoreLevelUpMessage` |
| Continue pressed | 0 → 1 | Pan back + zoom out | `LevelUpDismissedMessage` |
| Restore complete | 1 | Base | `OnRestoreComplete` |

## Screen-Space UI Compensation

Progress bars live on a Screen Space - Camera canvas. The trail's DOTween target is a world position that lines up with the bar at the base camera view. Panning shifts the camera, so LateUpdate shifts the tracked trail by `cameraDelta` to keep it aligned. DOTween overwrites position each Update; our shift is re-applied each LateUpdate.

## Key Systems

- **ScoreController** — `WillLevelUp` predicts level-up (only projects pending for the scored color; other colors must already be complete). `PointsNeededForLevelUp` returns arrivals needed based on actual `_levelProgress`.
- **ScoreTrailService** — spawns trails, assigns IDs, maintains `Dictionary<int, TrailInfo>` of in-flight trails. Exposes `GetTrailTransform(int trailId)` and returns spawned trail IDs for a given message.
- **LevelUpTrailEffect** — orchestrates slow-mo, camera follow, and restore. Identifies tipping trail by ID instead of counting arrivals.
- **LevelUpPopUp** — publishes `LevelUpDismissedMessage` on Continue.
- **OrthogonalSizeCameraController** — disabled during cinematic.
- **Cinematic / ICinematicAware** — static state + listener interface. `ScoreTrailService` pauses/resumes trails.

## Open / Tuning

| Parameter | Default | Purpose |
|---|---|---|
| `_slowTimeScale` | 0.3 | Slow-mo intensity |
| `_slowDownDuration` | 0.15s | Ramp-in speed |
| `_restoreDuration` | 0.35s | Restore speed |
| `_zoomAmount` | 0.5 | Ortho size reduction |
| `_cameraPanWeight` | 0.7 | Camera offset toward trail (0 = stay, 1 = center) |
| `_cameraFollowSpeed` | 5 | Camera lerp speed |

## Edge Cases

- **Trail spawns after cinematic request** — slow-mo starts immediately; cinematic guard deferred until trail transform is available via ID lookup.
- **Level-up without cinematic** — `WillLevelUp` returned false for all recent pops, but trails arrive and naturally trigger `CheckLevelUp`. `OnLevelUpDismissed` restores `timeScale = 1` and transitions to Game.
- **Cinematic already playing** — `OnBalloonScored` returns early if `_active || Cinematic.IsPlaying`.
