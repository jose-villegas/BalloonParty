# Balloon/Spawner

Responsible for introducing balloons into the grid — both at game start and during play.

When a spawn-line signal is received, the spawner finds the first empty slot from the top in each column and creates a balloon there. Each balloon drops in from above its target position with a randomised animation duration. After all balloons in a line are placed, a balance pass is triggered so the grid settles correctly.

After each projectile death (starting from the second turn onward), the spawner automatically spawns `NewProjectileBalloonLines` lines with `NewBalloonLinesTimeInterval` delay between each. Delayed multi-line spawning uses `async UniTaskVoid` with a `CancellationTokenSource`, avoiding any coroutine runner dependency. The first projectile death is skipped because game-start lines are spawned separately via `GameStartButton`.

## Interactions

- **SlotGrid** — queried for empty slots, used to place balloon models
- **BalloonBalancer** — notified via `BalanceBalloonsMessage` after each line spawns
- **SpawnBalloonLineMessage** — triggers a single line spawn (used by `GameStartButton` and cheats)
- **ProjectileDestroyedMessage** — triggers automatic multi-line spawning after each turn
- **IGameConfiguration** — provides `BalloonColors`, `BalloonSpawnAnimationDurationRange`, `NewProjectileBalloonLines`, `NewBalloonLinesTimeInterval`
