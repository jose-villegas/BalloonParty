using System;
using System.Collections.Generic;
using System.Reflection;
using BalloonParty.Balloon.Model;
using BalloonParty.Configuration;
using BalloonParty.Game.Health;
using BalloonParty.Game.Level;
using BalloonParty.Game.Score;
using BalloonParty.Shared;
using BalloonParty.Shared.GameState;
using BalloonParty.Shared.Messages;
using BalloonParty.Slots.Capabilities;
using BalloonParty.Slots.Actor;
using MessagePipe;
using NSubstitute;
using NUnit.Framework;
using UniRx;
using UnityEngine;

namespace BalloonParty.Tests.Game
{
    [TestFixture]
    public class ScoreControllerTests
    {
        private const string Red = "Red";
        private const string Blue = "Blue";

        private IActiveLevelParameters _levelParams;
        private IGamePalette _palette;
        private IPublisher<ScorePointMessage> _scoredPublisher;
        private IPublisher<ScoreLevelUpMessage> _levelUpPublisher;
        private ScoreController _controller;
        private INavigation _navigation;
        private ReactiveProperty<NavigationState> _navState;
        private ILossForecast _lossForecast;
        private ColorStreakTracker _streakTracker;
        private IMessageHandler<ScoreTrailArrivedMessage> _trailArrivedHandler;

        [SetUp]
        public void SetUp()
        {
            ClearScorePrefs();

            _levelParams = Substitute.For<IActiveLevelParameters>();
            _levelParams.PointsRequiredForLevel(Arg.Any<int>()).Returns(10);
            _levelParams.AllowedColors.Returns(new List<string> { Red, Blue });

            _palette = Substitute.For<IGamePalette>();
            var colors = new List<PaletteEntry> { CreatePaletteEntry(Red), CreatePaletteEntry(Blue) };
            _palette.Colors.Returns(colors);
            _palette.ColorNames.Returns(new[] { Red, Blue });

            _navigation = Substitute.For<INavigation>();
            _navState = new ReactiveProperty<NavigationState>(NavigationState.Game);
            _navigation.Current.Returns(_navState);

            _lossForecast = Substitute.For<ILossForecast>();
            _lossForecast.LossImminent.Returns(false);

            _controller = BuildController();
            _controller.Start();
        }

        [TearDown]
        public void TearDown()
        {
            _controller.Dispose();
            Time.timeScale = 1f;
            ClearScorePrefs();
        }

        private ScoreController BuildController()
        {
            var trailArrivedSubscriber = Substitute.For<ISubscriber<ScoreTrailArrivedMessage>>();
            var levelUpSubscriber = Substitute.For<ISubscriber<ScoreLevelUpMessage>>();
            IMessageHandler<ScoreLevelUpMessage> levelUpHandler = null;
            levelUpSubscriber
                .Subscribe(
                    Arg.Do<IMessageHandler<ScoreLevelUpMessage>>(h => levelUpHandler = h),
                    Arg.Any<MessageHandlerFilter<ScoreLevelUpMessage>[]>())
                .Returns(Substitute.For<IDisposable>());
            _scoredPublisher = Substitute.For<IPublisher<ScorePointMessage>>();
            _levelUpPublisher = Substitute.For<IPublisher<ScoreLevelUpMessage>>();

            _levelUpPublisher
                .When(p => p.Publish(Arg.Any<ScoreLevelUpMessage>()))
                .Do(ci => levelUpHandler?.Handle(ci.Arg<ScoreLevelUpMessage>()));

            // Capture the IMessageHandler that ScoreController registers via the Subscribe extension method.
            // The extension wraps Action<T> in AnonymousMessageHandler and calls the interface method.
            trailArrivedSubscriber
                .Subscribe(
                    Arg.Do<IMessageHandler<ScoreTrailArrivedMessage>>(h => _trailArrivedHandler = h),
                    Arg.Any<MessageHandlerFilter<ScoreTrailArrivedMessage>[]>())
                .Returns(Substitute.For<IDisposable>());

            var projectileLoadedSubscriber = Substitute.For<ISubscriber<ProjectileLoadedMessage>>();
            projectileLoadedSubscriber
                .Subscribe(
                    Arg.Any<IMessageHandler<ProjectileLoadedMessage>>(),
                    Arg.Any<MessageHandlerFilter<ProjectileLoadedMessage>[]>())
                .Returns(Substitute.For<IDisposable>());

            _streakTracker = new ColorStreakTracker(levelUpSubscriber, projectileLoadedSubscriber);
            return new ScoreController(
                trailArrivedSubscriber,
                _scoredPublisher,
                _levelUpPublisher,
                _levelParams,
                _palette,
                _navigation,
                _lossForecast,
                _streakTracker);
        }

