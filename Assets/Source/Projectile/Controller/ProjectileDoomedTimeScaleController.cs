using System;
using BalloonParty.Projectile.Model;
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
        private readonly IProjectileRegistry _projectiles;
        private readonly ISubscriber<ProjectileDoomedStartedMessage> _doomedStartedSubscriber;
        private readonly ISubscriber<ProjectileDoomedEndedMessage> _doomedEndedSubscriber;
        private readonly CompositeDisposable _subscriptions = new();

        private bool _isDoomed;
        private IProjectileModel _doomedShot;
        private float _doomedElapsed;

        [Inject]
        internal ProjectileDoomedTimeScaleController(
            IProjectileFlightConfig config,
            TimeScaleService timeScale,
            IProjectileRegistry projectiles,
            ISubscriber<ProjectileDoomedStartedMessage> doomedStartedSubscriber,
            ISubscriber<ProjectileDoomedEndedMessage> doomedEndedSubscriber)
        {
            _config = config;
            _timeScale = timeScale;
            _projectiles = projectiles;
            _doomedStartedSubscriber = doomedStartedSubscriber;
            _doomedEndedSubscriber = doomedEndedSubscriber;
        }

        public void Start()
        {
            _doomedStartedSubscriber
                .Subscribe(msg => OnDoomedStarted(msg.Projectile))
                .AddTo(_subscriptions);
            _doomedEndedSubscriber
                .Subscribe(msg => OnDoomedEnded(msg.Projectile))
                .AddTo(_subscriptions);
        }

        public void Tick()
        {
            if (!_isDoomed)
            {
                return;
            }

            // A doomed ball scaled away by game over or a reset never publishes DoomedEnded.
            if (_doomedShot != null && !_projectiles.IsTracked(_doomedShot))
            {
                OnDoomedEnded(_doomedShot);
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

        // The slow-mo follows the latest ball to start its last breath; an earlier ball's end must not
        // cut it short.
        private void OnDoomedStarted(IProjectileModel projectile)
        {
            _isDoomed = true;
            _doomedShot = projectile;
            _doomedElapsed = 0f;
            var scale = Mathf.Clamp01(_config.LastShieldTimeScaleCurve.Evaluate(0f));
            _timeScale.Claim(TimeScaleSource.LastShield, scale);
        }

        private void OnDoomedEnded(IProjectileModel projectile)
        {
            if (!_isDoomed || projectile != _doomedShot)
            {
                return;
            }

            _isDoomed = false;
            _doomedShot = null;
            _timeScale.Release(TimeScaleSource.LastShield);
        }
    }
}
