using System.Collections.Generic;

namespace BalloonParty.Game.Telemetry
{
    // Shared payload for LevelMetricsSnapshot and RunMetricsSnapshot — the two differ only by the two
    // level-only fields (LevelIndex, Completed) each subclass adds; everything else was the same eight
    // members declared three times (this type, plus what used to be duplicated per snapshot). Holds the
    // MetricScopeState as-is rather than unpacking it into eight private fields per subclass, since
    // CopyState() already built it as one immutable value.
    //
    // Sealed and immutable (R16): built once by MetricScope.Seal(), read many times, never mutated. The
    // only other ISealedMetrics/IReadOnlyMetricSet implementer is the live, reset-in-place MetricSet,
    // aliasing which into a snapshot would break "immutable across the read boundary" outright.
    internal abstract class MetricsSnapshotBase : ISealedMetrics
    {
        private readonly MetricScopeState _state;

        public int this[MetricId id] => _state.Counters[(int)id];

        public float this[TimerId id] => _state.Timers[(int)id];

        public IReadOnlyList<ColorCount> PopsByColor => _state.PopsByColor;

        public IReadOnlyList<BalloonTypeCount> PopsByBalloonType => _state.PopsByBalloonType;

        public IReadOnlyList<BalloonTypeCount> DeflectsByBalloonType => _state.DeflectsByBalloonType;

        public IReadOnlyList<ColorCount> PointsByColor => _state.PointsByColor;

        public IReadOnlyList<ItemActivationCount> ItemsActivated => _state.ItemsActivated;

        protected MetricsSnapshotBase(MetricScopeState state)
        {
            _state = state;
        }

        public IReadOnlyList<int> AxisBucketsOf(MetricId id, MetricAxis axis)
        {
            return _state.AxisSlots[MetricCatalog.SlotOf(id, axis).Index];
        }

        public IReadOnlyList<int> AxisBucketsOf(AxisSlot slot)
        {
            return _state.AxisSlots[slot.Index];
        }
    }
}
