namespace BalloonParty.Game.Telemetry
{
    // One projectile's whole life, [Loaded, Destroyed). Adds only the ordinal, because the duration is
    // already in the shared payload: the Flight scope runs its own clocks and is Reset() at
    // ProjectileLoadedMessage, so this snapshot's `gameplay_seconds` IS the time of flight, and
    // `hold_seconds` is how much of it was fast-forwarded.
    internal sealed class FlightMetricsSnapshot : MetricsSnapshotBase
    {
        // 1-based within the level, so a level's flight records order without a timestamp sort and a
        // gap is visibly a dropped record rather than a slow frame.
        public int FlightIndex { get; }

        public FlightMetricsSnapshot(MetricScopeState state, int flightIndex) : base(state)
        {
            FlightIndex = flightIndex;
        }
    }
}
