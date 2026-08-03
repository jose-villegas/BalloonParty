using System.Collections.Generic;
using System.Threading;
using BalloonParty.Game.Telemetry;
using Cysharp.Threading.Tasks;

namespace BalloonParty.Tests.Game
{
    // Hand-written test fake (not a mock) — recorded envelopes are asserted on directly rather than
    // through expectation syntax, matching ScoreControllerTests' capture pattern. Used by
    // CompositeTelemetrySinkTests (W2) and, later, GameplayMetricsServiceTests (W3).
    internal sealed class RecordingTelemetrySink : ITelemetrySink
    {
        internal readonly List<TelemetryEnvelope> Written = new();

        internal int FlushCalls;
        internal int DisposeCalls;

        public void Write(in TelemetryEnvelope envelope)
        {
            Written.Add(envelope);
        }

        public UniTask FlushAsync(CancellationToken cancellationToken)
        {
            FlushCalls++;
            return UniTask.CompletedTask;
        }

        public void Dispose()
        {
            DisposeCalls++;
        }
    }
}
