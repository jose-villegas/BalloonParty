using System;
using System.Collections.Generic;
using BalloonParty.Configuration.Level;
using BalloonParty.Configuration.Palette;
using BalloonParty.Game.Health;
using BalloonParty.Game.Level;
using BalloonParty.Projectile.Controller;
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
        private IActiveProjectilePierce _pierce;
        private ReactiveProperty<bool> _isPiercing;
        private IMessageHandler<ScoreTrailArrivedMessage> _trailArrivedHandler;
        private IMessageHandler<LevelUpAbortedMessage> _abortedHandler;
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
            _palette.ProgressColorNames.Returns(new[] { Red, Blue });

            _navigation = Substitute.For<INavigation>();
            _navState = new ReactiveProperty<NavigationState>(NavigationState.Game);
            _navigation.Current.Returns(_navState);

            _lossForecast = Substitute.For<ILossForecast>();
            _lossForecast.LossImminent.Returns(false);

            _levelUpPublisher = Substitute.For<IPublisher<ScoreLevelUpMessage>>();

            _isPiercing = new ReactiveProperty<bool>(false);
            _pierce = Substitute.For<IActiveProjectilePierce>();
            _pierce.IsPiercing.Returns(_isPiercing);

            _controller = BuildController();
            _controller.Start();
        }

        [TearDown]
        public void TearDown()
        {
            _controller.Dispose();
            _isPiercing.Dispose();
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

            return new LevelController(
                _levelParams, _thresholds, _palette, _navigation, _lossForecast, _levelUpPublisher,
                trailArrivedSubscriber, abortedSubscriber, dismissedSubscriber, completedSubscriber, _pierce);
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
        public void TrailArrived_AllColorsConfirmed_PublishesOnceAndDefersLevel()
        {
            _thresholds.PointsRequiredForLevel(1).Returns(2);

            ScoreColor(Red, 2);
            ScoreColor(Blue, 2);

            _levelUpPublisher.Received(1).Publish(Arg.Is<ScoreLevelUpMessage>(m => m.NewLevel == 2));
            _navigation.Received(1).TransitionTo(NavigationState.LevelUp);
            Assert.AreEqual(1, _controller.Level.Value, "level advances only on dismissal");
        }

        [Test]
        public void FurtherTrailsWhilePending_DoNotPublishAgain()
        {
            _thresholds.PointsRequiredForLevel(1).Returns(2);
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
            _thresholds.PointsRequiredForLevel(1).Returns(2);
            ScoreColor(Red, 2);
            ScoreColor(Blue, 2);
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

            _levelUpPublisher.Received(1).Publish(Arg.Is<ScoreLevelUpMessage>(m => m.CompletedColors.Count == 2));
        }

        [Test]
        public void LevelUp_WhenLossImminent_DoesNotLevelUp()
        {
            _thresholds.PointsRequiredForLevel(1).Returns(1);
            _lossForecast.LossImminent.Returns(true);

            ScoreColor(Red, 1);
            ScoreColor(Blue, 1);

            _levelUpPublisher.DidNotReceive().Publish(Arg.Any<ScoreLevelUpMessage>());
            Assert.AreEqual(1, _controller.Level.Value);
        }

        [Test]
        public void LevelUp_WhenNotInGame_DoesNotLevelUp()
        {
            _thresholds.PointsRequiredForLevel(1).Returns(1);
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
            _thresholds.PointsRequiredForLevel(1).Returns(1);
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
            // Transitioning, AFTER progress has reset for the new level. Those arrivals carry the finished
            // level's cumulative score; landing them now must not step the reset numbering (the invariant the
            // resolve point rests on: the transition runs before the phase returns to Playing).
            _thresholds.PointsRequiredForLevel(Arg.Any<int>()).Returns(2);
            ScoreColor(Red, 2);
            ScoreColor(Blue, 2);
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
            Assert.AreEqual(LevelUpPhase.Pending, _controller.Phase.Value, "detected → Pending");

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

        [Test]
        public void CheckLevelUp_WhilePiercing_HoldsCommit()
        {
            // A confirming arrival that completes every colour mid-pierce must not fire the ceremony:
            // the commit is held so the plowing shot isn't interrupted mid-flight.
            _thresholds.PointsRequiredForLevel(1).Returns(2);
            SetPiercing(true);

            ScoreColor(Red, 2);
            ScoreColor(Blue, 2);

            _levelUpPublisher.DidNotReceive().Publish(Arg.Any<ScoreLevelUpMessage>());
            Assert.AreEqual(LevelUpPhase.Playing, _controller.Phase.Value, "phase stays Playing while held");
        }

        [Test]
        public void PierceEnded_WithRequirementMet_PublishesOnceAndGoesPending()
        {
            // The pierce discharges after the confirming trails landed — that's where the held commit fires.
            _thresholds.PointsRequiredForLevel(1).Returns(2);
            SetPiercing(true);
            ScoreColor(Red, 2);
            ScoreColor(Blue, 2);

            SetPiercing(false);

            _levelUpPublisher.Received(1).Publish(Arg.Is<ScoreLevelUpMessage>(m => m.NewLevel == 2));
            Assert.AreEqual(LevelUpPhase.Pending, _controller.Phase.Value);
        }

        [Test]
        public void NotPiercing_ConfirmingArrival_FiresImmediately()
        {
            // Regression guard: a not-piercing shot behaves exactly as before the pierce gate — the
            // confirming arrival commits on the spot, no pierce-end needed.
            _thresholds.PointsRequiredForLevel(1).Returns(2);

            ScoreColor(Red, 2);
            ScoreColor(Blue, 2);

            _levelUpPublisher.Received(1).Publish(Arg.Any<ScoreLevelUpMessage>());
            Assert.AreEqual(LevelUpPhase.Pending, _controller.Phase.Value);
        }

        [Test]
        public void PierceEnded_RequirementNotMet_DoesNotPublish()
        {
            // The pierce ended but only one colour reached the threshold — nothing to commit.
            _thresholds.PointsRequiredForLevel(1).Returns(5);
            SetPiercing(true);
            ScoreColor(Red, 5); // Blue still short

            SetPiercing(false);

            _levelUpPublisher.DidNotReceive().Publish(Arg.Any<ScoreLevelUpMessage>());
            Assert.AreEqual(LevelUpPhase.Playing, _controller.Phase.Value);
        }

        [Test]
        public void MultipleArrivalsDuringPierce_ThenPierceEnds_PublishesOnce()
        {
            // Many confirming arrivals plow past during one flight; the single discharge fires the ceremony once.
            _thresholds.PointsRequiredForLevel(1).Returns(2);
            SetPiercing(true);
            ScoreColor(Red, 2);
            ScoreColor(Blue, 2);
            FireTrailArrived(Red, 2);
            FireTrailArrived(Blue, 2);

            SetPiercing(false);

            _levelUpPublisher.Received(1).Publish(Arg.Any<ScoreLevelUpMessage>());
        }

        [Test]
        public void ClaimProgress_WhilePiercing_StillBanksExcess()
        {
            // Only the COMMIT is held mid-pierce; ClaimProgress isn't gated, so overflow still banks
            // and confirmed progress still advances during the plow.
            _thresholds.PointsRequiredForLevel(1).Returns(7);
            SetPiercing(true);

            ScoreColor(Red, 10); // 7 confirmed, 3 banked

            Assert.AreEqual(3, _controller.ExcessPoints(Red), "the bank fills regardless of the pierce hold");
            Assert.AreEqual(7, _controller.GetProgress(Red), "confirmed progress advances mid-pierce");
        }

        [Test]
        public void PierceEnded_WhenLossImminent_DoesNotPublish()
        {
            _thresholds.PointsRequiredForLevel(1).Returns(2);
            _lossForecast.LossImminent.Returns(true);
            SetPiercing(true);
            ScoreColor(Red, 2);
            ScoreColor(Blue, 2);

            SetPiercing(false);

            _levelUpPublisher.DidNotReceive().Publish(Arg.Any<ScoreLevelUpMessage>());
            Assert.AreEqual(LevelUpPhase.Playing, _controller.Phase.Value);
        }

        [Test]
        public void PierceEnded_WhenNotInGame_DoesNotPublish()
        {
            _thresholds.PointsRequiredForLevel(1).Returns(2);
            _navState.Value = NavigationState.GameOver;
            SetPiercing(true);
            ScoreColor(Red, 2);
            ScoreColor(Blue, 2);

            SetPiercing(false);

            _levelUpPublisher.DidNotReceive().Publish(Arg.Any<ScoreLevelUpMessage>());
            Assert.AreEqual(LevelUpPhase.Playing, _controller.Phase.Value);
        }

        [Test]
        public void PierceEnded_WhileAlreadyPending_DoesNotRePublish()
        {
            // A pierce cycle after the ceremony is already Pending must not re-fire it — the phase guard holds.
            _thresholds.PointsRequiredForLevel(1).Returns(2);
            ScoreColor(Red, 2);
            ScoreColor(Blue, 2);
            _levelUpPublisher.Received(1).Publish(Arg.Any<ScoreLevelUpMessage>());

            SetPiercing(true);
            SetPiercing(false);

            _levelUpPublisher.Received(1).Publish(Arg.Any<ScoreLevelUpMessage>());
        }

        [Test]
        public void PierceEnded_AfterDispose_DoesNotFireOrThrow()
        {
            _thresholds.PointsRequiredForLevel(1).Returns(2);
            SetPiercing(true);
            ScoreColor(Red, 2);
            ScoreColor(Blue, 2);

            _controller.Dispose();

            Assert.DoesNotThrow(() => SetPiercing(false));
            _levelUpPublisher.DidNotReceive().Publish(Arg.Any<ScoreLevelUpMessage>());
        }

        private void SetPiercing(bool piercing)
        {
            _isPiercing.Value = piercing;
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
    }
}
