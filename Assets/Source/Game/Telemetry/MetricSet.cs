using System;
using System.Collections.Generic;

namespace BalloonParty.Game.Telemetry
{
    // Dense storage only (R5, R29): a counters array over MetricId plus one array per declared
    // AxisSlot — one per (MetricId, MetricAxis) pair, not one per axis kind (see "Axis storage" in
    // PLAN-GameplayTelemetry.md). BalloonType/ItemType slots are sized from their own enum length; a
    // Color slot's size is caller-supplied because it depends on the configured palette
    // (ProgressColorNames plus a trailing "other" bucket), which this pure-C# type has no access to.
    // Every write below is a bounds-checked array index — no Dictionary, no LINQ, no allocation past
    // construction.
    internal sealed class MetricSet : IReadOnlyMetricSet
    {
        private static readonly int MetricCount = Enum.GetValues(typeof(MetricId)).Length;

        private readonly int[] _counters;
        private readonly int[][] _axisSlots;

        public int this[MetricId id] => _counters[(int)id];

        public MetricSet(int colorAxisSize)
        {
            _counters = new int[MetricCount];

            var slots = MetricCatalog.AllSlots;
            _axisSlots = new int[slots.Count][];
            for (var i = 0; i < slots.Count; i++)
            {
                var info = slots[i];
                var bucketCount = info.BucketCount == AxisSlotInfo.RuntimeSuppliedBucketCount ? colorAxisSize : info.BucketCount;
                _axisSlots[i] = new int[bucketCount];
            }
        }

        public void Increment(MetricId id)
        {
            _counters[(int)id]++;
        }

        public void Add(MetricId id, int value)
        {
            _counters[(int)id] += value;
        }

        public void RecordMax(MetricId id, int value)
        {
            var index = (int)id;
            if (value > _counters[index])
            {
                _counters[index] = value;
            }
        }

        public void SetLast(MetricId id, int value)
        {
            _counters[(int)id] = value;
        }

        public void IncrementAxis(MetricId id, MetricAxis axis, int bucket)
        {
            _axisSlots[MetricCatalog.SlotOf(id, axis).Index][bucket]++;
        }

        public void AddAxis(MetricId id, MetricAxis axis, int bucket, int value)
        {
            _axisSlots[MetricCatalog.SlotOf(id, axis).Index][bucket] += value;
        }

        public IReadOnlyList<int> AxisBucketsOf(MetricId id, MetricAxis axis)
        {
            return _axisSlots[MetricCatalog.SlotOf(id, axis).Index];
        }

        public IReadOnlyList<int> AxisBucketsOf(AxisSlot slot)
        {
            return _axisSlots[slot.Index];
        }

        // Snapshots must never alias the live arrays — Reset() reuses this instance, and a later reset
        // must not corrupt a snapshot already handed out (R16, "Immutable across the read boundary").
        public int[] CopyCounters()
        {
            return (int[])_counters.Clone();
        }

        public int[] CopyAxis(MetricId id, MetricAxis axis)
        {
            return (int[])_axisSlots[MetricCatalog.SlotOf(id, axis).Index].Clone();
        }

        public int[] CopyAxis(AxisSlot slot)
        {
            return (int[])_axisSlots[slot.Index].Clone();
        }

        public void Reset()
        {
            Array.Clear(_counters, 0, _counters.Length);
            for (var i = 0; i < _axisSlots.Length; i++)
            {
                Array.Clear(_axisSlots[i], 0, _axisSlots[i].Length);
            }
        }
    }
}
