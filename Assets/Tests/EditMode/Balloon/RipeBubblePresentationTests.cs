using BalloonParty.Balloon.Type;
using BalloonParty.Balloon.View;
using BalloonParty.Shared.Messages;
using NUnit.Framework;
using UnityEngine;

namespace BalloonParty.Tests.Balloon
{
    /// <summary>
    ///     The arithmetic behind how a ripe cluster looks and what its burst tells the views: the float rebase that
    ///     keeps bubbles where they are when they speed up, the hop path the foam shares with a merge, and the
    ///     burst message's neighbour bits.
    /// </summary>
    [TestFixture]
    public class RipeBubblePresentationTests
    {
        [TestCase(0f, 0.3f, 0.8f, 1.28f)]
        [TestCase(37.5f, 12f, 0.8f, 1.28f)]
        [TestCase(120f, -4f, 1.28f, 0.8f)]
        public void RebaseTimeOffset_KeepsTheFloatWhereItIs(float time, float offset, float from, float to)
        {
            var rebased = SoapBubbleShader.RebaseTimeOffset(time, offset, from, to);

            Assert.AreEqual(time * from + offset, time * to + rebased, 1e-3f);
        }

        [Test]
        public void RebaseTimeOffset_ThenRunsAtTheNewSpeed()
        {
            var rebased = SoapBubbleShader.RebaseTimeOffset(10f, 2f, 0.8f, 1.6f);

            var phaseNow = 10f * 1.6f + rebased;
            var phaseLater = 11f * 1.6f + rebased;
            Assert.AreEqual(1.6f, phaseLater - phaseNow, 1e-4f);
        }

        [Test]
        public void TheHop_LeavesFromAndLandsOn_ItsEnds()
        {
            var from = new Vector3(1f, 2f, 0f);
            var to = new Vector3(4f, 2f, 0f);

            AssertNear(from, SoapBubbleHop.PositionAt(from, to, 1f, 0.5f, 0f));
            AssertNear(to, SoapBubbleHop.PositionAt(from, to, 1f, 0.5f, 1f));
        }

        [Test]
        public void TheHop_BowsToTheSideItIsGiven_AtTheMiddle()
        {
            var from = Vector3.zero;
            var to = new Vector3(2f, 0f, 0f);

            AssertNear(new Vector3(1f, 0.5f, 0f), SoapBubbleHop.PositionAt(from, to, 1f, 0.5f, 0.5f));
            AssertNear(new Vector3(1f, -0.5f, 0f), SoapBubbleHop.PositionAt(from, to, -1f, 0.5f, 0.5f));
        }

        [Test]
        public void TheHop_SwellsMidwayAndLandsSmaller()
        {
            Assert.AreEqual(1f, SoapBubbleHop.ScaleAt(0f), 1e-5f);
            Assert.Greater(SoapBubbleHop.ScaleAt(0.3f), 1f);
            Assert.AreEqual(SoapBubbleHop.LandingScale, SoapBubbleHop.ScaleAt(1f), 1e-5f);
        }

        [Test]
        public void TheBurstMessage_CountsAndNamesTheNeighboursItTouched()
        {
            var message = new RipeBubbleBurstMessage(RipeBurstKind.Foam, Vector2Int.one, Vector3.zero, 0b100101);

            Assert.AreEqual(3, message.Reached);
            Assert.IsTrue(message.Touched(0));
            Assert.IsFalse(message.Touched(1));
            Assert.IsTrue(message.Touched(2));
            Assert.IsFalse(message.Touched(3));
            Assert.IsFalse(message.Touched(4));
            Assert.IsTrue(message.Touched(5));
        }

        [Test]
        public void ABurstThatTouchedNobody_ReachedNone()
        {
            var message = new RipeBubbleBurstMessage(RipeBurstKind.Ripple, Vector2Int.zero, Vector3.zero, 0);

            Assert.AreEqual(0, message.Reached);
            for (var direction = 0; direction < 6; direction++)
            {
                Assert.IsFalse(message.Touched(direction));
            }
        }

        [Test]
        public void TheHandout_CarriesEachBubblesTakerInOrder()
        {
            var handout = 0;
            handout = RipeBubbleBurstMessage.AddGift(handout, FoamGift.Coat, 5);
            handout = RipeBubbleBurstMessage.AddGift(handout, FoamGift.Cluster, 0);
            handout = RipeBubbleBurstMessage.AddGift(handout, FoamGift.Coat, 0);
            handout = RipeBubbleBurstMessage.AddGift(handout, FoamGift.Leftover, -1);
            var message = new RipeBubbleBurstMessage(RipeBurstKind.Foam, Vector2Int.one, Vector3.zero, 0b100001, handout);

            Assert.AreEqual(4, message.Bubbles);
            Assert.AreEqual(2, message.Reached, "the coats");
            Assert.AreEqual(FoamGift.Coat, message.GiftOf(0, out var d0));
            Assert.AreEqual(5, d0);
            Assert.AreEqual(FoamGift.Cluster, message.GiftOf(1, out var d1));
            Assert.AreEqual(0, d1);
            Assert.AreEqual(FoamGift.Coat, message.GiftOf(2, out var d2));
            Assert.AreEqual(0, d2);
            Assert.AreEqual(FoamGift.Leftover, message.GiftOf(3, out var d3));
            Assert.AreEqual(-1, d3);
        }

        [Test]
        public void TheHandout_HoldsSixAtMost()
        {
            var handout = 0;
            for (var i = 0; i < 8; i++)
            {
                handout = RipeBubbleBurstMessage.AddGift(handout, FoamGift.Cluster, 5);
            }

            Assert.AreEqual(RipeBubbleBurstMessage.MaxHandout, RipeBubbleBurstMessage.HandoutCount(handout));
            for (var bubble = 0; bubble < RipeBubbleBurstMessage.MaxHandout; bubble++)
            {
                Assert.AreEqual(FoamGift.Cluster, RipeBubbleBurstMessage.HandoutGift(handout, bubble, out var d));
                Assert.AreEqual(5, d);
            }
        }

        [Test]
        public void ARipple_HandsNothingOut()
        {
            Assert.AreEqual(0, new RipeBubbleBurstMessage(RipeBurstKind.Ripple, Vector2Int.zero, Vector3.zero, 0b11).Bubbles);
        }

        private static void AssertNear(Vector3 expected, Vector3 actual)
        {
            Assert.AreEqual(expected.x, actual.x, 1e-4f);
            Assert.AreEqual(expected.y, actual.y, 1e-4f);
            Assert.AreEqual(expected.z, actual.z, 1e-4f);
        }
    }
}
