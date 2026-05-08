# Thrower

The launcher the player controls to aim and fire projectiles.

The thrower tracks the player's pointer, rotates to face the aim direction, and fires a projectile on input. After firing it waits for the projectile to resolve before reloading. A prediction trace shows where the projectile will travel before the shot is taken.

The thrower interacts with the projectile (it creates and launches it) and with the game loop (which controls when firing is allowed).


