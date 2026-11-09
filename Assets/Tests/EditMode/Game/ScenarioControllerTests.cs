using System;
using BalloonParty.Configuration.Cinematics;
using BalloonParty.Configuration.Level;
using BalloonParty.Game.Level;
using BalloonParty.Game.Run;
using BalloonParty.Scenario;
using BalloonParty.Shared.Messages;
using MessagePipe;
using NSubstitute;
using NUnit.Framework;
using UniRx;
using UnityEngine;
using VContainer.Unity;

namespace BalloonParty.Tests.Game
{
    [TestFixture]
    public class ScenarioControllerTests
    {
        private const int RunSeedValue = 4242;
        // Descent curve 1.2 s at speed 0.3 (4 s), restart rise 5 s: the shipped beats' shape.
        private const float AscentSeconds = 4f;
        private const float RiseSeconds = 5f;

        private ReactiveProperty<int> _level;
        private ActiveScenario _scenario;
        private ILevelPacingConfiguration _pacing;
        private IMessageHandler<LevelAscendStartedMessage> _ascendHandler;
        private IMessageHandler<RunResetMessage> _resetHandler;
        private IMessageHandler<RestartRiseStartedMessage> _riseHandler;
        private IMessageHandler<LevelTransitionCompletedMessage> _transitionCompletedHandler;
        private IMessageHandler<RunRestartCompletedMessage> _restartCompletedHandler;
        private IDisposable _subscription;
        private IDisposable _resetSubscription;
        private ScenarioController _controller;
        private IRunSeed _runSeed;

        [SetUp]
        public void SetUp()
        {
            _level = new ReactiveProperty<int>(1);
            var levelProgress = Substitute.For<ILevelProgress>();
            levelProgress.Level.Returns(_level);

            _pacing = Substitute.For<ILevelPacingConfiguration>();
            _pacing.ScenarioForLevel(Arg.Any<int>(), Arg.Any<int>())
                .Returns(call => (int)call[0] >= 16 ? ScenarioKind.Ground : ScenarioKind.Sky);

            _scenario = new ActiveScenario();

            _subscription = Substitute.For<IDisposable>();
            var subscriber = Substitute.For<ISubscriber<LevelAscendStartedMessage>>();
            subscriber
                .Subscribe(
                    Arg.Do<IMessageHandler<LevelAscendStartedMessage>>(h => _ascendHandler = h),
                    Arg.Any<MessageHandlerFilter<LevelAscendStartedMessage>[]>())
                .Returns(_subscription);

            _resetSubscription = Substitute.For<IDisposable>();
            var resetSubscriber = Substitute.For<ISubscriber<RunResetMessage>>();
            resetSubscriber
                .Subscribe(
                    Arg.Do<IMessageHandler<RunResetMessage>>(h => _resetHandler = h),
                    Arg.Any<MessageHandlerFilter<RunResetMessage>[]>())
                .Returns(_resetSubscription);

            var riseSubscriber = Substitute.For<ISubscriber<RestartRiseStartedMessage>>();
            riseSubscriber
                .Subscribe(
                    Arg.Do<IMessageHandler<RestartRiseStartedMessage>>(h => _riseHandler = h),
                    Arg.Any<MessageHandlerFilter<RestartRiseStartedMessage>[]>())
                .Returns(Substitute.For<IDisposable>());

            _runSeed = Substitute.For<IRunSeed>();
            _runSeed.Value.Returns(RunSeedValue);

            var cinematics = Substitute.For<ICinematicsSettings>();
            cinematics.LevelAscend.Returns(new LevelAscendSettings(
                AnimationCurve.Linear(0f, 1f, 1.2f, 0f), 0.3f, AnimationCurve.Linear(0f, 0f, RiseSeconds, 1f)));

            var transitionCompleted = Substitute.For<ISubscriber<LevelTransitionCompletedMessage>>();
            transitionCompleted
                .Subscribe(
                    Arg.Do<IMessageHandler<LevelTransitionCompletedMessage>>(h => _transitionCompletedHandler = h),
                    Arg.Any<MessageHandlerFilter<LevelTransitionCompletedMessage>[]>())
                .Returns(Substitute.For<IDisposable>());
            var restartCompleted = Substitute.For<ISubscriber<RunRestartCompletedMessage>>();
            restartCompleted
                .Subscribe(
                    Arg.Do<IMessageHandler<RunRestartCompletedMessage>>(h => _restartCompletedHandler = h),
                    Arg.Any<MessageHandlerFilter<RunRestartCompletedMessage>[]>())
                .Returns(Substitute.For<IDisposable>());

            _controller = new ScenarioController(
                _pacing, _runSeed, levelProgress, _scenario, cinematics, subscriber, resetSubscriber, riseSubscriber,
                transitionCompleted, restartCompleted);
        }

        [TearDown]
        public void TearDown()
        {
            _level.Dispose();
        }

        [Test]
        public void Start_CutsToTheScenarioOfTheCurrentLevel()
        {
            _level.Value = 20;

            ((IStartable)_controller).Start();

            Assert.AreEqual(new ScenarioShift(ScenarioKind.Ground, 0f), _scenario.Current.Value);
        }

        [Test]
        public void Start_AsksForTheLevelInThisRun()
        {
            _level.Value = 12;

            ((IStartable)_controller).Start();

            _pacing.Received().ScenarioForLevel(12, RunSeedValue);
        }

