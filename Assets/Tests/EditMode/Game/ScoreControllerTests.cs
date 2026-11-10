using System;
using System.Collections.Generic;
using System.Reflection;
using BalloonParty.Balloon.Model;
using BalloonParty.Configuration;
using BalloonParty.Game.Level;
using BalloonParty.Game.Run;
using BalloonParty.Game.Score;
using BalloonParty.Projectile;
using BalloonParty.Projectile.Model;
using BalloonParty.Shared.Messages;
using BalloonParty.Shared.SceneLight;
using BalloonParty.Slots.Capabilities;
using BalloonParty.Slots.Actor;
using MessagePipe;
using NSubstitute;
using NUnit.Framework;
using UnityEngine;
using BalloonParty.Configuration.Palette;

namespace BalloonParty.Tests.Game
{
    [TestFixture]
    public class ScoreControllerTests
    {
        private const string Red = "Red";
        private const string Blue = "Blue";

        private ILevelProgress _levelProgress;
        private IGamePalette _palette;
        private IPublisher<ScorePointsGroupMessage> _scoredPublisher;
        private IPublisher<StreakGrewMessage> _streakGrewPublisher;
        private ITimeOfDayNight _timeOfDayNight;
        private ScoreController _controller;
        private ColorStreakTracker _streakTracker;
        private ProjectileRegistry _registry;
        private IMessageHandler<ScoreTrailArrivedMessage> _trailArrivedHandler;
        private IMessageHandler<ScoreLevelUpMessage> _scoreLevelUpHandler;

        [SetUp]
        public void SetUp()
        {
            ClearScorePrefs();

            _levelProgress = Substitute.For<ILevelProgress>();
            // Default: the level grants exactly what's requested from a zero base (no cap). Individual
            // tests override for a specific (color, points) to exercise capping/base-numbering.
            _levelProgress.ClaimProgress(Arg.Any<string>(), Arg.Any<int>())
                .Returns(ci => (0, ci.Arg<int>()));

            _palette = Substitute.For<IGamePalette>();
            _palette.ColorNames.Returns(new[] { Red, Blue });
            _palette.ProgressColorNames.Returns(new[] { Red, Blue });

            _controller = BuildController();
            _controller.Start();
        }

        [TearDown]
        public void TearDown()
        {
            _controller.Dispose();
            ClearScorePrefs();
        }

        private ScoreController BuildController()
        {
            var trailArrivedSubscriber = Substitute.For<ISubscriber<ScoreTrailArrivedMessage>>();
            trailArrivedSubscriber
                .Subscribe(
                    Arg.Do<IMessageHandler<ScoreTrailArrivedMessage>>(h => _trailArrivedHandler = h),
                    Arg.Any<MessageHandlerFilter<ScoreTrailArrivedMessage>[]>())
                .Returns(Substitute.For<IDisposable>());

            _scoredPublisher = Substitute.For<IPublisher<ScorePointsGroupMessage>>();
            _streakGrewPublisher = Substitute.For<IPublisher<StreakGrewMessage>>();
            _timeOfDayNight = Substitute.For<ITimeOfDayNight>();

            var levelUpSubscriber = Substitute.For<ISubscriber<ScoreLevelUpMessage>>();
            levelUpSubscriber
                .Subscribe(Arg.Any<IMessageHandler<ScoreLevelUpMessage>>(),
                    Arg.Any<MessageHandlerFilter<ScoreLevelUpMessage>[]>())
                .Returns(Substitute.For<IDisposable>());
            var projectileLoadedSubscriber = Substitute.For<ISubscriber<ProjectileLoadedMessage>>();
            projectileLoadedSubscriber
                .Subscribe(Arg.Any<IMessageHandler<ProjectileLoadedMessage>>(),
                    Arg.Any<MessageHandlerFilter<ProjectileLoadedMessage>[]>())
                .Returns(Substitute.For<IDisposable>());
            _registry = new ProjectileRegistry();
            _streakTracker = new ColorStreakTracker(
                Substitute.For<IPublisher<StreakChangedMessage>>(), levelUpSubscriber, projectileLoadedSubscriber,
                _registry);

            var scoreLevelUpSubscriber = Substitute.For<ISubscriber<ScoreLevelUpMessage>>();
            scoreLevelUpSubscriber
                .Subscribe(
                    Arg.Do<IMessageHandler<ScoreLevelUpMessage>>(h => _scoreLevelUpHandler = h),
                    Arg.Any<MessageHandlerFilter<ScoreLevelUpMessage>[]>())
                .Returns(Substitute.For<IDisposable>());

            return new ScoreController(
                trailArrivedSubscriber,
                scoreLevelUpSubscriber,
                _scoredPublisher,
                _streakGrewPublisher,
                _levelProgress,
                _palette,
                Substitute.For<IRetryState>(),
                _streakTracker,
                _timeOfDayNight);
        }

