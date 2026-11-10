using BalloonParty.Configuration.HeadsUp;
using BalloonParty.Game.HeadsUp;
using NUnit.Framework;
using UnityEngine;
using static BalloonParty.Tests.HeadsUp.HeadsUpTestSettings;

namespace BalloonParty.Tests.HeadsUp
{
    /// <summary>
    ///     Which heads-ups <see cref="HeadsUpQueue" /> lets on screen: the argument range, merge rewrites, cooldowns,
    ///     the cap with its eviction order, the held lane, and handles that go stale.
    /// </summary>
    [TestFixture]
    public class HeadsUpQueueTests
    {
        private const HeadsUpStyleId Low = HeadsUpStyleId.BounceWall;
        private const HeadsUpStyleId Mid = HeadsUpStyleId.BouncePickup;
        private const HeadsUpStyleId High = HeadsUpStyleId.HeartLost;
        private const HeadsUpStyleId Ranged = HeadsUpStyleId.Streak;
        private const HeadsUpStyleId Cooled = HeadsUpStyleId.SpeedTap;
        private const HeadsUpStyleId Held = HeadsUpStyleId.Ready;

        private static readonly object Ball = new();

        private HeadsUpQueue NewQueue(int cap)
        {
            return new HeadsUpQueue(With(cap,
                Style(Low, 1),
                Style(Mid, 2),
                Style(High, 5),
                Style(Ranged, 3).WithArgRange(3, 9),
                Style(Cooled, 1).WithCooldown(1f),
                Style(Held, 0).HeldUntilReleased()));
        }

        [Test]
        public void AFreeSlot_Shows()
        {
            var queue = NewQueue(2);

            var offer = queue.Offer(Request(Low), 0f);

            Assert.AreEqual(HeadsUpOfferKind.Show, offer.Kind);
            Assert.IsFalse(offer.Evicted.IsValid);
            Assert.IsTrue(queue.IsShowing(offer.Handle));
        }

        [TestCase(2)]
        [TestCase(10)]
        public void AnArgumentOutsideTheStylesRange_IsDropped(int arg)
        {
            var queue = NewQueue(2);

            Assert.AreEqual(HeadsUpOfferKind.Dropped, queue.Offer(Request(Ranged, arg), 0f).Kind);
        }

        [TestCase(3)]
        [TestCase(9)]
        public void AnArgumentAtTheRangesEnds_Shows(int arg)
        {
            var queue = NewQueue(2);

            Assert.AreEqual(HeadsUpOfferKind.Show, queue.Offer(Request(Ranged, arg), 0f).Kind);
        }

        [Test]
        public void AFullScreen_EvictsTheOldestOfEqualPriority()
        {
            var queue = NewQueue(2);
            var oldest = queue.Offer(Request(Low), 0f).Handle;
            queue.Offer(Request(Low), 0f);

            var offer = queue.Offer(Request(Low), 0.1f);

            Assert.AreEqual(HeadsUpOfferKind.Show, offer.Kind);
            Assert.AreEqual(oldest.Slot, offer.Evicted.Slot);
            Assert.IsFalse(queue.IsShowing(oldest));
            Assert.AreEqual(2, queue.ShowingCount);
        }

        // A burst of equals in one frame would otherwise show and evict each other before a stroke is drawn.
        [Test]
        public void AFullScreen_OfEqualsShownThisMoment_DropsTheRequest()
        {
            var queue = NewQueue(2);
            queue.Offer(Request(Low), 0.5f);
            queue.Offer(Request(Low), 0.5f);

            Assert.AreEqual(HeadsUpOfferKind.Dropped, queue.Offer(Request(Low), 0.5f).Kind);
        }

        [Test]
        public void AFullScreen_OfLowerPriorityShownThisMoment_StillEvicts()
        {
            var queue = NewQueue(1);
            queue.Offer(Request(Low), 0.5f);

            Assert.AreEqual(HeadsUpOfferKind.Show, queue.Offer(Request(High), 0.5f).Kind);
        }

