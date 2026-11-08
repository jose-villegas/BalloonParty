using System;
using BalloonParty.Projectile.Model;
using BalloonParty.Shared;
using BalloonParty.Shared.Messages;
using BalloonParty.Slots.Capabilities;
using MessagePipe;
using UniRx;
using VContainer;
using VContainer.Unity;

namespace BalloonParty.Projectile.Controller
{
    /// <summary>
    ///     Counts each ball's deflects off balloons since it last touched a wall, announces the run from
    ///     <see cref="DeflectChainRule.MinChain" /> deflects on (<see cref="DeflectChainMessage" />), and pays
    ///     the ball a shield every <see cref="IRunConfig.DeflectChainShieldEvery" /> in a row. A wall spends a
    ///     shield, so it breaks the run; pops don't.
    /// </summary>
    internal sealed class DeflectChainController : IStartable, IDisposable
    {
        private readonly ISubscriber<ActorHitMessage> _hitSubscriber;
        private readonly ISubscriber<WallHitMessage> _wallHitSubscriber;
        private readonly ISubscriber<ProjectileDestroyedMessage> _destroyedSubscriber;
        private readonly IPublisher<DeflectChainMessage> _chainPublisher;
        private readonly IPublisher<ShieldGainedMessage> _shieldGainedPublisher;
        private readonly IRunConfig _runConfig;
        private readonly ProjectileTally _chains = new();
        private readonly CompositeDisposable _subscriptions = new();

        [Inject]
        internal DeflectChainController(
            ISubscriber<ActorHitMessage> hitSubscriber,
            ISubscriber<WallHitMessage> wallHitSubscriber,
            ISubscriber<ProjectileDestroyedMessage> destroyedSubscriber,
            IPublisher<DeflectChainMessage> chainPublisher,
            IPublisher<ShieldGainedMessage> shieldGainedPublisher,
            IRunConfig runConfig)
        {
            _hitSubscriber = hitSubscriber;
            _wallHitSubscriber = wallHitSubscriber;
            _destroyedSubscriber = destroyedSubscriber;
            _chainPublisher = chainPublisher;
            _shieldGainedPublisher = shieldGainedPublisher;
            _runConfig = runConfig;
        }

        public void Start()
        {
            _hitSubscriber.Subscribe(OnActorHit).AddTo(_subscriptions);
            _wallHitSubscriber.Subscribe(msg => _chains.Forget(msg.Projectile)).AddTo(_subscriptions);
            _destroyedSubscriber.Subscribe(msg => _chains.Forget(msg.Projectile)).AddTo(_subscriptions);
        }

        public void Dispose()
        {
            _subscriptions.Dispose();
            _chains.Clear();
        }

        private void OnActorHit(ActorHitMessage msg)
        {
            // Only the ball's own contacts: a bomb or laser hitting a tough balloon carries the ball that set
            // it off, but the ball didn't bank off anything.
            var ball = msg.Context.Projectile;
            if (ball == null || (msg.Outcome & HitOutcome.Deflect) == 0
                || (msg.Context.Flags & DamageFlags.DirectHit) == 0)
            {
                return;
            }

            var chain = _chains.Record(ball);
            if (chain < DeflectChainRule.MinChain)
            {
                return;
            }

            _chainPublisher.Publish(new DeflectChainMessage(ball, chain, msg.WorldPosition));
            if (DeflectChainRule.GrantsShield(chain, _runConfig.DeflectChainShieldEvery)
                && ball is IWriteableProjectileModel writeable)
            {
                writeable.ShieldsRemaining.Value++;
                _shieldGainedPublisher.Publish(new ShieldGainedMessage(ball, msg.Actor.SlotIndex));
            }
        }
    }
}
