namespace BalloonParty.Game.Telemetry
{
    // One ball's flight, [Fired, Destroyed). Adds only the ordinal, because the duration is already in
    // the shared payload: each ball's Flight scope runs its own clocks and is Reset() at its
    // ProjectileFiredMessage, so this snapshot's `gameplay_seconds` IS the time of flight.
    internal sealed class FlightMetricsSnapshot : MetricsSnapshotBase
    {
        // 1-based within the level and assigned at the seal, so a level's flight records order by when
        // each ball died and a gap is visibly a dropped record rather than a slow frame.
        public int FlightIndex { get; }

        public FlightMetricsSnapshot(MetricScopeState state, int flightIndex) : base(state)
        {
            FlightIndex = flightIndex;
        }
    }
}
