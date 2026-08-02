using System;
using BalloonParty.Balloon.Type;
using BalloonParty.Configuration.Items;

namespace BalloonParty.Game.Telemetry
{
    // One scope's working state: its MetricSet plus its TimerId-indexed clocks. Flight, Level, Run and
    // Session are four instances of this one type, never four hand-written accumulator classes (see
    // "Superseded decisions" in PLAN-GameplayTelemetry.md).
    internal sealed class MetricScope
    {
        private readonly MetricScopeKind _scope;
        private readonly MetricSet _metrics;
        private readonly TelemetryStopwatch[] _timers;

        public MetricScopeKind Scope => _scope;

        // Read-only on purpose (see "Cheap hardening" in the W1 rework) — mutation goes through the
        // pass-throughs below so nothing outside this type can bypass the fold/reset lifecycle.
        public IReadOnlyMetricSet Metrics => _metrics;

        private MetricScope(MetricScopeKind scope, MetricSet metrics, TelemetryStopwatch[] timers)
        {
            _scope = scope;
            _metrics = metrics;
            _timers = timers;
        }

        // Sole construction path — sizes the timer array from TimerId itself rather than a caller
        // passing a literal, so a malformed scope (wrong timer count) cannot be built.
        internal static MetricScope Create(MetricScopeKind scope, int colorAxisSize, Func<float> clock)
        {
            var metrics = new MetricSet(colorAxisSize);
            var timerCount = Enum.GetValues(typeof(TimerId)).Length;
            var timers = new TelemetryStopwatch[timerCount];
            for (var i = 0; i < timerCount; i++)
            {
                timers[i] = new TelemetryStopwatch(clock);
            }

            return new MetricScope(scope, metrics, timers);
        }

        public TelemetryStopwatch Timer(TimerId id)
        {
            return _timers[(int)id];
        }

        public void Increment(MetricId id)
        {
            _metrics.Increment(id);
        }

        public void Add(MetricId id, int value)
        {
            _metrics.Add(id, value);
        }

        public void RecordMax(MetricId id, int value)
        {
            _metrics.RecordMax(id, value);
        }

        public void SetLast(MetricId id, int value)
        {
            _metrics.SetLast(id, value);
        }

        public void IncrementAxis(MetricId id, MetricAxis axis, int bucket)
        {
            _metrics.IncrementAxis(id, axis, bucket);
        }

        public void AddAxis(MetricId id, MetricAxis axis, int bucket, int value)
        {
            _metrics.AddAxis(id, axis, bucket, value);
        }

        // The mechanical Flight->Level / Level->Run roll-up (R4): every MetricId folds into this scope
        // by its own declared rule, so adding a metric never touches this loop. Gated on Scope: a
        // metric declared above the child being folded is skipped, because Sum/Max tolerate absorbing
        // a zero (their identity) but Last does not — e.g. RetriesUsed is Last at Run scope, so folding
        // a Level child's untouched zero would report zero retries on every run record.
        //
        // Counters and axes only — never timers. Every scope runs its own clocks, driven together by
        // the service (pausing the gameplay clock is one loop over the scopes, not one call per scope
        // per timer). A Run scope's Gameplay stopwatch has already measured the whole run; adding each
        // Level's elapsed on top would double it (see "Every scope runs its own clocks" in
        // PLAN-GameplayTelemetry.md's Read model section).
        //
        // The child must be exactly one scope below this one — Absorb has no way to tell "fold Level
        // into Session" apart from a caller that meant to fold through Run first, and folding a
        // non-adjacent or identical scope silently skips or double-counts the scope in between.
        public void Absorb(MetricScope child)
        {
            if (ReferenceEquals(child, this))
            {
                throw new ArgumentException("A scope cannot absorb itself.", nameof(child));
            }

            if (child.Scope != _scope - 1)
            {
                throw new ArgumentException(
                    $"Absorb requires a child exactly one scope below {_scope} (got {child.Scope}).",
                    nameof(child));
            }

            var ids = MetricCatalog.AllIds;
            for (var i = 0; i < ids.Length; i++)
            {
                var id = ids[i];
                if (MetricCatalog.ScopeOf(id) > child.Scope)
                {
                    continue;
                }

                var value = child.Metrics[id];
                switch (MetricCatalog.FoldOf(id))
                {
                    case FoldRule.Sum:
                        _metrics.Add(id, value);
                        break;
                    case FoldRule.Max:
                        _metrics.RecordMax(id, value);
                        break;
                    case FoldRule.Last:
                        _metrics.SetLast(id, value);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(
                            nameof(id), MetricCatalog.FoldOf(id), $"Absorb has no case for this FoldRule ({id}).");
                }
            }

            AbsorbAxes(child);
        }

