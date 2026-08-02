using System;

namespace BalloonParty.Game.Telemetry
{
    // Owns its clock: constructed with a Func<float>, keeps _lastSample internally, and folds
    // clock() - _lastSample into Elapsed on Pause()/Resume()/Elapsed reads. Deliberately has no
    // Advance() — a "call it at every boundary" contract is exactly the kind of thing a later wave
    // forgets once under one call site; owning the clock makes miss-a-boundary bugs impossible.
    internal sealed class TelemetryStopwatch
    {
        private readonly Func<float> _clock;
        private float _elapsed;
        private float _lastSample;
        private bool _running;

        public float Elapsed
        {
            get
            {
                FoldPending();
                return _elapsed;
            }
        }

        public bool IsRunning => _running;

        public TelemetryStopwatch(Func<float> clock)
        {
            _clock = clock;
            _lastSample = clock();
        }

        public void Resume()
        {
            if (_running)
            {
                return;
            }

            _lastSample = _clock();
            _running = true;
        }

        public void Pause()
        {
            if (!_running)
            {
                return;
            }

            FoldPending();
            _running = false;
        }

        // Preserves whatever running state the clock already had — a level's Wall clock spans the
        // whole level lifetime and is never explicitly paused/resumed at the flush boundary (only
        // Gameplay is), so a Reset() that force-stopped every timer silently lost Wall from the second
        // level onward. Re-sampling _lastSample when still running avoids folding in the instant
        // between the last read and this Reset() as if it had already elapsed.
        public void Reset()
        {
            _elapsed = 0f;
            if (_running)
            {
                _lastSample = _clock();
            }
        }

        // A backwards-going clock (device clock skew, a suspect Func in a test) clamps its own delta to
        // zero rather than subtracting from Elapsed — and still resyncs _lastSample to `now`, so the lost
        // interval isn't double-clamped on the next read.
        private void FoldPending()
        {
            if (!_running)
            {
                return;
            }

            var now = _clock();
            var delta = now - _lastSample;
            _elapsed += delta > 0f ? delta : 0f;
            _lastSample = now;
        }
    }
}
