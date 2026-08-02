using System.Collections.Generic;

namespace BalloonParty.Game.Telemetry
{
    // Sealed and immutable, same shape as LevelMetricsSnapshot minus the two level-only fields
    // (LevelIndex, Completed) — the Level->Run roll-up (MetricScope.Absorb) folds into exactly this
    // vocabulary, so the run's read surface carries the same MetricId/TimerId/axis indexers.
    internal sealed class RunMetricsSnapshot
    {
        private readonly int[] _counters;
        private readonly float[] _timers;
        private readonly IReadOnlyList<ColorPopCount> _popsByColor;
        private readonly IReadOnlyList<BalloonTypeCount> _popsByBalloonType;
        private readonly IReadOnlyList<ItemActivationCount> _itemsActivated;

        public int this[MetricId id] => _counters[(int)id];

        public float this[TimerId id] => _timers[(int)id];

        public IReadOnlyList<ColorPopCount> PopsByColor => _popsByColor;

        public IReadOnlyList<BalloonTypeCount> PopsByBalloonType => _popsByBalloonType;

        public IReadOnlyList<ItemActivationCount> ItemsActivated => _itemsActivated;

        public RunMetricsSnapshot(
            int[] counters,
            float[] timers,
            IReadOnlyList<ColorPopCount> popsByColor,
            IReadOnlyList<BalloonTypeCount> popsByBalloonType,
            IReadOnlyList<ItemActivationCount> itemsActivated)
        {
            _counters = counters;
            _timers = timers;
            _popsByColor = popsByColor;
            _popsByBalloonType = popsByBalloonType;
            _itemsActivated = itemsActivated;
        }
    }
}
