using System;
using BalloonParty.Balloon.Model;
using BalloonParty.Balloon.Reward;
using BalloonParty.Balloon.Type;
using BalloonParty.Configuration;
using BalloonParty.Game.Health;
using BalloonParty.Game.Upgrades;
using BalloonParty.Shared.Messages;
using BalloonParty.Slots.Capabilities;
using MessagePipe;
using NSubstitute;
using NUnit.Framework;
using UnityEngine;

namespace BalloonParty.Tests.Balloon
{
    [TestFixture]
    public class RewardBalloonPayoutTests
    {
        private IFreeCardGrant _freeCards;
        private IPlayerHealing _healing;
        private RewardBalloonPayout _payout;

        [SetUp]
        public void SetUp()
        {
            _freeCards = Substitute.For<IFreeCardGrant>();
            _healing = Substitute.For<IPlayerHealing>();
            _payout = new RewardBalloonPayout(Substitute.For<ISubscriber<ActorHitMessage>>(), _freeCards, _healing);
        }

        [Test]
        public void TheGoldenBalloonsPop_GrantsAFreeCard()
        {
            _payout.OnHit(Hit(Golden(), HitOutcome.Pop));

            _freeCards.Received(1).GrantFreeCard(Arg.Any<Action>());
            _healing.DidNotReceiveWithAnyArgs().Heal(default);
        }

        [Test]
        public void EveryCardAtItsLimit_GivesAHeartInstead()
        {
            _freeCards.When(grant => grant.GrantFreeCard(Arg.Any<Action>())).Do(call => call.Arg<Action>()());

            _payout.OnHit(Hit(Golden(), HitOutcome.Pop));

            _healing.Received(1).Heal(1);
        }

        [TestCase(HitOutcome.Deflect)]
        [TestCase(HitOutcome.None)]
        public void AGoldenHitThatIsNoPop_PaysNothing(HitOutcome outcome)
        {
            _payout.OnHit(Hit(Golden(), outcome));

            _freeCards.DidNotReceive().GrantFreeCard(Arg.Any<Action>());
        }

        [Test]
        public void TheSameGoldenBalloonPoppedTwice_PaysOnce()
        {
            var golden = Golden();

            _payout.OnHit(Hit(golden, HitOutcome.Pop));
            _payout.OnHit(Hit(golden, HitOutcome.Pop));

            _freeCards.Received(1).GrantFreeCard(Arg.Any<Action>());
        }

        [Test]
        public void TheNextGoldenBalloon_PaysAgain()
        {
            _payout.OnHit(Hit(Golden(), HitOutcome.Pop));
            _payout.OnHit(Hit(Golden(), HitOutcome.Pop));

            _freeCards.Received(2).GrantFreeCard(Arg.Any<Action>());
        }

        [Test]
        public void AnotherBalloonsPop_PaysNothing()
        {
            var unbreakable = new UnbreakableBalloonModel(new BalloonModelConfig(BalloonType.Unbreakable));

            _payout.OnHit(Hit(unbreakable, HitOutcome.Pop));

            _freeCards.DidNotReceive().GrantFreeCard(Arg.Any<Action>());
        }

        private static GoldenBalloonModel Golden()
        {
            return new GoldenBalloonModel(new BalloonModelConfig(BalloonType.Golden));
        }

        private static ActorHitMessage Hit(IBalloonModel balloon, HitOutcome outcome)
        {
            var context = new DamageContext(1, DamageFlags.Piercing | DamageFlags.DirectHit);
            return new ActorHitMessage(balloon, Vector3.zero, Vector3.up, outcome, context);
        }
    }
}
