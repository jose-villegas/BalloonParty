using BalloonParty.Configuration;
using BalloonParty.Shared.Diagnostics;
using UnityEngine;
using VContainer.Unity;

namespace BalloonParty.Shared.Thermal
{
    /// <summary>
    ///     Thermal-aware frame-rate governor: walks a rate ladder (e.g. 120 / 80 / 60) so the app
    ///     settles on a lower but STABLE refresh rate under sustained thermal pressure instead of
    ///     juddering with half-missed vsyncs at the top rate. Asymmetric hysteresis (fast down, slow up,
    ///     plus a minimum dwell per rung) prevents oscillation.
    /// </summary>
    /// <remarks>
    ///     Decisions are driven purely by <see cref="IThermalSource" /> (headroom/status), never by any
    ///     reported refresh rate, so applying a down-step through <c>FrameRateSettings.ApplyGovernedRate</c>
    ///     cannot trigger the ARR echo-loop the MatchDisplay vote path guards against.
    ///     <para />
    ///     Time is fed in as accumulated deltas via <see cref="Advance" /> (called from
    ///     <see cref="ITickable.Tick" /> with unscaled delta time) so the state machine has no
    ///     <c>Time.*</c> dependency and EditMode tests can drive it directly.
    /// </remarks>
    internal sealed class ThermalFrameRateGovernor : IStartable, ITickable
    {
        private const string LogTag = "ThermalGovernor";

        private readonly IThermalGovernorSettings _settings;
        private readonly IThermalSource _thermal;
        private readonly int[] _ladder;
        private readonly bool _enabled;

        private int _rungIndex;
        private float _pollAccumulator;
        private float _downSustained;
        private float _upSustained;
        private float _dwell;

        internal int CurrentRate => _ladder.Length > 0 ? _ladder[_rungIndex] : -1;
        internal int CurrentRungIndex => _rungIndex;

        public ThermalFrameRateGovernor(IThermalGovernorSettings settings, IThermalSource thermal)
        {
            _settings = settings;
            _thermal = thermal;

            var ladder = settings.RateLadder;
            _ladder = new int[ladder?.Count ?? 0];
            for (var i = 0; i < _ladder.Length; i++)
            {
                _ladder[i] = ladder[i];
            }

            _enabled = settings.Enabled && _ladder.Length > 0;
        }

        void IStartable.Start()
        {
            // Intentionally does NOT apply a rate here. FrameRateSettings already voted the panel's
            // best rate at boot (the effective top rung); the governor only intervenes once a thermal
            // transition is warranted, so a disabled or still-cool device keeps the boot vote untouched.
        }

        void ITickable.Tick()
        {
            Advance(Time.unscaledDeltaTime);
        }

        internal void Advance(float dt)
        {
            // Gate here, not in Tick: Advance is the sanctioned seam (tests and any future caller),
            // so the Enabled flag must hold no matter who drives the state machine.
            if (!_enabled)
            {
                return;
            }

            _pollAccumulator += dt;
            if (_pollAccumulator < _settings.PollIntervalSeconds)
            {
                return;
            }

            var elapsed = _pollAccumulator;
            _pollAccumulator = 0f;
            Evaluate(elapsed);
        }

        private void Evaluate(float elapsed)
        {
            _dwell += elapsed;

            var headroom = _thermal.Headroom;
            var status = _thermal.Status;

            // Higher headroom means closer to the throttle threshold (hotter). Status is a coarse
            // secondary trigger for devices that report status but not headroom.
            var hot = headroom >= _settings.DownHeadroom || status >= 1;
            var cool = headroom <= _settings.UpHeadroom && status <= 0;

            if (hot)
            {
                _downSustained += elapsed;
                _upSustained = 0f;
            }
            else if (cool)
            {
                _upSustained += elapsed;
                _downSustained = 0f;
            }
            else
            {
                _downSustained = 0f;
                _upSustained = 0f;
            }

            if (_downSustained >= _settings.DownSustainSeconds && _rungIndex < _ladder.Length - 1)
            {
                StepTo(_rungIndex + 1, headroom, status);
            }
            else if (_upSustained >= _settings.UpSustainSeconds
                     && _dwell >= _settings.MinDwellSeconds
                     && _rungIndex > 0)
            {
                StepTo(_rungIndex - 1, headroom, status);
            }
        }

        private void StepTo(int newIndex, float headroom, int status)
        {
            var oldRate = _ladder[_rungIndex];
            _rungIndex = newIndex;
            var newRate = _ladder[_rungIndex];

            _downSustained = 0f;
            _upSustained = 0f;
            _dwell = 0f;

            Log.Info(LogTag, $"{oldRate} -> {newRate} fps (headroom {headroom:F2}, status {status})");
            FrameRateSettings.ApplyGovernedRate(newRate);
        }
    }
}
