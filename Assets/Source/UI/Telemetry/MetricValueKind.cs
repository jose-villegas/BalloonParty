namespace BalloonParty.UI.Telemetry
{
    // What MetricBinding's id fields mean once resolved against a snapshot — see MetricValueResolver.
    internal enum MetricValueKind
    {
        Metric,
        Timer,
        AxisBucket,
        RecordField
    }
}
