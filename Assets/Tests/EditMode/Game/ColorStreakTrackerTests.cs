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

        // ─── Rainbow Streak Carry ───────────────────────────────────────────

        [Test]
        public void RecordAndCarry_SameColor_IncrementsStreak()
        {
            _tracker.Record("Green");
            _tracker.RecordAndCarry("Green");

            Assert.AreEqual(3, _tracker.Record("Green"));
        }

        [Test]
        public void RecordAndCarry_ThenRecordDifferentColor_CarriesStreak()
        {
            _tracker.Record("Green");
            _tracker.Record("Green");
            // Green x2 → rainbow pop arms carry at x3
            _tracker.RecordAndCarry("Green");

            // Switch to Purple: streak carries (3+1 = 4), not reset to 1
            Assert.AreEqual(4, _tracker.Record("Purple"));
        }

        [Test]
        public void RecordAndCarry_ThenSameColor_ThenDifferentColor_CarryStillArmed()
        {
            _tracker.RecordAndCarry("Green");
            // Same color — carry not consumed yet
            _tracker.Record("Green");

            // Color change — carry is consumed, streak carries
            Assert.AreEqual(3, _tracker.Record("Purple"));
        }

        [Test]
        public void RecordAndCarry_ThenReset_ClearsCarry()
        {
            _tracker.Record("Green");
            _tracker.Record("Green");
            _tracker.RecordAndCarry("Green");

            _projectileLoadedHandler.Handle(new ProjectileLoadedMessage(null));

            // After reset, different color starts at 1 — carry was cleared
            Assert.AreEqual(1, _tracker.Record("Purple"));
        }

        [Test]
        public void RecordWildcard_ArmsCarry_NextDifferentColorInheritsStreak()
        {
            _tracker.Record("Green");
            _tracker.Record("Green");
            // Wildcard: streak climbs to 3, carry armed
            _tracker.RecordWildcard();

            // Different color inherits the streak (3+1 = 4)
            Assert.AreEqual(4, _tracker.Record("Purple"));
        }

        [Test]
        public void RecordAndCarry_MultipleCalls_CarryStaysArmed()
        {
            _tracker.RecordAndCarry("Green");
            _tracker.RecordAndCarry("Green");
            _tracker.RecordAndCarry("Green");

            // Three carries stacked — carry still armed, streak = 3
            // Color change carries: 3+1 = 4
            Assert.AreEqual(4, _tracker.Record("Purple"));
        }

        [Test]
        public void Carry_ConsumedOnColorChange_SubsequentChangeResetsNormally()
        {
            _tracker.Record("Green");
            _tracker.RecordAndCarry("Green");

            // First color change — carry consumed, streak = 3
            _tracker.Record("Purple");

            // Second color change — no carry, resets to 1
            Assert.AreEqual(1, _tracker.Record("Red"));
        }

        [Test]
        public void RecordAndCarry_WithDeferredPops_FoldsInCorrectly()
        {
            _tracker.RecordDeferred();
            _tracker.RecordDeferred();
            // RecordAndCarry as the first color attribution — folds 2 deferred pops
            var streak = _tracker.RecordAndCarry("Green");

            // 1 + 2 deferred = 3, carry armed
            Assert.AreEqual(3, streak);
            // Color change carries: 3+1 = 4
            Assert.AreEqual(4, _tracker.Record("Purple"));
        }
    }
}


