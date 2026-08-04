namespace BalloonParty.UI.Telemetry
{
    // Which of ILevelMetricsView's three reactive snapshots a MetricBinding reads. CeremonyLevel and
    // LastFlushedLevel both resolve to a LevelMetricsSnapshot (see the telemetry README's "Two snapshots
    // per level" for why both exist); Run resolves to the RunMetricsSnapshot roll-up. All three implement
    // ISealedMetrics, which is what MetricValueResolver actually consumes.
    internal enum MetricBindingSource
    {
        CeremonyLevel,
        LastFlushedLevel,
        Run
    }
}
