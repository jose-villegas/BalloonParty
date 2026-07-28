using System;
using System.Collections.Generic;
using BalloonParty.Configuration.Cinematics;
using BalloonParty.Configuration.Level;
using BalloonParty.Configuration.Palette;
using BalloonParty.Game.Health;
using BalloonParty.Game.Level;
using BalloonParty.Game.Run;
using BalloonParty.Shared;
using BalloonParty.Shared.GameState;
using BalloonParty.Shared.Messages;
using BalloonParty.Shared.Pause;
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
        private ITimeScaleClaims _timeScale;
        private ICinematicsSettings _cinematics;
        private IPublisher<ScoreLevelUpMessage> _levelUpPublisher;
        private IPublisher<LevelUpAbandonedMessage> _abandonedPublisher;
        private IMessageHandler<ScoreTrailArrivedMessage> _trailArrivedHandler;
        private IMessageHandler<LevelUpAbortedMessage> _abortedHandler;
        private IMessageHandler<LevelUpDismissedMessage> _dismissedHandler;
        private IMessageHandler<LevelTransitionCompletedMessage> _completedHandler;
        private IMessageHandler<ProjectileDestroyedMessage> _destroyedHandler;
        private IMessageHandler<GameOverMessage> _gameOverHandler;
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
            _palette.ProgressColorNames.Returns(new[] { Red, Blue });

            _navigation = Substitute.For<INavigation>();
            _navState = new ReactiveProperty<NavigationState>(NavigationState.Game);
            _navigation.Current.Returns(_navState);

            _lossForecast = Substitute.For<ILossForecast>();
            _lossForecast.LossImminent.Returns(false);

            _timeScale = Substitute.For<ITimeScaleClaims>();

            _cinematics = Substitute.For<ICinematicsSettings>();
            var entry = new CinematicStateEntry();
            // Set the private _rig field to contain a TimeScaleCurve for the beat.
            var rigField = typeof(CinematicStateEntry).GetField("_rig",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var rig = new CameraRigCinematicSettings();
            var curveField = typeof(CameraRigCinematicSettings).GetField("_timeScaleCurve",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            curveField.SetValue(rig, AnimationCurve.Constant(0f, 0.3f, 0.5f));
            rigField.SetValue(entry, rig);
            _cinematics.EntryOf(CinematicState.LevelCompleteHit).Returns(entry);

            _levelUpPublisher = Substitute.For<IPublisher<ScoreLevelUpMessage>>();
            _abandonedPublisher = Substitute.For<IPublisher<LevelUpAbandonedMessage>>();

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

            var abortedSubscriber = Substitute.For<ISubscriber<LevelUpAbortedMessage>>();
            abortedSubscriber
                .Subscribe(
                    Arg.Do<IMessageHandler<LevelUpAbortedMessage>>(h => _abortedHandler = h),
                    Arg.Any<MessageHandlerFilter<LevelUpAbortedMessage>[]>())
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

            var destroyedSubscriber = Substitute.For<ISubscriber<ProjectileDestroyedMessage>>();
            destroyedSubscriber
                .Subscribe(
                    Arg.Do<IMessageHandler<ProjectileDestroyedMessage>>(h => _destroyedHandler = h),
                    Arg.Any<MessageHandlerFilter<ProjectileDestroyedMessage>[]>())
                .Returns(Substitute.For<IDisposable>());

            var gameOverSubscriber = Substitute.For<ISubscriber<GameOverMessage>>();
            gameOverSubscriber
                .Subscribe(
                    Arg.Do<IMessageHandler<GameOverMessage>>(h => _gameOverHandler = h),
                    Arg.Any<MessageHandlerFilter<GameOverMessage>[]>())
                .Returns(Substitute.For<IDisposable>());

            return new LevelController(
                _levelParams, _thresholds, _palette, _navigation, _lossForecast,
                Substitute.For<IRetryState>(), _timeScale, _cinematics, Substitute.For<IRunConfig>(),
                _levelUpPublisher, _abandonedPublisher,
                trailArrivedSubscriber, abortedSubscriber, dismissedSubscriber, completedSubscriber,
                destroyedSubscriber, gameOverSubscriber);
        }

        [Test]
        public void Start_StartsAtLevelOne()
        {
            Assert.AreEqual(1, _controller.Level.Value);
        }

        [Test]
        public void ClaimProgress_BelowThreshold_GrantsAll()
        {
            _thresholds.PointsRequiredForLevel(1).Returns(5);

            var (baseProgress, granted) = _controller.ClaimProgress(Red, 3);

            Assert.AreEqual(0, baseProgress);
            Assert.AreEqual(3, granted);
        }

        [Test]
        public void ClaimProgress_AboveThreshold_CapsAndDropsExcess()
        {
            _thresholds.PointsRequiredForLevel(1).Returns(3);

            var (baseProgress, granted) = _controller.ClaimProgress(Red, 4);

            Assert.AreEqual(0, baseProgress);
            Assert.AreEqual(3, granted);
        }

        [Test]
        public void ClaimProgress_AdvancesProjectedBase()
        {
            _thresholds.PointsRequiredForLevel(1).Returns(5);

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
        public void ClaimProgress_AboveThreshold_BanksExcess()
        {
            // Requirement 7, a pop lands the color at 10 — the 3 leftover is banked, not lost.
            _thresholds.PointsRequiredForLevel(1).Returns(7);

            _controller.ClaimProgress(Red, 10);

            Assert.AreEqual(3, _controller.ExcessPoints(Red));
            Assert.AreEqual(3, _controller.TotalExcessPoints());
        }

        [Test]
        public void ClaimProgress_MultipleAttributionsAfterCap_EachBanksFullPoints()
        {
            // Once the cap is already hit this burst, every further attribution for the colour banks in
            // full — not just the remainder past the first excess.
            _thresholds.PointsRequiredForLevel(1).Returns(5);

            _controller.ClaimProgress(Red, 5); // reaches the cap exactly, no excess yet
            _controller.ClaimProgress(Red, 4); // cap already hit — the whole burst banks
            _controller.ClaimProgress(Red, 6); // a second attribution, same story

            Assert.AreEqual(10, _controller.ExcessPoints(Red));
        }

        [Test]
        public void ExcessBank_AccumulatesAcrossLevels()
        {
            // The bank is a running total for the whole run — it keeps growing past a level-up, not reset.
            _thresholds.PointsRequiredForLevel(Arg.Any<int>()).Returns(2);

            ScoreColor(Red, 5);  // 2 granted, 3 banked
            ScoreColor(Blue, 2); // completes level 1
            FireFlightEnded();
            FireDismissed();     // → level 2, progress resets, bank untouched
            FireTransitionComplete();

            ScoreColor(Red, 4);  // 2 granted, 2 more banked

            Assert.AreEqual(5, _controller.ExcessPoints(Red), "bank accumulates across the level-up");
        }

        [Test]
        public void ClaimProgress_UnderBlockLevelUpCheat_DoesNotBank()
        {
            _thresholds.PointsRequiredForLevel(1).Returns(5);
            BalloonParty.Cheats.CheatState.BlockLevelUp = true;

            try
            {
                _controller.ClaimProgress(Red, 10);
            }
            finally
            {
                BalloonParty.Cheats.CheatState.BlockLevelUp = false;
            }

            Assert.AreEqual(0, _controller.ExcessPoints(Red), "the cheat's grant isn't real progress");
        }

        [Test]
        public void WillLevelUp_AllColorsProjected_ReturnsTrue()
        {
            _thresholds.PointsRequiredForLevel(1).Returns(1);

            _controller.ClaimProgress(Red, 1);
            _controller.ClaimProgress(Blue, 1);

            Assert.IsTrue(_controller.WillLevelUp());
        }

        [Test]
        public void WillLevelUp_OneColorShort_ReturnsFalse()
        {
            _thresholds.PointsRequiredForLevel(1).Returns(1);

            _controller.ClaimProgress(Red, 1);

            Assert.IsFalse(_controller.WillLevelUp());
        }

        [Test]
        public void WillLevelUp_ColorGatedOutOfLevel_IsNotRequired()
        {
            _current.AllowedColors.Returns(new List<string> { Red });
            _thresholds.PointsRequiredForLevel(1).Returns(1);

            _controller.ClaimProgress(Red, 1);

            Assert.IsTrue(_controller.WillLevelUp());
        }

        [Test]
        public void TrailArrived_AllColorsConfirmed_GoesToCompleting()
        {
            _thresholds.PointsRequiredForLevel(1).Returns(2);

            ScoreColor(Red, 2);
            ScoreColor(Blue, 2);

            Assert.AreEqual(LevelUpPhase.Completing, _controller.Phase.Value);
            _timeScale.Received(1).ClaimExclusive(TimeScaleSource.LevelUpCeremony, Arg.Any<float>());
            _levelUpPublisher.DidNotReceive().Publish(Arg.Any<ScoreLevelUpMessage>());
        }

        [Test]
        public void FlightEnded_WhileCompleting_PublishesAndGoesPending()
        {
            _thresholds.PointsRequiredForLevel(1).Returns(2);

            ScoreColor(Red, 2);
            ScoreColor(Blue, 2);
            FireFlightEnded();

            _levelUpPublisher.Received(1).Publish(Arg.Is<ScoreLevelUpMessage>(m => m.NewLevel == 2));
            _navigation.Received(1).TransitionTo(NavigationState.LevelUp);
            Assert.AreEqual(LevelUpPhase.Pending, _controller.Phase.Value);
            Assert.AreEqual(1, _controller.Level.Value, "level advances only on dismissal");
        }

        [Test]
        public void FurtherTrailsWhileCompleting_DoNotRePublish()
        {
            _thresholds.PointsRequiredForLevel(1).Returns(2);
            ScoreColor(Red, 2);
            ScoreColor(Blue, 2);

            // A straggler arriving during Completing must not fire anything — the ceremony hasn't presented yet.
            FireTrailArrived(Red, 2);
            FireTrailArrived(Blue, 2);

            Assert.AreEqual(LevelUpPhase.Completing, _controller.Phase.Value);
            _levelUpPublisher.DidNotReceive().Publish(Arg.Any<ScoreLevelUpMessage>());
        }

        [Test]
        public void SecondFlightEnded_WhilePending_DoesNotRePublish()
        {
            _thresholds.PointsRequiredForLevel(1).Returns(2);
            ScoreColor(Red, 2);
            ScoreColor(Blue, 2);
            FireFlightEnded();

            // Every ordinary shot fires ProjectileDestroyedMessage — Pending must reject it.
            FireFlightEnded();

            _levelUpPublisher.Received(1).Publish(Arg.Any<ScoreLevelUpMessage>());
        }

        [Test]
        public void LevelUpDismissed_AdvancesLevel()
        {
            _thresholds.PointsRequiredForLevel(1).Returns(2);
            ScoreColor(Red, 2);
            ScoreColor(Blue, 2);
            FireFlightEnded();
            Assert.AreEqual(1, _controller.Level.Value);

            FireDismissed();

            Assert.AreEqual(2, _controller.Level.Value);
        }

        [Test]
        public void LevelUp_PublishesCompletedColorsSnapshot()
        {
            _thresholds.PointsRequiredForLevel(1).Returns(2);

            ScoreColor(Red, 2);
            ScoreColor(Blue, 2);
            FireFlightEnded();

            _levelUpPublisher.Received(1).Publish(Arg.Is<ScoreLevelUpMessage>(m => m.CompletedColors.Count == 2));
        }

        [Test]
        public void LevelUp_WhenLossImminentAtFlightEnd_StillPresents()
        {
            // THE PINNED REGRESSION: LossImminent going true before end-of-flight used to permanently
            // block the ceremony. The new model evaluates navigation ONCE at the boundary and deliberately
            // does NOT gate on LossImminent — it is a prediction, and gating on it is the bug.
            _thresholds.PointsRequiredForLevel(1).Returns(1);

            ScoreColor(Red, 1);
            ScoreColor(Blue, 1);

            // Loss becomes imminent between tipping claim and end-of-flight.
            _lossForecast.LossImminent.Returns(true);
            FireFlightEnded();

            _levelUpPublisher.Received(1).Publish(Arg.Any<ScoreLevelUpMessage>());
            Assert.AreEqual(LevelUpPhase.Pending, _controller.Phase.Value);
        }

        [Test]
        public void FlightEnded_WhenNotInGame_AbandonsCeremony()
        {
            _thresholds.PointsRequiredForLevel(1).Returns(1);

            ScoreColor(Red, 1);
            ScoreColor(Blue, 1);
            Assert.AreEqual(LevelUpPhase.Completing, _controller.Phase.Value);

            // Run leaves gameplay before the flight ends.
            _navState.Value = NavigationState.GameOver;
            FireFlightEnded();

            _levelUpPublisher.DidNotReceive().Publish(Arg.Any<ScoreLevelUpMessage>());
            Assert.AreEqual(LevelUpPhase.Playing, _controller.Phase.Value);
            _abandonedPublisher.Received(1).Publish(Arg.Any<LevelUpAbandonedMessage>());
        }

        [Test]
        public void Detection_WhileTransitioning_DoesNotFire()
        {
            // Dismissed → Transitioning (Ascent running). A trail landing now belongs to the finished
            // level and must not trip a second level-up until the Ascent completes.
            _thresholds.PointsRequiredForLevel(1).Returns(1);
            ScoreColor(Red, 1);
            ScoreColor(Blue, 1);
            FireFlightEnded();
            FireDismissed();
            Assert.AreEqual(LevelUpPhase.Transitioning, _controller.Phase.Value);

            ScoreColor(Red, 1);
            ScoreColor(Blue, 1);

            _levelUpPublisher.Received(1).Publish(Arg.Any<ScoreLevelUpMessage>());
        }

        [Test]
        public void LevelUp_OneColorShort_DoesNotLevelUp()
        {
            _thresholds.PointsRequiredForLevel(1).Returns(5);

            ScoreColor(Red, 5);

            _levelUpPublisher.DidNotReceive().Publish(Arg.Any<ScoreLevelUpMessage>());
            Assert.AreEqual(1, _controller.Level.Value);
        }

        [Test]
        public void LevelUpDismissed_ResetsColorProgress()
        {
            _thresholds.PointsRequiredForLevel(1).Returns(2);
            ScoreColor(Red, 2);
            ScoreColor(Blue, 2);
            FireFlightEnded();

            // Progress holds through the ceremony, then resets on dismissal.
            Assert.AreEqual(2, _controller.GetProgress(Red), "progress persists while pending");

            FireDismissed();

            Assert.AreEqual(0, _controller.GetProgress(Red));
            Assert.AreEqual(0, _controller.GetProgress(Blue));
        }

        [Test]
        public void LevelUpDismissed_ResetsProgressToZero_DoesNotSeedFromBank()
        {
            // The excess bank never feeds back into progress — a new level starts every colour at 0.
            _thresholds.PointsRequiredForLevel(1).Returns(7);

            ScoreColor(Red, 10); // 7 granted + confirmed, 3 banked
            ScoreColor(Blue, 7);
            FireFlightEnded();

            FireDismissed();

            Assert.AreEqual(0, _controller.GetProgress(Red), "progress resets to zero, unseeded by the bank");
            Assert.AreEqual(0, _controller.GetProgress(Blue));
        }

        [Test]
        public void LevelUpDismissed_DoesNotClearBank()
        {
            _thresholds.PointsRequiredForLevel(1).Returns(7);
            ScoreColor(Red, 10); // 3 banked
            ScoreColor(Blue, 7);
            FireFlightEnded();

            FireDismissed();

            Assert.AreEqual(3, _controller.ExcessPoints(Red), "the bank survives a level-up");
        }

        [Test]
        public void ResetRun_ClearsBank()
        {
            _thresholds.PointsRequiredForLevel(1).Returns(7);
            _controller.ClaimProgress(Red, 10); // 3 banked

            _controller.ResetRun(2);

            Assert.AreEqual(0, _controller.ExcessPoints(Red), "a fresh run clears the bank");
            Assert.AreEqual(0, _controller.TotalExcessPoints());
        }

        [Test]
        public void GetProgress_ReflectsConfirmedArrivals()
        {
            _thresholds.PointsRequiredForLevel(1).Returns(5);

            ScoreColor(Red, 3);

            Assert.AreEqual(3, _controller.GetProgress(Red));
        }

        [Test]
        public void TrailArrived_ConfirmedCappedAtClaimed()
        {
            // A trail can't confirm past the claim — projected is the ceiling.
            _thresholds.PointsRequiredForLevel(1).Returns(10);

            _controller.ClaimProgress(Red, 3);
            FireTrailArrived(Red, 7); // carries a stale higher score

            Assert.AreEqual(3, _controller.GetProgress(Red));
        }

        [Test]
        public void ResetRun_ResetsLevelToOne()
        {
            _thresholds.PointsRequiredForLevel(1).Returns(1);
            ScoreColor(Red, 1);
            ScoreColor(Blue, 1);
            FireFlightEnded();
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
            _thresholds.PointsRequiredForLevel(1).Returns(1);
            ScoreColor(Red, 1);
            ScoreColor(Blue, 1);
            FireFlightEnded();
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
        public void OutgoingSurvivorArrival_WhileTransitioning_DoesNotStepResetProgress()
        {
            // The level-up ceremony freezes its surviving score trails behind the popup, then resolves them
            // (CompleteAll) as outgoing-level content when the transition runs — while the phase is still
            // Transitioning, AFTER progress has reset for the new level.
            _thresholds.PointsRequiredForLevel(Arg.Any<int>()).Returns(2);
            ScoreColor(Red, 2);
            ScoreColor(Blue, 2);
            FireFlightEnded();
            FireDismissed();
            Assert.AreEqual(LevelUpPhase.Transitioning, _controller.Phase.Value);
            Assert.AreEqual(0, _controller.GetProgress(Red), "progress reset for the new level on dismissal");

            FireTrailArrived(Red, 2); // a frozen survivor completing, carrying the finished level's score

            Assert.AreEqual(0, _controller.GetProgress(Red), "an outgoing survivor must not step the new level");
        }

        [Test]
        public void Phase_CyclesThroughTheCeremony()
        {
            _thresholds.PointsRequiredForLevel(1).Returns(2);
            Assert.AreEqual(LevelUpPhase.Playing, _controller.Phase.Value);

            ScoreColor(Red, 2);
            ScoreColor(Blue, 2);
            Assert.AreEqual(LevelUpPhase.Completing, _controller.Phase.Value, "tipping claim → Completing");

            FireFlightEnded();
            Assert.AreEqual(LevelUpPhase.Pending, _controller.Phase.Value, "flight ended → Pending");

            FireDismissed();
            Assert.AreEqual(LevelUpPhase.Transitioning, _controller.Phase.Value, "dismissed → Transitioning");

            FireTransitionComplete();
            Assert.AreEqual(LevelUpPhase.Playing, _controller.Phase.Value, "Ascent done → Playing");
        }

        [Test]
        public void TransitionCompleted_NavigationReturnsToGame()
        {
            _thresholds.PointsRequiredForLevel(1).Returns(1);
            ScoreColor(Red, 1);
            ScoreColor(Blue, 1);
            FireFlightEnded();
            _navState.Value = NavigationState.LevelUp;
            FireDismissed();

            FireTransitionComplete();

            _navigation.Received(1).TransitionTo(NavigationState.Game);
            Assert.AreEqual(LevelUpPhase.Playing, _controller.Phase.Value);
        }

        [Test]
        public void TransitionCompleted_WhileNavIsGameOver_DoesNotOverrideToGame()
        {
            _thresholds.PointsRequiredForLevel(1).Returns(1);
            ScoreColor(Red, 1);
            ScoreColor(Blue, 1);
            FireFlightEnded();
            FireDismissed();
            _navState.Value = NavigationState.GameOver;

            FireTransitionComplete();

            _navigation.DidNotReceive().TransitionTo(NavigationState.Game);
            Assert.AreEqual(LevelUpPhase.Playing, _controller.Phase.Value);
        }

        [Test]
        public void Dismiss_DoesNotTransitionNavToGame()
        {
            _thresholds.PointsRequiredForLevel(1).Returns(1);
            ScoreColor(Red, 1);
            ScoreColor(Blue, 1);
            FireFlightEnded();

            FireDismissed();

            _navigation.DidNotReceive().TransitionTo(NavigationState.Game);
            Assert.AreEqual(LevelUpPhase.Transitioning, _controller.Phase.Value);
        }

        [Test]
        public void FullCeremony_NavState_LevelUpThenGame()
        {
            _thresholds.PointsRequiredForLevel(1).Returns(1);

            ScoreColor(Red, 1);
            ScoreColor(Blue, 1);
            FireFlightEnded();
            _navState.Value = NavigationState.LevelUp;
            FireDismissed();
            FireTransitionComplete();

            Received.InOrder(() =>
            {
                _navigation.TransitionTo(NavigationState.LevelUp);
                _navigation.TransitionTo(NavigationState.Game);
            });
        }

        [Test]
        public void Abort_WhilePending_ResetsPhaseAndNavToGame()
        {
            _thresholds.PointsRequiredForLevel(1).Returns(1);
            ScoreColor(Red, 1);
            ScoreColor(Blue, 1);
            FireFlightEnded();
            _navState.Value = NavigationState.LevelUp;

            FireAborted();

            _navigation.Received(1).TransitionTo(NavigationState.Game);
            Assert.AreEqual(LevelUpPhase.Playing, _controller.Phase.Value);
            Assert.AreEqual(1, _controller.Level.Value, "abort before dismissal keeps the current level");
        }

        [Test]
        public void Dismiss_OutsidePending_Ignored()
        {
            // A stray dismissal while Playing must not advance the level.
            FireDismissed();

            Assert.AreEqual(LevelUpPhase.Playing, _controller.Phase.Value);
            Assert.AreEqual(1, _controller.Level.Value);
        }

        // Pierce-gate tests removed: the pierce mechanism is deleted by this plan. The Completing phase
        // replaces it by holding the ceremony until end-of-flight regardless of pierce state.

        [Test]
        public void FlightEnded_WhilePlaying_DoesNothing()
        {
            // Every ordinary shot fires ProjectileDestroyedMessage — when not Completing it must be ignored.
            _thresholds.PointsRequiredForLevel(1).Returns(10);

            FireFlightEnded();

            _levelUpPublisher.DidNotReceive().Publish(Arg.Any<ScoreLevelUpMessage>());
            Assert.AreEqual(LevelUpPhase.Playing, _controller.Phase.Value);
        }

        [Test]
        public void TrailArrival_DuringCompleting_KeepsConfirmingProgress()
        {
            // Bars must keep filling while the flight plays out.
            _thresholds.PointsRequiredForLevel(1).Returns(3);
            _controller.ClaimProgress(Red, 3);
            _controller.ClaimProgress(Blue, 3);
            // Now in Completing.
            Assert.AreEqual(LevelUpPhase.Completing, _controller.Phase.Value);

            FireTrailArrived(Red, 2);

            Assert.AreEqual(2, _controller.GetProgress(Red));
        }

        [Test]
        public void DroppedTrail_StillReachesPendingAtFlightEnd()
        {
            // Claims with no arrivals (trail lost) still present the ceremony at end-of-flight.
            _thresholds.PointsRequiredForLevel(1).Returns(2);
            _controller.ClaimProgress(Red, 2);
            _controller.ClaimProgress(Blue, 2);
            // Completing entered but no trails ever arrive.
            Assert.AreEqual(LevelUpPhase.Completing, _controller.Phase.Value);

            FireFlightEnded();

            Assert.AreEqual(LevelUpPhase.Pending, _controller.Phase.Value);
            _levelUpPublisher.Received(1).Publish(Arg.Any<ScoreLevelUpMessage>());
        }

        [Test]
        public void GameOver_DuringCompleting_AbandonsCeremony()
        {
            _thresholds.PointsRequiredForLevel(1).Returns(1);
            ScoreColor(Red, 1);
            ScoreColor(Blue, 1);
            Assert.AreEqual(LevelUpPhase.Completing, _controller.Phase.Value);

            FireGameOver();

            Assert.AreEqual(LevelUpPhase.Playing, _controller.Phase.Value);
            _abandonedPublisher.Received(1).Publish(Arg.Any<LevelUpAbandonedMessage>());
            _levelUpPublisher.DidNotReceive().Publish(Arg.Any<ScoreLevelUpMessage>());
        }

        [Test]
        public void Abort_WhileCompleting_AbandonsCeremony()
        {
            _thresholds.PointsRequiredForLevel(1).Returns(1);
            ScoreColor(Red, 1);
            ScoreColor(Blue, 1);
            Assert.AreEqual(LevelUpPhase.Completing, _controller.Phase.Value);

            FireAborted();

            Assert.AreEqual(LevelUpPhase.Playing, _controller.Phase.Value);
            _abandonedPublisher.Received(1).Publish(Arg.Any<LevelUpAbandonedMessage>());
        }

        [Test]
        public void Dismiss_WhileCompleting_IsIgnored()
        {
            _thresholds.PointsRequiredForLevel(1).Returns(1);
            ScoreColor(Red, 1);
            ScoreColor(Blue, 1);
            Assert.AreEqual(LevelUpPhase.Completing, _controller.Phase.Value);

            FireDismissed();

            Assert.AreEqual(LevelUpPhase.Completing, _controller.Phase.Value);
            Assert.AreEqual(1, _controller.Level.Value, "dismiss during Completing must not advance the level");
        }

        [Test]
        public void Tick_BeatCurveEnds_ReleasesExclusiveAndStartsRamp()
        {
            _thresholds.PointsRequiredForLevel(1).Returns(1);
            ScoreColor(Red, 1);
            ScoreColor(Blue, 1);
            Assert.AreEqual(LevelUpPhase.Completing, _controller.Phase.Value);
            _timeScale.ClearReceivedCalls();

            // Force elapsed past the beat curve duration (0.3s curve set up in SetUp).
            SetField("_completingElapsed", 0.31f);

            _controller.Tick();

            // Still Completing — projectile death is the sole trigger for presenting.
            Assert.AreEqual(LevelUpPhase.Completing, _controller.Phase.Value);
            // Released the exclusive beat claim.
            _timeScale.Received(1).ReleaseExclusive(TimeScaleSource.LevelUpCeremony);
            // Started the ramp with an initial Claim(1f).
            _timeScale.Received(1).Claim(TimeScaleSource.LevelUpCeremony, 1f);
        }

        [Test]
        public void Tick_DuringRamp_ClaimsIncreasingScale()
        {
            var runConfig = Substitute.For<IRunConfig>();
            runConfig.LevelCompleteRampUpDuration.Returns(100f);
            runConfig.LevelCompleteRampUpScale.Returns(2f);
            RebuildWithRunConfig(runConfig);

            _thresholds.PointsRequiredForLevel(1).Returns(1);
            ScoreColor(Red, 1);
            ScoreColor(Blue, 1);

            // Transition past the beat curve into ramp.
            SetField("_completingElapsed", 0.31f);
            _controller.Tick();
            _timeScale.ClearReceivedCalls();

            // Advance the ramp to exactly halfway.
            SetField("_rampUpElapsed", 50f);
            _controller.Tick();

            // Lerp(1, 2, 0.5) = 1.5
            _timeScale.Received(1).Claim(TimeScaleSource.LevelUpCeremony, 1.5f);
        }

        [Test]
        public void Tick_RampComplete_StopsClaimingFurtherValues()
        {
            var runConfig = Substitute.For<IRunConfig>();
            runConfig.LevelCompleteRampUpDuration.Returns(100f);
            runConfig.LevelCompleteRampUpScale.Returns(2f);
            RebuildWithRunConfig(runConfig);

            _thresholds.PointsRequiredForLevel(1).Returns(1);
            ScoreColor(Red, 1);
            ScoreColor(Blue, 1);

            // Beat curve ends → ramp starts.
            SetField("_completingElapsed", 0.31f);
            _controller.Tick();

            // Ramp past 100%.
            SetField("_rampUpElapsed", 101f);
            _controller.Tick();
            _timeScale.ClearReceivedCalls();

            // Further ticks should not claim again.
            _controller.Tick();
            _timeScale.DidNotReceive().Claim(Arg.Any<TimeScaleSource>(), Arg.Any<float>());
        }

        [Test]
        public void FlightEnded_DuringRamp_ReleasesClaimsAndPresents()
        {
            _thresholds.PointsRequiredForLevel(1).Returns(1);
            ScoreColor(Red, 1);
            ScoreColor(Blue, 1);

            // Beat curve ends → ramp starts.
            SetField("_completingElapsed", 0.31f);
            _controller.Tick();
            _timeScale.ClearReceivedCalls();

            // Simulate ramp in progress.
            SetField("_rampUpElapsed", 0.2f);
            SetField("_rampingUp", true);

            FireFlightEnded();

            Assert.AreEqual(LevelUpPhase.Pending, _controller.Phase.Value);
            _levelUpPublisher.Received(1).Publish(Arg.Is<ScoreLevelUpMessage>(m => m.NewLevel == 2));
            // Releases both exclusive and normal claims.
            _timeScale.Received(1).ReleaseExclusive(TimeScaleSource.LevelUpCeremony);
            _timeScale.Received(1).Release(TimeScaleSource.LevelUpCeremony);
        }

        [Test]
        public void FlightEnded_BeforeBeatCurveEnds_StillPresentsLevelUp()
        {
            // Edge case: projectile dies very quickly, before the cinematic beat finishes.
            _thresholds.PointsRequiredForLevel(1).Returns(1);
            ScoreColor(Red, 1);
            ScoreColor(Blue, 1);
            Assert.AreEqual(LevelUpPhase.Completing, _controller.Phase.Value);

            // Don't advance elapsed — beat is still playing.
            FireFlightEnded();

            Assert.AreEqual(LevelUpPhase.Pending, _controller.Phase.Value);
            _levelUpPublisher.Received(1).Publish(Arg.Any<ScoreLevelUpMessage>());
        }

        [Test]
        public void GameOver_DuringRamp_AbandonsCeremony()
        {
            _thresholds.PointsRequiredForLevel(1).Returns(1);
            ScoreColor(Red, 1);
            ScoreColor(Blue, 1);

            // Enter ramp phase.
            SetField("_completingElapsed", 0.31f);
            _controller.Tick();

            FireGameOver();

            Assert.AreEqual(LevelUpPhase.Playing, _controller.Phase.Value);
            _abandonedPublisher.Received(1).Publish(Arg.Any<LevelUpAbandonedMessage>());
        }

        [Test]
        public void Tick_ZeroRampDuration_RampCompletesImmediately()
        {
            // IRunConfig returns 0 for LevelCompleteRampUpDuration by default (NSubstitute).
            _thresholds.PointsRequiredForLevel(1).Returns(1);
            ScoreColor(Red, 1);
            ScoreColor(Blue, 1);

            // Beat curve ends → ramp starts with zero duration.
            SetField("_completingElapsed", 0.31f);
            _controller.Tick();
            _timeScale.ClearReceivedCalls();

            // Next tick: rampDuration is 0 → t = 1 immediately, ramp stops.
            _controller.Tick();
            // Should NOT keep claiming after this.
            _timeScale.ClearReceivedCalls();
            _controller.Tick();
            _timeScale.DidNotReceive().Claim(Arg.Any<TimeScaleSource>(), Arg.Any<float>());
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
            _trailArrivedHandler.Handle(new ScoreTrailArrivedMessage(color, score, points: 1, Vector3.zero));
        }

        private void FireFlightEnded()
        {
            _destroyedHandler.Handle(new ProjectileDestroyedMessage());
        }

        private void FireGameOver()
        {
            _gameOverHandler.Handle(new GameOverMessage(1, 0));
        }

        private void FireDismissed()
        {
            _dismissedHandler.Handle(new LevelUpDismissedMessage());
        }

        private void FireAborted()
        {
            _abortedHandler.Handle(new LevelUpAbortedMessage());
        }

        private void FireTransitionComplete()
        {
            _completedHandler.Handle(new LevelTransitionCompletedMessage());
        }

        private void SetField(string fieldName, object value)
        {
            var field = typeof(LevelController).GetField(fieldName,
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            field.SetValue(_controller, value);
        }

        private void RebuildWithRunConfig(IRunConfig runConfig)
        {
            _controller.Dispose();

            var trailArrivedSubscriber = Substitute.For<ISubscriber<ScoreTrailArrivedMessage>>();
            trailArrivedSubscriber
                .Subscribe(
                    Arg.Do<IMessageHandler<ScoreTrailArrivedMessage>>(h => _trailArrivedHandler = h),
                    Arg.Any<MessageHandlerFilter<ScoreTrailArrivedMessage>[]>())
                .Returns(Substitute.For<IDisposable>());

            var abortedSubscriber = Substitute.For<ISubscriber<LevelUpAbortedMessage>>();
            abortedSubscriber
                .Subscribe(
                    Arg.Do<IMessageHandler<LevelUpAbortedMessage>>(h => _abortedHandler = h),
                    Arg.Any<MessageHandlerFilter<LevelUpAbortedMessage>[]>())
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

            var destroyedSubscriber = Substitute.For<ISubscriber<ProjectileDestroyedMessage>>();
            destroyedSubscriber
                .Subscribe(
                    Arg.Do<IMessageHandler<ProjectileDestroyedMessage>>(h => _destroyedHandler = h),
                    Arg.Any<MessageHandlerFilter<ProjectileDestroyedMessage>[]>())
                .Returns(Substitute.For<IDisposable>());

            var gameOverSubscriber = Substitute.For<ISubscriber<GameOverMessage>>();
            gameOverSubscriber
                .Subscribe(
                    Arg.Do<IMessageHandler<GameOverMessage>>(h => _gameOverHandler = h),
                    Arg.Any<MessageHandlerFilter<GameOverMessage>[]>())
                .Returns(Substitute.For<IDisposable>());

            _controller = new LevelController(
                _levelParams, _thresholds, _palette, _navigation, _lossForecast,
                Substitute.For<IRetryState>(), _timeScale, _cinematics, runConfig,
                _levelUpPublisher, _abandonedPublisher,
                trailArrivedSubscriber, abortedSubscriber, dismissedSubscriber, completedSubscriber,
                destroyedSubscriber, gameOverSubscriber);
            _controller.Start();
        }
    }
}
