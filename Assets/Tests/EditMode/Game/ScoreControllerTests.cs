using System;
using System.Collections.Generic;
using System.Reflection;
using BalloonParty.Balloon.Model;
using BalloonParty.Configuration;
using BalloonParty.Game.Score;
using BalloonParty.Shared;
using BalloonParty.Shared.Messages;
using BalloonParty.Slots.Capabilities;
using BalloonParty.Slots.Actor;
using MessagePipe;
using NSubstitute;
using NUnit.Framework;
using UnityEngine;

namespace BalloonParty.Tests.Game
{
    [TestFixture]
    public class ScoreControllerTests
    {
        private const string Red = "Red";
        private const string Blue = "Blue";

        private IGameConfiguration _config;
        private GamePalette _palette;
        private IPublisher<ScorePointMessage> _scoredPublisher;
        private IPublisher<ScoreLevelUpMessage> _levelUpPublisher;
        private ScoreController _controller;
        private ColorStreakTracker _streakTracker;
        private IMessageHandler<ActorHitMessage> _hitHandler;
        private IMessageHandler<ScoreTrailArrivedMessage> _trailArrivedHandler;

        [SetUp]
        public void SetUp()
        {
            // Clean PlayerPrefs keys used by ScoreController to avoid cross-test pollution
            PlayerPrefs.DeleteKey("Level");
            PlayerPrefs.DeleteKey(Red);
            PlayerPrefs.DeleteKey(Blue);
            PlayerPrefs.DeleteKey(Red + ".Progress");
            PlayerPrefs.DeleteKey(Blue + ".Progress");
            PlayerPrefs.Save();

            _config = Substitute.For<IGameConfiguration>();
            _config.PointsRequiredForLevel(Arg.Any<int>()).Returns(10);

            _palette = ScriptableObject.CreateInstance<GamePalette>();
            var colors = new[] { CreatePaletteEntry(Red), CreatePaletteEntry(Blue) };
            SetField(_palette, "_colors", colors);

            var hitSubscriber = Substitute.For<ISubscriber<ActorHitMessage>>();
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
            hitSubscriber
                .Subscribe(
                    Arg.Do<IMessageHandler<ActorHitMessage>>(h => _hitHandler = h),
                    Arg.Any<MessageHandlerFilter<ActorHitMessage>[]>())
                .Returns(Substitute.For<IDisposable>());

            trailArrivedSubscriber
                .Subscribe(
                    Arg.Do<IMessageHandler<ScoreTrailArrivedMessage>>(h => _trailArrivedHandler = h),
                    Arg.Any<MessageHandlerFilter<ScoreTrailArrivedMessage>[]>())
                .Returns(Substitute.For<IDisposable>());

            _streakTracker = new ColorStreakTracker(levelUpSubscriber);
            _controller = new ScoreController(
                hitSubscriber,
                trailArrivedSubscriber,
                _scoredPublisher,
                _levelUpPublisher,
                _config,
                _palette,
                _streakTracker);

            _controller.Start();
        }

        [TearDown]
        public void TearDown()
        {
            _controller.Dispose();
            Time.timeScale = 1f;

            PlayerPrefs.DeleteKey("Level");
            PlayerPrefs.DeleteKey(Red);
            PlayerPrefs.DeleteKey(Blue);
            PlayerPrefs.DeleteKey(Red + ".Progress");
            PlayerPrefs.DeleteKey(Blue + ".Progress");
            PlayerPrefs.Save();

            UnityEngine.Object.DestroyImmediate(_palette);
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
            _config.PointsRequiredForLevel(2).Returns(2);

            FireTrailArrived(Red, 1);
            FireTrailArrived(Red, 2);
            FireTrailArrived(Blue, 1);
            FireTrailArrived(Blue, 2);

            _levelUpPublisher.Received(1).Publish(
                Arg.Is<ScoreLevelUpMessage>(m => m.NewLevel == 2));
            Assert.AreEqual(2, _controller.Level.Value);
        }

