namespace BalloonParty.Configuration
{
    /// <summary>
    ///     Tuning for the overflow pile below the grid (rejected balloons) — see
    ///     <c>Balloon/Spawner/RejectedBalloonEffect</c>.
    /// </summary>
    internal interface IOverflowSettings
    {
        float AppearStaggerSeconds { get; }
        float LingerSeconds { get; }
        float PopIntervalSeconds { get; }
        float MoveSharpness { get; }
        float ArrivalRadius { get; }
    }
}
