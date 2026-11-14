using System;
using BalloonParty.Balloon.Model;
using BalloonParty.Balloon.Type;
using BalloonParty.Game.Health;
using BalloonParty.Game.Upgrades;
using BalloonParty.Shared.Messages;
using BalloonParty.Slots.Capabilities;
using MessagePipe;
using VContainer.Unity;

namespace BalloonParty.Balloon.Reward
{
    /// <summary>
    ///     Pays a reward balloon's pop. The golden balloon gives a free card, or a heart back when every card is at
    ///     its limit; the heart balloon gives a heart back, up to the maximum. Only a pop pays: the leave
    ///     callback also fires for a board clear and a level change, and the level's closing pop wave publishes
    ///     no hit at all. A pierce can land after the level's last claim;
    ///     <see cref="IFreeCardGrant" /> holds that card back while the level-up hand is on screen, and gives the
    ///     heart then if the pick took the last card. The remover cheat's forced pop pays too.
    /// </summary>
    internal sealed class RewardBalloonPayout : IStartable, IDisposable
    {
        private readonly ISubscriber<ActorHitMessage> _hitSubscriber;
        private readonly IFreeCardGrant _freeCards;
        private readonly IPlayerHealing _healing;
        private readonly Action _giveAHeart;

        private IDisposable _subscription;
        // At most one of each is on the board at a time, so the last one paid of each is enough to never pay twice.
        private IBalloonModel _lastGoldenPaid;
        private IBalloonModel _lastHeartPaid;

        public RewardBalloonPayout(
            ISubscriber<ActorHitMessage> hitSubscriber, IFreeCardGrant freeCards, IPlayerHealing healing)
        {
            _hitSubscriber = hitSubscriber;
            _freeCards = freeCards;
            _healing = healing;
            _giveAHeart = GiveAHeart;
        }

        public void Start()
        {
            _subscription = _hitSubscriber.Subscribe(OnHit);
        }

        public void Dispose()
        {
            _subscription?.Dispose();
        }

        internal void OnHit(ActorHitMessage msg)
        {
            if (msg.Outcome != HitOutcome.Pop || msg.Actor is not IBalloonModel balloon)
            {
                return;
            }

            switch (balloon.TypeName)
            {
                case BalloonType.Golden when !ReferenceEquals(balloon, _lastGoldenPaid):
                    _lastGoldenPaid = balloon;
                    _freeCards.GrantFreeCard(_giveAHeart);
                    break;
                case BalloonType.Heart when !ReferenceEquals(balloon, _lastHeartPaid):
                    _lastHeartPaid = balloon;
                    GiveAHeart();
                    break;
            }
        }

        private void GiveAHeart()
        {
            _healing.Heal(1);
        }
    }
}
