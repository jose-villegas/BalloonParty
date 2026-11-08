using System;
using BalloonParty.Game.Coins;
using BalloonParty.Shared;
using BalloonParty.Shared.Messages;
using BalloonParty.Shared.Pool;
using MessagePipe;
using NSubstitute;
using NUnit.Framework;
using UnityEngine;

namespace BalloonParty.Tests.Game
{
    // The flight path with a registered counter needs a pooled FlyingTrail and DOTween (PlayMode), so these
    // cover the no-target path the tests and HUD-less scenes take.
    [TestFixture]
    public class CoinTrailServiceTests
    {
        private IPublisher<CoinLandedMessage> _landedPublisher;
        private IDisposable _subscription;
        private IMessageHandler<CoinDroppedMessage> _droppedHandler;
        private CoinTrailService _service;

        [SetUp]
        public void SetUp()
        {
            var config = Substitute.For<IRunConfig>();
            config.Coins.Returns(Substitute.For<ICoinSettings>());

            _landedPublisher = Substitute.For<IPublisher<CoinLandedMessage>>();
            _subscription = Substitute.For<IDisposable>();

            var droppedSubscriber = Substitute.For<ISubscriber<CoinDroppedMessage>>();
            droppedSubscriber
                .Subscribe(
                    Arg.Do<IMessageHandler<CoinDroppedMessage>>(h => _droppedHandler = h),
                    Arg.Any<MessageHandlerFilter<CoinDroppedMessage>[]>())
                .Returns(_subscription);

            _service = new CoinTrailService(config, droppedSubscriber, _landedPublisher, new PoolManager());
            _service.Start();
        }

        [TearDown]
        public void TearDown()
        {
            _service?.Dispose();
        }

        [Test]
        public void CoinDropped_WithNoTarget_LandsAtOnceWithTheSameAmount()
        {
            var position = new Vector3(1f, 2f, 0f);

            _droppedHandler.Handle(new CoinDroppedMessage(3, position));

            _landedPublisher.Received(1).Publish(Arg.Is<CoinLandedMessage>(m =>
                m.Amount == 3 && m.WorldPosition == position));
            Assert.AreEqual(0, _service.FlyingCount);
        }

        [Test]
        public void ResetRun_WithNothingFlying_DoesNotThrowOrPublish()
        {
            Assert.DoesNotThrow(() => _service.ResetRun(1));

            _landedPublisher.DidNotReceiveWithAnyArgs().Publish(default);
        }

        [Test]
        public void HoldOutgoing_WithNothingFlying_DoesNotThrowOrPublish()
        {
            Assert.DoesNotThrow(() => _service.HoldOutgoing(null, 0f));
            Assert.DoesNotThrow(() => _service.ReleaseOutgoing());

            _landedPublisher.DidNotReceiveWithAnyArgs().Publish(default);
        }

        [Test]
        public void UnregisterTarget_UnknownEndpoint_StillLandsAtOnce()
        {
            _service.UnregisterTarget(Substitute.For<ITrailEndpoint>());

            _droppedHandler.Handle(new CoinDroppedMessage(1, Vector3.zero));

            _landedPublisher.Received(1).Publish(Arg.Any<CoinLandedMessage>());
        }

        [Test]
        public void Dispose_UnsubscribesFromDrops()
        {
            _service.Dispose();
            // Disposing twice would throw on the cancelled source; the teardown must skip it.
            _service = null;

            _subscription.Received(1).Dispose();
        }
    }
}
