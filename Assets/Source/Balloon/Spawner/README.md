# Balloon/Spawner

Responsible for introducing balloons into the grid — both at game start and during play.

When a spawn-line signal is received, the spawner finds the bottom-most empty slot in each column and creates a balloon there. Each balloon drops in from above its target position with a randomised animation duration. After all balloons in a line are placed, a balance pass is triggered so the grid settles correctly.

The spawner interacts with the slot grid (to place balloons), the balancer (which it notifies after each line), and the game loop (which controls when lines are spawned and how many).

