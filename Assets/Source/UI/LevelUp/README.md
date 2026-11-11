# UI/LevelUp

The full-screen level-up ceremony that plays when all color bars complete, and where upgrade cards
are picked. It's switched off for now (`_pickScreen`, off on the prefab): it stopped play, so every
level-up passes in play and a card level-up takes its first card dealt (see *Card level-ups and
in-play level-ups* below). The pick screen is banked for later.

## Contents

| File | What it does |
|---|---|
| `LevelUpLifetimeScope` | VContainer child scope on the LevelUp popup root; registers `LevelUpPopUp` and a `CinematicEndGate(LevelCompleteHit)` by concrete type (not `.As<IReadyGate>()`) so the popup names the exact gate it waits on, plus `RegisterMetricLabels(this)` (see `UI/Telemetry/`), which binds any `MetricLabel` under the popup to `ILevelMetricsView` and no-ops when there are none |
| `LevelUpPopUp` | Holds a `PauseService` pause while it waits for the level-complete hit cinematic to end (via an injected `CinematicEndGate`). With `_pickScreen` off, or on a level-up that deals no cards, it then dismisses at once, taking the first card dealt if there is one. Otherwise it freezes time via `TimeScaleService`, shows the popup with the new and previous level numbers, spawns fill trails from each `ColorProgressBar` to the level fill, shows `IUpgradeDraft.Offer` in its `UpgradeCardView` slots, and publishes `LevelUpDismissedMessage` once a card is picked |
| `UpgradeCardView` | One card slot: a `Button` with title and description labels and an optional held-count label (`1/3`). Shows a dealt card and reports its slot index when tapped; the popup forwards the slot to `IUpgradeDraft.Pick`. `IsWired` is false while a required reference is unassigned, and the popup skips such a slot |

## How it works

### Sequence

1. **Hit beat ends** — `LevelUpCinematic` ends the level-complete hit phase after the authored `LevelCompleteHit` curve duration elapses (duration-based, not wall-hit triggered), setting `CinematicState` to `None`.
2. **Gate opens** — `CinematicEndGate(LevelCompleteHit)` unblocks: `Cinematic.Current != LevelCompleteHit` is now true.
3. **Popup shows** — `LevelUpPopUp.ShowAfterGateAsync` claims a `PauseService` pause (`PauseSource.LevelUp`) the moment `ScoreLevelUpMessage` arrives, before it even awaits the gate. Once the gate opens it claims `TimeScaleSource.LevelUpPopup = 0` via `TimeScaleService` (effective `Time.timeScale` drops to 0), triggers the `"Appear"` animator, and waits for the appear animation to finish. `_levelLabel` shows the level being entered (`NewLevel`) and the optional `_previousLevelLabel` shows the one just completed (`NewLevel - 1`); both are set once here and neither changes while the popup is up.
4. **Fill trails** — After the appear animation completes, `LevelUpPopUp` publishes `LevelUpFillTrailsMessage` (triggers `ColorProgressBar.DrainSliderAsync` to drain each bar in sync), then spawns decorative `FlyingTrail` orbs from each bar's random position to random offsets around the level fill centre. Trails fly in unscaled time (`Spawn(..., useUnscaledTime: true)`), staggered across waves (`_fillTrailsPerBar` waves × the message's completed-colors count). As each trail arrives, `_levelFill.localScale` tweens toward the new fraction over `_fillStepDuration` (`Ease.OutCubic`, unscaled). The tween is *restarted* from wherever it currently is rather than queued, so a burst of arrivals blends into one continuous ramp instead of a staircase.
5. **Fill completes → stats reveal** — when the fill tween reaches full, `_continueButton` becomes interactable, `_statsRevealCover` scales down to zero, and each entry in `_statContainers` pops in from zero on a `_statPopStagger` cadence. All unscaled. Completion fires off the *tween*, not the arrival count, so the reveal follows what the player watches fill rather than the frame the last trail landed.
6. **Player picks a card (or taps Continue)** — on a level-up that deals cards a pick closes the popup (see below); otherwise `OnContinue()` triggers `"Hide"` and calls `Resume()` synchronously (no delay), which publishes `LevelUpDismissedMessage`, releases the popup's `TimeScaleService` claim, and resumes the `PauseService` pause (`PauseSource.LevelUp`).
7. **Level advances (two-phase commit)** — `LevelController` receives `LevelUpDismissedMessage` and *now* advances the `Level` integer to the pending value, resets progress, and flips `LevelUpPhase` from `Pending` to `Transitioning`. The popup has shown the new number since it opened (step 3), but the authoritative `Level` only changes here — see `Game/Level/README.md`.
8. **Bar reset** — Each `ColorProgressBar` receives `LevelUpDismissedMessage` and applies the stashed new max value, resetting progress to zero.
9. **Ascent + navigate** — the phase flip to `Transitioning` triggers `LevelTransitionController` (the Ascent), which slides the new level in. The camera un-zoom was already started at `EndPanIn` by `LevelUpCinematic` (via `CinematicCameraRig.RestoreCurveDriven`) — the Ascent does not touch the camera. `LevelController` owns the nav return to `Game` once `LevelTransitionCompletedMessage` arrives. There is **no** `LevelCompleteRestore` cinematic state played — it's kept in the enum only for serialized-index stability; its curve is read for the `RestoreCurveDriven` call.

