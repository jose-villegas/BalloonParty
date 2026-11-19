using System;
using BalloonParty.Projectile;
using BalloonParty.Projectile.Model;
using BalloonParty.Shared;
using BalloonParty.Shared.Messages;
using MessagePipe;
using VContainer.Unity;

namespace BalloonParty.Item.Snipe
{
    /// <summary>
    ///     Pays the meadow's mushroom chain (@ref plan_meadow_mushrooms). It hears every bounce off a mushroom
    ///     and, when the chain rule answered <see cref="MushroomReward.Arm" /> for it, arms the ball as the Snipe
    ///     lance through <see cref="ISnipeArmer" /> and announces it (<see cref="MushroomLanceMessage" />).
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         It keeps no count and no state of its own, and makes no judgement of its own: the count lives on
    ///         the ball's flight, the rule that reads it is <see cref="MushroomChainRule" />, and the answer
    ///         rides on the bounce's message. So a run reset, a level change and a game over leave nothing here
    ///         to clear.
    ///     </para>
    ///     <para>
    ///         <b>When the ball becomes a lance.</b> A bounce that pays ends the ball's fixed step where it
    ///         happened (<c>ProjectileMotionResolver</c>): the ball has turned and flown no further. The bounce
    ///         is published from inside that step, before the ball is moved, and messages are delivered at
    ///         once. So the ball is armed on the mushroom, at the point of the bounce that completed the chain,
    ///         and everything after that point (the next step, at the lance's speed, and whatever wall,
    ///         balloon or cap it meets) is the lance's: from there it bounces off no mushroom and cuts the caps in
    ///         its way. No wall, and so no death, shares a step with the arming.
    ///     </para>
    ///     <para>
    ///         Only a ball still in flight is paid. One that has died, or was taken off the board by a reset, a
    ///         level change or the game over, is no longer in flight in the registry, and its reward is dropped
    ///         with it.
    ///     </para>
    /// </remarks>
    internal sealed class MushroomChainController : IStartable, IDisposable
    {
        private readonly ISubscriber<MushroomBouncedMessage> _bouncedSubscriber;
        private readonly IPublisher<MushroomLanceMessage> _lancePublisher;
        private readonly ISnipeArmer _armer;
        private readonly IProjectileRegistry _projectiles;

        private IDisposable _subscription;

        internal MushroomChainController(
            ISubscriber<MushroomBouncedMessage> bouncedSubscriber, IPublisher<MushroomLanceMessage> lancePublisher,
            ISnipeArmer armer, IProjectileRegistry projectiles)
        {
            _bouncedSubscriber = bouncedSubscriber;
            _lancePublisher = lancePublisher;
            _armer = armer;
            _projectiles = projectiles;
        }

        public void Start()
        {
            _subscription = _bouncedSubscriber.Subscribe(OnMushroomBounced);
        }

        public void Dispose()
        {
            _subscription?.Dispose();
        }

        private void OnMushroomBounced(MushroomBouncedMessage msg)
        {
            if (msg.Reward != MushroomReward.Arm)
            {
                return;
            }

            if (msg.Projectile is not IWriteableProjectileModel shot || !_projectiles.IsInFlight(shot))
            {
                return;
            }

            _armer.ArmAt(shot, msg.Point);
            _lancePublisher.Publish(new MushroomLanceMessage(shot, msg.Point, msg.Count));
        }
    }
}
