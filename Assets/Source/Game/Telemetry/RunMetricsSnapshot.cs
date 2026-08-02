namespace BalloonParty.Game.Telemetry
{
    // Adds nothing to MetricsSnapshotBase's shared payload — the Level->Run roll-up (MetricScope.Absorb)
    // folds into exactly the same vocabulary, so the run's read surface carries the same MetricId/
    // TimerId/axis indexers with no extra fields. Kept as its own type (rather than a type alias) since
    // it is distinct in the read model (ILevelMetricsView.Run) — see MetricsSnapshotBase for the
    // immutability/aliasing contract.
    internal sealed class RunMetricsSnapshot : MetricsSnapshotBase
    {
        public RunMetricsSnapshot(MetricScopeState state) : base(state)
        {
        }
    }
}
