# GameOver

The loss screen shown when a run ends. Placeholder presentation; the dramatic loss cinematic
is a separate follow-on.

## Contents

| File | What it does |
|---|---|
| `GameOverScreen` | `MonoBehaviour` view. Subscribes to `GameOverMessage` — on receipt, fills the final level/score (from the message) and the persisted best level/score (from `IRunMeta`), then reveals itself via its `CanvasGroup`. Hides again when navigation leaves `NavigationState.GameOver` (i.e. after Restart). `OnRestartPressed` (wired to the Restart button) calls `RunController.RestartRun` |
| `GameOverLifetimeScope` | Child `LifetimeScope` on the screen root; injects `GameOverScreen`, inheriting run services from `GameLifetimeScope` |

## Wiring requirements

- The screen root GameObject must stay **active** in the scene — visibility is driven by the
  `CanvasGroup` (alpha / interactable / blocksRaycasts), not `SetActive`. If it were disabled,
  `Start` would never run and `GameOverMessage` would never be subscribed.
- Attach `GameOverLifetimeScope` to that root so `RegisterComponentInHierarchy<GameOverScreen>`
  finds the view.
- Assign the four `TMP_Text` label fields and wire the Restart button's onClick to
  `GameOverScreen.OnRestartPressed`.

## Interactions

- **`GameOverMessage`** — published by `RunController.EndRun` (see `Game/Run/`); carries the final
  level/score. Triggers the screen to appear.
- **`IRunMeta`** — read for best level/score (the only cross-run persisted progression).
- **`RunController`** — `RestartRun` clears the board and re-spawns a fresh run.
- **`Navigation`** — the screen self-hides when state leaves `GameOver`.