        public LevelMetricsSnapshot Seal(int levelIndex, bool completed)
        {
            if (_scope != MetricScopeKind.Level)
            {
                throw new InvalidOperationException(
                    $"Seal(int, bool) produces a LevelMetricsSnapshot and is only valid on a Level scope " +
                    $"(this scope is {_scope}).");
            }

            return new LevelMetricsSnapshot(CopyState(), levelIndex, completed);
        }

        public RunMetricsSnapshot Seal()
        {
            if (_scope != MetricScopeKind.Run)
            {
                throw new InvalidOperationException(
                    $"Seal() produces a RunMetricsSnapshot and is only valid on a Run scope " +
                    $"(this scope is {_scope}).");
            }

            return new RunMetricsSnapshot(CopyState());
        }

        public void Reset()
        {
            _metrics.Reset();
            for (var i = 0; i < _timers.Length; i++)
            {
                _timers[i].Reset();
            }
        }

        // Same catalog-driven loop as Absorb, over AllSlots instead of AllIds — one array-of-arrays
        // roll-up regardless of how many (MetricId, MetricAxis) pairs the catalog declares. Reads
        // through info.Slot rather than re-resolving (info.Id, info.Axis) — the slot index is already
        // in hand from the loop over AllSlots.
        private void AbsorbAxes(MetricScope child)
        {
            var slots = MetricCatalog.AllSlots;
            for (var i = 0; i < slots.Count; i++)
            {
                var info = slots[i];
                if (MetricCatalog.ScopeOf(info.Id) > child.Scope)
                {
                    continue;
                }

                var childBuckets = child.Metrics.AxisBucketsOf(info.Slot);
                for (var b = 0; b < childBuckets.Count; b++)
                {
                    _metrics.AddAxis(info.Id, info.Axis, b, childBuckets[b]);
                }
            }
        }

        // Shared by both Seal() overloads so the array-copying logic exists exactly once — a snapshot
        // must never alias the live arrays Reset() goes on to reuse. The raw per-slot arrays are kept
        // (not discarded after building the named breakdown lists) so the snapshots can implement
        // ISealedMetrics.AxisBucketsOf at no extra allocation.
        private MetricScopeState CopyState()
        {
            var counters = _metrics.CopyCounters();

            var timers = new float[_timers.Length];
            for (var i = 0; i < _timers.Length; i++)
            {
                timers[i] = _timers[i].Elapsed;
            }

            var slots = MetricCatalog.AllSlots;
            var axisSlots = new int[slots.Count][];
            for (var i = 0; i < slots.Count; i++)
            {
                axisSlots[i] = _metrics.CopyAxis(slots[i].Slot);
            }

            var popsByColor = BuildColorCounts(MetricId.Pops, axisSlots);
            var pointsByColor = BuildColorCounts(MetricId.PointsBanked, axisSlots);
            var popsByBalloonType = BuildBalloonTypeCounts(MetricId.Pops, axisSlots);
            var deflectsByBalloonType = BuildBalloonTypeCounts(MetricId.Deflects, axisSlots);
            var itemsActivated = BuildItemActivationCounts(axisSlots);

            return new MetricScopeState(counters, timers, axisSlots, popsByColor, popsByBalloonType,
                deflectsByBalloonType, pointsByColor, itemsActivated);
        }

        private static ColorCount[] BuildColorCounts(MetricId id, int[][] axisSlots)
        {
            var bucket = axisSlots[MetricCatalog.SlotOf(id, MetricAxis.Color).Index];
            var result = new ColorCount[bucket.Length];
            for (var i = 0; i < bucket.Length; i++)
            {
                result[i] = new ColorCount(i, bucket[i]);
            }

            return result;
        }

        private static BalloonTypeCount[] BuildBalloonTypeCounts(MetricId id, int[][] axisSlots)
        {
            var bucket = axisSlots[MetricCatalog.SlotOf(id, MetricAxis.BalloonType).Index];
            var result = new BalloonTypeCount[bucket.Length];
            for (var i = 0; i < bucket.Length; i++)
            {
                result[i] = new BalloonTypeCount((BalloonType)i, bucket[i]);
            }

            return result;
        }

        private static ItemActivationCount[] BuildItemActivationCounts(int[][] axisSlots)
        {
            var bucket = axisSlots[MetricCatalog.SlotOf(MetricId.ItemsActivated, MetricAxis.ItemType).Index];
            var result = new ItemActivationCount[bucket.Length];
            for (var i = 0; i < bucket.Length; i++)
            {
                result[i] = new ItemActivationCount((ItemType)i, bucket[i]);
            }

            return result;
        }
    }
}
