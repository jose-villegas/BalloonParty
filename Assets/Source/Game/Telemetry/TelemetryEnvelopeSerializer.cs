using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using BalloonParty.Configuration.Palette;

namespace BalloonParty.Game.Telemetry
{
    // Hand-rolled, reflection-free JSON (R27). Neither library option works: Newtonsoft is
    // unreachable from the runtime asmdef (overrideReferences: true, DOTween only), and JsonUtility
    // serializes only Unity-serializable public fields — fed a readonly struct of properties plus an
    // interface-typed Metrics member it silently returns "{}", with no error and no way to emit a
    // record-kind discriminator.
    //
    // One reused StringBuilder field, cleared per record, never reallocated. The metrics/timers/axes
    // bodies are loops over MetricCatalog/TimerCatalog, not one append per field — that mechanical
    // loop is the entire reason those catalogs exist (R6). Every numeric and date append goes through
    // CultureInfo.InvariantCulture: on a comma-decimal device float.ToString() emits "12,5" and the
    // whole log becomes unparseable.
    //
    // Envelope-level key names (record_kind, schema_version, session_id, ...) live here rather than in
    // a catalog, but they are just as append-only once a file ships. Metric and timer wire names are
    // transcribed verbatim from MetricCatalog/TimerCatalog; axis buckets are keyed by name, never by
    // the dense index, so an enum insert cannot silently re-label historical data.
    internal sealed class TelemetryEnvelopeSerializer
    {
        private readonly IGamePalette _palette;
        private readonly StringBuilder _builder = new(512);

        public TelemetryEnvelopeSerializer(IGamePalette palette)
        {
            _palette = palette;
        }

        public string Serialize(in TelemetryEnvelope envelope)
        {
            _builder.Clear();
            _builder.Append('{');

            AppendKind(envelope.Kind);
            AppendInt("schema_version", envelope.SchemaVersion);
            AppendString("session_id", envelope.SessionId);
            AppendInt("run_id", envelope.RunId);
            AppendInt("attempt_id", envelope.AttemptId);
            AppendInt("attempt_index", envelope.AttemptIndex);
            AppendInt("level_index", envelope.LevelIndex);
            AppendInt("level_attempt_ordinal", envelope.LevelAttemptOrdinal);
            AppendInt("flight_index", envelope.FlightIndex);
            AppendBool("completed", envelope.Completed);
            AppendBool("cheat_active", envelope.CheatActive);
            AppendString("end_cause", envelope.EndCause);
            AppendTimestamp(envelope.TimestampUtcTicks);
            AppendMetrics(envelope.Metrics);
            AppendTimers(envelope.Metrics);
            AppendAxes(envelope.Metrics);

            _builder.Append('}');
            return _builder.ToString();
        }

        private static string RecordKindWireName(RecordKind kind)
        {
            return kind switch
            {
                RecordKind.Flight => "flight",
                RecordKind.Level => "level",
                RecordKind.Run => "run",
                RecordKind.Session => "session",
                _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unhandled RecordKind.")
            };
        }

        private static string AxisWireName(MetricAxis axis)
        {
            return axis switch
            {
                MetricAxis.Color => "color",
                MetricAxis.BalloonType => "balloon_type",
                MetricAxis.ItemType => "item_type",
                _ => throw new ArgumentOutOfRangeException(nameof(axis), axis, "Unhandled MetricAxis.")
            };
        }

        // Delegates to AxisBucketNaming (shared with MetricValueResolver in BalloonParty.UI.Telemetry,
        // which needs this same forward mapping plus its inverse) rather than keeping its own copy — see
        // that type for the "other"/"unknown" fallback rationale. (A palette colour literally named
        // "other" would merge into that bucket; no guard, just don't.)
        private string BucketName(MetricAxis axis, int bucket)
        {
            return AxisBucketNaming.BucketName(_palette.ProgressColorNames, axis, bucket);
        }

        private static bool AnyNonZero(IReadOnlyList<int> buckets)
        {
            for (var i = 0; i < buckets.Count; i++)
            {
                if (buckets[i] != 0)
                {
                    return true;
                }
            }

            return false;
        }

        // "record_kind", not "type": several analytics providers reserve a top-level "type" field and
        // remap it to their own event taxonomy, which would silently rewrite our discriminator.
        private void AppendKind(RecordKind kind)
        {
            BeginField();
            AppendKey("record_kind");
            AppendJsonString(RecordKindWireName(kind));
        }

        private void AppendString(string wireName, string value)
        {
            BeginField();
            AppendKey(wireName);
            if (value == null)
            {
                _builder.Append("null");
            }
            else
            {
                AppendJsonString(value);
            }
        }

