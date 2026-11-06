using System;
using System.Collections.Generic;
using BalloonParty.Projectile.Model;
using BalloonParty.Shared;
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
    ///     Claims a global slow-motion time scale for a shot's doomed 'last breath' (see
    ///     <see cref="ProjectileDoomedStartedMessage" />). The time-scale value is driven by an animation
    ///     curve sampled on normalized doomed progress (elapsed / duration), so the slow-mo can ease in
    ///     and out instead of snapping to a flat value. The slow-mo is world-wide, so it plays only while
    ///     the doomed ball flies alone (<see cref="ProjectileRegistryExtensions.DoomedShotFliesAlone" />,
    ///     the same rule the spawner and balancer hold to); with other balls in flight it is released and
    ///     the doomed look stays on the ball. Each ball's last breath keeps running on wall time
    ///     meanwhile, so a ball left alone resumes where its last breath now is, not from the start.
    /// </summary>
    internal sealed class ProjectileDoomedTimeScaleController : IStartable, ITickable, IDisposable
    {
        private readonly IProjectileFlightConfig _config;
        private readonly TimeScaleService _timeScale;
        private readonly IProjectileRegistry _projectiles;
        private readonly ISubscriber<ProjectileDoomedStartedMessage> _doomedStartedSubscriber;
        private readonly CompositeDisposable _subscriptions = new();

        // When each ball's last breath began, on this controller's own wall-time clock.
        private readonly Dictionary<IProjectileModel, float> _doomedSince = new();

        private float _clock;

        [Inject]
        internal ProjectileDoomedTimeScaleController(
            IProjectileFlightConfig config,
            TimeScaleService timeScale,
            IProjectileRegistry projectiles,
            ISubscriber<ProjectileDoomedStartedMessage> doomedStartedSubscriber)
        {
            _config = config;
            _timeScale = timeScale;
            _projectiles = projectiles;
            _doomedStartedSubscriber = doomedStartedSubscriber;
        }

        public void Start()
        {
            _doomedStartedSubscriber
                .Subscribe(msg => OnDoomedStarted(msg.Projectile))
                .AddTo(_subscriptions);
            _projectiles.Retired
                .Subscribe(projectile => _doomedSince.Remove(projectile))
                .AddTo(_subscriptions);
        }

        public void Tick()
        {
            Advance(Time.unscaledDeltaTime);
        }

        public void Dispose()
        {
            _timeScale.Release(TimeScaleSource.LastShield);
            _subscriptions.Dispose();
        }

        internal void Advance(float unscaledDeltaTime)
        {
            _clock += unscaledDeltaTime;
            Apply();
        }

        private void OnDoomedStarted(IProjectileModel projectile)
        {
            // An untracked ball would leave an entry no Retired ever drops.
            if (!_projectiles.IsTracked(projectile))
            {
                return;
            }

            _doomedSince[projectile] = _clock;
            Apply();
        }

        // Whether a doomed ball flies alone is read off the registry, never tracked from Doomed messages:
        // a ball scaled away by game over or a reset never publishes DoomedEnded, and a remembered
        // "latest doomed ball" goes stale when two balls are doomed and the later one dies first.
        private void Apply()
        {
            if (!_projectiles.DoomedShotFliesAlone()
                || !_doomedSince.TryGetValue(_projectiles.InFlightAt(0), out var since))
            {
                _timeScale.Release(TimeScaleSource.LastShield);
                return;
            }

            var duration = _config.LastShieldApproachDuration;
            var progress = duration > 0f ? Mathf.Clamp01((_clock - since) / duration) : 1f;
            var scale = Mathf.Clamp01(_config.LastShieldTimeScaleCurve.Evaluate(progress));
            _timeScale.Claim(TimeScaleSource.LastShield, scale);
        }
    }
}
