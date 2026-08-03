namespace BalloonParty.Game.Telemetry
{
    // One uniform record for every scope (R24) — a single Write(in TelemetryEnvelope) with the
    // RecordKind discriminator on the envelope itself, not an overload per record kind, so adding
    // Session later touches no sink. Carries ISealedMetrics, not IReadOnlyMetricSet: the latter cannot
    // express this[TimerId] (both snapshots have one), and its only other implementer is the live,
    // reset-in-place MetricSet — aliasing that in would break "immutable across the read boundary" the
    // moment a sink's deferred write ran after the scope's next reset.
    internal readonly struct TelemetryEnvelope
    {
        public RecordKind Kind { get; }

        public int SchemaVersion { get; }

        public string SessionId { get; }

        public int RunId { get; }

        public int AttemptId { get; }

        public int AttemptIndex { get; }

        public int LevelIndex { get; }

        public int LevelAttemptOrdinal { get; }

        public bool Completed { get; }

        public bool CheatActive { get; }

        public string EndCause { get; }

        public long TimestampUtcTicks { get; }

        public ISealedMetrics Metrics { get; }

        public TelemetryEnvelope(
            RecordKind kind,
            int schemaVersion,
            string sessionId,
            int runId,
            int attemptId,
            int attemptIndex,
            int levelIndex,
            int levelAttemptOrdinal,
            bool completed,
            bool cheatActive,
            string endCause,
            long timestampUtcTicks,
            ISealedMetrics metrics)
        {
            Kind = kind;
            SchemaVersion = schemaVersion;
            SessionId = sessionId;
            RunId = runId;
            AttemptId = attemptId;
            AttemptIndex = attemptIndex;
            LevelIndex = levelIndex;
            LevelAttemptOrdinal = levelAttemptOrdinal;
            Completed = completed;
            CheatActive = cheatActive;
            EndCause = endCause;
            TimestampUtcTicks = timestampUtcTicks;
            Metrics = metrics;
        }
    }
}
