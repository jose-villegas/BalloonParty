using System;
using BalloonParty.Game.Run;
using BalloonParty.Item.Snipe;
using BalloonParty.Shared;
using BalloonParty.Shared.Messages;
using MessagePipe;
using NSubstitute;
using NUnit.Framework;

namespace BalloonParty.Tests.Item
{
    /// <summary>
    ///     The Sharpshooter card's first showing: the first time the run wins it, the next ball loaded is a sure Snipe,
    ///     once a run.
    /// </summary>
    [TestFixture]
    public class SharpshooterSnipeGiftTests
    {
        private ISnipeGuarantee _guarantee;
        private IRetryState _retryState;
        private IMessageHandler<UpgradeCardHeldMessage> _held;
        private SharpshooterSnipeGift _gift;

        [SetUp]
        public void SetUp()
        {
            _guarantee = Substitute.For<ISnipeGuarantee>();
            _retryState = Substitute.For<IRetryState>();
            var subscriber = Substitute.For<ISubscriber<UpgradeCardHeldMessage>>();
            subscriber
                .Subscribe(Arg.Do<IMessageHandler<UpgradeCardHeldMessage>>(h => _held = h),
                    Arg.Any<MessageHandlerFilter<UpgradeCardHeldMessage>[]>())
                .Returns(Substitute.For<IDisposable>());
            _gift = new SharpshooterSnipeGift(subscriber, _guarantee, _retryState);
            _gift.Start();
        }

        [TearDown]
        public void TearDown()
        {
            _gift.Dispose();
        }

        [Test]
        public void FirstSharpshooterWon_GuaranteesTheNextSnipe()
        {
            Hold(UpgradeKind.SnipeChance, 1);

            _guarantee.Received(1).GuaranteeNext();
        }

        [Test]
        public void AnotherCard_GuaranteesNothing()
        {
            Hold(UpgradeKind.Shield, 1);
            Hold(UpgradeKind.RewardOdds, 1);

            _guarantee.DidNotReceive().GuaranteeNext();
        }

        [Test]
        public void SecondSharpshooter_GivesNoMore()
        {
            Hold(UpgradeKind.SnipeChance, 1);
            Hold(UpgradeKind.SnipeChance, 2);

            _guarantee.Received(1).GuaranteeNext();
        }

        [Test]
        public void NewRun_GivesItAgain()
        {
            Hold(UpgradeKind.SnipeChance, 1);
            _retryState.RetryLevel.Returns(0);
            _gift.ResetRun(1);

            Hold(UpgradeKind.SnipeChance, 1);

            _guarantee.Received(2).GuaranteeNext();
        }

        [Test]
        public void Retry_KeepsItGiven()
        {
            Hold(UpgradeKind.SnipeChance, 1);
            _retryState.RetryLevel.Returns(4);
            _gift.ResetRun(1);

            Hold(UpgradeKind.SnipeChance, 2);

            _guarantee.Received(1).GuaranteeNext();
        }

        private void Hold(UpgradeKind kind, int stacks)
        {
            var card = Substitute.For<IUpgradeCardSettings>();
            card.Kind.Returns(kind);
            _held.Handle(new UpgradeCardHeldMessage(card, stacks));
        }
    }
}
