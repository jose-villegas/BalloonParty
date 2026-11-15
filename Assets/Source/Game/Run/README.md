# Run

Owns the **run lifecycle** — the run-based progression model where a loss ends the run and
resets it. A run starts at level 1, and only a meta record (best level / best score) survives
across runs. The one exception is a run the app was closed or killed in: it's saved at each level
boundary and offered as **Continue** on the next launch (see Resuming an interrupted run below).

## Contents

| File | What it does |
|---|---|
| `RunController` | Entry point (`IStartable`/`IDisposable`) singleton. `EndRun()` snapshots the final level/score, commits them to `IRunMeta`, publishes `GameOverMessage`, and transitions to `NavigationState.GameOver`. `RestartRun(bool resetBoard = true)` invokes every `IRunResettable` in ascending `ResetOrder` — `resetBoard: false` skips implementations of `IBoardResettable` (e.g. `BoardClearController`) so a transition cinematic can swap the board itself while run state (score/level/health/counters) still resets; the loss-dismiss transition uses this. It then publishes `RunResetMessage` (for views that can't reset reactively or live outside the reset graph, e.g. progress bars and the thrower's projectile), then transitions back to `NavigationState.Game`. `ResumeRun()` and `RestoreLoss(level, score)` pick up a saved run (see Resuming an interrupted run). Loss triggers reach it two ways: the dev cheat calls `EndRun()` directly; the player-HP pool raises an `EndRunRequestedMessage` which `RunController` subscribes to in `Start`. (A message rather than a direct call, because a loss trigger that is itself an `IRunResettable` — like `PlayerHealthController` — can't depend on `RunController` without forming a DI cycle through the resettable collection this controller resolves at construction.) |
| `IRunResettable` | Implemented by services holding per-run state that must be cleared on restart. `RestartRun` runs them in ascending `ResetOrder` so teardown that must precede other resets (quiesce async work, return pooled actors, clear the grid) can order itself ahead of the rest |
| `RunResetOrder` | Named stages for `ResetOrder` — `Quiesce(0)` → `Board(20)` → `Derived(40)` → `Counters(60)` → `Score(100)` → `Respawn(120)` — so a new resettable picks a stage instead of guessing a magic number |
| `BoardClearController` | `IRunResettable` at the `Board` stage — broadcasts `BoardClearMessage` so every actor returns its pooled view and vacates its grid slot; MessagePipe publishes synchronously, so the board is empty when its `ResetRun` returns |
| `IBoardResettable` | Marker interface for `IRunResettable` implementations whose reset work is board teardown/population (clearing/spawning actors). `RunController.RestartRun(resetBoard: false)` skips these so a transition cinematic can swap the board itself while run state still resets |
| `IRunScore` | Read-only view of the run's `TotalScore` and `LevelStartScore` (what the level being played began with, which its retry and the run save start from), implemented by `ScoreController`. When a run ends `RunController` snapshots the score from here and the level from `ILevelProgress` (`Game/Level/`) |
| `IRetryState` | Read-only interface exposing retry state — `IsUnlimited`, `RetriesRemaining` (`int.MaxValue` while unlimited), `RetriesUsed`, `RetryLevel` (the level the next restart should begin at; zero means a fresh run) and `RetryWithBonusCard` (the staged retry deals a bonus card; cleared with `RetryLevel`) and `Resuming` (the saved run the staged retry resumes, or null; cleared with `RetryLevel`) |
| `RetryTracker` | An `IRunResettable` at the `Score + 10` stage that tracks per-run retries. `IRunConfig.MaxRetries` of 0 or less means unlimited (the shipped asset is 0: a loss can always be retried from the level reached, with the same seeded mix). `PrepareRetry(level, withBonusCard)` (its `IRetryStaging` side) stages a level to retry at, and whether that attempt gets a bonus card; on reset, consumes a retry if one was staged, otherwise clears the counter for a fresh run, so a fresh run also resets the ad gate. Retries are counted even when unlimited, because the ad gate reads the count. `PrepareResume(snapshot)` (its `IRunResumeStaging` side) stages a saved run the same way, and on reset sets the retries used to the saved count rather than counting one. Exposed as `IRetryState` (read), `IRetryStaging` and `IRunResumeStaging` (write) |
| `IRunSeed` / `RunSeed` | The run's own roll, mixed into which levels are on the ground (`Configuration/README.md`, `ScenarioSchedule`), so each run meets its first meadow somewhere between levels 6 and 8. An `IRunResettable` at the `Board + 10` stage: a fresh run rolls a new seed, a retry keeps it, a resume takes the saved one, so the retried level is set where it was. It runs before `LevelDifficultyResolver` reads it (`ScenarioController` reads it on `RunResetMessage`, after every reset), and before `RetryTracker` clears the retry level that tells the two apart |
| `IRetryStaging` | The write side of retry state: `PrepareRetry(level, withBonusCard)`. Only `FailureOffers` holds it, so nothing can stage a retry, or a bonus card, around an ad |
| `IFailureOffers` / `FailureOffers` | What a loss offers: `Current` is a `FailureOffer` (`CanRetry`, `RetryNeedsAd`, `RetriesLeft`, -1 when unlimited, and `FreeRetries`/`FreeRetriesLeft`, the retries the run gets without an ad: the free ones, never more than a capped run has, or with no ad gate every retry of a capped run; the game-over screen pins one heart per free retry). A run's first `IRunConfig.RetriesBeforeAd` retries are free; each one after needs a rewarded ad (0 = never). The shipped asset gives 3 free retries, and with `MaxRetries` at 0 the ad-paid ones have no cap (owner, 2026-11-09). Until an ad SDK is in, `NoAdService` counts every ad as watched, so past the free three the button only changes its label. `TryRetryAsync(level, ct)` settles the cost and stages the retry in one step: it shows the ad when needed and stages only if the player didn't skip it. An ad that can't be shown (`Unavailable`, or the SDK throwing) grants the retry, since that isn't the player's doing. Nothing is staged once `ct` is cancelled. `CanBonusRetry` offers a second way back: `TryBonusRetryAsync(level, ct)` plays a rewarded ad (`AdPlacement.BonusRetry`) and, only if it's watched through, stages the retry with a bonus card. A reward needs its ad watched, so a skip, no fill or an SDK error stages nothing and leaves the plain retry on offer; the one ad also pays a gated retry. It's offered only while `IBonusCardSource.CanDealBonus`, so the ad is never sold for a card that can't be dealt. The only place the failure flow meets ads, so the game-over screen never talks to an ad SDK |
| `IBonusCardSource` | `CanDealBonus`: whether a bonus card could be dealt right now. Implemented by `Game/Upgrades/RunUpgrades`, which deals the card at the retry's reset |
| `IAdService` / `NoAdService` / `AdPlacement` / `AdResult` | The ad SDK seam. The game shows ads only on failure, so the one call is `ShowRewardedAsync(placement)`, which reports `Watched`, `Skipped` or `Unavailable`; what each costs the player is `FailureOffers`' call. No SDK yet: `NoAdService` reports every ad as watched. An ad never buys score or the ball cap. Before a real SDK goes in, decide what a watched ad whose screen was left mid-ad earns: `FailureOffers` stages nothing once `ct` is cancelled (a stale retry would hijack the next reset), so today the reward is lost, which `NoAdService` never triggers but a real ad could |
| `GameOverPresentationGate` | An `IReadyGate` holding the GameOver screen closed until the loss cinematic finishes. Starts closed and is opened by the cinematic producer's `Open()` call; ensures the screen never displays before the beat completes, even on paths with no cinematic beat |
| `IRunMeta` / `RunMeta` | The only state that survives a run — best level and best score, persisted to `PlayerPrefs` (`BestLevel`, `BestScore`). `RecordRun(level, score)` keeps the max of each independently and persists on change. Loaded for display, never fed back into a live run |
| `RunSnapshot` | The saved run: the level, the seed, the score and hearts that level began with, the held cards by title with their stacks (and the free cards dealt so far, and the live attempt's bonus card), the retries used, and whether it was saved on the game-over screen (with the final score). `TryParse` drops a corrupt save, one of another `CurrentVersion`, or one with impossible values (no level, a level begun with no hearts) with a `Log.Warn`. Progression, like `RunMeta`, never telemetry |
| `IRunSave` / `PlayerPrefsRunSave` | Where the snapshot lives between launches: one JSON string in `PlayerPrefs` (`RunSave`), saved at once on each write, since a kill gives no warning. A bad save is deleted the first time it's read. Registered in `AppLifetimeScope`, so the launch screen can read it |
| `RunSaveRecorder` | Keeps the save at the last level boundary. As a level begins, after `LevelTransitionCompletedMessage` and after a retry's reset, it saves what the level begins with; on `GameOverMessage` it saves the game-over screen; a fresh run clears it. It reads whether a reset was fresh, a retry or a resume at `Score + 5`, before `RetryTracker` clears that, and saves on `RunResetMessage`, once the retry count is updated: the retry just spent is in the save. Once a loss is saved, only a reset saves a level start again, because a loss deferred past a level-up arrives inside the transition's own handlers. A resume's reset saves nothing, so a game-over save stays one until the screen is back |
| `IRunResumeRequest` / `RunResumeRequest` | The launch screen's Continue, handed to the game scope (the two scopes share no message bus). App scope; the request waits until the game takes it, even if the game scene finishes loading after the tap |
| `IRunResumeStaging` | The write side of a resume: `PrepareResume(snapshot)`, implemented by `RetryTracker`. Only `RunResumeController` holds it |
| `RunResumeController` | Takes the request while the launch screen is up: loads the save, stages it, and calls `RunController.ResumeRun`. A run saved on the game-over screen goes back to it on the first `Tick` after navigation reaches `Game` (a frame later rather than inside the navigation change, so the two transitions don't nest). Leaving the launch screen without a resume (Play) is a new run, and clears the save |

## Loss flow

```
trigger → RunController.EndRun() → (loss cinematic) → GameOver screen
        → [Restart] → RunController.RestartRun() → Game
        → [Retry]   → IFailureOffers.TryRetryAsync(level): ad if gated, then stages the retry
                    → RunController.RestartRun() → Game, same level
        → [Retry + bonus card] → IFailureOffers.TryBonusRetryAsync(level): rewarded ad, then stages
                    the retry with a bonus card → RunController.RestartRun() → Game, same level,
                    RunUpgrades deals the bonus card
```

Reset happens on **restart**, not on GameOver entry, so the GameOver screen can still show the
final score.

## Resuming an interrupted run

A phone call or the OS killing the app would otherwise cost the whole run. The run is saved at level
boundaries only: a resume replays the saved level from its start, so there's no mid-level board to
restore.

```
launch → save exists → the Continue balloon drifts about beside Play
       → LaunchContinueController: IRunResumeRequest.Request(), then the Play ascent
       → RunResumeController (still on Launch): IRunSave.TryLoad → RetryTracker.PrepareResume(snapshot)
                                               → RunController.ResumeRun()
       → ascent ends → Game, the first spawn lays out level N
       → saved on the game-over screen? next Tick: RunController.RestoreLoss → GameOver, same offer
```

The resume **reuses the retry path**. `PrepareResume` sets `RetryLevel` to the saved level, so every reader
of a retry (level, difficulty, seed, cards, score) sets the level up as its retry would, and
`IRetryState.Resuming` carries the snapshot for the parts a retry doesn't keep: `RunSeed` takes the saved
seed, `ScoreController` the saved level-start score, `PlayerHealthController` the saved hearts, and
`RunUpgrades` the saved cards. `RetryTracker` then sets the retries used to the saved count instead of
counting one more: **a resume spends no retry, and can't buy back the ad gate**, since quitting and
relaunching brings back the same count. Killed on the game-over screen, the player comes back to it,
with the same `FailureOffers.Current`.

`ResumeRun` resets everything but the board and stays on `Launch`: nothing has spawned before the first
`Game` (the first spawn waits for it), so the held spawn lays out the resumed level, and resetting the
board here would spawn it twice. Its `RunResetMessage` says the board is settled, since nothing waits on a
cinematic. `RestoreLoss` publishes `GameOverMessage` with `Restored` set, without `RecordRun`: the loss was
recorded, sounded and scored when it happened, so the loss cinematic skips its beat (the screen shows bare,
and a dismiss restarts at once), the game-over sting stays quiet, and telemetry writes nothing for it.

A save is dropped when the player starts over (a fresh restart) or presses Play instead of Continue.
Playing the Game scene directly in the Editor leaves `Launch` without a resume too, so it clears the save. A loss arriving during a `BlocksLoss` cinematic or the `LevelUp` state is **deferred,
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