        private static void ClearScorePrefs()
        {
            PlayerPrefs.DeleteKey("Level");
            PlayerPrefs.DeleteKey(Red);
            PlayerPrefs.DeleteKey(Blue);
            PlayerPrefs.DeleteKey(Red + ".Progress");
            PlayerPrefs.DeleteKey(Blue + ".Progress");
            PlayerPrefs.Save();
        }


        [Test]
        public void OnBalloonHit_BalloonSurvives_DoesNotScore()
        {
            var model = CreateModel(Red, 3);

            FireHit(model, 1);

            _scoredPublisher.DidNotReceive().Publish(Arg.Any<ScorePointMessage>());
        }

        [Test]
        public void OnBalloonHit_BalloonPops_PublishesScorePoints()
        {
            var model = CreateModel(Red, 1, 5);

            FireHit(model, 1);

            _scoredPublisher.Received(5).Publish(
                Arg.Is<ScorePointMessage>(m => m.ColorName == Red));
            Assert.AreEqual(0, _controller.TotalScore.Value);
        }

        [Test]
        public void OnTrailArrived_AccumulatesScore()
        {
            FireTrailArrived(Red, 1);
            FireTrailArrived(Red, 2);

            Assert.AreEqual(2, _controller.TotalScore.Value);
        }

        [Test]
        public void CheckLevelUp_AllColorsMeetThreshold_LevelsUp()
        {
            _levelParams.PointsRequiredForLevel(2).Returns(2);

            FireTrailArrived(Red, 1);
            FireTrailArrived(Red, 2);
            FireTrailArrived(Blue, 1);
            FireTrailArrived(Blue, 2);

            _levelUpPublisher.Received(1).Publish(
                Arg.Is<ScoreLevelUpMessage>(m => m.NewLevel == 2));
            Assert.AreEqual(2, _controller.Level.Value);
            _navigation.Received(1).TransitionTo(NavigationState.LevelUp);
        }

        [Test]
        public void CheckLevelUp_PublishesCompletedColorsSnapshot()
        {
            // The ceremony must celebrate the colors that just completed, not whatever the
            // level-range resolver has re-resolved to by the time it reads live state.
            _levelParams.PointsRequiredForLevel(2).Returns(2);

            FireTrailArrived(Red, 1);
            FireTrailArrived(Red, 2);
            FireTrailArrived(Blue, 1);
            FireTrailArrived(Blue, 2);

            _levelUpPublisher.Received(1).Publish(
                Arg.Is<ScoreLevelUpMessage>(m => m.CompletedColors.Count == 2));
        }

        [Test]
        public void CheckLevelUp_WhenLossImminent_DoesNotLevelUp()
        {
            // No level-up on a doomed run: queued overflow charges already cover the remaining HP.
            _levelParams.PointsRequiredForLevel(2).Returns(1);
            _lossForecast.LossImminent.Returns(true);

            FireTrailArrived(Red, 1);
            FireTrailArrived(Blue, 1);

            _levelUpPublisher.DidNotReceive().Publish(Arg.Any<ScoreLevelUpMessage>());
            _navigation.DidNotReceive().TransitionTo(NavigationState.LevelUp);
            Assert.AreEqual(1, _controller.Level.Value);
        }

