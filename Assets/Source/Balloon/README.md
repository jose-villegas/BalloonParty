# Balloon

Represents a single balloon in the game — its state, appearance, and behaviour.

A balloon knows its color, where it sits in the grid, and whether it has settled into position. When any of these change, the visual updates automatically.

Balloons interact with the slot grid (they occupy a position in it), with the balancer (which moves them when gaps appear below), and with the projectile (which triggers their destruction on collision). Power-up balloons extend this with additional area effects when hit.


