namespace BalloonParty.Game.Telemetry
{
    // The envelope's actual contract (R24). IReadOnlyMetricSet alone cannot express this[TimerId] —
    // both snapshots have one — which would force W2's serializer to downcast to a concrete snapshot
    // type, defeating R24's one-uniform-record requirement. It also cannot promise "safe to alias":
    // MetricSet.AxisBucketsOf hands back its live backing array through that interface, so a plain
    // IReadOnlyMetricSet is not proof the data behind it is immutable (see IReadOnlyMetricSet's
    // remarks). ISealedMetrics is implemented only by LevelMetricsSnapshot and RunMetricsSnapshot —
    // never by MetricSet — so a reference typed as ISealedMetrics is the actual "this came from a
    // Seal(), it will never change under you" guarantee TelemetryEnvelope needs.
    internal interface ISealedMetrics : IReadOnlyMetricSet
    {
        float this[TimerId id] { get; }
    }
}
