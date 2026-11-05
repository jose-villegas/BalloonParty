using System;
using BalloonParty.Game.Score;
using BalloonParty.Projectile;
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
        private ColorStreak _streak;
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
                Substitute.For<IPublisher<StreakChangedMessage>>(), subscriber, projectileLoadedSubscriber,
                new ProjectileRegistry());
            _streak = _tracker.For(null);
        }

        [Test]
        public void ProjectileLoaded_ResetsStreak()
        {
            _streak.Record("Red");
            _streak.Record("Red");

            _projectileLoadedHandler.Handle(new ProjectileLoadedMessage(null));

            Assert.AreEqual(1, _streak.Record("Red"));
        }

        [Test]
        public void Record_FirstPop_ReturnsOne()
        {
            Assert.AreEqual(1, _streak.Record("Red"));
        }

        [Test]
        public void Record_ConsecutiveSameColor_IncrementsStreak()
        {
            _streak.Record("Red");
            _streak.Record("Red");

            Assert.AreEqual(3, _streak.Record("Red"));
        }

        [Test]
        public void Record_DifferentColor_ResetsToOne()
        {
            _streak.Record("Red");
            _streak.Record("Red");

            Assert.AreEqual(1, _streak.Record("Blue"));
        }

        [Test]
        public void Record_BreaksStreak_ResetsAndReturnsOne()
        {
            _streak.Record("Red");
            _streak.Record("Red");

            Assert.AreEqual(1, _streak.Record("Red", breaksStreak: true));
        }

        [Test]
        public void Record_AfterBreak_NextSameColor_StartsAtOne()
        {
            _streak.Record("Red");
            _streak.Record("Red");
            _streak.Record("Red", breaksStreak: true);

            Assert.AreEqual(1, _streak.Record("Red"));
        }

        [Test]
        public void Record_CanIncrementFalse_MatchingColor_DoesNotGrowStreak()
        {
            _streak.Record("Red");
            _streak.Record("Red");

            Assert.AreEqual(2, _streak.Record("Red", canIncrement: false));
            Assert.AreEqual(2, _streak.CurrentStreak);
        }

        [Test]
        public void Record_CanIncrementFalse_DifferentColor_StillResetsToOne()
        {
            _streak.Record("Red");
            _streak.Record("Red");

            Assert.AreEqual(1, _streak.Record("Blue", canIncrement: false));
        }

        [Test]
        public void Record_CanIncrementFalse_ThenTrue_ResumesGrowingFromCurrentStreak()
        {
            _streak.Record("Red");
            _streak.Record("Red");
            _streak.Record("Red", canIncrement: false);

            Assert.AreEqual(3, _streak.Record("Red"));
        }

        [Test]
        public void Record_CanIncrementFalse_DifferentColor_CarryArmed_LeavesCarryIntactForDirectHit()
        {
            _streak.Record("Green");
            _streak.RecordWildcard(); // arms carry, CurrentStreak = 2, LastColor still "Green"

            // An item/AOE pop of a different colour, while carry is armed, must not consume it — only
            // the direct hit that actually changes the projectile's colour may cash it in.
            Assert.AreEqual(1, _streak.Record("Purple", canIncrement: false));
            Assert.AreEqual("Green", _streak.LastColor, "state stays untouched so the carry still lands");
            Assert.AreEqual(2, _streak.CurrentStreak);

            // The next direct hit still cashes in the carry correctly, as if the item pop never happened.
            Assert.AreEqual(3, _streak.Record("Purple"));
        }

        [Test]
        public void Record_CanIncrementFalse_DifferentColor_PreservesDeferredBankForDirectHit()
        {
            _streak.RecordDeferred();
            _streak.RecordDeferred();

            // An intervening item/AOE pop of some OTHER colour must not burn the bank — only a direct
            // hit establishing a real colour may fold it in.
            _streak.Record("Blue", canIncrement: false);

            Assert.AreEqual(3, _streak.Record("Red"), "the banked deferred pops still fold into the first direct hit");
        }

        [Test]
        public void GetStreak_MatchingColor_ReturnsCurrentStreak()
        {
            _streak.Record("Red");
            _streak.Record("Red");

            Assert.AreEqual(2, _tracker.GetStreak("Red"));
        }

        [Test]
        public void GetStreak_NonMatchingColor_ReturnsZero()
        {
            _streak.Record("Red");
            _streak.Record("Red");

            Assert.AreEqual(0, _tracker.GetStreak("Blue"));
        }

        [Test]
        public void RecordDeferred_ThenRecord_FoldsIntoStreak()
        {
            _streak.RecordDeferred();
            _streak.RecordDeferred();

            Assert.AreEqual(3, _streak.Record("Red"));
        }

        [Test]
        public void RecordDeferred_ThenSameColorContinues_KeepsClimbing()
        {
            _streak.RecordDeferred();

            _streak.Record("Red");

            Assert.AreEqual(3, _streak.Record("Red"));
        }

        [Test]
        public void RecordDeferred_ResetClearsDeferredPops()
        {
            _streak.RecordDeferred();
            _streak.RecordDeferred();

            _projectileLoadedHandler.Handle(new ProjectileLoadedMessage(null));

            Assert.AreEqual(1, _streak.Record("Red"));
        }

        [Test]
        public void RecordDeferred_ClearedOnColorChange()
        {
            _streak.RecordDeferred();
            _streak.Record("Red");

            Assert.AreEqual(1, _streak.Record("Blue"));
        }

        // ─── Rainbow Streak Carry ───────────────────────────────────────────

        [Test]
        public void Chain_Rainbow_Color1_Rainbow_Gives3x()
        {
            // rainbow → color1 → rainbow = 3× color1
            Assert.AreEqual(1, _streak.RecordWildcard());
            Assert.AreEqual(2, _streak.Record("Green"));
            Assert.AreEqual(3, _streak.RecordWildcard());
            Assert.AreEqual("Green", _streak.LastColor);
        }

        [Test]
        public void Chain_Rainbow_Rainbow_Color1_Gives3x()
        {
            // rainbow → rainbow → color1 = 3× color1
            Assert.AreEqual(1, _streak.RecordWildcard());
            Assert.AreEqual(2, _streak.RecordWildcard());
            Assert.AreEqual(3, _streak.Record("Green"));
            Assert.AreEqual("Green", _streak.LastColor);
        }

        [Test]
        public void Chain_Color1_Rainbow_Rainbow_Gives3x()
        {
            // color1 → rainbow → rainbow = 3× color1
            Assert.AreEqual(1, _streak.Record("Green"));
            Assert.AreEqual(2, _streak.RecordWildcard());
            Assert.AreEqual(3, _streak.RecordWildcard());
            Assert.AreEqual("Green", _streak.LastColor);
        }

        [Test]
        public void Chain_Color1_Rainbow_Color1_Gives3x()
        {
            // color1 → rainbow → color1 = 3× color1
            Assert.AreEqual(1, _streak.Record("Green"));
            Assert.AreEqual(2, _streak.RecordWildcard());
            Assert.AreEqual(3, _streak.Record("Green"));
            Assert.AreEqual("Green", _streak.LastColor);
        }

        [Test]
        public void Chain_Color1_Rainbow_Color2_Gives3xColor2()
        {
            // color1 → rainbow → color2 = 3× color2
            Assert.AreEqual(1, _streak.Record("Green"));
            Assert.AreEqual(2, _streak.RecordWildcard());
            Assert.AreEqual(3, _streak.Record("Purple"));
            Assert.AreEqual("Purple", _streak.LastColor);
        }

        [Test]
        public void Chain_Color1_Color2_Rainbow_Gives2x()
        {
            // color1 → color2 → rainbow = 2× (color2 streak reset, then rainbow climbs)
            Assert.AreEqual(1, _streak.Record("Green"));
            Assert.AreEqual(1, _streak.Record("Purple"));
            Assert.AreEqual(2, _streak.RecordWildcard());
            Assert.AreEqual("Purple", _streak.LastColor);
        }

        [Test]
        public void Wildcard_ThenSameColor_ThenDifferentColor_CarryStillArmed()
        {
            _streak.Record("Green");
            // Rainbow pop arms carry
            _streak.RecordWildcard();
            // Same color — carry not consumed yet
            _streak.Record("Green");

            // Color change — carry is consumed, streak carries
            Assert.AreEqual(4, _streak.Record("Purple"));
        }

        [Test]
        public void Wildcard_ThenReset_ClearsCarry()
        {
            _streak.Record("Green");
            _streak.Record("Green");
            _streak.RecordWildcard();

            _projectileLoadedHandler.Handle(new ProjectileLoadedMessage(null));

            // After reset, different color starts at 1 — carry was cleared
            Assert.AreEqual(1, _streak.Record("Purple"));
        }

        [Test]
        public void Carry_ConsumedOnColorChange_SubsequentChangeResetsNormally()
        {
            _streak.Record("Green");
            _streak.RecordWildcard();

            // First color change — carry consumed, streak = 3
            _streak.Record("Purple");

            // Second color change — no carry, resets to 1
            Assert.AreEqual(1, _streak.Record("Red"));
        }

        [Test]
        public void Deferred_ThenWildcards_ThenColor_FoldsCorrectly()
        {
            // Two deferred rainbow pops (colourless projectile), then projectile gets a colour,
            // then two more rainbow pops (wildcard), then a real colour hit.
            _streak.RecordDeferred();
            _streak.RecordDeferred();
            // Wildcard clears deferred bank and starts streak at 1
            _streak.RecordWildcard();
            _streak.RecordWildcard();

            // First real colour inherits wildcard streak (2+1 = 3)
            Assert.AreEqual(3, _streak.Record("Blue"));
        }
    }
}