        [Test]
        public void CheckLevelUp_WhenNotInGame_DoesNotLevelUp()
        {
            // A trail arriving post-mortem must not yank navigation out of GameOver.
            _levelParams.PointsRequiredForLevel(2).Returns(1);
            _navState.Value = NavigationState.GameOver;

            FireTrailArrived(Red, 1);
            FireTrailArrived(Blue, 1);

            _levelUpPublisher.DidNotReceive().Publish(Arg.Any<ScoreLevelUpMessage>());
            _navigation.DidNotReceive().TransitionTo(NavigationState.LevelUp);
            Assert.AreEqual(1, _controller.Level.Value);
        }

        [Test]
        public void CheckLevelUp_OneColorShort_DoesNotLevelUp()
        {
            _levelParams.PointsRequiredForLevel(2).Returns(5);

            for (var i = 1; i <= 5; i++)
            {
                FireTrailArrived(Red, i);
            }
            // Blue has not scored — progress is 0

            _levelUpPublisher.DidNotReceive().Publish(Arg.Any<ScoreLevelUpMessage>());
            Assert.AreEqual(1, _controller.Level.Value);
        }

        [Test]
        public void CheckLevelUp_ColorGatedOutOfLevel_IsNotRequired()
        {
            // Blue is gated out of the active range — only Red needs to hit threshold.
            _levelParams.AllowedColors.Returns(new List<string> { Red });
            _levelParams.PointsRequiredForLevel(2).Returns(5);

            for (var i = 1; i <= 5; i++)
            {
                FireTrailArrived(Red, i);
            }
            // Blue has not scored and never will this level — must not block the level-up.

            _levelUpPublisher.Received(1).Publish(Arg.Any<ScoreLevelUpMessage>());
            Assert.AreEqual(2, _controller.Level.Value);
        }

        [Test]
        public void CheckLevelUp_LevelUp_ResetsAllColorProgress()
        {
            _levelParams.PointsRequiredForLevel(2).Returns(2);

            FireTrailArrived(Red, 1);
            FireTrailArrived(Red, 2);
            FireTrailArrived(Blue, 1);
            FireTrailArrived(Blue, 2);

            Assert.AreEqual(0, _controller.GetProgress(Red));
            Assert.AreEqual(0, _controller.GetProgress(Blue));
        }