        private void AppendInt(string wireName, int value)
        {
            BeginField();
            AppendKey(wireName);
            _builder.Append(value.ToString(CultureInfo.InvariantCulture));
        }

        private void AppendBool(string wireName, bool value)
        {
            BeginField();
            AppendKey(wireName);
            _builder.Append(value ? "true" : "false");
        }

        private void AppendTimestamp(long ticks)
        {
            BeginField();
            AppendKey("timestamp_utc");
            var utc = new DateTime(ticks, DateTimeKind.Utc);
            AppendJsonString(utc.ToString("o", CultureInfo.InvariantCulture));
        }

        // Skips zero-valued counters to keep lines small — the sparse cases (an unused item, a level
        // with no shields) are the common case, not the exception.
        private void AppendMetrics(ISealedMetrics metrics)
        {
            BeginField();
            AppendKey("metrics");
            _builder.Append('{');

            var ids = MetricCatalog.AllIds;
            for (var i = 0; i < ids.Length; i++)
            {
                var id = ids[i];
                var value = metrics[id];
                if (value == 0)
                {
                    continue;
                }

                BeginField();
                AppendKey(MetricCatalog.WireNameOf(id));
                _builder.Append(value.ToString(CultureInfo.InvariantCulture));
            }

            _builder.Append('}');
        }

        // Unlike counters, every timer is always emitted — a level with a genuine 0-second ceremony is
        // data, not clutter, and there are only ever four of them.
        private void AppendTimers(ISealedMetrics metrics)
        {
            BeginField();
            AppendKey("timers");
            _builder.Append('{');

            var ids = TimerCatalog.AllIds;
            for (var i = 0; i < ids.Length; i++)
            {
                var id = ids[i];
                BeginField();
                AppendKey(TimerCatalog.WireNameOf(id));
                _builder.Append(metrics[id].ToString(CultureInfo.InvariantCulture));
            }

            _builder.Append('}');
        }

        // One key per (MetricId, MetricAxis) slot declared in MetricCatalog.AllSlots, read through the
        // envelope's ISealedMetrics — never hand-unrolled per axis kind. A slot with every bucket at
        // zero is skipped entirely; a present slot still skips its own zero buckets, same rationale as
        // AppendMetrics.
        //
        // Buckets are keyed by NAME, not by dense index. The indices are enum ordinals and palette
        // positions, both of which have already shifted once (BalloonType gained Tougher) — an index-
        // keyed history would silently re-label itself on the next insert, and the warehouse has no
        // way to detect it.
        private void AppendAxes(ISealedMetrics metrics)
        {
            BeginField();
            AppendKey("axes");
            _builder.Append('{');

            var slots = MetricCatalog.AllSlots;
            for (var i = 0; i < slots.Count; i++)
            {
                var slot = slots[i];
                var buckets = metrics.AxisBucketsOf(slot.Slot);
                if (!AnyNonZero(buckets))
                {
                    continue;
                }

                BeginField();
                AppendKey($"{MetricCatalog.WireNameOf(slot.Id)}_{AxisWireName(slot.Axis)}");
                _builder.Append('{');

                for (var b = 0; b < buckets.Count; b++)
                {
                    var value = buckets[b];
                    if (value == 0)
                    {
                        continue;
                    }

                    BeginField();
                    AppendKey(BucketName(slot.Axis, b));
                    _builder.Append(value.ToString(CultureInfo.InvariantCulture));
                }

                _builder.Append('}');
            }

            _builder.Append('}');
        }

        // A comma before the next field, unless we are the first entry in the current object — checked
        // by inspecting the trailing character rather than tracking a separate "first" flag per nesting
        // level, so the same helper serves the top-level object and every nested one.
        private void BeginField()
        {
            var last = _builder[_builder.Length - 1];
            if (last != '{')
            {
                _builder.Append(',');
            }
        }

        private void AppendKey(string wireName)
        {
            _builder.Append('"').Append(wireName).Append("\":");
        }

        private void AppendJsonString(string value)
        {
            _builder.Append('"');
            foreach (var c in value)
            {
                switch (c)
                {
                    case '"':
                        _builder.Append("\\\"");
                        break;
                    case '\\':
                        _builder.Append("\\\\");
                        break;
                    case '\n':
                        _builder.Append("\\n");
                        break;
                    case '\r':
                        _builder.Append("\\r");
                        break;
                    case '\t':
                        _builder.Append("\\t");
                        break;
                    default:
                        if (c < 0x20)
                        {
                            _builder.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                        }
                        else
                        {
                            _builder.Append(c);
                        }

                        break;
                }
            }

            _builder.Append('"');
        }
    }
}
