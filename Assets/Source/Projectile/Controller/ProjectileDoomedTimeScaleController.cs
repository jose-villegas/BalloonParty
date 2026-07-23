using System;
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
    ///     Claims a global slow-motion time scale for the duration of a shot's doomed 'last breath'
    ///     (see <see cref="ProjectileDoomedStartedMessage" />) and releases it when the moment ends.
    ///     The time-scale value is driven by an animation curve sampled on normalized doomed progress
    ///     (elapsed / duration), so the slow-mo can ease in and out instead of snapping to a flat value.
    /// </summary>
    internal sealed class ProjectileDoomedTimeScaleController : IStartable, ITickable, IDisposable
    {
        private readonly IProjectileFlightConfig _config;
        private readonly TimeScaleService _timeScale;
        private readonly ISubscriber<ProjectileDoomedStartedMessage> _doomedStartedSubscriber;
        private readonly ISubscriber<ProjectileDoomedEndedMessage> _doomedEndedSubscriber;
        private readonly CompositeDisposable _subscriptions = new();

        private bool _isDoomed;
        private float _doomedElapsed;

        [Inject]
        internal ProjectileDoomedTimeScaleController(
            IProjectileFlightConfig config,
            TimeScaleService timeScale,
            ISubscriber<ProjectileDoomedStartedMessage> doomedStartedSubscriber,
            ISubscriber<ProjectileDoomedEndedMessage> doomedEndedSubscriber)
        {
            _config = config;
            _timeScale = timeScale;
            _doomedStartedSubscriber = doomedStartedSubscriber;
            _doomedEndedSubscriber = doomedEndedSubscriber;
        }

        public void Start()
        {
            _doomedStartedSubscriber
                .Subscribe(_ => OnDoomedStarted())
                .AddTo(_subscriptions);
            _doomedEndedSubscriber
                .Subscribe(_ => OnDoomedEnded())
                .AddTo(_subscriptions);
        }

        public void Tick()
        {
            if (!_isDoomed)
            {
                return;
            }

            _doomedElapsed += Time.unscaledDeltaTime;
            var duration = _config.LastShieldApproachDuration;
            var progress = duration > 0f ? Mathf.Clamp01(_doomedElapsed / duration) : 1f;
            var scale = Mathf.Clamp01(_config.LastShieldTimeScaleCurve.Evaluate(progress));
            _timeScale.Claim(TimeScaleSource.LastShield, scale);
        }

        public void Dispose()
        {
            _timeScale.Release(TimeScaleSource.LastShield);
            _subscriptions.Dispose();
        }

        private void OnDoomedStarted()
        {
            _isDoomed = true;
            _doomedElapsed = 0f;
            var scale = Mathf.Clamp01(_config.LastShieldTimeScaleCurve.Evaluate(0f));
            _timeScale.Claim(TimeScaleSource.LastShield, scale);
        }

        private void OnDoomedEnded()
        {
            _isDoomed = false;
            _timeScale.Release(TimeScaleSource.LastShield);
        }
    }
}
