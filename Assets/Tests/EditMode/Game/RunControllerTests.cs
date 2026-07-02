using System;
using System.Collections.Generic;
using BalloonParty.Game.Run;
using BalloonParty.Shared.GameState;
using BalloonParty.Shared.Messages;
using MessagePipe;
using NSubstitute;
using NUnit.Framework;
using UniRx;

namespace BalloonParty.Tests.Game
{
    [TestFixture]
    public class RunControllerTests
    {
        private ReactiveProperty<NavigationState> _navState;
        private INavigation _navigation;
        private ICinematicState _cinematic;
        private IRunMeta _runMeta;
        private IRunScore _score;
        private IPublisher<GameOverMessage> _gameOverPublisher;
        private IPublisher<RunResetMessage> _resetPublisher;
        private ISubscriber<EndRunRequestedMessage> _endRunSubscriber;
        private IMessageHandler<EndRunRequestedMessage> _endRunHandler;

        [SetUp]
        public void SetUp()
        {
            _navState = new ReactiveProperty<NavigationState>(NavigationState.Game);
            _navigation = Substitute.For<INavigation>();
            _navigation.Current.Returns(_navState);

            _cinematic = Substitute.For<ICinematicState>();
            _cinematic.IsPlaying.Returns(false);
            _cinematic.Has(Arg.Any<CinematicTraits>()).Returns(false);

            _runMeta = Substitute.For<IRunMeta>();

            _score = Substitute.For<IRunScore>();
            _score.Level.Returns(new ReactiveProperty<int>(4));
            _score.TotalScore.Returns(new ReactiveProperty<int>(37));

            _gameOverPublisher = Substitute.For<IPublisher<GameOverMessage>>();
            _resetPublisher = Substitute.For<IPublisher<RunResetMessage>>();

            _endRunSubscriber = Substitute.For<ISubscriber<EndRunRequestedMessage>>();
            _endRunSubscriber
                .Subscribe(
                    Arg.Do<IMessageHandler<EndRunRequestedMessage>>(h => _endRunHandler = h),
                    Arg.Any<MessageHandlerFilter<EndRunRequestedMessage>[]>())
                .Returns(Substitute.For<IDisposable>());
        }

        [Test]
        public void EndRun_RecordsMetaWithFinalLevelAndScore()
        {
            CreateController().EndRun();

            _runMeta.Received(1).RecordRun(4, 37);
        }

        [Test]
        public void EndRun_PublishesGameOverOnce()
        {
            CreateController().EndRun();

            _gameOverPublisher.Received(1).Publish(
                Arg.Is<GameOverMessage>(m => m.FinalLevel == 4 && m.FinalScore == 37));
        }

        [Test]
        public void EndRun_TransitionsToGameOver()
        {
            CreateController().EndRun();

            _navigation.Received(1).TransitionTo(NavigationState.GameOver);
        }

        [Test]
        public void EndRun_WhileLossBlockingCinematic_DoesNothing()
        {
            _cinematic.Has(CinematicTraits.BlocksLoss).Returns(true);

            CreateController().EndRun();

            _runMeta.DidNotReceive().RecordRun(Arg.Any<int>(), Arg.Any<int>());
            _gameOverPublisher.DidNotReceive().Publish(Arg.Any<GameOverMessage>());
            _navigation.DidNotReceive().TransitionTo(NavigationState.GameOver);
        }

        [Test]
        public void EndRun_WhenNotInGame_DoesNothing()
        {
            _navState.Value = NavigationState.LevelUp;

            CreateController().EndRun();

            _navigation.DidNotReceive().TransitionTo(NavigationState.GameOver);
        }

        [Test]
        public void EndRun_WhenAlreadyGameOver_DoesNothing()
        {
            _navState.Value = NavigationState.GameOver;

            CreateController().EndRun();

            _runMeta.DidNotReceive().RecordRun(Arg.Any<int>(), Arg.Any<int>());
        }

        [Test]
        public void EndRunRequestedMessage_AfterStart_EndsTheRun()
        {
            var controller = CreateController();
            controller.Start();

            _endRunHandler.Handle(default);

            _navigation.Received(1).TransitionTo(NavigationState.GameOver);
        }

        [Test]
        public void RestartRun_InvokesResettablesInAscendingOrder()
        {
            var order = new List<int>();
            var first = new RecordingResettable(10, _ => order.Add(10));
            var second = new RecordingResettable(20, _ => order.Add(20));
            var third = new RecordingResettable(30, _ => order.Add(30));

            // Passed out of order — the controller must sort by ResetOrder.
            CreateController(second, third, first).RestartRun();

            Assert.AreEqual(new[] { 10, 20, 30 }, order.ToArray());
        }

        [Test]
        public void RestartRun_PassesOneIncrementingGenerationToAllResettables()
        {
            var a = new RecordingResettable(10, _ => { });
            var b = new RecordingResettable(20, _ => { });
            var controller = CreateController(a, b);

            controller.RestartRun();
            var firstGeneration = a.LastGeneration;
            Assert.AreEqual(firstGeneration, b.LastGeneration, "all resettables share one run number");

            controller.RestartRun();
            Assert.AreEqual(firstGeneration + 1, a.LastGeneration, "run number increments per restart");
        }

        [Test]
        public void RestartRun_TransitionsToGame()
        {
            CreateController().RestartRun();

            _navigation.Received(1).TransitionTo(NavigationState.Game);
        }

        [Test]
        public void RestartRun_PublishesRunReset()
        {
            CreateController().RestartRun();

            _resetPublisher.Received(1).Publish(Arg.Any<RunResetMessage>());
        }

        [Test]
        public void RestartRun_DoesNotRecordMeta()
        {
            CreateController().RestartRun();

            _runMeta.DidNotReceive().RecordRun(Arg.Any<int>(), Arg.Any<int>());
        }

        private RunController CreateController(params IRunResettable[] resettables)
        {
            return new RunController(
                _navigation,
                _cinematic,
                _runMeta,
                _score,
                _gameOverPublisher,
                _resetPublisher,
                _endRunSubscriber,
                resettables);
        }

        private sealed class RecordingResettable : IRunResettable
        {
            private readonly Action<int> _onReset;

            public RecordingResettable(int order, Action<int> onReset)
            {
                ResetOrder = order;
                _onReset = onReset;
            }

            public int ResetOrder { get; }

            public int LastGeneration { get; private set; }

            public void ResetRun(int generation)
            {
                LastGeneration = generation;
                _onReset(generation);
            }
        }
    }
}
