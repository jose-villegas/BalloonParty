namespace BalloonParty.Game.Telemetry
{
    // What MetricScope.CopyState hands to MetricsSnapshotBase's constructor — one object instead of
    // eight positional arrays/lists, per CLAUDE.md's "prefer a config/settings object over many
    // parameters". The raw per-slot axis arrays are kept (not discarded after building the named
    // breakdown lists) so the snapshots can implement ISealedMetrics.AxisBucketsOf at no extra
    // allocation.
    internal readonly struct MetricScopeState
    {
        public readonly int[] Counters;
        public readonly float[] Timers;
        public readonly int[][] AxisSlots;
        public readonly ColorCount[] PopsByColor;
        public readonly BalloonTypeCount[] PopsByBalloonType;
        public readonly BalloonTypeCount[] DeflectsByBalloonType;
        public readonly ColorCount[] PointsByColor;
        public readonly ItemActivationCount[] ItemsActivated;

        public MetricScopeState(
            int[] counters,
            float[] timers,
            int[][] axisSlots,
            ColorCount[] popsByColor,
            BalloonTypeCount[] popsByBalloonType,
            BalloonTypeCount[] deflectsByBalloonType,
            ColorCount[] pointsByColor,
            ItemActivationCount[] itemsActivated)
        {
            Counters = counters;
            Timers = timers;
            AxisSlots = axisSlots;
            PopsByColor = popsByColor;
            PopsByBalloonType = popsByBalloonType;
            DeflectsByBalloonType = deflectsByBalloonType;
            PointsByColor = pointsByColor;
            ItemsActivated = itemsActivated;
        }
    }
}
