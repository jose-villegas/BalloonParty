# GameOver

The loss screen shown when a run ends. It sits *inside* the loss cinematic
(`Game/Cinematics/GameOverLossCinematic`), split like the level-up popup: a slow-mo push-in plays,
the screen shows over the held (pushed-in) frame, and on dismiss the camera pulls back — the run only
restarts once that pull-back ends.

## Contents

| File | What it does |
|---|---|
| `GameOverScreen` | `MonoBehaviour` view. Subscribes to `GameOverMessage` — on receipt, fills the level reached and its points (from the message) and the best run on one line, `_bestLabel`, whose template takes the best level as `{0}` and the best score as `{1}` (from `IRunMeta`), then **awaits `GameOverPresentationGate`** before revealing itself via its `CanvasGroup` (the loss cinematic opens the gate once its push-in finishes). Hides again when navigation leaves `NavigationState.GameOver`. `OnRestartPressed` (wired to the Restart button) hides the screen and publishes `GameOverDismissedMessage` — it does **not** restart directly; the cinematic plays the pull-back and restarts at its end. The Retry button follows `IFailureOffers.Current`, the screen's only view of retry state: interactable while the run can retry, labelled with the level it retries: `_retryFormat` (`Retry level {0}`), `_retryCountFormat` when retries are capped, or `_retryAdFormat` once the free retries are used. `OnRetryPressed` locks the whole screen and awaits `TryRetryAsync(deathLevel)`, which shows the ad when gated and stages the retry; then it dismisses the same way. A skipped ad unlocks the screen with the same offer. The optional bonus retry button, a tile in the `_offers` row with a "Boost" ribbon across its corner, is shown only while `FailureOffer.CanBonusRetry`, and the row with it; `OnBonusRetryPressed` claims the retry the same way through `TryBonusRetryAsync(deathLevel)`, which plays the rewarded ad and stages the retry with a bonus card for that attempt. Restart is ignored while a retry is being claimed, and each shown offer has its own cancellation, cancelled on dismiss, on leaving `GameOver` and on a new loss, so a late ad never stages a retry for a screen that's gone. On showing, `_panel` pops in (scale and fade, unscaled time, as the loss cinematic is slowed) and taps are ignored for `_inputGuardSeconds`, so a tap meant for the board can't dismiss it; `_primaryButton` (Retry) then pulses now and then. Hiding kills both tweens |
| `GameOverLifetimeScope` | Child `LifetimeScope` on the screen root. Its one registration is `RegisterMetricLabels(this)` (see `UI/Telemetry/`), which binds any `MetricLabel` under the screen to `ILevelMetricsView` and no-ops when there are none. `GameOverScreen` itself is injected via `RegisterComponentInHierarchy<GameOverScreen>` in `GameLifetimeScope` |
| `Game/Run/GameOverPresentationGate` | DI singleton the screen awaits before showing; armed and opened by `GameOverLossCinematic`. Registered in `GameLifetimeScope` (`RegisterPresentation`) so the producer and screen share one instance. Implements `IReadyGate` for consistency, but injected by concrete type — the producer needs `Arm`/`Open`, and binding it as the interface would collide with the scope's `NavigationReadyGate(Game)` |

## Wiring requirements

- The screen root GameObject must stay **active** in the scene — visibility is driven by the
  `CanvasGroup` (alpha / interactable / blocksRaycasts), not `SetActive`. If it were disabled,
  `Start` would never run and `GameOverMessage` would never be subscribed.
- The three result labels' authored text is a `FormattedLabel` template (e.g. `"Level {0}"`) captured
  in `Awake`, so repeated losses substitute cleanly. The builder assigns them and wires the Restart
  link to `GameOverScreen.OnRestartPressed`.
- The Restart button dismisses (hides the screen + plays the restore) rather than restarting on the
  spot — expect a beat between the press and the fresh board while the camera pulls back.
- The panel's layout is built by **Tools > BalloonParty > Game Over > Rebuild Layout**
  (`Editor/GameOverLayoutBuilder`): a rounded card sized to its content by layout groups, so a longer label or
  a new tile grows the card instead of spilling out of it. Restyle through the builder, not by hand: running
  it again replaces hand edits. The prefab is a variant of `Prefabs/UI/Popup/Popup.prefab`; the builder
  removes the base popup's panel, re-creates its Restart button (which a variant can't move), turns the
  popup's glow into a dark dim and hides its rays.

## Theme

The screen uses the UI theme, "glossy balloon": every panel and button is a stack of soft layers, back to
front a drop shadow, a glow outline, a darker lip under the face, the gradient face, a sheen over its top
and the balloons' specular along its rim. The shapes come from `Tools/art/make_ui_shapes.py` (see
`Sprites/README.md`), drawn from distance fields so every edge softens evenly.

| Part | Look |
|---|---|
| Card | Blue gradient body whose dark edge is blurred, smearing into the board, mostly see-through so the board shows behind the loss (the layers behind it are hollow); the "Game Over" title on a vermilion plaque across its top edge. What's information or an action stays solid |
| Result | A cream well: the level in white outlined in blue, the points in blue, the best in pencil (white on the bare blue is too faint) |
| Retry | Green, the largest button, pulsing now and then: the one to press |
| Offers | Plum tiles sharing the row's width, told by pictures (the ad tile: a "?" card and a play-AD pill) with a ribbon across the corner saying what they give |
| Start Over | Red, shorter than Retry: it throws the run away |

The light follows the time of day, as on the balloons. Each shape's specular is a `UI/TimeOfDaySpecular`,
which runs along the rim where it faces the scene light and takes a little of its colour; the faces take a
quarter of the light's tint through a `TimeOfDayTint` on the card. Text uses one TMP material per role
(title, number, button), kept in `Assets/Fonts/Materials/`.
- `_bonusRetryButton` and `_offers` are optional.

## Interactions

- **`GameOverMessage`** — published by `RunController.EndRun` (see `Game/Run/`); carries the final
  level/score. Populates the labels and starts the loss cinematic; the screen reveals once the
  cinematic opens `GameOverPresentationGate`.
- **`GameOverDismissedMessage`** — published by the screen on Restart; `GameOverLossCinematic` plays
  the restore in response and restarts the run when it ends.
- **`IRunMeta`** — read for best level/score (the only cross-run persisted progression).
- **`RunController`** — `RestartRun` (invoked by the cinematic at the restore's end, not the button)
  clears the board and re-spawns a fresh run.
- **`Navigation`** — the screen self-hides when state leaves `GameOver`.
