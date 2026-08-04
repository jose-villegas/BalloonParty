using System;
using BalloonParty.Game.Telemetry;
using UnityEngine;

namespace BalloonParty.UI.Telemetry
{
    // Names exactly one value to render: which snapshot (MetricBindingSource), how to interpret it
    // (MetricValueKind), and the id(s) that kind needs. A [CustomPropertyDrawer(typeof(MetricBinding))]
    // rather than a custom Editor for MetricLabel is what lets this become MetricBinding[] later for free
    // — Unity reuses a property drawer per array element automatically, so a collection needs only a
    // field change plus a string.Format(format, values) overload, with MetricBindingDrawer untouched.
    // Never fold these fields directly into MetricLabel — that forfeits the same thing.
    [Serializable]
    internal struct MetricBinding
    {
        [SerializeField] private MetricBindingSource _source;
        [SerializeField] private MetricValueKind _kind;
        [SerializeField] private MetricId _metric;
        [SerializeField] private TimerId _timer;
        [SerializeField] private MetricAxis _axis;

        // Colour buckets store the bucket NAME, not its palette index — the colour axis is sized from
        // IGamePalette.ProgressColorNames at runtime, so a stored index would silently repoint every
        // label the day the palette is reordered.
        [SerializeField] private string _colorBucketName;

        // Balloon-type/item-type buckets are stable enum ordinals — no runtime reordering risk, so no
        // name indirection is needed for them.
        [SerializeField] private int _bucketOrdinal;

        [SerializeField] private RecordField _field;

        public MetricBindingSource Source => _source;

        public MetricValueKind Kind => _kind;

        public MetricId Metric => _metric;

        public TimerId Timer => _timer;

        public MetricAxis Axis => _axis;

        public string ColorBucketName => _colorBucketName;

        public int BucketOrdinal => _bucketOrdinal;

        public RecordField Field => _field;

        internal static MetricBinding ForMetric(MetricBindingSource source, MetricId metric)
        {
            return new MetricBinding { _source = source, _kind = MetricValueKind.Metric, _metric = metric };
        }

        internal static MetricBinding ForTimer(MetricBindingSource source, TimerId timer)
        {
            return new MetricBinding { _source = source, _kind = MetricValueKind.Timer, _timer = timer };
        }

        internal static MetricBinding ForColorBucket(MetricBindingSource source, MetricId metric, string colorName)
        {
            return new MetricBinding
            {
                _source = source,
                _kind = MetricValueKind.AxisBucket,
                _metric = metric,
                _axis = MetricAxis.Color,
                _colorBucketName = colorName
            };
        }

        internal static MetricBinding ForOrdinalBucket(
            MetricBindingSource source, MetricId metric, MetricAxis axis, int ordinal)
        {
            return new MetricBinding
            {
                _source = source,
                _kind = MetricValueKind.AxisBucket,
                _metric = metric,
                _axis = axis,
                _bucketOrdinal = ordinal
            };
        }

        internal static MetricBinding ForRecordField(MetricBindingSource source, RecordField field)
        {
            return new MetricBinding { _source = source, _kind = MetricValueKind.RecordField, _field = field };
        }
    }
}
