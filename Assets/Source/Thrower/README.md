# Thrower

The thrower is the player-controlled launcher at the bottom of the screen. It aims at the balloon grid and fires projectiles.

## Gameplay

The player holds the mouse button to aim — the thrower rotates to face the cursor. Releasing the mouse fires the loaded projectile. Once the projectile is destroyed (shields depleted), the thrower automatically reloads and is ready for the next shot.

## How it works

`ThrowerController` is a MonoBehaviour placed in the scene. On start it slides into position from below the screen, then loads a projectile and waits for input. Each frame it:
- Updates its aim direction from the mouse cursor position
- Rotates its transform to match that direction
- Orbits the loaded projectile around the spawn point to preview the trajectory
- Fires on mouse-up when all balloons have settled (unstable balloons block firing)

When a `ProjectileDestroyedMessage` arrives, `ThrowerController` creates a new projectile automatically.

## Interactions

- **SlotGrid** — queried to check balloon stability before allowing a shot
- **ProjectileView** — instantiated and bound to a `ProjectileModel` on each load
- **ProjectileDestroyedMessage** — triggers reload
- **IGameConfiguration** — provides `ThrowerSpawnPoint`, `ProjectileSpawnPoint`, `ProjectileSpeed`, `ProjectileStartingShields`
