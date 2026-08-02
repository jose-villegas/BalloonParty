using System;
using System.Collections.Generic;
using BalloonParty.Balloon.Type;
using BalloonParty.Configuration.Items;

namespace BalloonParty.Game.Telemetry
{
    // Dense storage only (R5, R29): a counters array over MetricId plus one flattened array per
    // dimension axis. BalloonType/ItemType axes are sized from their own enum length; the color axis
    // size is caller-supplied because it depends on the configured palette (ProgressColorNames plus a
    // trailing "other" bucket) which this pure-C# type has no access to. Every write below is a
    // bounds-checked array index — no Dictionary, no LINQ, no allocation past construction.
    internal sealed class MetricSet : IReadOnlyMetricSet
    {
        private static readonly int MetricCount = Enum.GetValues(typeof(MetricId)).Length;
        private static readonly int BalloonTypeAxisSize = Enum.GetValues(typeof(BalloonType)).Length;
        private static readonly int ItemTypeAxisSize = Enum.GetValues(typeof(ItemType)).Length;

        private readonly int[] _counters;
        private readonly int[] _colorAxis;
        private readonly int[] _balloonTypeAxis;
        private readonly int[] _itemTypeAxis;

        public int this[MetricId id] => _counters[(int)id];

        public IReadOnlyList<int> ColorAxis => _colorAxis;

        public IReadOnlyList<int> BalloonTypeAxis => _balloonTypeAxis;

        public IReadOnlyList<int> ItemTypeAxis => _itemTypeAxis;

        public MetricSet(int colorAxisSize)
        {
            _counters = new int[MetricCount];
            _colorAxis = new int[colorAxisSize];
            _balloonTypeAxis = new int[BalloonTypeAxisSize];
            _itemTypeAxis = new int[ItemTypeAxisSize];
        }

        public void Increment(MetricId id)
        {
            _counters[(int)id]++;
        }

        public void Add(MetricId id, int value)
        {
            _counters[(int)id] += value;
        }

        // Min defaults to 0 (the array's zero value) until first recorded — a metric that wants a real
        // floor (e.g. MinHealth seeded at full HP) must Record its baseline before anything can lower it.
        public void RecordMin(MetricId id, int value)
        {
            var index = (int)id;
            if (value < _counters[index])
            {
                _counters[index] = value;
            }
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

        public void IncrementColorAxis(int colorIndex)
        {
            _colorAxis[colorIndex]++;
        }

        public void AddColorAxis(int colorIndex, int value)
        {
            _colorAxis[colorIndex] += value;
        }

        public void IncrementBalloonTypeAxis(BalloonType type)
        {
            _balloonTypeAxis[(int)type]++;
        }

        public void AddBalloonTypeAxis(BalloonType type, int value)
        {
            _balloonTypeAxis[(int)type] += value;
        }

        public void IncrementItemTypeAxis(ItemType type)
        {
            _itemTypeAxis[(int)type]++;
        }

        public void AddItemTypeAxis(ItemType type, int value)
        {
            _itemTypeAxis[(int)type] += value;
        }

        // Snapshots must never alias the live arrays — Reset() reuses this instance, and a later reset
        // must not corrupt a snapshot already handed out (R16, "Immutable across the read boundary").
        public int[] CopyCounters()
        {
            return (int[])_counters.Clone();
        }

        public int[] CopyColorAxis()
        {
            return (int[])_colorAxis.Clone();
        }

        public int[] CopyBalloonTypeAxis()
        {
            return (int[])_balloonTypeAxis.Clone();
        }

        public int[] CopyItemTypeAxis()
        {
            return (int[])_itemTypeAxis.Clone();
        }

        public void Reset()
        {
            Array.Clear(_counters, 0, _counters.Length);
            Array.Clear(_colorAxis, 0, _colorAxis.Length);
            Array.Clear(_balloonTypeAxis, 0, _balloonTypeAxis.Length);
            Array.Clear(_itemTypeAxis, 0, _itemTypeAxis.Length);
        }
    }
}