        [Test]
        public void CheckLevelUp_OneColorShort_DoesNotLevelUp()
        {
            _config.PointsRequiredForLevel(2).Returns(5);

            for (var i = 1; i <= 5; i++)
            {
                FireTrailArrived(Red, i);
            }
            // Blue has not scored — progress is 0

            _levelUpPublisher.DidNotReceive().Publish(Arg.Any<ScoreLevelUpMessage>());
            Assert.AreEqual(1, _controller.Level.Value);
        }

        [Test]
        public void CheckLevelUp_LevelUp_ResetsAllColorProgress()
        {
            _config.PointsRequiredForLevel(2).Returns(2);

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
            _config.PointsRequiredForLevel(2).Returns(1);

            FirePop(Red);
            FirePop(Red);

            FireTrailArrived(Red, 1);
            FireTrailArrived(Blue, 1);

            Assert.AreEqual(0, _streakTracker.GetStreak(Red));
        }

        [Test]
        public void WillLevelUp_AllColorsProjected_ReturnsTrue()
        {
            _config.PointsRequiredForLevel(2).Returns(1);

            FirePop(Red);
            FirePop(Blue);

            Assert.IsTrue(_controller.WillLevelUp());
        }

        [Test]
        public void WillLevelUp_OneColorShort_ReturnsFalse()
        {
            _config.PointsRequiredForLevel(2).Returns(2);

            FirePop(Red);

            Assert.IsFalse(_controller.WillLevelUp());
        }

        [Test]
        public void ScorePoint_BelowThreshold_NextLevelFalse()
        {
            _config.PointsRequiredForLevel(2).Returns(5);

            FirePop(Red);

            _scoredPublisher.Received(1).Publish(
                Arg.Is<ScorePointMessage>(m => !m.NextLevel && m.Level == 1 && m.Score == 1));
        }

        [Test]
        public void ScorePoint_AtThreshold_NotNextLevel()
        {
            // rawScore == required → rawScore > required is false → not next level
            _config.PointsRequiredForLevel(2).Returns(1);

            FirePop(Red);

            _scoredPublisher.Received(1).Publish(
                Arg.Is<ScorePointMessage>(m => !m.NextLevel && m.Level == 1 && m.Score == 1));
        }

        [Test]
        public void ScorePoint_AboveThreshold_NextLevelTrueAndScoreRenumbered()
        {
            // threshold = 3, scoreValue = 4, streak = 1 → publishes 4 messages:
            // i=0: rawScore=1 → Score=1, Level=1, NextLevel=false
            // i=1: rawScore=2 → Score=2, Level=1, NextLevel=false
            // i=2: rawScore=3 → Score=3, Level=1, NextLevel=false  (tipping point, 3 > 3 is false)
            // i=3: rawScore=4 → Score=1, Level=2, NextLevel=true   (renumbered: 4 - 3 = 1)
            _config.PointsRequiredForLevel(2).Returns(3);

            var model = new BalloonModel(new BalloonModelConfig(scoreValue: 4));
            model.Color.Value = Red;
            model.HitsRemaining.Value = 1;
            FireHit(model, 1);

            _scoredPublisher.Received(3).Publish(
                Arg.Is<ScorePointMessage>(m => !m.NextLevel && m.Level == 1));
            _scoredPublisher.Received(1).Publish(
                Arg.Is<ScorePointMessage>(m => m.NextLevel && m.Level == 2 && m.Score == 1));
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
            _hitHandler.Handle(new ActorHitMessage(actor, Vector3.zero, Vector3.up, actor.EvaluateHit(new DamageContext(1)), new DamageContext(1)));

            _scoredPublisher.DidNotReceive().Publish(Arg.Any<ScorePointMessage>());
        }

        private void FireHit(IBalloonModel model, int damage)
        {
            var outcome = model.EvaluateHit(new DamageContext(damage));
            _hitHandler.Handle(new ActorHitMessage(model, Vector3.zero, Vector3.up, outcome, new DamageContext(damage)));
        }

        private void FirePop(string color, int scoreValue = 1)
        {
            var model = CreateModel(color, 1, scoreValue);
            FireHit(model, 1);
        }

        private void FireTrailArrived(string color, int score)
        {
            _trailArrivedHandler.Handle(new ScoreTrailArrivedMessage(color, score, _controller.Level.Value, Vector3.zero));
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
