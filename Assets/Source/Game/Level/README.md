# Game/Level

Owns everything about the player's climb through levels: the current level and per-colour
progress, the **level-up ceremony** (detection → popup → Ascent), and the **per-level
difficulty mix** each level resolves to. Level logic lives here, not in `ScoreController` —
scoring only tallies points and hands them to `ILevelProgress`.

## Components

| File | Responsibility |
|---|---|
| `LevelController.cs` | The progression owner (plain C# `IStartable`/`IRunResettable`, implements `ILevelProgress`). Holds the current `Level`, per-colour confirmed + projected progress, and the `LevelUpPhase`. Detects a level-up, publishes `ScoreLevelUpMessage`, and drives the two-phase commit below. |
| `LevelUpPhase.cs` | The ceremony as one explicit state — `Playing → Pending → Transitioning → Playing`. Replaces the old scattered guard flags: a level-up is only *detected* in `Playing`, and every out-of-phase input (a second detection, a straggler trail, a duplicate dismissal) is rejected because no transition exists for it. |
| `ILevelProgress.cs` | Read surface of progression: `Level`, `Phase`, `GetRequiredPoints`/`GetProgress`, `WillLevelUp` (projected), `ClaimProgress` (the scoring write-back, capped per level, banking the excess), and `ExcessPoints`/`TotalExcessPoints` (the run-scoped banked excess). |
| `LevelDifficultyResolver.cs` | Resolves and caches the live per-level mix (implements `IActiveLevelParameters` + `ILevelThresholds`). On level-up it re-resolves `LevelParameters` for the new level, bridging range weights onto the balloon/item catalogs and computing the allowed-colour set. Also owns the points-required formula. |
| `IActiveLevelParameters.cs` | Single read surface for the live difficulty mix (`Current`). Never read `ILevelPacingConfiguration` directly. |
| `ILevelThresholds.cs` | The per-level score goal (`PointsRequiredForLevel(level)`) for any level, not just the active one. |
| `LevelTransitionController.cs` | The **Ascent** — the level-transition cinematic. Phase-driven (see below); holds `PauseSource.LevelTransition` for the whole sequence. |

## The level-up ceremony (two-phase commit)

Progress lands via **score trails**: `ScoreController` publishes points, and each trail's arrival
fires `ScoreTrailArrivedMessage`. `LevelController.OnTrailArrived` confirms progress (capped at the
projected claim so a previous-level straggler can't re-inflate it) and calls `CheckLevelUp`.

The level does **not** advance the moment the bars fill — the ceremony is a two-phase commit so the
animations have a stable state to play against:

1. **`Playing → Pending`** — `CheckLevelUp` sees every allowed colour confirmed at the threshold (and
   the run isn't ending — gated on `NavigationState.Game` + `!ILossForecast.LossImminent`, the
   *no level-up after a loss* rule). It snapshots the completed colours, publishes
   `ScoreLevelUpMessage(newLevel, colours)`, transitions nav to `LevelUp`, and records the pending
   level. **The `Level` integer and progress have not changed yet.** The phase is the single
   reentrancy guard — no second detection can fire until the ceremony resolves.
2. **`Pending → Transitioning`** — the player dismisses the popup (`LevelUpDismissedMessage`). *Now*
   `Level` advances to the pending value and progress resets to zero.
3. **`Transitioning → Playing`** — the Ascent reports it has settled (`LevelTransitionCompletedMessage`),
   so scoring reopens.

`Phase` is the cue the rest of the ceremony reads instead of inferring from nav/pause: the popup shows
on `ScoreLevelUpMessage`, and the Ascent (`LevelTransitionController`) starts on
`Phase → Transitioning` — deterministic, fires exactly once, no extra re-entrancy flag.

### Banked excess

`ClaimProgress` caps `granted` at one level's worth per colour — a pop landing a colour past its
threshold still only grants the remaining room, and progress still resets to zero at level-up. The
excess past the cap doesn't feed back into progress, but it isn't discarded either: it's banked
per colour (`_bankedExcess`, exposed as `ExcessPoints`/`TotalExcessPoints`) as a run-scoped running
total that accumulates across levels and is logged (editor/dev builds) as it accrues. The bank is
cleared only on a run reset (`ClearRunState`), never at level-up, and the dev cheat
(`CheatState.BlockLevelUp`) never banks because it doesn't advance real progress. Nothing in
gameplay or UI reads the bank today — it's reserved for a future per-level currency system
(Balatro-style) feeding the level-up popup.

## The Ascent (level transition)

`LevelTransitionController` subscribes to `Phase → Transitioning` and holds
`PauseSource.LevelTransition` for the whole run so the thrower and spawn-loss checks stay inert until
the reveal is ready. Its sequence (see the file for the full choreography): wait for any in-flight
cinematic + the overflow drain → pop the old level's balloons in a **slow-mo anti-diagonal wave** from
the outermost populated bands inward → slide the **outgoing content** out the bottom on a shared
conveyor while the **new** scenario descends into place, then reopen scoring. The Ascent moves the
shared `ScenarioContentRoot`, not the camera; the camera un-zoom is tweened here (synced to the pop
wave), *not* by `LevelUpCinematic`. Its tuning is `ICinematicsSettings.LevelAscend` — see
`Game/Cinematics/README.md` and `Configuration/README.md`.

## Difficulty resolution

`LevelDifficultyResolver` resolves `LevelParameters` for the current level on start, on level-up, and
on run reset (before the grid respawns, `RunResetOrder.Derived`). It looks up the authored
`LevelRangeEntry` (falling back to the last range as the open-ended tail), resolves its `RangedValue`
bands with the run's seeded RNG, then bridges the result onto the catalogs: balloon/item **type
gates** (a type absent or zero-weight in the range's set is excluded) with effective weight
`catalogEntry.Weight × rangeWeight.Weight`, and the allowed-colour set from the range's colour mask.
Consumers read only `IActiveLevelParameters.Current` — see `Configuration/Level/` for the authored
config and `LevelParameters`.
