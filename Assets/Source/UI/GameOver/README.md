# GameOver

The loss screen shown when a run ends. It sits *inside* the loss cinematic
(`Game/Cinematics/GameOverLossCinematic`), split like the level-up popup: a slow-mo push-in plays,
the screen shows over the held (pushed-in) frame, and on dismiss the camera pulls back — the run only
restarts once that pull-back ends.

## Contents

| File | What it does |
|---|---|
| `GameOverScreen` | `MonoBehaviour` view. Subscribes to `GameOverMessage` — on receipt, fills the final level/score (from the message) and the persisted best level/score (from `IRunMeta`), then **awaits `GameOverPresentationGate`** before revealing itself via its `CanvasGroup` (the loss cinematic opens the gate once its push-in finishes). Hides again when navigation leaves `NavigationState.GameOver`. `OnRestartPressed` (wired to the Restart button) hides the screen and publishes `GameOverDismissedMessage` — it does **not** restart directly; the cinematic plays the pull-back and restarts at its end |
| `GameOverLifetimeScope` | Empty child `LifetimeScope` on the screen root — the screen has no local registrations; `GameOverScreen` is injected via `RegisterComponentInHierarchy<GameOverScreen>` in `GameLifetimeScope` |
| `Game/Run/GameOverPresentationGate` | DI singleton (`IReadyGate`) the screen awaits before showing; armed and opened by `GameOverLossCinematic`. Registered in `GameLifetimeScope` (`RegisterPresentation`) so the producer and screen share one instance |

## Wiring requirements

- The screen root GameObject must stay **active** in the scene — visibility is driven by the
  `CanvasGroup` (alpha / interactable / blocksRaycasts), not `SetActive`. If it were disabled,
  `Start` would never run and `GameOverMessage` would never be subscribed.
- Assign the four `TMP_Text` label fields and wire the Restart button's onClick to
  `GameOverScreen.OnRestartPressed`. Each label's authored text is a `FormattedLabel` template
  (e.g. `"Level: {0}"`) captured in `Awake`, so repeated losses substitute cleanly.
- The Restart button dismisses (hides the screen + plays the restore) rather than restarting on the
  spot — expect a beat between the press and the fresh board while the camera pulls back.

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
