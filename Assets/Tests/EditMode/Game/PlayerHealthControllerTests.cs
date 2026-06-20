using System;
using BalloonParty.Game.Health;
using BalloonParty.Shared;
using BalloonParty.Shared.Messages;
using MessagePipe;
using NSubstitute;
using NUnit.Framework;

namespace BalloonParty.Tests.Game
{
    [TestFixture]
    public class PlayerHealthControllerTests
    {
        private const int StartingHitPoints = 3;

        private IGameConfiguration _config;
        private ISubscriber<SpawnBlockedMessage> _spawnBlockedSubscriber;
        private IMessageHandler<SpawnBlockedMessage> _spawnBlockedHandler;
        private IPublisher<EndRunRequestedMessage> _endRunPublisher;

        private PlayerHealthController _controller;

        [SetUp]
        public void SetUp()
        {
            _config = Substitute.For<IGameConfiguration>();
            _config.StartingHitPoints.Returns(StartingHitPoints);

            _spawnBlockedSubscriber = Substitute.For<ISubscriber<SpawnBlockedMessage>>();
            _spawnBlockedSubscriber
                .Subscribe(
                    Arg.Do<IMessageHandler<SpawnBlockedMessage>>(h => _spawnBlockedHandler = h),
                    Arg.Any<MessageHandlerFilter<SpawnBlockedMessage>[]>())
                .Returns(Substitute.For<IDisposable>());

            _endRunPublisher = Substitute.For<IPublisher<EndRunRequestedMessage>>();

            _controller = BuildController();
            _controller.Start();
        }

        [TearDown]
        public void TearDown()
        {
            _controller.Dispose();
        }

        [Test]
        public void Start_InitializesCurrentToStartingHitPoints()
        {
            Assert.AreEqual(StartingHitPoints, _controller.Current.Value);
        }

        [Test]
        public void BlockedSpawn_CostsOneHitPoint()
        {
            Block();

            Assert.AreEqual(StartingHitPoints - 1, _controller.Current.Value);
        }

        [Test]
        public void ReachingZero_RequestsEndRunExactlyOnce()
        {
            for (var i = 0; i < StartingHitPoints; i++)
            {
                Block();
            }

            Assert.AreEqual(0, _controller.Current.Value);
            _endRunPublisher.Received(1).Publish(Arg.Any<EndRunRequestedMessage>());
        }

        [Test]
        public void BlockedSpawn_AtZero_DoesNotRequestAgainOrUnderflow()
        {
            for (var i = 0; i < StartingHitPoints + 5; i++)
            {
                Block();
            }

            Assert.AreEqual(0, _controller.Current.Value, "HP clamps at zero");
            _endRunPublisher.Received(1).Publish(Arg.Any<EndRunRequestedMessage>());
        }

        [Test]
        public void ResetRun_RestoresStartingHitPoints()
        {
            Block();
            Block();

            _controller.ResetRun(2);

            Assert.AreEqual(StartingHitPoints, _controller.Current.Value);
        }

        [Test]
        public void Start_ClampsStartingHitPointsToHardCap()
        {
            _controller.Dispose();
            _config.StartingHitPoints.Returns(5000);

            _controller = BuildController();
            _controller.Start();

            Assert.AreEqual(999, _controller.Current.Value);
        }

        private void Block()
        {
            _spawnBlockedHandler.Handle(new SpawnBlockedMessage(0, default));
        }

        private PlayerHealthController BuildController()
        {
            return new PlayerHealthController(_config, _spawnBlockedSubscriber, _endRunPublisher);
        }
    }
}
