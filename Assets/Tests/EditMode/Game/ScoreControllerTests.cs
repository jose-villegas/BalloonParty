using System;
using System.Reflection;
using BalloonParty.Balloon.Model;
using BalloonParty.Configuration;
using BalloonParty.Game;
using BalloonParty.Shared;
using BalloonParty.Shared.Messages;
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
        private IPublisher<BalloonScoredMessage> _scoredPublisher;
        private IPublisher<ScoreLevelUpMessage> _levelUpPublisher;
        private ScoreController _controller;
        private IMessageHandler<BalloonHitMessage> _hitHandler;
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

            var hitSubscriber = Substitute.For<ISubscriber<BalloonHitMessage>>();
            var trailArrivedSubscriber = Substitute.For<ISubscriber<ScoreTrailArrivedMessage>>();
            _scoredPublisher = Substitute.For<IPublisher<BalloonScoredMessage>>();
            _levelUpPublisher = Substitute.For<IPublisher<ScoreLevelUpMessage>>();

            // Capture the IMessageHandler that ScoreController registers via the Subscribe extension method.
            // The extension wraps Action<T> in AnonymousMessageHandler and calls the interface method.
            hitSubscriber
                .Subscribe(
                    Arg.Do<IMessageHandler<BalloonHitMessage>>(h => _hitHandler = h),
                    Arg.Any<MessageHandlerFilter<BalloonHitMessage>[]>())
                .Returns(Substitute.For<IDisposable>());

            trailArrivedSubscriber
                .Subscribe(
                    Arg.Do<IMessageHandler<ScoreTrailArrivedMessage>>(h => _trailArrivedHandler = h),
                    Arg.Any<MessageHandlerFilter<ScoreTrailArrivedMessage>[]>())
                .Returns(Substitute.For<IDisposable>());

            _controller = new ScoreController(
                hitSubscriber,
                trailArrivedSubscriber,
                _scoredPublisher,
                _levelUpPublisher,
                _config,
                _palette);

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
        public void OnBalloonHit_Unbreakable_DoesNotScore()
        {
            var model = CreateModel(Red, -1);

            FireHit(model, 1);

            _scoredPublisher.DidNotReceive().Publish(Arg.Any<BalloonScoredMessage>());
        }

        [Test]
        public void OnBalloonHit_BalloonSurvives_DoesNotScore()
        {
            var model = CreateModel(Red, 3);

            FireHit(model, 1);

            _scoredPublisher.DidNotReceive().Publish(Arg.Any<BalloonScoredMessage>());
        }

        [Test]
        public void OnBalloonHit_BalloonPops_PublishesScoredMessage()
        {
            var model = CreateModel(Red, 1, 5);

            FireHit(model, 1);

            _scoredPublisher.Received(1).Publish(
                Arg.Is<BalloonScoredMessage>(m => m.ColorName == Red && m.Points == 5));
            Assert.AreEqual(0, _controller.TotalScore.Value);
        }

        [Test]
        public void OnTrailArrived_AccumulatesScore()
        {
            FireTrailArrived(Red);
            FireTrailArrived(Red);

            Assert.AreEqual(2, _controller.TotalScore.Value);
        }

        [Test]
        public void CheckLevelUp_AllColorsMeetThreshold_LevelsUp()
        {
            _config.PointsRequiredForLevel(2).Returns(2);

            FireTrailArrived(Red);
            FireTrailArrived(Red);
            FireTrailArrived(Blue);
            FireTrailArrived(Blue);

            _levelUpPublisher.Received(1).Publish(
                Arg.Is<ScoreLevelUpMessage>(m => m.NewLevel == 2));
            Assert.AreEqual(2, _controller.Level.Value);
        }

        [Test]
        public void CheckLevelUp_OneColorShort_DoesNotLevelUp()
        {
            _config.PointsRequiredForLevel(2).Returns(5);

            for (var i = 0; i < 5; i++)
            {
                FireTrailArrived(Red);
            }
            // Blue has not scored — progress is 0

            _levelUpPublisher.DidNotReceive().Publish(Arg.Any<ScoreLevelUpMessage>());
            Assert.AreEqual(1, _controller.Level.Value);
        }

        [Test]
        public void CheckLevelUp_LevelUp_ResetsAllColorProgress()
        {
            _config.PointsRequiredForLevel(2).Returns(2);

            FireTrailArrived(Red);
            FireTrailArrived(Red);
            FireTrailArrived(Blue);
            FireTrailArrived(Blue);

            Assert.AreEqual(0, _controller.GetProgress(Red));
            Assert.AreEqual(0, _controller.GetProgress(Blue));
        }

        [Test]
        public void OnBalloonHit_Pop_DoesNotIncrementLevelProgress()
        {
            FirePop(Red, 3);

            Assert.AreEqual(0, _controller.GetProgress(Red));
        }

        private void FireHit(IBalloonModel model, int damage)
        {
            _hitHandler.Handle(new BalloonHitMessage(model, Vector3.zero, Vector3.up, damage));
        }

        private void FirePop(string color, int scoreValue = 1)
        {
            var model = CreateModel(color, 1, scoreValue);
            FireHit(model, 1);
        }

        private void FireTrailArrived(string color)
        {
            _trailArrivedHandler.Handle(new ScoreTrailArrivedMessage(color));
        }

        private static IBalloonModel CreateModel(string color, int hitsRemaining, int scoreValue = 1)
        {
            var model = new BalloonModel();
            model.Color.Value = color;
            model.HitsRemaining.Value = hitsRemaining;
            model.ScoreValue = scoreValue;
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
    }
}