        [Test]
        public void OnActorHit_AtNight_DoublesTheAwardedPoints()
        {
            _timeOfDayNight.IsNight.Returns(true);

            // 3 x streak(1) x2 night = 6.
            FireHit(CreateModel(Red, 1, 3), 1);

            _scoredPublisher.Received(1).Publish(
                Arg.Is<ScorePointsGroupMessage>(m => m.ColorName == Red && m.Points == 6));
        }

        [Test]
        public void OnActorHit_ByDay_AwardsBasePoints()
        {
            _timeOfDayNight.IsNight.Returns(false);

            FireHit(CreateModel(Red, 1, 3), 1);

            _scoredPublisher.Received(1).Publish(
                Arg.Is<ScorePointsGroupMessage>(m => m.ColorName == Red && m.Points == 3));
        }

        [Test]
        public void StreakGrew_ABallsSecondAndThirdSameColourPops_AnnounceEachAtThePop()
        {
            var ball = NewBall();
            var at = new Vector3(1f, 2f, 0f);

            FirePopFrom(ball, Red, at);
            _streakGrewPublisher.DidNotReceive().Publish(Arg.Any<StreakGrewMessage>());

            FirePopFrom(ball, Red, at);
            FirePopFrom(ball, Red, at);

            Received.InOrder(() =>
            {
                _streakGrewPublisher.Publish(Arg.Is<StreakGrewMessage>(m =>
                    m.Projectile == ball && m.Color == Red && m.Streak == 2 && m.WorldPosition == at));
                _streakGrewPublisher.Publish(Arg.Is<StreakGrewMessage>(m =>
                    m.Projectile == ball && m.Color == Red && m.Streak == 3 && m.WorldPosition == at));
            });
        }

        [Test]
        public void StreakGrew_AtNight_CarriesTheStreak_NotTheDoubledMultiplier()
        {
            _timeOfDayNight.IsNight.Returns(true);
            var ball = NewBall();

            FirePopFrom(ball, Red);
            FirePopFrom(ball, Red);

            _scoredPublisher.Received(1).Publish(Arg.Is<ScorePointsGroupMessage>(m => m.Multiplier == 4));
            _streakGrewPublisher.Received(1).Publish(Arg.Is<StreakGrewMessage>(m => m.Streak == 2));
        }

        [Test]
        public void StreakGrew_AnItemPopOnTheStreaksColour_AnnouncesNothing()
        {
            var ball = NewBall();
            FirePopFrom(ball, Red);
            FirePopFrom(ball, Red);
            _streakGrewPublisher.ClearReceivedCalls();
            _scoredPublisher.ClearReceivedCalls();

            FirePopFrom(ball, Red, Vector3.zero, DamageFlags.Normal);

            _scoredPublisher.Received(1).Publish(Arg.Is<ScorePointsGroupMessage>(m => m.Multiplier == 2));
            _streakGrewPublisher.DidNotReceive().Publish(Arg.Any<StreakGrewMessage>());
        }

        [Test]
        public void StreakGrew_AColourChangeThatRestartsTheStreak_AnnouncesNothing()
        {
            var ball = NewBall();
            FirePopFrom(ball, Red);
            FirePopFrom(ball, Red);
            _streakGrewPublisher.ClearReceivedCalls();

            FirePopFrom(ball, Blue);

            _streakGrewPublisher.DidNotReceive().Publish(Arg.Any<StreakGrewMessage>());
        }

