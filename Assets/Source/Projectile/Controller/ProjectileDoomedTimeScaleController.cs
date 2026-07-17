using System;
using BalloonParty.Shared;
using BalloonParty.Shared.Extensions;
using BalloonParty.Shared.Messages;
using BalloonParty.Shared.Pause;
using MessagePipe;
using UniRx;
using VContainer;
using VContainer.Unity;

namespace BalloonParty.Projectile.Controller
{
    /// <summary>
    ///     Claims a global slow-motion time scale for the duration of a shot's doomed 'last breath'
    ///     (see <see cref="ProjectileDoomedStartedMessage" />) and releases it when the moment ends —
    ///     the whole game eases into bullet-time while the shot drifts to the wall it dies on. Because
    ///     the approach duration is measured in GAME time, the slow-mo stretches it in real time
    ///     without changing the shot's own eased traversal.
    /// </summary>
    internal sealed class ProjectileDoomedTimeScaleController : IStartable, IDisposable
    {
        private readonly IGameConfiguration _config;
        private readonly TimeScaleService _timeScale;
        private readonly ISubscriber<ProjectileDoomedStartedMessage> _doomedStartedSubscriber;
        private readonly ISubscriber<ProjectileDoomedEndedMessage> _doomedEndedSubscriber;
        private readonly CompositeDisposable _subscriptions = new();

        [Inject]
        internal ProjectileDoomedTimeScaleController(
            IGameConfiguration config,
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
                .Subscribe(_ => _timeScale.Claim(TimeScaleSource.LastShield, _config.LastShieldTimeScale))
                .AddTo(_subscriptions);
            _doomedEndedSubscriber
                .Subscribe(_ => _timeScale.Release(TimeScaleSource.LastShield))
                .AddTo(_subscriptions);
        }

        public void Dispose()
        {
            _timeScale.Release(TimeScaleSource.LastShield);
            _subscriptions.Dispose();
        }
    }
}
