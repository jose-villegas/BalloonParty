using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace BalloonParty.Game.Telemetry
{
    // Fans out to every registered leaf sink (R28b). An empty array IS the "no export configured"
    // state — do not add a NullTelemetrySink, an empty loop already behaves like one. Lands in W2
    // rather than W5 because W3 must resolve ITelemetrySink two waves before the consent/batching
    // decorators exist. Each leaf is expected to already be never-throw-guarded (TelemetrySinkBase or
    // equivalent) — this type does not re-guard them, it only distributes the call.
    internal sealed class CompositeTelemetrySink : ITelemetrySink
    {
        private readonly IReadOnlyList<ITelemetrySink> _sinks;

        public CompositeTelemetrySink(IReadOnlyList<ITelemetrySink> sinks)
        {
            _sinks = sinks;
        }

        public void Write(in TelemetryEnvelope envelope)
        {
            for (var i = 0; i < _sinks.Count; i++)
            {
                _sinks[i].Write(envelope);
            }
        }

        public async UniTask FlushAsync(CancellationToken cancellationToken)
        {
            for (var i = 0; i < _sinks.Count; i++)
            {
                await _sinks[i].FlushAsync(cancellationToken);
            }
        }

        public void Dispose()
        {
            for (var i = 0; i < _sinks.Count; i++)
            {
                _sinks[i].Dispose();
            }
        }
    }
}