        [Test]
        public void StreakGrew_AMultiColourPop_AnnouncesOnce()
        {
            FireMultiColor((Red, 1, true));
            _streakGrewPublisher.ClearReceivedCalls();
            _scoredPublisher.ClearReceivedCalls();

            FireMultiColor((Red, 1, true), (Blue, 1, false));

            _scoredPublisher.Received(2).Publish(Arg.Any<ScorePointsGroupMessage>());
            _streakGrewPublisher.Received(1).Publish(Arg.Is<StreakGrewMessage>(m => m.Color == Red && m.Streak == 2));
        }

        [Test]
        public void StreakGrew_ColourBarAlreadyFull_StillAnnounces()
        {
            _levelProgress.ClaimProgress(Red, Arg.Any<int>()).Returns((10, 0));
            var ball = NewBall();

            FirePopFrom(ball, Red);
            FirePopFrom(ball, Red);

            _scoredPublisher.DidNotReceive().Publish(Arg.Any<ScorePointsGroupMessage>());
            _streakGrewPublisher.Received(1).Publish(Arg.Is<StreakGrewMessage>(m => m.Streak == 2));
        }

        [Test]
        public void StreakGrew_TwoBalls_EachAnnouncesItsOwnStreak()
        {
            var ballA = NewBall();
            var ballB = NewBall();
            FirePopFrom(ballA, Red);
            FirePopFrom(ballA, Red);
            FirePopFrom(ballA, Red);

            FirePopFrom(ballB, Red);
            FirePopFrom(ballB, Red);

            _streakGrewPublisher.Received(1).Publish(Arg.Is<StreakGrewMessage>(m => m.Projectile == ballA && m.Streak == 3));
            _streakGrewPublisher.Received(1).Publish(Arg.Is<StreakGrewMessage>(m => m.Projectile == ballB && m.Streak == 2));
        }

        // The rainbow buff climbs on any colour without changing it, so the message keeps the last real one.
        [Test]
        public void StreakGrew_AWildcardPop_AnnouncesTheClimbUnderTheLastRealColour()
        {
            var ball = NewBall();
            FirePopFrom(ball, Red);
            FirePopFrom(ball, Red);
            _streakGrewPublisher.ClearReceivedCalls();

            FireRainbowPopFrom(ball, DamageFlags.DirectHit | DamageFlags.WildcardStreak);

            _streakGrewPublisher.Received(1).Publish(Arg.Is<StreakGrewMessage>(m =>
                m.Projectile == ball && m.Color == Red && m.Streak == 3));
        }

        [Test]
        public void StreakGrew_AWildcardFirstPop_IsNoStreakYet()
        {
            FireRainbowPopFrom(NewBall(), DamageFlags.DirectHit | DamageFlags.WildcardStreak);

            _streakGrewPublisher.DidNotReceive().Publish(Arg.Any<StreakGrewMessage>());
        }

        [Test]
        public void StreakGrew_ACarriedStreakCashedInOnANewColour_AnnouncesUnderTheNewColour()
        {
            var ball = NewBall();
            FirePopFrom(ball, Red);
            FirePopFrom(ball, Red);
            FireRainbowPopFrom(ball, DamageFlags.DirectHit | DamageFlags.CarryStreak);
            _streakGrewPublisher.ClearReceivedCalls();

            FirePopFrom(ball, Blue);

            _streakGrewPublisher.Received(1).Publish(Arg.Is<StreakGrewMessage>(m => m.Color == Blue && m.Streak == 4));
        }

        [Test]
        public void StreakGrew_BankedRainbowPops_AnnounceOnceWhenTheyLandOnAColour()
        {
            var ball = NewBall();
            FireRainbowPopFrom(ball, DamageFlags.DirectHit | DamageFlags.DeferredStreak);
            FireRainbowPopFrom(ball, DamageFlags.DirectHit | DamageFlags.DeferredStreak);
            _streakGrewPublisher.DidNotReceive().Publish(Arg.Any<StreakGrewMessage>());

            FirePopFrom(ball, Red);

            _streakGrewPublisher.Received(1).Publish(Arg.Is<StreakGrewMessage>(m => m.Color == Red && m.Streak == 3));
        }

        [Test]
        public void StreakGrew_AfterAStreakBreaks_TheNextPopStartsOver()
        {
            FireMultiColor((Red, 1, true));
            FireMultiColor((Red, 1, true));
            _streakGrewPublisher.ClearReceivedCalls();

            FireMultiColor((Red, 1, false), (Blue, 1, false));
            FireMultiColor((Red, 1, true));

            _streakGrewPublisher.DidNotReceive().Publish(Arg.Any<StreakGrewMessage>());
        }

