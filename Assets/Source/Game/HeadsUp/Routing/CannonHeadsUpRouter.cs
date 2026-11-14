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
    ///     until the ball fires. A ball carrying the Snipe lance says <c>snipe!</c> instead, from the moment it
    ///     gets the lance, loaded with it or armed while it waits. Play stopping (a level-up, game over, leaving
    ///     the game) clears it with every other heads-up.
    /// </summary>
    internal sealed class CannonHeadsUpRouter : IStartable, IDisposable
    {
        private readonly IHeadsUps _headsUps;
        private readonly ISubscriber<ThrowerReadyMessage> _readySubscriber;
        private readonly ISubscriber<ProjectileFiredMessage> _firedSubscriber;
        private readonly CompositeDisposable _subscriptions = new();

        private HeadsUpHandle _ready = HeadsUpHandle.None;
        private IDisposable _lanceWatch;

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
            _lanceWatch?.Dispose();
            _subscriptions.Dispose();
        }

        private void OnReady(ThrowerReadyMessage msg)
        {
            _lanceWatch?.Dispose();
            _lanceWatch = null;
            var ball = msg.Projectile;
            if (ball != null && ball.IsPiercing.Value)
            {
                Show(HeadsUpStyleId.SnipeReady);
                return;
            }

            Show(HeadsUpStyleId.Ready);

            // Only the arming is watched: a ball loses its lance on going back to the pool, and a label
            // switching back to ready then would show up over a level-up that cleared it. An arming off the
            // game screen (a ball still flying into game over) writes nothing: the service takes no requests there.
            _lanceWatch = ball?.IsPiercing
                .Where(piercing => piercing)
                .Take(1)
                .Subscribe(_ =>
                {
                    _headsUps.Release(_ready);
                    Show(HeadsUpStyleId.SnipeReady);
                });
        }

        private void OnFired(ProjectileFiredMessage msg)
        {
            _lanceWatch?.Dispose();
            _lanceWatch = null;
            _headsUps.Release(_ready);
            _ready = HeadsUpHandle.None;
        }

        private void Show(HeadsUpStyleId style)
        {
            _ready = _headsUps.Request(new HeadsUpRequest(style, 0, HeadsUpAnchor.Endpoint(TrailEndpointKeys.Cannon)));
        }
    }
}
