namespace BalloonParty.Game.Telemetry
{
    // The level-only two fields on top of MetricsSnapshotBase's shared payload — what the level-up
    // popup renders (ceremony snapshot) and what the sink receives and folds into the run (flush
    // snapshot). See MetricsSnapshotBase for the immutability/aliasing contract.
    internal sealed class LevelMetricsSnapshot : MetricsSnapshotBase
    {
        public int LevelIndex { get; }

        public bool Completed { get; }

        public LevelMetricsSnapshot(MetricScopeState state, int levelIndex, bool completed) : base(state)
        {
            LevelIndex = levelIndex;
            Completed = completed;
        }
    }
}