The Animator's `updateMode` is set to `UnscaledTime` in `Start()`, so animations play even while the game is paused.

### Fill trail spawning

`LevelUpPopUp` owns a per-color dictionary of `TrailSpawner` instances (pool key `FillTrail_{colorName}`, sorting order 3200). For each wave, it iterates the message's completed colors, reads the bar's position via `_scoreTrailService.GetTarget(colorName).RandomPosition()`, picks a random offset within `_fillTargetRadiusMultiplier` of the **target area's** radius, and calls `Spawn` with `useUnscaledTime: true`. Arrival increments `_fillTrailArrivedCount` and tweens the fill toward the new fraction. Trails reuse the same `SimplePoolChannel<FlyingTrail>` factory as score trails but are pooled under separate keys.

**`_levelFill` and `_fillTargetArea` are two different objects on purpose.** The fill scales from 0 to 1 over the ceremony, so measuring the arrival disc from it would send every trail to a single point at the start and drift the destination outward as it grew. `_fillTargetArea` keeps its size throughout and is the only thing `TryGetFillTargetGeometry` reads — do not collapse them back into one reference.

### Gate pattern

`LevelUpLifetimeScope` registers `CinematicEndGate(CinematicState.LevelCompleteHit)` **by concrete type**, and `LevelUpPopUp` injects `CinematicEndGate` directly — not `IReadyGate`. That keeps the dependency explicit and stops the popup from silently resolving the parent scope's `NavigationReadyGate(Game)` if this registration ever went missing. Both are `IReadyGate` implementations over `UniTask.WaitUntil` on a reactive property, just on different state machines — though no current consumer actually injects the interface; every gate, including `GridSpawnerCoordinator`'s `NavigationReadyGate`, is injected by concrete type.

```
CinematicEndGate(LevelCompleteHit) → opens when Cinematic.Current != LevelCompleteHit
NavigationReadyGate(Game)      → opens when Navigation.Current == Game
```

### Continue is gated on the fill

`OnContinue` returns early unless `_fillComplete`, and `_continueButton.interactable` is false until
then — two halves of one rule, because the popup's full-screen button keeps receiving raycasts either
way. **Anything that can prevent the fill from completing therefore makes the popup unmissable**, so
both known cases complete it immediately instead: a level-up with no completed colours (reachable via
the cheat that grants a level directly) and an unassigned `_levelFill`. The second is not paranoia —
that field changed type from `Image` to `RectTransform`, so every prefab authored before that starts
out unassigned.

### Card level-ups and in-play level-ups

The hand is the model's: `IUpgradeDraft.Offer` opens when the level-up is presented
(`LevelUpPhase.Pending`) and the popup only shows it and forwards the tapped slot. The rules of the
hand (one pick per level-up, held only once the level advances, void on an abort) live in
`Game/Upgrades/RunUpgrades`, not here.