        [Test]
        public void StreakGrew_AnItemPopOnAnotherColour_AnnouncesNothing()
        {
            var ball = NewBall();
            FirePopFrom(ball, Red);
            FirePopFrom(ball, Red);
            _streakGrewPublisher.ClearReceivedCalls();

            FirePopFrom(ball, Blue, Vector3.zero, DamageFlags.Normal);

            _streakGrewPublisher.DidNotReceive().Publish(Arg.Any<StreakGrewMessage>());
        }

        [Test]
        public void StreakGrew_ADirectHitNoBallIsTrackedFor_AnnouncesTheSharedStreak()
        {
            FirePopFrom(null, Red);
            FirePopFrom(null, Red);

            _streakGrewPublisher.Received(1).Publish(Arg.Is<StreakGrewMessage>(m => m.Projectile == null && m.Streak == 2));
        }

        private static void ClearScorePrefs()
        {
            PlayerPrefs.DeleteKey("Level");
            PlayerPrefs.DeleteKey(Red);
            PlayerPrefs.DeleteKey(Blue);
            PlayerPrefs.Save();
        }

        [Test]
        public void OnBalloonHit_BalloonSurvives_DoesNotScore()
        {
            var model = CreateModel(Red, 3);

            FireHit(model, 1);

            _scoredPublisher.DidNotReceive().Publish(Arg.Any<ScorePointsGroupMessage>());
        }

        [Test]
        public void OnBalloonHit_BalloonPops_PublishesScorePoints()
        {
            var model = CreateModel(Red, 1, 5);

            FireHit(model, 1);

            _scoredPublisher.Received(1).Publish(
                Arg.Is<ScorePointsGroupMessage>(m => m.ColorName == Red && m.Points == 5));
            // Total only counts on confirmed arrival, not on the pop.
            Assert.AreEqual(0, _controller.TotalScore.Value);
        }

        [Test]
        public void OnTrailArrived_AccumulatesTotalScore()
        {
            FireTrailArrived(Red, 1);
            FireTrailArrived(Red, 2);

            Assert.AreEqual(2, _controller.TotalScore.Value);
        }

        [Test]
        public void LevelUp_SnapsTotalToProjected_WithoutDoubleCountingSurvivors()
        {
            // A 10-point pop is granted and published (projected = 10); only 4 have landed when the
            // level-up fires — the popup must show the full reached score, not the low in-flight value.
            FirePop(Red, 10);
            FireTrailArrived(Red, 4, 4);
            Assert.AreEqual(4, _controller.TotalScore.Value, "only the landed points before the level-up");

            _scoreLevelUpHandler.Handle(new ScoreLevelUpMessage(2));
            Assert.AreEqual(10, _controller.TotalScore.Value, "snapped to the completed level's full score");

            // The remaining 6, frozen and landing later at CompleteAll, are absorbed — no overshoot.
            FireTrailArrived(Red, 10, 6);
            Assert.AreEqual(10, _controller.TotalScore.Value, "survivors absorbed, not double-counted");
        }

        [Test]
        public void OnActorHit_AbsorbOutcome_DoesNotScore()
        {
            var actor = new AbsorbingActor("Red");
            _controller.OnActorHit(new ActorHitMessage(actor, Vector3.zero, Vector3.up,
                actor.EvaluateHit(new DamageContext(1)), new DamageContext(1)));

            _scoredPublisher.DidNotReceive().Publish(Arg.Any<ScorePointsGroupMessage>());
        }

        [Test]
        public void Streak_FirstPop_StreakIsOne()
        {
            FirePop(Red);

            Assert.AreEqual(1, _streakTracker.GetStreak(Red));
        }

        [Test]
        public void Streak_ConsecutiveSameColor_Increments()
        {
            FirePop(Red);
            FirePop(Red);
            FirePop(Red);

            Assert.AreEqual(3, _streakTracker.GetStreak(Red));
        }

        [Test]
        public void Streak_DifferentColor_Resets()
        {
            FirePop(Red);
            FirePop(Red);
            FirePop(Blue);

            Assert.AreEqual(0, _streakTracker.GetStreak(Red));
            Assert.AreEqual(1, _streakTracker.GetStreak(Blue));
        }