        [Test]
        public void ARewrite_MakesItsLineTheNewest()
        {
            var queue = NewQueue(2);
            var streak = queue.Offer(Request(Ranged, 3, Key(Ball, "Red")), 0f).Handle;
            var other = queue.Offer(Request(Ranged, 3, Key(Ball, "Blue")), 0.1f).Handle;
            queue.Offer(Request(Ranged, 4, Key(Ball, "Red")), 0.2f);

            var offer = queue.Offer(Request(Ranged, 3), 0.3f);

            Assert.AreEqual(other.Slot, offer.Evicted.Slot);
            Assert.IsTrue(queue.IsShowing(streak));
        }

        [Test]
        public void AFullScreen_EvictsTheLowestPriority_EvenIfItIsNewer()
        {
            var queue = NewQueue(2);
            var mid = queue.Offer(Request(Mid), 0f).Handle;
            var low = queue.Offer(Request(Low), 0f).Handle;

            var offer = queue.Offer(Request(High), 0f);

            Assert.AreEqual(low.Slot, offer.Evicted.Slot);
            Assert.IsTrue(queue.IsShowing(mid));
        }

        [Test]
        public void AFullScreenOfHigherPriority_DropsTheRequest()
        {
            var queue = NewQueue(2);
            queue.Offer(Request(Mid), 0f);
            queue.Offer(Request(Mid), 0f);

            Assert.AreEqual(HeadsUpOfferKind.Dropped, queue.Offer(Request(Low), 0f).Kind);
            Assert.AreEqual(2, queue.ShowingCount);
        }

        [Test]
        public void TheSameStyleAndMergeKey_RewritesTheShowingLine()
        {
            var queue = NewQueue(2);
            var first = queue.Offer(Request(Ranged, 3, Key(Ball, "Red")), 0f).Handle;

            var offer = queue.Offer(Request(Ranged, 4, Key(Ball, "Red")), 0f);

            Assert.AreEqual(HeadsUpOfferKind.Rewrite, offer.Kind);
            Assert.AreEqual(first.Slot, offer.Handle.Slot);
            Assert.AreEqual(first.Generation, offer.Handle.Generation);
            Assert.AreEqual(1, queue.ShowingCount);
        }

        [Test]
        public void AnotherChannelOrOwner_GetsALineOfItsOwn()
        {
            var queue = NewQueue(3);
            queue.Offer(Request(Ranged, 3, Key(Ball, "Red")), 0f);

            Assert.AreEqual(HeadsUpOfferKind.Show, queue.Offer(Request(Ranged, 3, Key(Ball, "Blue")), 0f).Kind);
            Assert.AreEqual(HeadsUpOfferKind.Show, queue.Offer(Request(Ranged, 3, Key(new object(), "Red")), 0f).Kind);
        }

        // A late pop from a ball already retired carries no ball: those still merge per colour.
        [Test]
        public void ANullOwner_IsAKeyLikeAnyOther()
        {
            var queue = NewQueue(2);
            queue.Offer(Request(Ranged, 3, Key(null, "Red")), 0f);

            Assert.AreEqual(HeadsUpOfferKind.Rewrite, queue.Offer(Request(Ranged, 4, Key(null, "Red")), 0f).Kind);
        }

        [Test]
        public void NoMergeKey_NeverMerges()
        {
            var queue = NewQueue(2);
            queue.Offer(Request(Low), 0f);

            Assert.AreEqual(HeadsUpOfferKind.Show, queue.Offer(Request(Low), 0f).Kind);
        }

        [Test]
        public void ACooldown_DropsTheNextUntilItHasPassed()
        {
            var queue = NewQueue(3);
            queue.Offer(Request(Cooled), 0f);

            Assert.AreEqual(HeadsUpOfferKind.Dropped, queue.Offer(Request(Cooled), 0.5f).Kind);
            Assert.AreEqual(HeadsUpOfferKind.Show, queue.Offer(Request(Cooled), 1f).Kind);
        }

