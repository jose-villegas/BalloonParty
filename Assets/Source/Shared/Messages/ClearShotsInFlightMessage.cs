namespace BalloonParty.Shared.Messages
{
    /// <summary>
    ///     Commands the thrower to kill every shot in flight through its canonical death path, and to keep the ball
    ///     waiting in the cannon. Published by the nuke's blast (PLAN-NukeBalloon). Unlike
    ///     <see cref="ForceDestroyProjectileMessage" />, it never touches the loaded ball.
    /// </summary>
    public readonly struct ClearShotsInFlightMessage
    {
    }
}
