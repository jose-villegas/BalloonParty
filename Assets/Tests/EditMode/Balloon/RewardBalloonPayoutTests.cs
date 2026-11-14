using System;
using BalloonParty.Balloon.Model;
using BalloonParty.Balloon.Reward;
using BalloonParty.Balloon.Type;
using BalloonParty.Configuration;
using BalloonParty.Configuration.Level;
using BalloonParty.Game.Health;
using BalloonParty.Game.Level;
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
        private ILevelParameters _level;
        private RewardBalloonPayout _payout;

        [SetUp]
        public void SetUp()
        {
            _freeCards = Substitute.For<IFreeCardGrant>();
            _healing = Substitute.For<IPlayerHealing>();
            _level = Substitute.For<ILevelParameters>();
            var levelParams = Substitute.For<IActiveLevelParameters>();
            levelParams.Current.Returns(_level);
            _payout = new RewardBalloonPayout(
                Substitute.For<ISubscriber<ActorHitMessage>>(), _freeCards, _healing, levelParams);
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

        [Test]
        public void TheHeartBalloonsPop_GivesAHeart()
        {
            _payout.OnHit(Hit(Heart(), HitOutcome.Pop));

            _healing.Received(1).Heal(1);
            _freeCards.DidNotReceive().GrantFreeCard(Arg.Any<Action>());
        }

        [Test]
        public void TheHeartBalloonsPop_OnAPurpleLevel_GivesAnExtraHeart()
        {
            _level.IsExtraColorLevel.Returns(true);

            _payout.OnHit(Hit(Heart(), HitOutcome.Pop));

            _healing.Received(1).HealPastMax(1);
            _healing.DidNotReceiveWithAnyArgs().Heal(default);
        }

        [Test]
        public void TheHeartBalloonsPop_NoActiveLevel_StaysWithinTheMax()
        {
            var levelParams = Substitute.For<IActiveLevelParameters>();
            levelParams.Current.Returns((ILevelParameters)null);
            var payout = new RewardBalloonPayout(
                Substitute.For<ISubscriber<ActorHitMessage>>(), _freeCards, _healing, levelParams);

            payout.OnHit(Hit(Heart(), HitOutcome.Pop));

            _healing.Received(1).Heal(1);
            _healing.DidNotReceiveWithAnyArgs().HealPastMax(default);
        }

        [Test]
        public void TheGoldenBalloonsHeart_OnAPurpleLevel_StaysWithinTheMax()
        {
            _level.IsExtraColorLevel.Returns(true);
            _freeCards.When(grant => grant.GrantFreeCard(Arg.Any<Action>())).Do(call => call.Arg<Action>()());

            _payout.OnHit(Hit(Golden(), HitOutcome.Pop));

            _healing.Received(1).Heal(1);
            _healing.DidNotReceiveWithAnyArgs().HealPastMax(default);
        }

        [TestCase(HitOutcome.Deflect)]
        [TestCase(HitOutcome.None)]
        public void AHeartHitThatIsNoPop_PaysNothing(HitOutcome outcome)
        {
            _payout.OnHit(Hit(Heart(), outcome));

            _healing.DidNotReceiveWithAnyArgs().Heal(default);
        }

        [Test]
        public void TheSameHeartBalloonPoppedTwice_HealsOnce()
        {
            var heart = Heart();

            _payout.OnHit(Hit(heart, HitOutcome.Pop));
            _payout.OnHit(Hit(heart, HitOutcome.Pop));

            _healing.Received(1).Heal(1);
        }

        [Test]
        public void AGoldenPopBetweenTwoPopsOfOneHeart_StillHealsOnce()
        {
            var heart = Heart();

            _payout.OnHit(Hit(heart, HitOutcome.Pop));
            _payout.OnHit(Hit(Golden(), HitOutcome.Pop));
            _payout.OnHit(Hit(heart, HitOutcome.Pop));

            _healing.Received(1).Heal(1);
            _freeCards.Received(1).GrantFreeCard(Arg.Any<Action>());
        }

        [Test]
        public void TheNextHeartBalloon_HealsAgain()
        {
            _payout.OnHit(Hit(Heart(), HitOutcome.Pop));
            _payout.OnHit(Hit(Heart(), HitOutcome.Pop));

            _healing.Received(2).Heal(1);
        }

        private static HeartBalloonModel Heart()
        {
            return new HeartBalloonModel(new BalloonModelConfig(BalloonType.Heart));
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
