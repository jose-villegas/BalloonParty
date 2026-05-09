# Balloon

Represents a single balloon in the game — its state, appearance, and behaviour.

A balloon knows its color, where it sits in the grid, and whether it has settled into position. When any of these change, the visual updates automatically.

When a gap appears in the grid (because a balloon was destroyed), the balancer scans for unbalanced balloons and moves them upward along the best available path, animating smoothly into their new slot. A balloon is marked unstable for the duration of that animation.

When a projectile hits a balloon, `BalloonController` receives the `BalloonHitMessage` (filtered to its own model), plays the pop particle effect at the balloon's world position, removes the model from the slot grid, destroys the view GameObject, and publishes a `BalanceBalloonsMessage` so the remaining balloons settle.

Balloons interact with the slot grid (they occupy a position in it), with the balancer (which moves them when gaps appear below), with the projectile (which triggers their destruction on collision), and with the score system (which records the hit). Power-up balloons extend this with additional area effects when hit.
