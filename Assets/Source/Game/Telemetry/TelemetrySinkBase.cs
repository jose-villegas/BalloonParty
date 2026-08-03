using System;
using System.Threading;
using BalloonParty.Shared.Diagnostics;
using Cysharp.Threading.Tasks;

namespace BalloonParty.Game.Telemetry
{
    // The never-throw guard (R25), and only here — never in a single sink. MessagePipe runs handlers
    // synchronously on the publisher's stack with no exception isolation, and both flush points
    // (LevelUpPopUp's dismissal publish, RunController.EndRun) publish before releasing critical state,
    // so an escaping sink exception freezes the game. A future HTTP sink that throws at a flush
    // boundary must be exactly as safe as the file sink, which is only true if the guard lives in the
    // template method both extend, not in JsonLinesTelemetrySink.
    //
    // Write/FlushAsync share one latch (_disabled): once either hook throws, both become permanent
    // no-ops for the rest of the session rather than a second chance to fail the same way. Dispose is
    // guarded independently (_disposed) — a prior Write failure must not leak the resource DisposeCore
    // releases, and Dispose must stay idempotent regardless of what happened before it.
    internal abstract class TelemetrySinkBase : ITelemetrySink
    {
        private const string LogTag = "Telemetry";

        private bool _disabled;
        private bool _disposed;
        private bool _writing;

        public void Write(in TelemetryEnvelope envelope)
        {
            if (_disabled)
            {
                return;
            }

            // Re-entrancy drops the nested record rather than corrupting the outer one:
            // TelemetryEnvelopeSerializer reuses a single StringBuilder, so a nested Serialize would
            // Clear() the buffer the outer call is still filling — silent corruption, not a crash.
            // Nothing re-enters today; this makes that a property of the type rather than of the
            // current call graph.
            if (_writing)
            {
                Log.Warn(LogTag, $"{GetType().Name}.Write re-entered; the nested record was dropped.");
                return;
            }

            _writing = true;

            try
            {
                WriteCore(envelope);
            }
            catch (Exception ex)
            {
                Disable(ex, nameof(Write));
            }
            finally
            {
                _writing = false;
            }
        }

        public async UniTask FlushAsync(CancellationToken cancellationToken)
        {
            if (_disabled)
            {
                return;
            }

            try
            {
                await FlushCoreAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                Disable(ex, nameof(FlushAsync));
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;

            try
            {
                DisposeCore();
            }
            catch (Exception ex)
            {
                Disable(ex, nameof(Dispose));
            }
        }

        protected abstract void WriteCore(in TelemetryEnvelope envelope);

        protected virtual UniTask FlushCoreAsync(CancellationToken cancellationToken)
        {
            return UniTask.CompletedTask;
        }

        protected virtual void DisposeCore()
        {
        }

        private void Disable(Exception ex, string member)
        {
            _disabled = true;
            Log.Warn(LogTag, $"{GetType().Name}.{member} threw; sink disabled for the rest of the session: {ex}");
        }
    }
}
