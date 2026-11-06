using System;
using System.Collections.Generic;
using BalloonParty.Game.Score;
using BalloonParty.Projectile;
using BalloonParty.Projectile.Model;
using BalloonParty.Shared.Messages;
using MessagePipe;
using NSubstitute;
using NUnit.Framework;

namespace BalloonParty.Tests.Game
{
    /// <summary>
    ///     What the per-ball streaks announce and what they report: the ball named on each change, silence
    ///     for a reset with nothing to reset (every load resets the shared streak, so with rapid fire every
    ///     shot would otherwise play a StreakStep), one announcement for the level-up reset of all, and the
    ///     highest streak among balls still in play.
    /// </summary>
    [TestFixture]
    public class ColorStreakAnnouncementTests
    {
        private readonly List<StreakChangedMessage> _published = new();

        private ProjectileRegistry _registry;
        private ColorStreakTracker _tracker;
        private IMessageHandler<ScoreLevelUpMessage> _levelUpHandler;
        private IMessageHandler<ProjectileLoadedMessage> _loadedHandler;

        [SetUp]
        public void SetUp()
        {
            _registry = new ProjectileRegistry();

            var publisher = Substitute.For<IPublisher<StreakChangedMessage>>();
            publisher.When(p => p.Publish(Arg.Any<StreakChangedMessage>()))
                .Do(call => _published.Add(call.Arg<StreakChangedMessage>()));

            _tracker = new ColorStreakTracker(
                publisher,
                Capture<ScoreLevelUpMessage>(h => _levelUpHandler = h),
                Capture<ProjectileLoadedMessage>(h => _loadedHandler = h),
                _registry);
        }

        [TearDown]
        public void TearDown()
        {
            _tracker.Dispose();
            _registry.Dispose();
        }

        [Test]
        public void Record_OnABallsStreak_NamesThatBallOnTheMessage()
        {
            var ball = Fly();

            _tracker.For(ball).Record("Red");

            Assert.AreEqual(1, _published.Count);
            Assert.AreSame(ball, _published[0].Projectile);
            Assert.AreEqual("Red", _published[0].Color);
            Assert.AreEqual(1, _published[0].Streak);
        }

        [Test]
        public void Record_OnTheSharedStreak_NamesNoBall()
        {
            _tracker.For(null).Record("Red");

            Assert.AreEqual(1, _published.Count);
            Assert.IsNull(_published[0].Projectile);
        }

        [Test]
        public void Record_OnAnUntrackedBall_FallsBackToTheSharedStreak()
        {
            _tracker.For(new ProjectileModel()).Record("Red");

            Assert.IsNull(_published[0].Projectile, "a ball the registry no longer tracks must not get its own entry");
        }

        [Test]
        public void Reset_StreakWithNothingToReset_PublishesNothing()
        {
            _tracker.For(Fly()).Reset();

            Assert.AreEqual(0, _published.Count);
        }

        [Test]
        public void Reset_StreakWithState_PublishesOnceAndThenStaysSilent()
        {
            var ball = Fly();
            var streak = _tracker.For(ball);
            streak.Record("Red");
            _published.Clear();

            streak.Reset();
            streak.Reset();

            Assert.AreEqual(1, _published.Count);
            Assert.AreSame(ball, _published[0].Projectile);
            Assert.AreEqual(0, _published[0].Streak);
        }

        [Test]
        public void Reset_OnlyDeferredPopsBanked_StillCountsAsStateToAnnounce()
        {
            // A banked rainbow pop is state a reset really discards, even with no colour or streak yet.
            var streak = _tracker.For(Fly());
            streak.RecordDeferred();

            streak.Reset();

            Assert.AreEqual(1, _published.Count);
        }

        [Test]
        public void ProjectileLoaded_NothingOnTheSharedStreak_PublishesNothing()
        {
            _loadedHandler.Handle(new ProjectileLoadedMessage(null));

            Assert.AreEqual(0, _published.Count);
        }

        [Test]
        public void ProjectileLoaded_SharedStreakHasState_AnnouncesItsResetOnce()
        {
            _tracker.For(null).Record("Red");
            _published.Clear();

            _loadedHandler.Handle(new ProjectileLoadedMessage(null));

            Assert.AreEqual(1, _published.Count);
            Assert.IsNull(_published[0].Projectile);
            Assert.AreEqual(0, _published[0].Streak);
        }