        [Test]
        public void AMergeRewrite_IgnoresTheCooldown()
        {
            var queue = new HeadsUpQueue(With(2, Style(Ranged, 3).WithArgRange(3, 9).WithCooldown(5f)));
            queue.Offer(Request(Ranged, 3, Key(Ball, "Red")), 0f);

            Assert.AreEqual(HeadsUpOfferKind.Rewrite, queue.Offer(Request(Ranged, 4, Key(Ball, "Red")), 0.1f).Kind);
        }

        [Test]
        public void AHeldStyle_ShowsOutsideAFullScreen_WithoutEvicting()
        {
            var queue = NewQueue(1);
            var low = queue.Offer(Request(Low), 0f).Handle;

            var offer = queue.Offer(Request(Held), 0f);

            Assert.AreEqual(HeadsUpOfferKind.Show, offer.Kind);
            Assert.IsFalse(offer.Evicted.IsValid);
            Assert.IsTrue(queue.IsShowing(low));
        }

        [Test]
        public void AHeldStyleShowingAlready_IsRewritten()
        {
            var queue = NewQueue(1);
            var first = queue.Offer(Request(Held), 0f).Handle;

            var offer = queue.Offer(Request(Held), 0f);

            Assert.AreEqual(HeadsUpOfferKind.Rewrite, offer.Kind);
            Assert.AreEqual(first.Slot, offer.Handle.Slot);
        }

        [Test]
        public void AHeldLine_NeverTakesARegularSlot()
        {
            var queue = NewQueue(1);
            queue.Offer(Request(Held), 0f);

            Assert.AreEqual(HeadsUpOfferKind.Show, queue.Offer(Request(Low), 0f).Kind);
        }

        [Test]
        public void Free_AFinishedLine_OpensItsSlot()
        {
            var queue = NewQueue(1);
            var shown = queue.Offer(Request(Mid), 0f).Handle;

            Assert.IsTrue(queue.Free(shown));

            var offer = queue.Offer(Request(Low), 0f);
            Assert.AreEqual(HeadsUpOfferKind.Show, offer.Kind);
            Assert.IsFalse(offer.Evicted.IsValid);
        }

        // A line evicted and still fading reports finished after its slot went to another.
        [Test]
        public void Free_AStaleHandle_LeavesTheSlotsNewLine()
        {
            var queue = NewQueue(1);
            var evicted = queue.Offer(Request(Low), 0f).Handle;
            var replacement = queue.Offer(Request(Low), 0.1f).Handle;

            Assert.IsFalse(queue.Free(evicted));
            Assert.IsTrue(queue.IsShowing(replacement));
        }

        [Test]
        public void ClearAll_EmptiesTheScreen_AndMakesEveryHandleStale()
        {
            var queue = NewQueue(2);
            var shown = queue.Offer(Request(Low), 0f).Handle;
            var held = queue.Offer(Request(Held), 0f).Handle;

            queue.ClearAll();
            var next = queue.Offer(Request(Low), 0f).Handle;

            Assert.IsFalse(queue.IsShowing(shown));
            Assert.IsFalse(queue.IsShowing(held));
            Assert.IsFalse(queue.Free(shown));
            Assert.IsTrue(queue.IsShowing(next));
        }

        [Test]
        public void AStyleMissingFromTheSettings_IsDropped()
        {
            var queue = new HeadsUpQueue(With(2, Style(Low)));

            Assert.AreEqual(HeadsUpOfferKind.Dropped, queue.Offer(Request(High), 0f).Kind);
        }

        // The oldest away from slot 0, where the victim search starts, so a lost age check would show.
        [Test]
        public void AFullScreen_EvictsTheOldest_WhereverItSits()
        {
            var queue = NewQueue(2);
            var first = queue.Offer(Request(Low), 0f).Handle;
            var oldest = queue.Offer(Request(Low), 0f).Handle;
            queue.Free(first);
            queue.Offer(Request(Low), 0f);

            var offer = queue.Offer(Request(Low), 0.1f);

            Assert.AreEqual(oldest.Slot, offer.Evicted.Slot);
        }

