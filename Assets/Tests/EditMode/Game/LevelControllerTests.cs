using System;
using System.Collections.Generic;
using BalloonParty.Configuration.Level;
using BalloonParty.Configuration.Palette;
using BalloonParty.Game.Health;
using BalloonParty.Game.Level;
using BalloonParty.Shared.GameState;
using BalloonParty.Shared.Messages;
using MessagePipe;
using NSubstitute;
using NUnit.Framework;
using UniRx;
using UnityEngine;

namespace BalloonParty.Tests.Game
{
    [TestFixture]
    public class LevelControllerTests
    {
        private const string Red = "Red";
        private const string Blue = "Blue";

        private IActiveLevelParameters _levelParams;
        private ILevelThresholds _thresholds;
        private ILevelParameters _current;
        private IGamePalette _palette;
        private INavigation _navigation;
        private ReactiveProperty<NavigationState> _navState;
        private ILossForecast _lossForecast;
        private IPublisher<ScoreLevelUpMessage> _levelUpPublisher;
        private IMessageHandler<ScoreTrailArrivedMessage> _trailArrivedHandler;
        private LevelController _controller;

        [SetUp]
        public void SetUp()
        {
            _levelParams = Substitute.For<IActiveLevelParameters>();
            _current = Substitute.For<ILevelParameters>();
            _levelParams.Current.Returns(_current);
            _current.AllowedColors.Returns(new List<string> { Red, Blue });

            _thresholds = Substitute.For<ILevelThresholds>();
            _thresholds.PointsRequiredForLevel(Arg.Any<int>()).Returns(10);

            _palette = Substitute.For<IGamePalette>();
            _palette.ColorNames.Returns(new[] { Red, Blue });

            _navigation = Substitute.For<INavigation>();
            _navState = new ReactiveProperty<NavigationState>(NavigationState.Game);
            _navigation.Current.Returns(_navState);

            _lossForecast = Substitute.For<ILossForecast>();
            _lossForecast.LossImminent.Returns(false);

            _levelUpPublisher = Substitute.For<IPublisher<ScoreLevelUpMessage>>();

            _controller = BuildController();
            _controller.Start();
        }

        [TearDown]
        public void TearDown()
        {
            _controller.Dispose();
        }

        private LevelController BuildController()
        {
            var trailArrivedSubscriber = Substitute.For<ISubscriber<ScoreTrailArrivedMessage>>();
            trailArrivedSubscriber
                .Subscribe(
                    Arg.Do<IMessageHandler<ScoreTrailArrivedMessage>>(h => _trailArrivedHandler = h),
                    Arg.Any<MessageHandlerFilter<ScoreTrailArrivedMessage>[]>())
                .Returns(Substitute.For<IDisposable>());

            return new LevelController(
                _levelParams, _thresholds, _palette, _navigation, _lossForecast, _levelUpPublisher, trailArrivedSubscriber);
        }

        [Test]
        public void Start_StartsAtLevelOne()
        {
            Assert.AreEqual(1, _controller.Level.Value);
        }

        [Test]
        public void ClaimProgress_BelowThreshold_GrantsAll()
        {
            _thresholds.PointsRequiredForLevel(2).Returns(5);

            var (baseProgress, granted) = _controller.ClaimProgress(Red, 3);

            Assert.AreEqual(0, baseProgress);
            Assert.AreEqual(3, granted);
        }

        [Test]
        public void ClaimProgress_AboveThreshold_CapsAndDropsExcess()
        {
            _thresholds.PointsRequiredForLevel(2).Returns(3);

            var (baseProgress, granted) = _controller.ClaimProgress(Red, 4);

            Assert.AreEqual(0, baseProgress);
            Assert.AreEqual(3, granted);
        }

        [Test]
        public void ClaimProgress_AdvancesProjectedBase()
        {
            _thresholds.PointsRequiredForLevel(2).Returns(5);

            _controller.ClaimProgress(Red, 2);
            var (baseProgress, granted) = _controller.ClaimProgress(Red, 2);

            Assert.AreEqual(2, baseProgress);
            Assert.AreEqual(2, granted);
        }

        [Test]
        public void ClaimProgress_UnknownColor_GrantsZero()
        {
            var (baseProgress, granted) = _controller.ClaimProgress("Purple", 3);

            Assert.AreEqual(0, baseProgress);
            Assert.AreEqual(0, granted);
        }

        [Test]
        public void WillLevelUp_AllColorsProjected_ReturnsTrue()
        {
            _thresholds.PointsRequiredForLevel(2).Returns(1);

            _controller.ClaimProgress(Red, 1);
            _controller.ClaimProgress(Blue, 1);

            Assert.IsTrue(_controller.WillLevelUp());
        }

        [Test]
        public void WillLevelUp_OneColorShort_ReturnsFalse()
        {
            _thresholds.PointsRequiredForLevel(2).Returns(1);

            _controller.ClaimProgress(Red, 1);

            Assert.IsFalse(_controller.WillLevelUp());
        }

