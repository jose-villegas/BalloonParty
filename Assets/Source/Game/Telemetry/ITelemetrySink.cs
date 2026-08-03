using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace BalloonParty.Game.Telemetry
{
    // The write seam (R24). One Write(in TelemetryEnvelope) with the RecordKind discriminator living
    // on the envelope, never an overload per record kind — a future record kind must not change every
    // sink. IDisposable so the future decorator chain (consent -> batch -> composite, W5) can release
    // an inner sink the same way a leaf sink releases its own resource.
    internal interface ITelemetrySink : IDisposable
    {
        void Write(in TelemetryEnvelope envelope);

        UniTask FlushAsync(CancellationToken cancellationToken);
    }
}
