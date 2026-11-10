using System;
using BalloonParty.Configuration.HeadsUp;
using BalloonParty.Shared.Messages;
using BalloonParty.Shared.Pool;
using MessagePipe;
using UniRx;
using VContainer.Unity;

namespace BalloonParty.Game.HeadsUp.Routing
{
    /// <summary>
    ///     <c>ready</c> beside the cannon for as long as it's ready: held from <see cref="ThrowerReadyMessage" />
    ///     until the ball fires. Play stopping (a level-up, game over, leaving the game) clears it with every other
    ///     heads-up.
    /// </summary>
    internal sealed class CannonHeadsUpRouter : IStartable, IDisposable
    {
        private readonly IHeadsUps _headsUps;
        private readonly ISubscriber<ThrowerReadyMessage> _readySubscriber;
        private readonly ISubscriber<ProjectileFiredMessage> _firedSubscriber;
        private readonly CompositeDisposable _subscriptions = new();

        private HeadsUpHandle _ready = HeadsUpHandle.None;

        internal CannonHeadsUpRouter(
            IHeadsUps headsUps,
            ISubscriber<ThrowerReadyMessage> readySubscriber,
            ISubscriber<ProjectileFiredMessage> firedSubscriber)
        {
            _headsUps = headsUps;
            _readySubscriber = readySubscriber;
            _firedSubscriber = firedSubscriber;
        }

        public void Start()
        {
            _readySubscriber.Subscribe(OnReady).AddTo(_subscriptions);
            _firedSubscriber.Subscribe(OnFired).AddTo(_subscriptions);
        }

        public void Dispose()
        {
            _subscriptions.Dispose();
        }

        private void OnReady(ThrowerReadyMessage msg)
        {
            _ready = _headsUps.Request(new HeadsUpRequest(
                HeadsUpStyleId.Ready, 0, HeadsUpAnchor.Endpoint(TrailEndpointKeys.Cannon)));
        }

        private void OnFired(ProjectileFiredMessage msg)
        {
            _headsUps.Release(_ready);
            _ready = HeadsUpHandle.None;
        }
    }
}
