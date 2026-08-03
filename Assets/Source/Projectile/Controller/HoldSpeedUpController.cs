using System;
using BalloonParty.Game.Flight;
using BalloonParty.Shared;
using BalloonParty.Shared.Extensions;
using BalloonParty.Shared.Messages;
using BalloonParty.Shared.Pause;
using MessagePipe;
using UniRx;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace BalloonParty.Projectile.Controller
{
    /// <summary>
    ///     While a projectile is in flight, holding a finger on the screen lerps the global time scale
    ///     up to <see cref="IProjectileFlightConfig.HoldSpeedUpMax"/>. Releasing or projectile death
    ///     lerps back to 1×. Only active between <see cref="ProjectileFiredMessage"/> and
    ///     <see cref="ProjectileDestroyedMessage"/>.
    /// </summary>
    internal sealed class HoldSpeedUpController : IStartable, ITickable, IDisposable
    {
        private readonly IProjectileFlightConfig _config;
        private readonly TimeScaleService _timeScale;
        private readonly IFlightScope _flightScope;
        private readonly CompositeDisposable _subscriptions = new();

        private bool _inFlight;
        private float _t;
        private bool _wasActive;

        /// <summary>
        ///     True from the moment speed-up engages until one frame after the projectile dies.
        ///     The thrower checks this to suppress the fire that would otherwise occur on finger lift.
        /// </summary>
        internal bool ConsumedInput => _wasActive;

        [Inject]
        internal HoldSpeedUpController(
            IProjectileFlightConfig config,
            TimeScaleService timeScale,
            IFlightScope flightScope)
        {
            _config = config;
            _timeScale = timeScale;
            _flightScope = flightScope;
        }

        public void Start()
        {
            _flightScope.IsAirborne
                .Subscribe(OnAirborneChanged)
                .AddTo(_subscriptions);
        }

        public void Tick()
        {
            // Clear the consumed-input flag once the finger lifts after flight ends.
            // GetMouseButtonUp is true on the same frame GetMouseButton goes false, so we must
            // survive that frame — the thrower reads ConsumedInput on the Up frame.
            if (_wasActive && !_inFlight && !Input.GetMouseButton(0) && !Input.GetMouseButtonUp(0))
            {
                _wasActive = false;
            }

            if (!_inFlight)
            {
                return;
            }

            var holding = Input.GetMouseButton(0) && !InputHelper.PointerIsOverUI();
            var duration = _config.HoldSpeedUpLerpDuration;
            var step = duration > 0f ? Time.unscaledDeltaTime / duration : 1f;

            _t = holding
                ? Mathf.Min(_t + step, 1f)
                : Mathf.Max(_t - step, 0f);

            if (_t > 0f)
            {
                _wasActive = true;
                var scale = Mathf.Lerp(1f, _config.HoldSpeedUpMax, _t);
                _timeScale.Claim(TimeScaleSource.HoldSpeedUp, scale);
            }
            else
            {
                _timeScale.Release(TimeScaleSource.HoldSpeedUp);
            }
        }

        public void Dispose()
        {
            _timeScale.Release(TimeScaleSource.HoldSpeedUp);
            _subscriptions.Dispose();
        }

        private void OnAirborneChanged(bool airborne)
        {
            _inFlight = airborne;
            _t = 0f;

            if (!airborne)
            {
                _timeScale.Release(TimeScaleSource.HoldSpeedUp);
            }
        }
    }
}
