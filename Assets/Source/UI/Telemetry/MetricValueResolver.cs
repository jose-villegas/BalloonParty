using System;
using System.Collections.Generic;
using System.Globalization;
using BalloonParty.Configuration.Palette;
using BalloonParty.Game.Telemetry;
using BalloonParty.Shared.Diagnostics;

namespace BalloonParty.UI.Telemetry
{
    /// <summary>
    ///     Turns one <see cref="MetricBinding" /> plus a snapshot into the object a label formats.
    ///     Pure C# and the only place that knows how a binding becomes a number, so it is the only
    ///     part of W4 that needs tests.
    /// </summary>
    internal sealed class MetricValueResolver
    {
        // What a label shows when its binding cannot be resolved — the same placeholder
        // PlainCounterDisplay uses for an unbound counter, so the two never look like different bugs.
        internal const string Placeholder = "--";

        private const string HundredthsUnit = "level_hundredths";

        private readonly IGamePalette _palette;

        // A colour that has left the palette is a permanent authoring mistake, not a transient state:
        // the binding is serialized in a prefab and will fail identically on every snapshot for the
        // rest of the session. Warning per resolve would flood the console during a popup.
        private readonly HashSet<string> _warnedColorNames = new();

        internal MetricValueResolver(IGamePalette palette)
        {
            _palette = palette;
        }

        internal string Resolve(ISealedMetrics snapshot, in MetricBinding binding)
        {
            return Resolve(snapshot, binding, out _);
        }

        // isZero answers "is this value nothing" from the number, never from the rendered text. A caller
        // that string-matched the output against "0" would break the day a metric's unit changed its
        // shape, and silently on any culture whose digit glyphs are not ASCII.
        internal string Resolve(ISealedMetrics snapshot, in MetricBinding binding, out bool isZero)
        {
            isZero = false;
            if (snapshot == null || !IsAuthorable(binding))
            {
                return Placeholder;
            }

            switch (binding.Kind)
            {
                case MetricValueKind.Metric:
                    var value = snapshot[binding.Metric];
                    isZero = value == 0;
                    return FormatCount(value, MetricCatalog.UnitOf(binding.Metric));
                case MetricValueKind.Timer:
                    var seconds = snapshot[binding.Timer];
                    isZero = seconds <= 0f;
                    return FormatSeconds(seconds);
                case MetricValueKind.AxisBucket:
                    return ResolveBucket(snapshot, binding, out isZero);
                case MetricValueKind.RecordField:
                    return ResolveField(snapshot, binding.Field);
                default:
                    return Placeholder;
            }
        }

        // A binding is serialized in a prefab and outlives the catalog it was authored against. Every
        // read below this point indexes a dense array or a dictionary keyed by the stored ordinal —
        // MetricsSnapshotBase's counters and timers by index, MetricCatalog.UnitOf/AxesOf/ScopeOf by
        // key — so an id that has since been REMOVED from its enum throws rather than reading zero.
        // Asked once here instead of four times below.
        private static bool IsAuthorable(in MetricBinding binding)
        {
            switch (binding.Kind)
            {
                case MetricValueKind.Metric:
                    return Enum.IsDefined(typeof(MetricId), binding.Metric);
                case MetricValueKind.Timer:
                    return Enum.IsDefined(typeof(TimerId), binding.Timer);
                case MetricValueKind.AxisBucket:
                    return Enum.IsDefined(typeof(MetricId), binding.Metric)
                        && Enum.IsDefined(typeof(MetricAxis), binding.Axis);
                case MetricValueKind.RecordField:
                    return Enum.IsDefined(typeof(RecordField), binding.Field);
                default:
                    return false;
            }
        }

        // m:ss below an hour, h:mm:ss above it. A bare "83.4" reads as a count in a stat line, which is
        // the whole failure mode the unit column exists to prevent.
        private static string FormatSeconds(float seconds)
        {
            if (seconds < 0f)
            {
                return Placeholder;
            }

            var total = (int)seconds;
            var minutes = total / 60;
            if (minutes < 60)
            {
                return string.Format(CultureInfo.InvariantCulture, "{0}:{1:00}", minutes, total % 60);
            }

            return string.Format(CultureInfo.InvariantCulture, "{0}:{1:00}:{2:00}",
                minutes / 60, minutes % 60, total % 60);
        }

        private static string FormatCount(int value, string unit)
        {
            // The danger level is stored as hundredths because MetricSet holds ints (see
            // GameplayMetricsService.DangerLevelScale) — rendering the raw 87 would read as a count.
            if (unit == HundredthsUnit)
            {
                return value.ToString(CultureInfo.CurrentCulture) + "%";
            }

            return value.ToString("N0", CultureInfo.CurrentCulture);
        }

        private static string ResolveField(ISealedMetrics snapshot, RecordField field)
        {
            // Run snapshots carry no level identity — a binding that asks for one is authored against
            // the wrong source, and a placeholder says so without taking the popup down.
            if (snapshot is not LevelMetricsSnapshot level)
            {
                return Placeholder;
            }

            switch (field)
            {
                case RecordField.LevelIndex:
                    return level.LevelIndex.ToString(CultureInfo.CurrentCulture);
                case RecordField.Completed:
                    return level.Completed.ToString();
                default:
                    return Placeholder;
            }
        }

        private string ResolveBucket(ISealedMetrics snapshot, in MetricBinding binding, out bool isZero)
        {
            isZero = false;

            // Asked BEFORE the read: AxisBucketsOf routes through MetricCatalog.SlotOf, which THROWS on
            // a pair the catalog does not declare — it never returns null. A binding is serialized in a
            // prefab, so it outlives the catalog row it was authored against: drop an axis from a metric
            // and every label still pointing at it would take the popup down on open.
            var axes = MetricCatalog.AxesOf(binding.Metric);
            var declared = false;
            for (var i = 0; i < axes.Count; i++)
            {
                if (axes[i] == binding.Axis)
                {
                    declared = true;
                    break;
                }
            }

            if (!declared)
            {
                return Placeholder;
            }

            var buckets = snapshot.AxisBucketsOf(binding.Metric, binding.Axis);
            var index = binding.Axis == MetricAxis.Color ? ColorBucketIndex(binding) : binding.BucketOrdinal;
            if (index < 0 || index >= buckets.Count)
            {
                return Placeholder;
            }

            isZero = buckets[index] == 0;
            return FormatCount(buckets[index], MetricCatalog.UnitOf(binding.Metric));
        }

        private int ColorBucketIndex(in MetricBinding binding)
        {
            var name = binding.ColorBucketName;
            if (AxisBucketNaming.TryColorBucketIndexOf(_palette.ProgressColorNames, name, out var index))
            {
                return index;
            }

            if (_warnedColorNames.Add(name))
            {
                Log.Warn("Telemetry", $"Metric label bound to colour '{name}', which is not in the " +
                    "palette — showing a placeholder. Re-pick the bucket on the label.");
            }

            return -1;
        }
    }
}