        [Test]
        public void Streak_ItemPop_MatchingColor_ScoresAtCurrentMultiplier_ButDoesNotGrow()
        {
            FirePop(Red);
            FirePop(Red);
            Assert.AreEqual(2, _streakTracker.GetStreak(Red));

            _scoredPublisher.ClearReceivedCalls();
            FireItemPop(Red);

            // Scored at the already-live x2 multiplier from the direct hits...
            _scoredPublisher.Received(1).Publish(Arg.Is<ScorePointsGroupMessage>(m => m.ColorName == Red && m.Points == 2));
            // ...but an item/AOE pop can't climb the streak past what direct hits built.
            Assert.AreEqual(2, _streakTracker.GetStreak(Red));
        }

        [Test]
        public void Streak_ItemPop_DifferentColor_StillResetsStreak()
        {
            FirePop(Red);
            FirePop(Red);

            // A mismatched pop resets the streak regardless of what popped it — the streak tracks
            // colour, not the source.
            FireItemPop(Blue);

            Assert.AreEqual(0, _streakTracker.GetStreak(Red));
            Assert.AreEqual(1, _streakTracker.GetStreak(Blue));
        }

        [Test]
        public void Streak_ItemPop_ThenDirectHit_ResumesGrowingFromCurrentStreak()
        {
            FirePop(Red);
            FirePop(Red);
            FireItemPop(Red);

            FirePop(Red);

            Assert.AreEqual(3, _streakTracker.GetStreak(Red));
        }

        [Test]
        public void Streak_WildcardGroupWithOnePrimary_RecordsAgainstPrimaryAndGrows()
        {
            FirePop(Red);
            FireMultiColor((Red, 1, true), (Blue, 1, false));

            Assert.AreEqual(2, _streakTracker.GetStreak(Red));
        }

        [Test]
        public void Streak_ItemPop_MultiAttributionPrimary_ScoresAtCurrentMultiplier_ButDoesNotGrow()
        {
            // Simulates an item (Bomb/Laser/Lightning) popping a rainbow balloon: a multi-attribution
            // group with one primary entry, built from a non-DirectHit context — the primary-attribution
            // branch of RecordStreakMultiplier must respect the same gate as the single-attribution one.
            FirePop(Red);
            FirePop(Red);
            Assert.AreEqual(2, _streakTracker.GetStreak(Red));

            _scoredPublisher.ClearReceivedCalls();
            FireMultiColor(DamageFlags.Normal, (Red, 1, true), (Blue, 1, false));

            _scoredPublisher.Received(1).Publish(Arg.Is<ScorePointsGroupMessage>(m => m.ColorName == Red && m.Points == 2));
            Assert.AreEqual(2, _streakTracker.GetStreak(Red), "an item pop's primary attribution can't grow the streak either");
        }

        [Test]
        public void Streak_MixedGroupWithNoPrimary_StillBreaks()
        {
            // Mirrors Tough/BubbleCluster's shape — every attribution false — must keep breaking.
            FirePop(Red);
            FireMultiColor((Red, 1, false), (Blue, 1, false));

            Assert.AreEqual(0, _streakTracker.GetStreak(Red));
        }

        [Test]
        public void Streak_MixedGroupWithTwoPrimaries_FallsBackToBreak()
        {
            // Ambiguous — more than one anchor candidate — safest to treat as a break, not guess.
            FirePop(Red);
            FireMultiColor((Red, 1, true), (Blue, 1, true));

            Assert.AreEqual(0, _streakTracker.GetStreak(Red));
        }

        [Test]
        public void OnBalloonHit_RainbowMode_ScoresFullToEachAllowedColor()
        {
            FireHitWithColor(CreateRainbowModel(2), 1, Red);

            // First pop → streak multiplier 1, so each allowed colour scores its full value (2).
            _scoredPublisher.Received(1).Publish(Arg.Is<ScorePointsGroupMessage>(m => m.ColorName == Red && m.Points == 2));
            _scoredPublisher.Received(1).Publish(Arg.Is<ScorePointsGroupMessage>(m => m.ColorName == Blue && m.Points == 2));
        }

