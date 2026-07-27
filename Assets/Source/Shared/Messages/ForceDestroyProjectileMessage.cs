namespace BalloonParty.Shared.Messages
{
    /// <summary>Commands the thrower to force-destroy the active projectile through its canonical
    /// death path (pierce discharge, state cleanup, ProjectileDestroyedMessage). Published by the
    /// level controller when the board is empty or the Completing cap times out.</summary>
    public readonly struct ForceDestroyProjectileMessage
    {
    }
}
