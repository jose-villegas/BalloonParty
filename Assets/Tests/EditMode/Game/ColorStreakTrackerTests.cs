using System;
using BalloonParty.Game.Score;
using BalloonParty.Shared.Messages;
using MessagePipe;
using NSubstitute;
using NUnit.Framework;

namespace BalloonParty.Tests.Game
{
    [TestFixture]
    public class ColorStreakTrackerTests
    {
        private ColorStreakTracker _tracker;
        private IMessageHandler<ProjectileLoadedMessage> _projectileLoadedHandler;

        [SetUp]
        public void SetUp()
        {
            var subscriber = Substitute.For<ISubscriber<ScoreLevelUpMessage>>();
            subscriber
                .Subscribe(
                    Arg.Any<IMessageHandler<ScoreLevelUpMessage>>(),
                    Arg.Any<MessageHandlerFilter<ScoreLevelUpMessage>[]>())
                .Returns(Substitute.For<IDisposable>());

            var projectileLoadedSubscriber = Substitute.For<ISubscriber<ProjectileLoadedMessage>>();
            projectileLoadedSubscriber
                .Subscribe(
                    Arg.Do<IMessageHandler<ProjectileLoadedMessage>>(h => _projectileLoadedHandler = h),
                    Arg.Any<MessageHandlerFilter<ProjectileLoadedMessage>[]>())
                .Returns(Substitute.For<IDisposable>());

            _tracker = new ColorStreakTracker(
                Substitute.For<IPublisher<StreakChangedMessage>>(), subscriber, projectileLoadedSubscriber);
        }

        [Test]
        public void ProjectileLoaded_ResetsStreak()
        {
            _tracker.Record("Red");
            _tracker.Record("Red");

            _projectileLoadedHandler.Handle(new ProjectileLoadedMessage(null));

            Assert.AreEqual(1, _tracker.Record("Red"));
        }

        [Test]
        public void Record_FirstPop_ReturnsOne()
        {
            Assert.AreEqual(1, _tracker.Record("Red"));
        }

        [Test]
        public void Record_ConsecutiveSameColor_IncrementsStreak()
        {
            _tracker.Record("Red");
            _tracker.Record("Red");

            Assert.AreEqual(3, _tracker.Record("Red"));
        }

        [Test]
        public void Record_DifferentColor_ResetsToOne()
        {
            _tracker.Record("Red");
            _tracker.Record("Red");

            Assert.AreEqual(1, _tracker.Record("Blue"));
        }

        [Test]
        public void Record_BreaksStreak_ResetsAndReturnsOne()
        {
            _tracker.Record("Red");
            _tracker.Record("Red");

            Assert.AreEqual(1, _tracker.Record("Red", breaksStreak: true));
        }

        [Test]
        public void Record_AfterBreak_NextSameColor_StartsAtOne()
        {
            _tracker.Record("Red");
            _tracker.Record("Red");
            _tracker.Record("Red", breaksStreak: true);

            Assert.AreEqual(1, _tracker.Record("Red"));
        }

        [Test]
        public void GetStreak_MatchingColor_ReturnsCurrentStreak()
        {
            _tracker.Record("Red");
            _tracker.Record("Red");

            Assert.AreEqual(2, _tracker.GetStreak("Red"));
        }

        [Test]
        public void GetStreak_NonMatchingColor_ReturnsZero()
        {
            _tracker.Record("Red");
            _tracker.Record("Red");

            Assert.AreEqual(0, _tracker.GetStreak("Blue"));
        }

        [Test]
        public void RecordDeferred_ThenRecord_FoldsIntoStreak()
        {
            _tracker.RecordDeferred();
            _tracker.RecordDeferred();

            Assert.AreEqual(3, _tracker.Record("Red"));
        }

        [Test]
        public void RecordDeferred_ThenSameColorContinues_KeepsClimbing()
        {
            _tracker.RecordDeferred();

            _tracker.Record("Red");

            Assert.AreEqual(3, _tracker.Record("Red"));
        }

        [Test]
        public void RecordDeferred_ResetClearsDeferredPops()
        {
            _tracker.RecordDeferred();
            _tracker.RecordDeferred();

            _projectileLoadedHandler.Handle(new ProjectileLoadedMessage(null));

            Assert.AreEqual(1, _tracker.Record("Red"));
        }

        [Test]
        public void RecordDeferred_ClearedOnColorChange()
        {
            _tracker.RecordDeferred();
            _tracker.Record("Red");

            Assert.AreEqual(1, _tracker.Record("Blue"));
        }
    }
}