Once the hit beat ends, `ShowAfterGateAsync` first checks that its show is still current: the beat
also ends when the level-up is **aborted** (a loss became certain), and a level-up re-detected after
that starts a newer show. A show that is stale (a newer one started, or the phase is no longer
`Pending`) releases its own pause and returns, publishing nothing, since a dismissal there would land
on a ceremony that isn't pending. Pauses are counted per source, so a stale show never releases a
newer one's.

With `_pickScreen` off (the prefab's setting), or on an empty hand (every level-up but each
`CardEveryLevels`th, see `Game/Upgrades/README.md`), there is no screen. `PassInPlay` takes the hand's
first card through `IUpgradeDraft.Pick(0)` when one was dealt (the deal is already a draw by weight),
shows the on-screen tooltip (`TooltipMessage`; see `UI/Tooltip/README.md`), `Level N` by
`_inPlayTooltipFormat` or `Level N: Card` by `_inPlayCardTooltipFormat` (only `Level N` while a card
reveal's view is attached, `ICardReveals.HasStage`, since the reveal announces the card; see
`Game/CardReveal/README.md`), publishes
`LevelUpDismissedMessage(passedInPlay: true)` and releases its pause, with no freeze, no animator and
no fill trails, and the Ascent follows as usual. The pick comes before the dismissal: the dismissal
is what holds it, and it closes the hand. The colour bars reset on
that dismissal without the drain, and `ProgressionSoundRouter` skips the confirm sound, which is the
popup's button.

With `_pickScreen` on, a non-empty hand shows the popup as described above. When the fill completes, the cards are shown in
`_cardViews` in order (a slot beyond the hand, or one not `IsWired`, is hidden) and Continue stays
locked: only a pick closes the popup, through `IUpgradeDraft.Pick(slot)` and then the same hide and
`Resume()` as Continue. With no wired card slot, Continue stays the way out and picks the first card
dealt, with one warning per session, so an unwired popup still levels up and still grows the weapon.

## Wiring requirements

- The popup GameObject must be **active** in the scene at all times — visibility is controlled by CanvasGroup alpha (animated by the `LevelUp` animator), not by `SetActive`. If the object is disabled, `Start()` never runs and `ScoreLevelUpMessage` is never subscribed.
- Registered in `LevelUpLifetimeScope` via `RegisterComponentInHierarchy<LevelUpPopUp>()`.
- `_cardViews`: one `UpgradeCardView` per card dealt (3 by default), each above the full-screen
  Continue button so its taps reach it. A card may start inactive: it binds its button on first show.
  **Tools > BalloonParty > Level Up > Build Upgrade Card Slots** builds a row of three under the
  panel, copied from the Continue button, and assigns them; restyle the row by hand from there.
- `_levelFill` and `_fillTargetArea` must be **different** objects (see below). `_continueButton`,
  `_previousLevelLabel`, `_statsRevealCover` and `_statContainers` are all optional — unassigned, each
  simply drops its part of the ceremony rather than breaking it.

## Interactions

- **`LevelController`** (`Game/Level/`) — publishes `ScoreLevelUpMessage` (triggers `ShowAfterGateAsync`) and transitions navigation to `LevelUp`; on dismissal advances the level (two-phase commit) and flips `LevelUpPhase` to drive the Ascent
- **`LevelUpCinematic`** — opens the gate by ending the level-complete hit beat; on `LevelUpDismissedMessage` finalizes its own session state (no resume, no navigation — the Ascent handles the camera and `LevelController` owns the nav return to `Game`)
- **`LevelUpLifetimeScope`** — registers this component and provides the `CinematicEndGate` injection
- **`ColorProgressBar`** — receives `LevelUpFillTrailsMessage` to drain its slider in sync with fill trail waves; receives `LevelUpDismissedMessage` to apply the new max and reset progress
- **`ScoreTrailService`** — provides trail target positions for fill trail origin and the `FlyingTrail` prefab for pool channel creation
- **`PoolManager`** — hosts per-color `FillTrail_{colorName}` pools created lazily by popup
- **`GamePalette`** — provides palette entries (color names and tints) for iterating fill trails
