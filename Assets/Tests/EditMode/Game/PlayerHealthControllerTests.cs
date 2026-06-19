using System;
using System.Collections.Generic;
using BalloonParty.Game.Health;
using BalloonParty.Game.Run;
using BalloonParty.Shared;
using BalloonParty.Shared.GameState;
using BalloonParty.Shared.Messages;
using MessagePipe;
using NSubstitute;
using NUnit.Framework;
using UniRx;

namespace BalloonParty.Tests.Game
{
    [TestFixture]
    public class PlayerHealthControllerTests
    {
        private const int StartingHitPoints = 3;

        private IGameConfiguration _config;
        private ISubscriber<SpawnBlockedMessage> _spawnBlockedSubscriber;
        private IMessageHandler<SpawnBlockedMessage> _spawnBlockedHandler;

        private ReactiveProperty<NavigationState> _navState;
        private INavigation _navigation;
        private ICinematicState _cinematic;

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

            _navState = new ReactiveProperty<NavigationState>(NavigationState.Game);
            _navigation = Substitute.For<INavigation>();
            _navigation.Current.Returns(_navState);

            _cinematic = Substitute.For<ICinematicState>();
            _cinematic.IsPlaying.Returns(false);

            _controller = new PlayerHealthController(_config, _spawnBlockedSubscriber, BuildRunController());
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
        public void ReachingZero_EndsRunExactlyOnce()
        {
            for (var i = 0; i < StartingHitPoints; i++)
            {
                Block();
            }

            Assert.AreEqual(0, _controller.Current.Value);
            _navigation.Received(1).TransitionTo(NavigationState.GameOver);
        }

        [Test]
        public void BlockedSpawn_AtZero_DoesNotEndRunAgainOrUnderflow()
        {
            for (var i = 0; i < StartingHitPoints + 5; i++)
            {
                Block();
            }

            Assert.AreEqual(0, _controller.Current.Value, "HP clamps at zero");
            _navigation.Received(1).TransitionTo(NavigationState.GameOver);
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

            _controller = new PlayerHealthController(_config, _spawnBlockedSubscriber, BuildRunController());
            _controller.Start();

            Assert.AreEqual(999, _controller.Current.Value);
        }

        private void Block()
        {
            _spawnBlockedHandler.Handle(new SpawnBlockedMessage(0, default));
        }

        private RunController BuildRunController()
        {
            var runMeta = Substitute.For<IRunMeta>();

            var score = Substitute.For<IRunScore>();
            score.Level.Returns(new ReactiveProperty<int>(1));
            score.TotalScore.Returns(new ReactiveProperty<int>(0));

            var gameOverPublisher = Substitute.For<IPublisher<GameOverMessage>>();

            return new RunController(
                _navigation,
                _cinematic,
                runMeta,
                score,
                gameOverPublisher,
                Array.Empty<IRunResettable>());
        }
    }
}
