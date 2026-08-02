using System.Collections.Generic;

namespace BalloonParty.Game.Telemetry
{
    // Read-only face of MetricSet — the segregated-interface shape the repo already uses for
    // write/read seams (IFlightStats/IFlightStatsWriter). Implemented by the live MetricSet, and (via
    // ISealedMetrics) by both immutable snapshots (LevelMetricsSnapshot, RunMetricsSnapshot).
    //
    // This is a read-only *view*, not an immutability guarantee: MetricSet.AxisBucketsOf returns its
    // live backing array typed as IReadOnlyList<int>, and that array still mutates under a caller
    // holding this interface if the underlying MetricSet is Reset() or written to concurrently. Only
    // ISealedMetrics — implemented exclusively by the two snapshots, never by MetricSet — carries the
    // actual "safe to hand across the read boundary" contract (R16); TelemetryEnvelope takes that, not
    // this. MetricScope.Metrics is the one legitimate place a live IReadOnlyMetricSet view belongs.
    internal interface IReadOnlyMetricSet
    {
        int this[MetricId id] { get; }

        IReadOnlyList<int> AxisBucketsOf(MetricId id, MetricAxis axis);

        // Slot-indexed twin of the (MetricId, MetricAxis) overload above — for callers (MetricScope's
        // Absorb/CopyState loops) that already hold the resolved AxisSlot from MetricCatalog.AllSlots
        // and would otherwise pay a second SlotOf lookup for a value they already have.
        IReadOnlyList<int> AxisBucketsOf(AxisSlot slot);
    }
}