        [Test]
        public void AFullScreen_NeverEvictsTheHeldLine()
        {
            var queue = NewQueue(1);
            var held = queue.Offer(Request(Held), 0f).Handle;
            var low = queue.Offer(Request(Low), 0f).Handle;

            var offer = queue.Offer(Request(High), 0f);

            Assert.AreEqual(low.Slot, offer.Evicted.Slot);
            Assert.IsTrue(queue.IsShowing(held));
        }

        [Test]
        public void AFullHeldLane_DropsAnotherHeldStyle_WithoutEvicting()
        {
            var queue = new HeadsUpQueue(With(1,
                Style(Low),
                Style(Held, 0).HeldUntilReleased(),
                Style(Mid, 0).HeldUntilReleased(),
                Style(High, 0).HeldUntilReleased()));
            var low = queue.Offer(Request(Low), 0f).Handle;
            queue.Offer(Request(Held), 0f);
            queue.Offer(Request(Mid), 0f);

            Assert.AreEqual(HeadsUpOfferKind.Dropped, queue.Offer(Request(High), 0f).Kind);
            Assert.IsTrue(queue.IsShowing(low));
        }

        [Test]
        public void AnArgumentOutOfRange_IsDropped_EvenWhenItsKeyIsShowing()
        {
            var queue = NewQueue(2);
            queue.Offer(Request(Ranged, 3, Key(Ball, "Red")), 0f);

            Assert.AreEqual(HeadsUpOfferKind.Dropped, queue.Offer(Request(Ranged, 2, Key(Ball, "Red")), 0f).Kind);
            Assert.AreEqual(1, queue.ShowingCount);
        }

        [Test]
        public void AFreedLine_NoLongerMerges()
        {
            var queue = NewQueue(2);
            var shown = queue.Offer(Request(Ranged, 3, Key(Ball, "Red")), 0f).Handle;
            queue.Free(shown);

            Assert.AreEqual(HeadsUpOfferKind.Show, queue.Offer(Request(Ranged, 4, Key(Ball, "Red")), 0f).Kind);
        }

        [Test]
        public void TheSameKeyUnderAnotherStyle_DoesNotMerge()
        {
            var queue = NewQueue(2);
            queue.Offer(Request(Ranged, 3, Key(Ball, "Red")), 0f);

            Assert.AreEqual(HeadsUpOfferKind.Show, queue.Offer(Request(Low, 1, Key(Ball, "Red")), 0f).Kind);
        }

        [Test]
        public void ADroppedOffer_DoesNotStartTheCooldown()
        {
            var queue = NewQueue(1);
            var mid = queue.Offer(Request(Mid), 0f).Handle;
            queue.Offer(Request(Cooled), 0f);
            queue.Free(mid);

            Assert.AreEqual(HeadsUpOfferKind.Show, queue.Offer(Request(Cooled), 0f).Kind);
        }

        [Test]
        public void AMergeRewrite_DoesNotRestartTheCooldown()
        {
            var queue = new HeadsUpQueue(With(3, Style(Ranged, 3).WithArgRange(3, 9).WithCooldown(1f)));
            queue.Offer(Request(Ranged, 3, Key(Ball, "Red")), 0f);
            queue.Offer(Request(Ranged, 4, Key(Ball, "Red")), 0.9f);

            Assert.AreEqual(HeadsUpOfferKind.Show, queue.Offer(Request(Ranged, 3, Key(Ball, "Blue")), 1f).Kind);
        }

        private static HeadsUpRequest Request(HeadsUpStyleId style, int arg = 1, HeadsUpMergeKey key = default)
        {
            return new HeadsUpRequest(style, arg, HeadsUpAnchor.At(Vector2.zero), key);
        }

        private static HeadsUpMergeKey Key(object owner, string channel)
        {
            return new HeadsUpMergeKey(owner, channel);
        }
    }
}
