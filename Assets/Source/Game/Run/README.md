# Run

Owns the **run lifecycle** — the run-based progression model where a loss ends the run and
resets it. A run starts at level 1 every session; only a meta record (best level / best score)
survives across runs.

## Contents

| File | What it does |
|---|---|
| `RunController` | Entry point (`IStartable`/`IDisposable`) singleton. `EndRun()` snapshots the final level/score, commits them to `IRunMeta`, publishes `GameOverMessage`, and transitions to `NavigationState.GameOver`. `RestartRun()` invokes every `IRunResettable` in ascending `ResetOrder`, then transitions back to `NavigationState.Game`. Loss triggers reach it two ways: the dev cheat calls `EndRun()` directly; the player-HP pool raises an `EndRunRequestedMessage` which `RunController` subscribes to in `Start`. (A message rather than a direct call, because a loss trigger that is itself an `IRunResettable` — like `PlayerHealthController` — can't depend on `RunController` without forming a DI cycle through the resettable collection this controller resolves at construction.) |
| `IRunResettable` | Implemented by services holding per-run state that must be cleared on restart. `RestartRun` runs them in ascending `ResetOrder` so teardown that must precede other resets (quiesce async work, return pooled actors, clear the grid) can order itself ahead of the rest. `ScoreController` implements it at order 100 (no dependencies) |
| `IRunScore` | Read-only view (`Level`, `TotalScore`) that `RunController` snapshots when a run ends. Implemented by `ScoreController` |
| `IRunMeta` / `RunMeta` | The only state that survives a run — best level and best score, persisted to `PlayerPrefs` (`BestLevel`, `BestScore`). `RecordRun(level, score)` keeps the max of each independently and persists on change. Loaded for display, never fed back into a live run |

## Loss flow

```
trigger → RunController.EndRun() → (loss cinematic) → GameOver screen
        → [Restart] → RunController.RestartRun() → Game
```

Reset happens on **restart**, not on GameOver entry, so the GameOver screen can still show the
final score. `EndRun()` is suppressed unless the game is in `NavigationState.Game` with no
cinematic playing — GameOver and the level-up cinematic must never overlap.

## Testability seams

`RunController` reaches navigation and cinematic state through `INavigation` and `ICinematicState`
(`Shared/GameState/`) rather than the static `Navigation` / `Cinematic`, so the whole lifecycle is
unit-testable with substitutes.

## Interactions

- **`GameOverMessage`** — published by `RunController.EndRun()`; consumed by the GameOver screen
  and (later) the loss cinematic.
- **`ScoreController`** (`Game/Score/`) — registered as `IRunScore` (read final score) and
  `IRunResettable` (reset on restart).
- **`INavigation` / `ICinematicState`** (`Shared/GameState/`) — injectable seams over the static
  `Navigation` and `Cinematic`.
