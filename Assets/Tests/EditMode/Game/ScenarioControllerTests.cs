using System;
using BalloonParty.Configuration.Level;
using BalloonParty.Game.Level;
using BalloonParty.Game.Run;
using BalloonParty.Scenario;
using BalloonParty.Shared.Messages;
using MessagePipe;
using NSubstitute;
using NUnit.Framework;
using UniRx;
using VContainer.Unity;

namespace BalloonParty.Tests.Game
{
    [TestFixture]
    public class ScenarioControllerTests
    {
        private ReactiveProperty<int> _level;
        private ActiveScenario _scenario;
        private ILevelPacingConfiguration _pacing;
        private IMessageHandler<LevelAscendStartedMessage> _ascendHandler;
        private IDisposable _subscription;
        private ScenarioController _controller;

        [SetUp]
        public void SetUp()
        {
            _level = new ReactiveProperty<int>(1);
            var levelProgress = Substitute.For<ILevelProgress>();
            levelProgress.Level.Returns(_level);

            _pacing = Substitute.For<ILevelPacingConfiguration>();
            _pacing.ScenarioForLevel(Arg.Any<int>())
                .Returns(call => (int)call[0] >= 16 ? ScenarioKind.Ground : ScenarioKind.Sky);

            _scenario = new ActiveScenario();

            _subscription = Substitute.For<IDisposable>();
            var subscriber = Substitute.For<ISubscriber<LevelAscendStartedMessage>>();
            subscriber
                .Subscribe(
                    Arg.Do<IMessageHandler<LevelAscendStartedMessage>>(h => _ascendHandler = h),
                    Arg.Any<MessageHandlerFilter<LevelAscendStartedMessage>[]>())
                .Returns(_subscription);

            _controller = new ScenarioController(_pacing, levelProgress, _scenario, subscriber);
        }

        [TearDown]
        public void TearDown()
        {
            _level.Dispose();
        }

        [Test]
        public void Start_SnapsToTheScenarioOfTheCurrentLevel()
        {
            _level.Value = 20;

            ((IStartable)_controller).Start();

            Assert.AreEqual(ScenarioKind.Ground, _scenario.Kind.Value);
        }

        [Test]
        public void Ascend_SwitchesToTheNewLevelsScenario()
        {
            ((IStartable)_controller).Start();
            Assert.AreEqual(ScenarioKind.Sky, _scenario.Kind.Value);

            // The level has already advanced by the time the Ascent starts.
            _level.Value = 16;
            _ascendHandler.Handle(default);

            Assert.AreEqual(ScenarioKind.Ground, _scenario.Kind.Value);
        }

        [Test]
        public void Ascend_BackIntoSky_SwitchesBack()
        {
            _level.Value = 16;
            ((IStartable)_controller).Start();

            _level.Value = 18;
            _ascendHandler.Handle(default);

            Assert.AreEqual(ScenarioKind.Ground, _scenario.Kind.Value);

            _pacing.ScenarioForLevel(19).Returns(ScenarioKind.Sky);
            _level.Value = 19;
            _ascendHandler.Handle(default);

            Assert.AreEqual(ScenarioKind.Sky, _scenario.Kind.Value);
        }

        [Test]
        public void ResetRun_SnapsToTheRestartLevelsScenario()
        {
            _level.Value = 30;
            ((IStartable)_controller).Start();

            _level.Value = 1;
            _controller.ResetRun(2);

            Assert.AreEqual(ScenarioKind.Sky, _scenario.Kind.Value);
        }

        [Test]
        public void LevelChangeWithoutAnAscend_LeavesTheScenarioAlone()
        {
            ((IStartable)_controller).Start();

            _level.Value = 20;

            Assert.AreEqual(ScenarioKind.Sky, _scenario.Kind.Value);
        }

        [Test]
        public void ResetOrder_RunsAfterTheLevelIsRestored()
        {
            Assert.Greater(_controller.ResetOrder, RunResetOrder.Score);
        }

        [Test]
        public void Dispose_ReleasesTheAscendSubscription()
        {
            ((IStartable)_controller).Start();

            _controller.Dispose();

            _subscription.Received(1).Dispose();
        }

        [Test]
        public void Dispose_BeforeStart_DoesNotThrow()
        {
            Assert.DoesNotThrow(_controller.Dispose);
        }
    }
}
