namespace BalloonParty.UI.Telemetry
{
    // The only snapshot-level identity fields reachable outside MetricId/TimerId — LevelIndex and
    // Completed live directly on LevelMetricsSnapshot (see MetricsSnapshotBase). Everything else the
    // envelope carries (RunId, SessionId, SchemaVersion, ...) lives on TelemetryEnvelope, never on the
    // snapshot itself, so it is out of reach for a MetricLabel by construction.
    internal enum RecordField
    {
        LevelIndex,
        Completed
    }
}
