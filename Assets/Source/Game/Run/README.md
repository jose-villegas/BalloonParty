# Run

Owns the **run lifecycle** — the run-based progression model where a loss ends the run and
resets it. A run starts at level 1 every session; only a meta record (best level / best score)
survives across runs.

## Contents

| File | What it does |
|---|---|
| `RunController` | Entry point (`IStartable`/`IDisposable`) singleton. `EndRun()` snapshots the final level/score, commits them to `IRunMeta`, publishes `GameOverMessage`, and transitions to `NavigationState.GameOver`. `RestartRun(bool resetBoard = true)` invokes every `IRunResettable` in ascending `ResetOrder` — `resetBoard: false` skips implementations of `IBoardResettable` (e.g. `BoardClearController`) so a transition cinematic can swap the board itself while run state (score/level/health/counters) still resets; the loss-dismiss transition uses this. It then publishes `RunResetMessage` (for views that can't reset reactively or live outside the reset graph, e.g. progress bars and the thrower's projectile), then transitions back to `NavigationState.Game`. Loss triggers reach it two ways: the dev cheat calls `EndRun()` directly; the player-HP pool raises an `EndRunRequestedMessage` which `RunController` subscribes to in `Start`. (A message rather than a direct call, because a loss trigger that is itself an `IRunResettable` — like `PlayerHealthController` — can't depend on `RunController` without forming a DI cycle through the resettable collection this controller resolves at construction.) |
| `IRunResettable` | Implemented by services holding per-run state that must be cleared on restart. `RestartRun` runs them in ascending `ResetOrder` so teardown that must precede other resets (quiesce async work, return pooled actors, clear the grid) can order itself ahead of the rest |
| `RunResetOrder` | Named stages for `ResetOrder` — `Quiesce(0)` → `Board(20)` → `Derived(40)` → `Counters(60)` → `Score(100)` → `Respawn(120)` — so a new resettable picks a stage instead of guessing a magic number |
| `BoardClearController` | `IRunResettable` at the `Board` stage — broadcasts `BoardClearMessage` so every actor returns its pooled view and vacates its grid slot; MessagePipe publishes synchronously, so the board is empty when its `ResetRun` returns |
| `IBoardResettable` | Marker interface for `IRunResettable` implementations whose reset work is board teardown/population (clearing/spawning actors). `RunController.RestartRun(resetBoard: false)` skips these so a transition cinematic can swap the board itself while run state still resets |
| `IRunScore` | Read-only view of the run's `TotalScore`, implemented by `ScoreController`. When a run ends `RunController` snapshots the score from here and the level from `ILevelProgress` (`Game/Level/`) |
| `IRetryState` | Read-only interface exposing retry state — `IsUnlimited`, `RetriesRemaining` (`int.MaxValue` while unlimited), `RetriesUsed` and `RetryLevel` (the level the next restart should begin at; zero means a fresh run) |
| `RetryTracker` | An `IRunResettable` at the `Score + 10` stage that tracks per-run retries. `IRunConfig.MaxRetries` of 0 or less means unlimited (the shipped asset is 0: a loss can always be retried from the level reached, with the same seeded mix). `PrepareRetry(level)` (its `IRetryStaging` side) stages a level to retry at; on reset, consumes a retry if one was staged, otherwise clears the counter for a fresh run, so a fresh run also resets the ad gate. Retries are counted even when unlimited, because the ad gate reads the count. Exposed as `IRetryState` (read) and `IRetryStaging` (write) |
| `IRetryStaging` | The write side of retry state: `PrepareRetry(level)`. Only `FailureOffers` holds it, so nothing can stage a retry around the ad gate |
| `IFailureOffers` / `FailureOffers` | What a loss offers: `Current` is a `FailureOffer` (`CanRetry`, `RetryNeedsAd`, `RetriesLeft`, -1 when unlimited). A run's first `IRunConfig.RetriesBeforeAd` retries are free; each one after needs a rewarded ad (0 = never). `TryRetryAsync(level, ct)` settles the cost and stages the retry in one step: it shows the ad when needed and stages only if the player didn't skip it. An ad that can't be shown (`Unavailable`, or the SDK throwing) grants the retry, since that isn't the player's doing. Nothing is staged once `ct` is cancelled. The only place the failure flow meets ads, so the game-over screen never talks to an ad SDK |
| `IAdService` / `NoAdService` / `AdPlacement` / `AdResult` | The ad SDK seam. The game shows ads only on failure, so the one call is `ShowRewardedAsync(placement)`, which reports `Watched`, `Skipped` or `Unavailable`; what each costs the player is `FailureOffers`' call. No SDK yet: `NoAdService` reports every ad as watched. An ad never buys score or the ball cap |
| `GameOverPresentationGate` | An `IReadyGate` holding the GameOver screen closed until the loss cinematic finishes. Starts closed and is opened by the cinematic producer's `Open()` call; ensures the screen never displays before the beat completes, even on paths with no cinematic beat |
| `IRunMeta` / `RunMeta` | The only state that survives a run — best level and best score, persisted to `PlayerPrefs` (`BestLevel`, `BestScore`). `RecordRun(level, score)` keeps the max of each independently and persists on change. Loaded for display, never fed back into a live run |

## Loss flow

```
trigger → RunController.EndRun() → (loss cinematic) → GameOver screen
        → [Restart] → RunController.RestartRun() → Game
        → [Retry]   → IFailureOffers.TryRetryAsync(level): ad if gated, then stages the retry
                    → RunController.RestartRun() → Game, same level
```

Reset happens on **restart**, not on GameOver entry, so the GameOver screen can still show the
final score. A loss arriving during a `BlocksLoss` cinematic or the `LevelUp` state is **deferred,
never dropped** — `RunController` marks it pending and fires `EndRun()` the moment navigation
returns to `Game`. Outside those gates, `EndRun()` only commits from `NavigationState.Game`
(the GameOver state gate stops a second trigger from ending the run twice).

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