        [Test]
        public void LevelUp_SeveralStreaksHoldState_PublishesOneResetForAll()
        {
            var a = Fly();
            var b = Fly();
            _tracker.For(a).Record("Red");
            _tracker.For(b).Record("Blue");
            _tracker.For(null).Record("Green");
            _published.Clear();

            _levelUpHandler.Handle(new ScoreLevelUpMessage(2));

            Assert.AreEqual(1, _published.Count, "one announcement, not one per ball");
            Assert.IsNull(_published[0].Projectile, "a null ball means every streak reset");
            Assert.IsNull(_published[0].Color);
            Assert.AreEqual(0, _published[0].Streak);
            Assert.AreEqual(0, _tracker.GetStreak("Red"));
            Assert.AreEqual(0, _tracker.GetStreak("Blue"));
            Assert.AreEqual(0, _tracker.GetStreak("Green"));
        }

        [Test]
        public void LevelUp_NoStreakHoldsState_PublishesNothing()
        {
            Fly();
            _tracker.For(null);

            _levelUpHandler.Handle(new ScoreLevelUpMessage(2));

            Assert.AreEqual(0, _published.Count);
        }

        [Test]
        public void LevelUp_OnlyAFlyingBallHoldsState_StillAnnounces()
        {
            _tracker.For(Fly()).Record("Red");
            _published.Clear();

            _levelUpHandler.Handle(new ScoreLevelUpMessage(2));

            Assert.AreEqual(1, _published.Count);
        }

        [Test]
        public void Retire_AnnouncesTheBallsStreakEndOnce_AndTheNextBallStartsClean()
        {
            var first = Fly();
            _tracker.For(first).Record("Red");
            _tracker.For(first).Record("Red");
            _published.Clear();

            Retire(first);
            Assert.AreEqual(1, _published.Count, "no load follows a death any more, so the retire announces the end");
            Assert.AreSame(first, _published[0].Projectile);
            Assert.AreEqual(0, _published[0].Streak);
            _published.Clear();

            var second = Fly();
            _tracker.For(second).Record("Red");

            Assert.AreEqual(1, _published.Count);
            Assert.AreSame(second, _published[0].Projectile, "a reused pooled streak must be re-owned, not keep the old ball");
            Assert.AreEqual(1, _published[0].Streak, "and must not inherit the old ball's count");
        }

        [Test]
        public void HighestStreak_NoStreaks_Zero()
        {
            Assert.AreEqual(0, _tracker.HighestStreak);
        }

        [Test]
        public void HighestStreak_TakesTheBestAcrossBallsAndTheSharedStreak()
        {
            var a = Fly();
            var b = Fly();
            _tracker.For(a).Record("Red");
            _tracker.For(a).Record("Red");
            _tracker.For(b).Record("Blue");
            _tracker.For(null).Record("Green");
            _tracker.For(null).Record("Green");
            _tracker.For(null).Record("Green");

            Assert.AreEqual(3, _tracker.HighestStreak, "the shared streak counts");

            _tracker.For(null).Reset();

            Assert.AreEqual(2, _tracker.HighestStreak);
        }

        [Test]
        public void HighestStreak_LandedBall_NoLongerCounts_EvenBeforeItRetires()
        {
            var a = Fly();
            var b = Fly();
            _tracker.For(a).Record("Red");
            _tracker.For(a).Record("Red");
            _tracker.For(b).Record("Blue");

            _registry.Land(a);

            Assert.AreEqual(1, _tracker.HighestStreak);
        }

        [Test]
        public void HighestStreak_RetiredBall_NoLongerCounts()
        {
            var a = Fly();
            _tracker.For(a).Record("Red");
            _tracker.For(a).Record("Red");

            Retire(a);

            Assert.AreEqual(0, _tracker.HighestStreak);
        }

        [Test]
        public void Retire_ABallWithNoStreak_PublishesNothing()
        {
            var ball = Fly();
            _tracker.For(ball);
            _published.Clear();

            Retire(ball);

            Assert.AreEqual(0, _published.Count);
        }

        private ProjectileModel Fly()
        {
            var ball = new ProjectileModel();
            _registry.Load(ball, null);
            _registry.Launch(ball);
            return ball;
        }

        private void Retire(ProjectileModel ball)
        {
            _registry.Land(ball);
            _registry.Retire(ball);
        }

        private static ISubscriber<T> Capture<T>(Action<IMessageHandler<T>> capture)
        {
            var subscriber = Substitute.For<ISubscriber<T>>();
            subscriber
                .Subscribe(
                    Arg.Do(capture),
                    Arg.Any<MessageHandlerFilter<T>[]>())
                .Returns(Substitute.For<IDisposable>());
            return subscriber;
        }
    }
}