        [Test]
        public void Streak_RainbowPopsCarryAndGrowStreak()
        {
            FireHitWithColor(CreateRainbowModel(2), 1, Red);
            Assert.AreEqual(1, _streakTracker.GetStreak(Red));

            FireHitWithColor(CreateRainbowModel(2), 1, Red);
            Assert.AreEqual(2, _streakTracker.GetStreak(Red));
        }

        [Test]
        public void Streak_TwoBalls_EachPopRecordsOnItsOwnBallsStreak()
        {
            var ballA = NewBall();
            var ballB = NewBall();
            FirePopFrom(ballA, Red);
            FirePopFrom(ballA, Red);

            _scoredPublisher.ClearReceivedCalls();
            FirePopFrom(ballB, Red);

            Assert.AreEqual(2, _streakTracker.For(ballA).CurrentStreak);
            Assert.AreEqual(1, _streakTracker.For(ballB).CurrentStreak);
            _scoredPublisher.Received(1).Publish(Arg.Is<ScorePointsGroupMessage>(m => m.ColorName == Red && m.Points == 1));
        }

        [Test]
        public void Streak_TwoBalls_AnotherBallsColorChangeDoesNotBreakTheFirst()
        {
            var ballA = NewBall();
            var ballB = NewBall();
            FirePopFrom(ballA, Red);
            FirePopFrom(ballA, Red);

            FirePopFrom(ballB, Blue);

            Assert.AreEqual(2, _streakTracker.For(ballA).CurrentStreak);
            Assert.AreEqual(Red, _streakTracker.For(ballA).LastColor);
        }

        // The rainbow self-feeding shape: a shot that rainbow-converts balloons and re-pops them climbs a
        // wildcard/carry streak. That climb must belong to the shot doing it, never be credited to (or cash
        // in) another ball's streak.
        [Test]
        public void Streak_BallBWildcardAndCarryPops_NeverTouchBallAsStreak()
        {
            var ballA = NewBall();
            var ballB = NewBall();
            FirePopFrom(ballA, Red);
            FirePopFrom(ballA, Red);

            FireRainbowPopFrom(ballB, DamageFlags.DirectHit | DamageFlags.WildcardStreak);
            FireRainbowPopFrom(ballB, DamageFlags.DirectHit | DamageFlags.CarryStreak);
            FireRainbowPopFrom(ballB, DamageFlags.DirectHit | DamageFlags.WildcardStreak);

            Assert.AreEqual(3, _streakTracker.For(ballB).CurrentStreak);
            Assert.AreEqual(2, _streakTracker.For(ballA).CurrentStreak);
            Assert.AreEqual(Red, _streakTracker.For(ballA).LastColor);

            _scoredPublisher.ClearReceivedCalls();
            FirePopFrom(ballA, Red);
            _scoredPublisher.Received(1).Publish(Arg.Is<ScorePointsGroupMessage>(m => m.ColorName == Red && m.Points == 3));
            Assert.AreEqual(3, _streakTracker.For(ballA).CurrentStreak, "A resumes from its own x2, not B's x3");
        }

        [Test]
        public void Streak_HitNoBallCaused_RecordsOnTheSharedStreak_NotOnAnyBall()
        {
            var ball = NewBall();
            FirePopFrom(ball, Red);

            FirePop(Blue);

            Assert.AreEqual(1, _streakTracker.For(ball).CurrentStreak);
            Assert.AreEqual(Red, _streakTracker.For(ball).LastColor);
            Assert.AreEqual(Blue, _streakTracker.For(null).LastColor);
        }

        [Test]
        public void Streak_MultipliesPoints()
        {
            // The streak multiplier scales the single group's point total (was: point-message count).
            FirePop(Red);
            _scoredPublisher.Received(1).Publish(Arg.Is<ScorePointsGroupMessage>(m => m.ColorName == Red && m.Points == 1));

            _scoredPublisher.ClearReceivedCalls();
            FirePop(Red);
            _scoredPublisher.Received(1).Publish(Arg.Is<ScorePointsGroupMessage>(m => m.ColorName == Red && m.Points == 2));

            _scoredPublisher.ClearReceivedCalls();
            FirePop(Red);
            _scoredPublisher.Received(1).Publish(Arg.Is<ScorePointsGroupMessage>(m => m.ColorName == Red && m.Points == 3));
        }

