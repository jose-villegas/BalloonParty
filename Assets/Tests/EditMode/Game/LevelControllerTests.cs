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
        private IMessageHandler<LevelUpDismissedMessage> _dismissedHandler;
        private IMessageHandler<LevelTransitionCompletedMessage> _completedHandler;
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

            var dismissedSubscriber = Substitute.For<ISubscriber<LevelUpDismissedMessage>>();
            dismissedSubscriber
                .Subscribe(
                    Arg.Do<IMessageHandler<LevelUpDismissedMessage>>(h => _dismissedHandler = h),
                    Arg.Any<MessageHandlerFilter<LevelUpDismissedMessage>[]>())
                .Returns(Substitute.For<IDisposable>());

            var completedSubscriber = Substitute.For<ISubscriber<LevelTransitionCompletedMessage>>();
            completedSubscriber
                .Subscribe(
                    Arg.Do<IMessageHandler<LevelTransitionCompletedMessage>>(h => _completedHandler = h),
                    Arg.Any<MessageHandlerFilter<LevelTransitionCompletedMessage>[]>())
                .Returns(Substitute.For<IDisposable>());

            return new LevelController(
                _levelParams, _thresholds, _palette, _navigation, _lossForecast, _levelUpPublisher,
                trailArrivedSubscriber, dismissedSubscriber, completedSubscriber);
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
        public void TrailArrived_AllColorsConfirmed_PublishesOnceAndDefersLevel()
        {
            _thresholds.PointsRequiredForLevel(2).Returns(2);

            ScoreColor(Red, 2);
            ScoreColor(Blue, 2);

            _levelUpPublisher.Received(1).Publish(Arg.Is<ScoreLevelUpMessage>(m => m.NewLevel == 2));
            _navigation.Received(1).TransitionTo(NavigationState.LevelUp);
            Assert.AreEqual(1, _controller.Level.Value, "level advances only on dismissal");
        }

        [Test]
        public void FurtherTrailsWhilePending_DoNotPublishAgain()
        {
            _thresholds.PointsRequiredForLevel(2).Returns(2);
            ScoreColor(Red, 2);
            ScoreColor(Blue, 2);

            // A straggler arriving during the ceremony must not fire a second level-up.
            FireTrailArrived(Red, 2);
            FireTrailArrived(Blue, 2);

            _levelUpPublisher.Received(1).Publish(Arg.Any<ScoreLevelUpMessage>());
        }

        [Test]
        public void LevelUpDismissed_AdvancesLevel()
        {
            _thresholds.PointsRequiredForLevel(2).Returns(2);
            ScoreColor(Red, 2);
            ScoreColor(Blue, 2);
            Assert.AreEqual(1, _controller.Level.Value);

            FireDismissed();

            Assert.AreEqual(2, _controller.Level.Value);
        }

        [Test]
        public void LevelUp_PublishesCompletedColorsSnapshot()
        {
            _thresholds.PointsRequiredForLevel(2).Returns(2);

            ScoreColor(Red, 2);
            ScoreColor(Blue, 2);

            _levelUpPublisher.Received(1).Publish(Arg.Is<ScoreLevelUpMessage>(m => m.CompletedColors.Count == 2));
        }

        [Test]
        public void LevelUp_WhenLossImminent_DoesNotLevelUp()
        {
            _thresholds.PointsRequiredForLevel(2).Returns(1);
            _lossForecast.LossImminent.Returns(true);

            ScoreColor(Red, 1);
            ScoreColor(Blue, 1);

            _levelUpPublisher.DidNotReceive().Publish(Arg.Any<ScoreLevelUpMessage>());
            Assert.AreEqual(1, _controller.Level.Value);
        }

        [Test]
        public void LevelUp_WhenNotInGame_DoesNotLevelUp()
        {
            _thresholds.PointsRequiredForLevel(2).Returns(1);
            _navState.Value = NavigationState.GameOver;

            ScoreColor(Red, 1);
            ScoreColor(Blue, 1);

            _levelUpPublisher.DidNotReceive().Publish(Arg.Any<ScoreLevelUpMessage>());
            Assert.AreEqual(1, _controller.Level.Value);
        }

        [Test]
        public void Detection_WhileTransitioning_DoesNotFire()
        {
            // Dismissed → Transitioning (Ascent running). A trail landing now belongs to the finished
            // level and must not trip a second level-up until the Ascent completes.
            _thresholds.PointsRequiredForLevel(2).Returns(1);
            ScoreColor(Red, 1);
            ScoreColor(Blue, 1);
            FireDismissed();
            Assert.AreEqual(LevelUpPhase.Transitioning, _controller.Phase.Value);

            ScoreColor(Red, 1);
            ScoreColor(Blue, 1);

            _levelUpPublisher.Received(1).Publish(Arg.Any<ScoreLevelUpMessage>());
        }

        [Test]
        public void LevelUp_OneColorShort_DoesNotLevelUp()
        {
            _thresholds.PointsRequiredForLevel(2).Returns(5);

            ScoreColor(Red, 5);

            _levelUpPublisher.DidNotReceive().Publish(Arg.Any<ScoreLevelUpMessage>());
            Assert.AreEqual(1, _controller.Level.Value);
        }

        [Test]
        public void LevelUpDismissed_ResetsColorProgress()
        {
            _thresholds.PointsRequiredForLevel(2).Returns(2);
            ScoreColor(Red, 2);
            ScoreColor(Blue, 2);

            // Progress holds through the ceremony, then resets on dismissal.
            Assert.AreEqual(2, _controller.GetProgress(Red), "progress persists while pending");

            FireDismissed();

            Assert.AreEqual(0, _controller.GetProgress(Red));
            Assert.AreEqual(0, _controller.GetProgress(Blue));
        }

        [Test]
        public void GetProgress_ReflectsConfirmedArrivals()
        {
            _thresholds.PointsRequiredForLevel(2).Returns(5);

            ScoreColor(Red, 3);

            Assert.AreEqual(3, _controller.GetProgress(Red));
        }

        [Test]
        public void TrailArrived_ConfirmedCappedAtClaimed()
        {
            // A trail can't confirm past the claim — projected is the ceiling.
            _thresholds.PointsRequiredForLevel(2).Returns(10);

            _controller.ClaimProgress(Red, 3);
            FireTrailArrived(Red, 7); // carries a stale higher score

            Assert.AreEqual(3, _controller.GetProgress(Red));
        }

        [Test]
        public void ResetRun_ResetsLevelToOne()
        {
            _thresholds.PointsRequiredForLevel(2).Returns(1);
            ScoreColor(Red, 1);
            ScoreColor(Blue, 1);
            FireDismissed();
            Assert.AreEqual(2, _controller.Level.Value);

            _controller.ResetRun(2);

            Assert.AreEqual(1, _controller.Level.Value);
        }

        [Test]
        public void StragglerTrail_AfterTransitionReopens_DoesNotStallColor()
        {
            // Full cycle back to Playing, then a late straggler from the finished level lands. It must
            // neither confirm nor poison projected — else ClaimProgress grants 0 and the bar never fills.
            _thresholds.PointsRequiredForLevel(2).Returns(1);
            ScoreColor(Red, 1);
            ScoreColor(Blue, 1);
            FireDismissed();          // → Transitioning (level advances, progress resets)
            FireTransitionComplete(); // → Playing (scoring reopens)
            Assert.AreEqual(2, _controller.Level.Value);

            FireTrailArrived(Red, 1); // straggler carrying the finished level's score

            Assert.AreEqual(0, _controller.GetProgress(Red), "straggler must not confirm into the new level");

            var (_, granted) = _controller.ClaimProgress(Red, 1);
            Assert.AreEqual(1, granted, "projected must stay clean so the colour can still score");
            _levelUpPublisher.Received(1).Publish(Arg.Any<ScoreLevelUpMessage>());
        }

        [Test]
        public void Phase_CyclesThroughTheCeremony()
        {
            _thresholds.PointsRequiredForLevel(2).Returns(2);
            Assert.AreEqual(LevelUpPhase.Playing, _controller.Phase.Value);

            ScoreColor(Red, 2);
            ScoreColor(Blue, 2);
            Assert.AreEqual(LevelUpPhase.Pending, _controller.Phase.Value, "detected → Pending");

            FireDismissed();
            Assert.AreEqual(LevelUpPhase.Transitioning, _controller.Phase.Value, "dismissed → Transitioning");

            FireTransitionComplete();
            Assert.AreEqual(LevelUpPhase.Playing, _controller.Phase.Value, "Ascent done → Playing");
        }

        [Test]
        public void Dismiss_OutsidePending_Ignored()
        {
            // A stray dismissal while Playing must not advance the level.
            FireDismissed();

            Assert.AreEqual(LevelUpPhase.Playing, _controller.Phase.Value);
            Assert.AreEqual(1, _controller.Level.Value);
        }

        // Mirrors production: claim each point (advancing projected), then confirm it as its trail lands.
        private void ScoreColor(string color, int points)
        {
            _controller.ClaimProgress(color, points);
            for (var i = 1; i <= points; i++)
            {
                FireTrailArrived(color, i);
            }
        }

        private void FireTrailArrived(string color, int score)
        {
            _trailArrivedHandler.Handle(new ScoreTrailArrivedMessage(color, score, Vector3.zero));
        }

        private void FireDismissed()
        {
            _dismissedHandler.Handle(new LevelUpDismissedMessage());
        }

        private void FireTransitionComplete()
        {
            _completedHandler.Handle(new LevelTransitionCompletedMessage());
        }
    }
}