        [Test]
        public void OnBalloonHit_Pop_DoesNotIncrementLevelProgress()
        {
            FirePop(Red, 3);

            Assert.AreEqual(0, _controller.GetProgress(Red));
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
        public void Streak_MultipliesPoints()
        {
            FirePop(Red);
            _scoredPublisher.Received(1).Publish(
                Arg.Is<ScorePointMessage>(m => m.ColorName == Red));

            _scoredPublisher.ClearReceivedCalls();
            FirePop(Red);
            _scoredPublisher.Received(2).Publish(
                Arg.Is<ScorePointMessage>(m => m.ColorName == Red));

            _scoredPublisher.ClearReceivedCalls();
            FirePop(Red);
            _scoredPublisher.Received(3).Publish(
                Arg.Is<ScorePointMessage>(m => m.ColorName == Red));
        }

        [Test]
        public void Streak_MultipliesWithScoreValue()
        {
            var model = CreateModel(Red, 1, 2);
            FireHit(model, 1);

            // First pop: streak 1 × scoreValue 2 = 2 points
            _scoredPublisher.Received(2).Publish(
                Arg.Is<ScorePointMessage>(m => m.ColorName == Red));

            _scoredPublisher.ClearReceivedCalls();
            var model2 = CreateModel(Red, 1, 2);
            FireHit(model2, 1);

            // Second pop: streak 2 × scoreValue 2 = 4 points
            _scoredPublisher.Received(4).Publish(
                Arg.Is<ScorePointMessage>(m => m.ColorName == Red));
        }

        [Test]
        public void Streak_ResetsOnLevelUp()
        {
            _levelParams.PointsRequiredForLevel(2).Returns(1);

            FirePop(Red);
            FirePop(Red);

            FireTrailArrived(Red, 1);
            FireTrailArrived(Blue, 1);

            Assert.AreEqual(0, _streakTracker.GetStreak(Red));
        }

        [Test]
        public void WillLevelUp_AllColorsProjected_ReturnsTrue()
        {
            _levelParams.PointsRequiredForLevel(2).Returns(1);

            FirePop(Red);
            FirePop(Blue);

            Assert.IsTrue(_controller.WillLevelUp());
        }

        [Test]
        public void WillLevelUp_OneColorShort_ReturnsFalse()
        {
            _levelParams.PointsRequiredForLevel(2).Returns(2);

            FirePop(Red);

            Assert.IsFalse(_controller.WillLevelUp());
        }

        [Test]
        public void WillLevelUp_ColorGatedOutOfLevel_IsNotRequired()
        {
            _levelParams.AllowedColors.Returns(new List<string> { Red });
            _levelParams.PointsRequiredForLevel(2).Returns(1);

            FirePop(Red);

            Assert.IsTrue(_controller.WillLevelUp());
        }

        [Test]
        public void ScorePoint_BelowThreshold_StaysCurrentLevel()
        {
            _levelParams.PointsRequiredForLevel(2).Returns(5);

            FirePop(Red);

            _scoredPublisher.Received(1).Publish(
                Arg.Is<ScorePointMessage>(m => m.Score == 1));
        }

        [Test]
        public void ScorePoint_AtThreshold_StaysCurrentLevel()
        {
            // rawScore == required → capped at the threshold, one point at Score 1
            _levelParams.PointsRequiredForLevel(2).Returns(1);

            FirePop(Red);

            _scoredPublisher.Received(1).Publish(
                Arg.Is<ScorePointMessage>(m => m.Score == 1));
        }

        [Test]
        public void ScorePoint_AboveThreshold_ExcessDropped()
        {
            // threshold = 3, scoreValue = 4 → progress is capped at 3, so only 3 points publish
            // (Score 1,2,3); the 4th is dropped rather than carried past the threshold (cap one
            // level-up per burst — no carry-over that would auto-complete the following level).
            _levelParams.PointsRequiredForLevel(2).Returns(3);

            var model = new BalloonModel(new BalloonModelConfig(scoreValue: 4));
            model.Color.Value = Red;
            model.HitsRemaining.Value = 1;
            FireHit(model, 1);

            _scoredPublisher.Received(3).Publish(
                Arg.Is<ScorePointMessage>(m => m.ColorName == Red));
            _scoredPublisher.DidNotReceive().Publish(
                Arg.Is<ScorePointMessage>(m => m.Score > 3));
        }

        [Test]
        public void ScorePoint_GroupSizeEqualsPoints()
        {
            var model = new BalloonModel(new BalloonModelConfig(scoreValue: 3));
            model.Color.Value = Red;
            model.HitsRemaining.Value = 1;
            FireHit(model, 1);

            // streak=1, scoreValue=3 → points=3, GroupSize=3 on all messages
            _scoredPublisher.Received(3).Publish(
                Arg.Is<ScorePointMessage>(m => m.GroupSize == 3));
        }

        [Test]
        public void ScorePoint_GroupIndexIsSequential()
        {
            var received = new List<ScorePointMessage>();
            _scoredPublisher
                .When(p => p.Publish(Arg.Any<ScorePointMessage>()))
                .Do(ci => received.Add(ci.Arg<ScorePointMessage>()));

            var model = new BalloonModel(new BalloonModelConfig(scoreValue: 3));
            model.Color.Value = Red;
            model.HitsRemaining.Value = 1;
            FireHit(model, 1);

            Assert.AreEqual(3, received.Count);
            Assert.AreEqual(0, received[0].GroupIndex);
            Assert.AreEqual(1, received[1].GroupIndex);
            Assert.AreEqual(2, received[2].GroupIndex);
        }


        [Test]
        public void OnActorHit_AbsorbOutcome_DoesNotScore()
        {
            var actor = new AbsorbingActor("Red");
            _controller.OnActorHit(new ActorHitMessage(actor, Vector3.zero, Vector3.up, actor.EvaluateHit(new DamageContext(1)), new DamageContext(1)));

            _scoredPublisher.DidNotReceive().Publish(Arg.Any<ScorePointMessage>());
        }

        [Test]
        public void Start_IgnoresPersistedLevel_StartsAtLevelOne()
        {
            PlayerPrefs.SetInt("Level", 5);
            PlayerPrefs.Save();

            var controller = BuildController();
            controller.Start();

            Assert.AreEqual(1, controller.Level.Value);
            controller.Dispose();
        }

        [Test]
        public void ResetRun_ResetsLevelToOne()
        {
            _levelParams.PointsRequiredForLevel(2).Returns(1);
            FireTrailArrived(Red, 1);
            FireTrailArrived(Blue, 1);
            Assert.AreEqual(2, _controller.Level.Value);

            _controller.ResetRun(2);

            Assert.AreEqual(1, _controller.Level.Value);
        }

        [Test]
        public void ResetRun_ClearsScoreAndColorProgress()
        {
            FireTrailArrived(Red, 1);
            FireTrailArrived(Red, 2);
            Assert.AreEqual(2, _controller.TotalScore.Value);

            _controller.ResetRun(2);

            Assert.AreEqual(0, _controller.TotalScore.Value);
            Assert.AreEqual(0, _controller.GetProgress(Red));
            Assert.AreEqual(0, _controller.GetProgress(Blue));
        }

        [Test]
        public void RunState_IsNotPersisted()
        {
            FireTrailArrived(Red, 3);

            _controller.Dispose();

            Assert.AreEqual(-1, PlayerPrefs.GetInt("Level", -1));
            Assert.AreEqual(-1, PlayerPrefs.GetInt(Red, -1));
        }

        // OnActorHit is invoked directly — ScoreController is a HitPipeline stage, not a
        // bus subscriber.
        private void FireHit(IBalloonModel model, int damage)
        {
            var outcome = model.EvaluateHit(new DamageContext(damage));
            _controller.OnActorHit(new ActorHitMessage(model, Vector3.zero, Vector3.up, outcome, new DamageContext(damage)));
        }

        private void FirePop(string color, int scoreValue = 1)
        {
            var model = CreateModel(color, 1, scoreValue);
            FireHit(model, 1);
        }

        private void FireTrailArrived(string color, int score)
        {
            _trailArrivedHandler.Handle(new ScoreTrailArrivedMessage(color, score, Vector3.zero));
        }

        private static IBalloonModel CreateModel(string color, int hitsRemaining, int scoreValue = 1)
        {
            var model = new BalloonModel(new BalloonModelConfig(scoreValue: scoreValue, hitsToPop: hitsRemaining));
            model.Color.Value = color;
            return model;
        }

        private static PaletteEntry CreatePaletteEntry(string name)
        {
            var entry = new PaletteEntry();
            SetField(entry, "_name", name);
            return entry;
        }

        private static void SetField(object target, string fieldName, object value)
        {
            target.GetType()
                .GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance)!
                .SetValue(target, value);
        }

        private class AbsorbingActor : ISlotActor, IHitable
        {
            public AbsorbingActor(string color) { }

            public Vector2Int SlotIndex { get; set; }
            public SlotActorKind Kind => SlotActorKind.Static;
            public HitOutcome EvaluateHit(DamageContext context) => HitOutcome.Absorb;
        }
    }
}
