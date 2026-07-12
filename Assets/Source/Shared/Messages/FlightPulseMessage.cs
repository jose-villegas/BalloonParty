namespace BalloonParty.Shared.Messages
{
    /// <summary>The flight-time heartbeat: published every FlightRebalanceInterval while a projectile is airborne — paces the in-flight rebalance and the deferred pop-spawns.</summary>
    public readonly struct FlightPulseMessage
    {
    }
}