        [Test]
        public void Streak_MultipliesWithScoreValue()
        {
            FireHit(CreateModel(Red, 1, 2), 1);
            _scoredPublisher.Received(1).Publish(Arg.Is<ScorePointsGroupMessage>(m => m.ColorName == Red && m.Points == 2));

            _scoredPublisher.ClearReceivedCalls();
            FireHit(CreateModel(Red, 1, 2), 1);
            _scoredPublisher.Received(1).Publish(Arg.Is<ScorePointsGroupMessage>(m => m.ColorName == Red && m.Points == 4));
        }

        [Test]
        public void ScorePoint_PublishesOnlyGrantedPoints()
        {
            // The level caps a scoreValue-4 pop at 3; the group carries only the granted 3.
            _levelProgress.ClaimProgress(Red, 4).Returns((0, 3));

            FireHit(CreateModel(Red, 1, 4), 1);

            _scoredPublisher.Received(1).Publish(
                Arg.Is<ScorePointsGroupMessage>(m => m.ColorName == Red && m.Points == 3 && m.LastScore == 3));
        }

        [Test]
        public void ScorePoint_NumbersFromClaimedBase()
        {
            // The group is numbered from the base progress the level reports, not from 1.
            _levelProgress.ClaimProgress(Red, Arg.Any<int>()).Returns((5, 2));

            FirePop(Red);

            _scoredPublisher.Received(1).Publish(
                Arg.Is<ScorePointsGroupMessage>(m => m.FirstScore == 6 && m.LastScore == 7));
        }

        [Test]
        public void ScorePoint_GroupCarriesTotalPoints()
        {
            FireHit(CreateModel(Red, 1, 3), 1);

            _scoredPublisher.Received(1).Publish(Arg.Is<ScorePointsGroupMessage>(m => m.Points == 3));
        }

        [Test]
        public void ScorePoint_PublishesOneGroupPerPop()
        {
            var received = new List<ScorePointsGroupMessage>();
            _scoredPublisher
                .When(p => p.Publish(Arg.Any<ScorePointsGroupMessage>()))
                .Do(ci => received.Add(ci.Arg<ScorePointsGroupMessage>()));

            FireHit(CreateModel(Red, 1, 3), 1);

            // One group per attribution entry, carrying a contiguous score range (was: N per-point messages).
            Assert.AreEqual(1, received.Count);
            Assert.AreEqual(1, received[0].FirstScore);
            Assert.AreEqual(3, received[0].LastScore);
        }

        [Test]
        public void ResetRun_ClearsTotalScore()
        {
            FireTrailArrived(Red, 1);
            FireTrailArrived(Red, 2);
            Assert.AreEqual(2, _controller.TotalScore.Value);

            _controller.ResetRun(2);

            Assert.AreEqual(0, _controller.TotalScore.Value);
        }

        [Test]
        public void RunState_IsNotPersisted()
        {
            FireTrailArrived(Red, 3);

            _controller.Dispose();

            Assert.AreEqual(-1, PlayerPrefs.GetInt(Red, -1));
        }

        // OnActorHit is invoked directly — ScoreController is a HitPipeline stage, not a bus subscriber.
        // Defaults to DirectHit — a real projectile contact/discharge always carries it (see
        // ProjectileHitResolver) — so plain FireHit/FirePop calls represent a direct pop that grows the
        // streak, matching every test below written before item/AOE pops were gated separately.
        private void FireHit(IBalloonModel model, int damage, DamageFlags flags = DamageFlags.DirectHit)
        {
            var context = new DamageContext(damage, flags);
            var outcome = model.EvaluateHit(context);
            _controller.OnActorHit(new ActorHitMessage(model, Vector3.zero, Vector3.up, outcome, context));
        }

        // A ball only owns a streak while the registry tracks it; an untracked one reads the shared streak.
        private ProjectileModel NewBall()
        {
            var ball = new ProjectileModel();
            _registry.Load(ball, null);
            _registry.Launch(ball);
            return ball;
        }

        private void FirePopFrom(IProjectileModel ball, string color)
        {
            FirePopFrom(ball, color, Vector3.zero);
        }

