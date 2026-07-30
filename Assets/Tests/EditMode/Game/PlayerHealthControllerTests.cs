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

        private IRunConfig _config;
        private ISubscriber<WaveDamageMessage> _waveDamageSubscriber;
        private IMessageHandler<WaveDamageMessage> _waveDamageHandler;
        private ISubscriber<ScoreLevelUpMessage> _levelUpSubscriber;
        private IMessageHandler<ScoreLevelUpMessage> _levelUpHandler;
        private IPublisher<EndRunRequestedMessage> _endRunPublisher;

        private PlayerHealthController _controller;

        [SetUp]
        public void SetUp()
        {
            _config = Substitute.For<IRunConfig>();
            _config.StartingHitPoints.Returns(StartingHitPoints);

            _waveDamageSubscriber = Substitute.For<ISubscriber<WaveDamageMessage>>();
            _waveDamageSubscriber
                .Subscribe(
                    Arg.Do<IMessageHandler<WaveDamageMessage>>(h => _waveDamageHandler = h),
                    Arg.Any<MessageHandlerFilter<WaveDamageMessage>[]>())
                .Returns(Substitute.For<IDisposable>());

            _levelUpSubscriber = Substitute.For<ISubscriber<ScoreLevelUpMessage>>();
            _levelUpSubscriber
                .Subscribe(
                    Arg.Do<IMessageHandler<ScoreLevelUpMessage>>(h => _levelUpHandler = h),
                    Arg.Any<MessageHandlerFilter<ScoreLevelUpMessage>[]>())
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
        public void WaveDamage_CostsHearts()
        {
            DamageWave(1);

            Assert.AreEqual(StartingHitPoints - 1, _controller.Current.Value);
        }

        [Test]
        public void WaveDamage_MultipleHearts()
        {
            DamageWave(2);

            Assert.AreEqual(StartingHitPoints - 2, _controller.Current.Value);
        }

        [Test]
        public void ReachingZero_RequestsEndRunExactlyOnce()
        {
            DamageWave(StartingHitPoints);

            Assert.AreEqual(0, _controller.Current.Value);
            _endRunPublisher.Received(1).Publish(Arg.Any<EndRunRequestedMessage>());
        }

        [Test]
        public void WaveDamage_AtZero_DoesNotRequestAgainOrUnderflow()
        {
            DamageWave(StartingHitPoints + 5);

            Assert.AreEqual(0, _controller.Current.Value, "HP clamps at zero");
            _endRunPublisher.Received(1).Publish(Arg.Any<EndRunRequestedMessage>());
        }

        [Test]
        public void ResetRun_RestoresStartingHitPoints()
        {
            DamageWave(2);

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

        [Test]
        public void LevelUp_RestoresStartingHitPoints()
        {
            DamageWave(2);

            _levelUpHandler.Handle(new ScoreLevelUpMessage(1));

            Assert.AreEqual(StartingHitPoints, _controller.Current.Value);
        }

        private void DamageWave(int heartsLost)
        {
            _waveDamageHandler.Handle(new WaveDamageMessage(heartsLost, heartsLost * 6, 6));
        }

        private PlayerHealthController BuildController()
        {
            return new PlayerHealthController(_config, _waveDamageSubscriber, _levelUpSubscriber, _endRunPublisher);
        }
    }
}