        [Test]
        public void WillLevelUp_ColorGatedOutOfLevel_IsNotRequired()
        {
            _current.AllowedColors.Returns(new List<string> { Red });
            _thresholds.PointsRequiredForLevel(2).Returns(1);

            _controller.ClaimProgress(Red, 1);

            Assert.IsTrue(_controller.WillLevelUp());
        }

        [Test]
        public void TrailArrived_AllColorsConfirmed_LevelsUp()
        {
            _thresholds.PointsRequiredForLevel(2).Returns(2);

            FireTrailArrived(Red, 1);
            FireTrailArrived(Red, 2);
            FireTrailArrived(Blue, 1);
            FireTrailArrived(Blue, 2);

            _levelUpPublisher.Received(1).Publish(Arg.Is<ScoreLevelUpMessage>(m => m.NewLevel == 2));
            _navigation.Received(1).TransitionTo(NavigationState.LevelUp);
            Assert.AreEqual(2, _controller.Level.Value);
        }

        [Test]
        public void LevelUp_PublishesCompletedColorsSnapshot()
        {
            _thresholds.PointsRequiredForLevel(2).Returns(2);

            FireTrailArrived(Red, 1);
            FireTrailArrived(Red, 2);
            FireTrailArrived(Blue, 1);
            FireTrailArrived(Blue, 2);

            _levelUpPublisher.Received(1).Publish(Arg.Is<ScoreLevelUpMessage>(m => m.CompletedColors.Count == 2));
        }

        [Test]
        public void LevelUp_WhenLossImminent_DoesNotLevelUp()
        {
            _thresholds.PointsRequiredForLevel(2).Returns(1);
            _lossForecast.LossImminent.Returns(true);

            FireTrailArrived(Red, 1);
            FireTrailArrived(Blue, 1);

            _levelUpPublisher.DidNotReceive().Publish(Arg.Any<ScoreLevelUpMessage>());
            Assert.AreEqual(1, _controller.Level.Value);
        }

        [Test]
        public void LevelUp_WhenNotInGame_DoesNotLevelUp()
        {
            _thresholds.PointsRequiredForLevel(2).Returns(1);
            _navState.Value = NavigationState.GameOver;

            FireTrailArrived(Red, 1);
            FireTrailArrived(Blue, 1);

            _levelUpPublisher.DidNotReceive().Publish(Arg.Any<ScoreLevelUpMessage>());
            Assert.AreEqual(1, _controller.Level.Value);
        }

        [Test]
        public void LevelUp_OneColorShort_DoesNotLevelUp()
        {
            _thresholds.PointsRequiredForLevel(2).Returns(5);

            for (var i = 1; i <= 5; i++)
            {
                FireTrailArrived(Red, i);
            }

            _levelUpPublisher.DidNotReceive().Publish(Arg.Any<ScoreLevelUpMessage>());
            Assert.AreEqual(1, _controller.Level.Value);
        }

        [Test]
        public void LevelUp_ResetsColorProgress()
        {
            _thresholds.PointsRequiredForLevel(2).Returns(2);

            FireTrailArrived(Red, 1);
            FireTrailArrived(Red, 2);
            FireTrailArrived(Blue, 1);
            FireTrailArrived(Blue, 2);

            Assert.AreEqual(0, _controller.GetProgress(Red));
            Assert.AreEqual(0, _controller.GetProgress(Blue));
        }

        [Test]
        public void GetProgress_ReflectsConfirmedArrivals()
        {
            _thresholds.PointsRequiredForLevel(2).Returns(5);

            FireTrailArrived(Red, 3);

            Assert.AreEqual(3, _controller.GetProgress(Red));
        }

        [Test]
        public void ResetRun_ResetsLevelToOne()
        {
            _thresholds.PointsRequiredForLevel(2).Returns(1);
            FireTrailArrived(Red, 1);
            FireTrailArrived(Blue, 1);
            Assert.AreEqual(2, _controller.Level.Value);

            _controller.ResetRun(2);

            Assert.AreEqual(1, _controller.Level.Value);
        }

        [Test]
        public void StragglerTrail_AfterLevelScored_DoesNotTouchProgress()
        {
            // A trail still in flight when the level completed lands in the next level; it must not
            // re-inflate progress (the scoring cap would then stop the colour below the new threshold).
            _thresholds.PointsRequiredForLevel(2).Returns(1);
            FireTrailArrived(Red, 1);
            FireTrailArrived(Blue, 1);
            Assert.AreEqual(2, _controller.Level.Value);

            FireTrailArrived(Red, 1); // straggler from the finished level

            Assert.AreEqual(0, _controller.GetProgress(Red));
            _levelUpPublisher.Received(1).Publish(Arg.Any<ScoreLevelUpMessage>());
        }

        private void FireTrailArrived(string color, int score)
        {
            _trailArrivedHandler.Handle(new ScoreTrailArrivedMessage(color, score, Vector3.zero));
        }
    }
}
