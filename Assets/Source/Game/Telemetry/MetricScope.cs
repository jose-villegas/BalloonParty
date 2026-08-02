using BalloonParty.Balloon.Type;
using BalloonParty.Configuration.Items;

namespace BalloonParty.Game.Telemetry
{
    // One scope's working state: its MetricSet plus its TimerId-indexed clocks. Flight, Level, Run and
    // Session are four instances of this one type, never four hand-written accumulator classes (see
    // "Superseded decisions" in PLAN-GameplayTelemetry.md).
    internal sealed class MetricScope
    {
        private readonly MetricSet _metrics;
        private readonly TelemetryStopwatch[] _timers;

        public MetricSet Metrics => _metrics;

        public MetricScope(MetricSet metrics, TelemetryStopwatch[] timers)
        {
            _metrics = metrics;
            _timers = timers;
        }

        public TelemetryStopwatch Timer(TimerId id)
        {
            return _timers[(int)id];
        }

        // The mechanical Flight->Level / Level->Run roll-up (R4): every MetricId folds into this scope
        // by its own declared rule, so adding a metric never touches this loop.
        public void Absorb(MetricScope child)
        {
            foreach (var id in MetricCatalog.AllIds)
            {
                var value = child.Metrics[id];
                switch (MetricCatalog.FoldOf(id))
                {
                    case FoldRule.Sum:
                        _metrics.Add(id, value);
                        break;
                    case FoldRule.Max:
                        _metrics.RecordMax(id, value);
                        break;
                    case FoldRule.Min:
                        _metrics.RecordMin(id, value);
                        break;
                    case FoldRule.Last:
                        _metrics.SetLast(id, value);
                        break;
                }
            }

            AbsorbAxes(child);
        }

        public LevelMetricsSnapshot Seal(int levelIndex, bool completed)
        {
            var state = CopyState();
            return new LevelMetricsSnapshot(
                state.Counters, state.Timers, state.PopsByColor, state.PopsByBalloonType, state.ItemsActivated,
                levelIndex, completed);
        }

        public RunMetricsSnapshot Seal()
        {
            var state = CopyState();
            return new RunMetricsSnapshot(
                state.Counters, state.Timers, state.PopsByColor, state.PopsByBalloonType, state.ItemsActivated);
        }

        public void Reset()
        {
            _metrics.Reset();
            for (var i = 0; i < _timers.Length; i++)
            {
                _timers[i].Reset();
            }
        }

        // Both of Pops's axes and the item axis are Sum-folded, same as their owning counters — the
        // roll-up is element-wise addition over a fixed-size array, not a re-derivation per bucket.
        private void AbsorbAxes(MetricScope child)
        {
            var childColor = child.Metrics.ColorAxis;
            for (var i = 0; i < childColor.Count; i++)
            {
                _metrics.AddColorAxis(i, childColor[i]);
            }

            var childBalloonType = child.Metrics.BalloonTypeAxis;
            for (var i = 0; i < childBalloonType.Count; i++)
            {
                _metrics.AddBalloonTypeAxis((BalloonType)i, childBalloonType[i]);
            }

            var childItemType = child.Metrics.ItemTypeAxis;
            for (var i = 0; i < childItemType.Count; i++)
            {
                _metrics.AddItemTypeAxis((ItemType)i, childItemType[i]);
            }
        }

        // Shared by both Seal() overloads so the array-copying logic exists exactly once — a snapshot
        // must never alias the live arrays Reset() goes on to reuse.
        private (int[] Counters, float[] Timers, ColorPopCount[] PopsByColor, BalloonTypeCount[] PopsByBalloonType,
            ItemActivationCount[] ItemsActivated) CopyState()
        {
            var counters = _metrics.CopyCounters();

            var timers = new float[_timers.Length];
            for (var i = 0; i < _timers.Length; i++)
            {
                timers[i] = _timers[i].Elapsed;
            }

            var colorAxis = _metrics.CopyColorAxis();
            var popsByColor = new ColorPopCount[colorAxis.Length];
            for (var i = 0; i < colorAxis.Length; i++)
            {
                popsByColor[i] = new ColorPopCount(i, colorAxis[i]);
            }

            var balloonTypeAxis = _metrics.CopyBalloonTypeAxis();
            var popsByBalloonType = new BalloonTypeCount[balloonTypeAxis.Length];
            for (var i = 0; i < balloonTypeAxis.Length; i++)
            {
                popsByBalloonType[i] = new BalloonTypeCount((BalloonType)i, balloonTypeAxis[i]);
            }

            var itemTypeAxis = _metrics.CopyItemTypeAxis();
            var itemsActivated = new ItemActivationCount[itemTypeAxis.Length];
            for (var i = 0; i < itemTypeAxis.Length; i++)
            {
                itemsActivated[i] = new ItemActivationCount((ItemType)i, itemTypeAxis[i]);
            }

            return (counters, timers, popsByColor, popsByBalloonType, itemsActivated);
        }
    }
}
