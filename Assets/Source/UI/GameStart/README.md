# UI/GameStart

The button the player presses to begin a game session.

## How it works

`GameStartButton` is a `[RequireComponent(typeof(Button))]` MonoBehaviour. When clicked it publishes one `SpawnBalloonLineMessage` per `IGameConfiguration.GameStartedBalloonLines`, then deactivates itself. The `BalloonSpawner` handles each message and the thrower activates once balancing completes — the button has no knowledge of either.

## Interactions

- **SpawnBalloonLineMessage** — published once per configured initial line count on click
- **IGameConfiguration** — `GameStartedBalloonLines` controls how many lines spawn at start

