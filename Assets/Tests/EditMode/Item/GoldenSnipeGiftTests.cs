using System;
using BalloonParty.Balloon.Model;
using BalloonParty.Balloon.Type;
using BalloonParty.Game.Run;
using BalloonParty.Item.Snipe;
using BalloonParty.Shared;
using BalloonParty.Shared.Messages;
using BalloonParty.Slots.Capabilities;
using MessagePipe;
using NSubstitute;
using NUnit.Framework;
using UnityEngine;

namespace BalloonParty.Tests.Item
{
    /// <summary>
    ///     The first golden balloon's lesson: once it has turned away the configured number of hits, the next ball
    ///     loaded is a sure Snipe, once a run, counted on that balloon only.
    /// </summary>
    [TestFixture]
    public class GoldenSnipeGiftTests
    {
        private const int Needed = 3;

        private ISnipeGuarantee _guarantee;
        private IAssistSettings _settings;
        private IRetryState _retryState;
        private IMessageHandler<ActorHitMessage> _hits;
        private GoldenSnipeGift _gift;

        [SetUp]
        public void SetUp()
        {
            _guarantee = Substitute.For<ISnipeGuarantee>();
            _settings = Substitute.For<IAssistSettings>();
            _settings.FirstGoldenSnipeAfterDeflects.Returns(Needed);
            _retryState = Substitute.For<IRetryState>();
            var subscriber = Substitute.For<ISubscriber<ActorHitMessage>>();
            subscriber
                .Subscribe(Arg.Do<IMessageHandler<ActorHitMessage>>(h => _hits = h),
                    Arg.Any<MessageHandlerFilter<ActorHitMessage>[]>())
                .Returns(Substitute.For<IDisposable>());
            _gift = new GoldenSnipeGift(subscriber, _guarantee, _settings, _retryState);
            _gift.Start();
        }

        [TearDown]
        public void TearDown()
        {
            _gift.Dispose();
        }

        [Test]
        public void ThirdBounce_OffTheFirstGolden_GuaranteesTheNextSnipe()
        {
            var golden = Balloon(BalloonType.Golden);

            Deflect(golden, Needed - 1);
            _guarantee.DidNotReceive().GuaranteeNext();

            Deflect(golden, 1);
            _guarantee.Received(1).GuaranteeNext();
        }

        [Test]
        public void OnceGiven_FurtherBounces_GiveNoMore()
        {
            var golden = Balloon(BalloonType.Golden);

            Deflect(golden, Needed * 3);
            Deflect(Balloon(BalloonType.Golden), Needed);

            _guarantee.Received(1).GuaranteeNext();
        }

        [Test]
        public void BouncesOffALaterGolden_AreNotCounted()
        {
            var first = Balloon(BalloonType.Golden);
            Deflect(first, 1);

            Deflect(Balloon(BalloonType.Golden), Needed);

            _guarantee.DidNotReceive().GuaranteeNext();
        }

        [Test]
        public void OtherBalloonsAndOtherOutcomes_AreNotCounted()
        {
            Deflect(Balloon(BalloonType.Unbreakable), Needed);
            Deflect(Balloon(BalloonType.Disco), Needed);
            var golden = Balloon(BalloonType.Golden);
            for (var i = 0; i < Needed; i++)
            {
                _hits.Handle(new ActorHitMessage(golden, Vector3.zero, Vector3.zero, HitOutcome.None));
            }

            _guarantee.DidNotReceive().GuaranteeNext();
        }

        [Test]
        public void Zero_TurnsItOff()
        {
            _settings.FirstGoldenSnipeAfterDeflects.Returns(0);

            Deflect(Balloon(BalloonType.Golden), Needed);

            _guarantee.DidNotReceive().GuaranteeNext();
        }

        [Test]
        public void FreshRun_TeachesAgain()
        {
            Deflect(Balloon(BalloonType.Golden), Needed);
            _retryState.RetryLevel.Returns(0);
            _gift.ResetRun(1);

            Deflect(Balloon(BalloonType.Golden), Needed);

            _guarantee.Received(2).GuaranteeNext();
        }

        [Test]
        public void Retry_OnceGiven_DoesNotTeachAgain()
        {
            Deflect(Balloon(BalloonType.Golden), Needed);
            _retryState.RetryLevel.Returns(6);
            _gift.ResetRun(1);

            Deflect(Balloon(BalloonType.Golden), Needed);

            _guarantee.Received(1).GuaranteeNext();
        }

        [Test]
        public void Retry_BeforeItWasGiven_CountsAFreshGoldenFromZero()
        {
            Deflect(Balloon(BalloonType.Golden), Needed - 1);
            _retryState.RetryLevel.Returns(5);
            _gift.ResetRun(1);

            var golden = Balloon(BalloonType.Golden);
            Deflect(golden, Needed - 1);
            _guarantee.DidNotReceive().GuaranteeNext();

            Deflect(golden, 1);
            _guarantee.Received(1).GuaranteeNext();
        }

        private static IBalloonModel Balloon(BalloonType type)
        {
            var balloon = Substitute.For<IBalloonModel>();
            balloon.TypeName.Returns(type);
            return balloon;
        }

        private void Deflect(IBalloonModel balloon, int times)
        {
            for (var i = 0; i < times; i++)
            {
                _hits.Handle(new ActorHitMessage(balloon, Vector3.zero, Vector3.zero, HitOutcome.Deflect));
            }
        }
    }
}
