namespace BalloonParty.Projectile.Controller
{
    /// <summary>
    ///     Whether hold-to-speed-up is currently engaged. A sanctioned read seam (R11 of
    ///     @ref plan_gameplay_telemetry): <see cref="HoldSpeedUpController" /> publishes nothing and
    ///     samples the pointer inside its own tick, so a metric that wants to tell a fast-forwarded
    ///     level from a slow one has no message to subscribe. Read-only and order-independent — it adds
    ///     no bus traffic and no publish path.
    /// </summary>
    internal interface IHoldSpeedUpState
    {
        bool IsHolding { get; }
    }
}
