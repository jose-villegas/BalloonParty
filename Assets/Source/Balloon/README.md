# Balloon

Represents a single balloon in the game — its state, appearance, and behaviour.

A balloon knows its color, where it sits in the grid, and whether it has settled into position. When any of these change, the visual updates automatically.

When a gap appears in the grid (because a balloon was destroyed), the balancer scans for unbalanced balloons and moves them upward along the best available path, animating smoothly into their new slot. A balloon is marked unstable for the duration of that animation.

Balloons interact with the slot grid (they occupy a position in it), with the balancer (which moves them when gaps appear below), and with the projectile (which triggers their destruction on collision). Power-up balloons extend this with additional area effects when hit.