        private void FirePopFrom(
            IProjectileModel ball, string color, Vector3 at, DamageFlags flags = DamageFlags.DirectHit)
        {
            var model = CreateModel(color, 1);
            var context = new DamageContext(1, flags, "", ball);
            _controller.OnActorHit(new ActorHitMessage(model, at, Vector3.up, model.EvaluateHit(context), context));
        }

        private void FireRainbowPopFrom(IProjectileModel ball, DamageFlags flags)
        {
            var model = CreateRainbowModel(1);
            var context = new DamageContext(1, flags, Red, ball);
            _controller.OnActorHit(new ActorHitMessage(model, Vector3.zero, Vector3.up, model.EvaluateHit(context), context));
        }

        private void FirePop(string color, int scoreValue = 1)
        {
            FireHit(CreateModel(color, 1, scoreValue), 1);
        }

        // Simulates an item/AOE pop (Bomb/Laser/Lightning) the way their handlers actually build a
        // DamageContext — from ItemSettings.Flags, which never carries DirectHit.
        private void FireItemPop(string color, int scoreValue = 1)
        {
            FireHit(CreateModel(color, 1, scoreValue), 1, DamageFlags.Normal);
        }

        private void FireMultiColor(params (string Color, int Points, bool IsPrimary)[] attributions)
        {
            FireMultiColor(DamageFlags.DirectHit, attributions);
        }

        private void FireMultiColor(DamageFlags flags, params (string Color, int Points, bool IsPrimary)[] attributions)
        {
            var actor = new MultiColorActor(attributions);
            _controller.OnActorHit(new ActorHitMessage(
                actor, Vector3.zero, Vector3.up, HitOutcome.Pop, new DamageContext(1, flags)));
        }

        // Unlike FireHit, carries a real SourceColorId — required for the rainbow's primary attribution.
        private void FireHitWithColor(IBalloonModel model, int damage, string sourceColorId)
        {
            var context = new DamageContext(damage, DamageFlags.DirectHit, sourceColorId);
            var outcome = model.EvaluateHit(context);
            _controller.OnActorHit(new ActorHitMessage(model, Vector3.zero, Vector3.up, outcome, context));
        }

        private static IBalloonModel CreateRainbowModel(int scoreValue)
        {
            var model = new BalloonModel(
                new BalloonModelConfig(scoreValue: scoreValue, hitsToPop: 1),
                allowedColors: new[] { Red, Blue });
            model.Color.Value = GamePalette.RainbowColorId;
            return model;
        }

        private void FireTrailArrived(string color, int score, int points = 1)
        {
            _trailArrivedHandler.Handle(new ScoreTrailArrivedMessage(color, score, points, Vector3.zero));
        }

        private static IBalloonModel CreateModel(string color, int hitsRemaining, int scoreValue = 1)
        {
            var model = new BalloonModel(new BalloonModelConfig(scoreValue: scoreValue, hitsToPop: hitsRemaining));
            model.Color.Value = color;
            return model;
        }

        private class AbsorbingActor : ISlotActor, IHitable
        {
            public AbsorbingActor(string color) { }

            public Vector2Int SlotIndex { get; set; }
            public SlotActorKind Kind => SlotActorKind.Static;
            public HitOutcome EvaluateHit(DamageContext context) => HitOutcome.Absorb;
        }

        // Emits a hand-built attribution group, to test RecordStreakMultiplier's IsPrimary branch in
        // isolation from any real balloon model.
        private class MultiColorActor : ISlotActor, IHitable, IHasScoreColor
        {
            private readonly (string Color, int Points, bool IsPrimary)[] _attributions;

            public MultiColorActor((string Color, int Points, bool IsPrimary)[] attributions)
            {
                _attributions = attributions;
            }

            public Vector2Int SlotIndex { get; set; }
            public SlotActorKind Kind => SlotActorKind.Static;
            public HitOutcome EvaluateHit(DamageContext context) => HitOutcome.Pop;

            public void ResolveScoreAttribution(
                in DamageContext context, IReadOnlyList<string> incompleteColors, IList<ScoreAttribution> results)
            {
                foreach (var (color, points, isPrimary) in _attributions)
                {
                    results.Add(new ScoreAttribution(color, points, breaksStreak: false, isPrimary: isPrimary));
                }
            }
        }
    }
}
