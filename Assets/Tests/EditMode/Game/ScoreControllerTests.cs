using System;
using System.Collections.Generic;
using System.Reflection;
using BalloonParty.Balloon.Model;
using BalloonParty.Configuration;
using BalloonParty.Game.Level;
using BalloonParty.Game.Score;
using BalloonParty.Shared.Messages;
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
        private ScoreController _controller;
        private ColorStreakTracker _streakTracker;
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
            _streakTracker = new ColorStreakTracker(
                Substitute.For<IPublisher<StreakChangedMessage>>(), levelUpSubscriber, projectileLoadedSubscriber);

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
                _levelProgress,
                _palette,
                _streakTracker);
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
        public void Streak_WildcardGroupWithOnePrimary_RecordsAgainstPrimaryAndGrows()
        {
            FirePop(Red);
            FireMultiColor((Red, 1, true), (Blue, 1, false));

            Assert.AreEqual(2, _streakTracker.GetStreak(Red));
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
        private void FireHit(IBalloonModel model, int damage)
        {
            var outcome = model.EvaluateHit(new DamageContext(damage));
            _controller.OnActorHit(new ActorHitMessage(model, Vector3.zero, Vector3.up, outcome, new DamageContext(damage)));
        }

        private void FirePop(string color, int scoreValue = 1)
        {
            FireHit(CreateModel(color, 1, scoreValue), 1);
        }

        private void FireMultiColor(params (string Color, int Points, bool IsPrimary)[] attributions)
        {
            var actor = new MultiColorActor(attributions);
            _controller.OnActorHit(new ActorHitMessage(
                actor, Vector3.zero, Vector3.up, HitOutcome.Pop, new DamageContext(1)));
        }

        // Unlike FireHit, carries a real SourceColorId — required for the rainbow's primary attribution.
        private void FireHitWithColor(IBalloonModel model, int damage, string sourceColorId)
        {
            var context = new DamageContext(damage, DamageFlags.Normal, sourceColorId);
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
