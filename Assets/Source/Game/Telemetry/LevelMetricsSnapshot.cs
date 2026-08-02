using System.Collections.Generic;

namespace BalloonParty.Game.Telemetry
{
    // Sealed and immutable (R16): built once by MetricScope.Seal(), read many times, never mutated —
    // the ceremony snapshot must not drift when later stragglers land or the level resets (R17).
    internal sealed class LevelMetricsSnapshot
    {
        private readonly int[] _counters;
        private readonly float[] _timers;
        private readonly IReadOnlyList<ColorPopCount> _popsByColor;
        private readonly IReadOnlyList<BalloonTypeCount> _popsByBalloonType;
        private readonly IReadOnlyList<ItemActivationCount> _itemsActivated;

        public int LevelIndex { get; }

        public bool Completed { get; }

        public int this[MetricId id] => _counters[(int)id];

        public float this[TimerId id] => _timers[(int)id];

        public IReadOnlyList<ColorPopCount> PopsByColor => _popsByColor;

        public IReadOnlyList<BalloonTypeCount> PopsByBalloonType => _popsByBalloonType;

        public IReadOnlyList<ItemActivationCount> ItemsActivated => _itemsActivated;

        public LevelMetricsSnapshot(
            int[] counters,
            float[] timers,
            IReadOnlyList<ColorPopCount> popsByColor,
            IReadOnlyList<BalloonTypeCount> popsByBalloonType,
            IReadOnlyList<ItemActivationCount> itemsActivated,
            int levelIndex,
            bool completed)
        {
            _counters = counters;
            _timers = timers;
            _popsByColor = popsByColor;
            _popsByBalloonType = popsByBalloonType;
            _itemsActivated = itemsActivated;
            LevelIndex = levelIndex;
            Completed = completed;
        }
    }
}