        [Test]
        public void Ascend_IntoAMeadow_RevealsItOverTheAscent()
        {
            ((IStartable)_controller).Start();

            // The level has already advanced by the time the Ascent starts.
            _level.Value = 16;
            _ascendHandler.Handle(default);

            Assert.AreEqual(ScenarioKind.Ground, _scenario.Current.Value.Kind);
            Assert.AreEqual(AscentSeconds, _scenario.Current.Value.Seconds, 1e-4f);
        }

        [Test]
        public void Ascend_BackIntoSky_RevealsTheSky()
        {
            _level.Value = 16;
            ((IStartable)_controller).Start();

            _pacing.ScenarioForLevel(19, Arg.Any<int>()).Returns(ScenarioKind.Sky);
            _level.Value = 19;
            _ascendHandler.Handle(default);

            Assert.AreEqual(ScenarioKind.Sky, _scenario.Current.Value.Kind);
            Assert.IsFalse(_scenario.Current.Value.IsCut);
        }

        [Test]
        public void Ascend_WithinAMeadow_LeavesTheBackdropAlone()
        {
            _level.Value = 16;
            ((IStartable)_controller).Start();
            var before = _scenario.Current.Value;

            _level.Value = 17;
            _ascendHandler.Handle(default);

            Assert.AreEqual(before, _scenario.Current.Value);
        }

        [Test]
        public void RestartWithTheRise_RevealsTheRestartLevelOverTheRise()
        {
            _level.Value = 30;
            ((IStartable)_controller).Start();

            _level.Value = 1;
            _resetHandler.Handle(new RunResetMessage(boardReset: false));
            _riseHandler.Handle(default);

            Assert.AreEqual(new ScenarioShift(ScenarioKind.Sky, RiseSeconds), _scenario.Current.Value);
        }

        [Test]
        public void RestartWithTheRise_WaitsForTheRise_SoTheNewSceneryIsPlacedUnscaled()
        {
            // The reveal scales the content root; the loss cinematic places the new scenery under it between
            // the reset and the rise, and a view placed under a scaled root keeps the wrong offset.
            _level.Value = 30;
            ((IStartable)_controller).Start();
            var before = _scenario.Current.Value;

            _level.Value = 1;
            _resetHandler.Handle(new RunResetMessage(boardReset: false));

            Assert.AreEqual(before, _scenario.Current.Value);
        }

        [Test]
        public void ImmediateRestart_CutsToTheRestartLevel()
        {
            _level.Value = 30;
            ((IStartable)_controller).Start();

            _level.Value = 1;
            _resetHandler.Handle(new RunResetMessage(boardReset: true));

            Assert.AreEqual(new ScenarioShift(ScenarioKind.Sky, 0f), _scenario.Current.Value);
        }

        [Test]
        public void Restart_ReadsTheRunSeedAgain_SoAFreshRunGetsItsOwnScenarios()
        {
            ((IStartable)_controller).Start();
            _runSeed.Value.Returns(777);
            _level.Value = 1;

            _resetHandler.Handle(new RunResetMessage(boardReset: true));

            _pacing.Received().ScenarioForLevel(1, 777);
        }

        [Test]
        public void TheAscentsEnd_SettlesItsReveal()
        {
            // A skipped Ascent completes at once; the mist must not keep playing over the board.
            ((IStartable)_controller).Start();
            _level.Value = 16;
            _ascendHandler.Handle(default);

            _transitionCompletedHandler.Handle(default);

            Assert.AreEqual(new ScenarioShift(ScenarioKind.Ground, 0f), _scenario.Current.Value);
        }

        [Test]
        public void TheRestartsEnd_SettlesItsReveal()
        {
            _level.Value = 30;
            ((IStartable)_controller).Start();
            _level.Value = 1;
            _resetHandler.Handle(new RunResetMessage(boardReset: false));
            _riseHandler.Handle(default);

            _restartCompletedHandler.Handle(default);

            Assert.AreEqual(new ScenarioShift(ScenarioKind.Sky, 0f), _scenario.Current.Value);
        }

        [Test]
        public void ARestartThatNeverRises_StillLandsOnTheRestartLevel()
        {
            // The loss cinematic can bail before its rise; the restart's end still settles the backdrop.
            _level.Value = 30;
            ((IStartable)_controller).Start();
            _level.Value = 1;
            _resetHandler.Handle(new RunResetMessage(boardReset: false));

            _restartCompletedHandler.Handle(default);

            Assert.AreEqual(new ScenarioShift(ScenarioKind.Sky, 0f), _scenario.Current.Value);
        }

        [Test]
        public void LevelChangeWithoutAnAscend_LeavesTheScenarioAlone()
        {
            ((IStartable)_controller).Start();

            _level.Value = 20;

            Assert.AreEqual(ScenarioKind.Sky, _scenario.Current.Value.Kind);
        }

        [Test]
        public void Dispose_ReleasesBothSubscriptions()
        {
            ((IStartable)_controller).Start();

            _controller.Dispose();

            _subscription.Received(1).Dispose();
            _resetSubscription.Received(1).Dispose();
        }

        [Test]
        public void Dispose_BeforeStart_DoesNotThrow()
        {
            Assert.DoesNotThrow(_controller.Dispose);
        }
    }
}
