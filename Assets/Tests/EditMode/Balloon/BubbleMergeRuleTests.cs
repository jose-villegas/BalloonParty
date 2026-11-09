using System.Collections.Generic;
using BalloonParty.Balloon.Controller;
using NUnit.Framework;
using UnityEngine;

namespace BalloonParty.Tests.Balloon
{
    [TestFixture]
    public class BubbleMergeRuleTests
    {
        private const int Max = 5;

        // Two neighbouring clusters one unit apart on x.
        private static readonly Vector2 A = Vector2.zero;
        private static readonly Vector2 B = Vector2.right;

        [Test]
        public void SittingSideBySide_NeverMerges()
        {
            Assert.IsFalse(BubbleMergeRule.Merges(A, Vector2.zero, B, Vector2.zero, 1, 1, Max));
        }

        [Test]
        public void ANudgeTowardTheNeighbour_Merges()
        {
            Assert.IsTrue(BubbleMergeRule.Merges(A, new Vector2(0.3f, 0f), B, Vector2.zero, 1, 1, Max));
        }

        [Test]
        public void ANudgeAwayFromTheNeighbour_DoesNotMerge()
        {
            Assert.IsFalse(BubbleMergeRule.Merges(A, new Vector2(-0.3f, 0f), B, Vector2.zero, 1, 1, Max));
        }

        [Test]
        public void ASidewaysNudge_DoesNotMerge()
        {
            // Square to the line between them: the gap only grows.
            Assert.IsFalse(BubbleMergeRule.Merges(A, new Vector2(0f, 0.3f), B, Vector2.zero, 1, 1, Max));
        }

        [Test]
        public void BothPushedTheSameWay_LeavesTheGap_AndDoesNotMerge()
        {
            var push = new Vector2(0.2f, 0.25f);

            Assert.AreEqual(0f, BubbleMergeRule.Approach(A, push, B, push), 1e-5f);
            Assert.IsFalse(BubbleMergeRule.Merges(A, push, B, push, 1, 1, Max));
        }

        [Test]
        public void BothPushedApart_DoesNotMerge()
        {
            // A hit between them pushes each away from it.
            Assert.IsFalse(BubbleMergeRule.Merges(
                A, new Vector2(-0.3f, 0.1f), B, new Vector2(0.3f, 0.1f), 1, 1, Max));
        }

        [Test]
        public void AShockwave_PushingTheNearerOneHarder_Merges()
        {
            // Origin to the left: both pushed right, the nearer harder, so it catches up.
            Assert.IsTrue(BubbleMergeRule.Merges(
                A, new Vector2(0.4f, 0f), B, new Vector2(0.15f, 0f), 2, 2, Max));
        }

        [Test]
        public void ATinyApproach_BelowTheThreshold_DoesNotMerge()
        {
            var push = new Vector2(BubbleMergeRule.MinApproach * 0.5f, 0f);

            Assert.IsFalse(BubbleMergeRule.Merges(A, push, B, Vector2.zero, 1, 1, Max));
        }

        [Test]
        public void BubblesThatFitInOneCluster_Merge([Values(2, 3, 4)] int a)
        {
            Assert.IsTrue(BubbleMergeRule.Merges(A, new Vector2(0.3f, 0f), B, Vector2.zero, a, Max - a, Max));
        }

        [Test]
        public void BubblesThatOverflowOneCluster_StayApart([Values(1, 3, 5)] int a)
        {
            Assert.IsFalse(BubbleMergeRule.Merges(A, new Vector2(0.3f, 0f), B, Vector2.zero, a, Max - a + 1, Max));
        }

        [Test]
        public void TheOnePushedTowardTheOther_IsAbsorbed()
        {
            var push = new Vector2(0.3f, 0f);
            var slotA = new Vector2Int(0, 0);
            var slotB = new Vector2Int(1, 0);

            Assert.IsTrue(BubbleMergeRule.IsAbsorbed(slotA, A, push, slotB, B, Vector2.zero));
            Assert.IsFalse(BubbleMergeRule.IsAbsorbed(slotB, B, Vector2.zero, slotA, A, push));
        }

        [Test]
        public void AnEvenPushFromBothSides_IsDecidedOnce()
        {
            var slotA = new Vector2Int(0, 0);
            var slotB = new Vector2Int(1, 0);
            var pushA = new Vector2(0.2f, 0f);
            var pushB = new Vector2(-0.2f, 0f);

            var aAbsorbed = BubbleMergeRule.IsAbsorbed(slotA, A, pushA, slotB, B, pushB);
            var bAbsorbed = BubbleMergeRule.IsAbsorbed(slotB, B, pushB, slotA, A, pushA);

            Assert.AreNotEqual(aAbsorbed, bAbsorbed);
        }

        [Test]
        public void PushedTowardTwoNeighbours_MergesWithTheOneItApproachedMost()
        {
            var slot = new Vector2Int(1, 1);
            var near = new Vector2Int(2, 1);
            var far = new Vector2Int(1, 2);
            var candidates = new List<BubbleMergeCandidate>
            {
                new(slot, far, 0.1f),
                new(slot, near, 0.25f),
            };
            var chosen = new List<BubbleMergeCandidate>();

            BubbleMergeRule.Choose(candidates, chosen, new HashSet<Vector2Int>());

            Assert.AreEqual(1, chosen.Count);
            Assert.AreEqual(near, chosen[0].Survivor);
        }

        [Test]
        public void OneNudge_MergesEachClusterOnce()
        {
            // a flows into b and b into c: b takes part once, so a chain waits for a later nudge.
            var a = new Vector2Int(0, 0);
            var b = new Vector2Int(1, 0);
            var c = new Vector2Int(2, 0);
            var candidates = new List<BubbleMergeCandidate>
            {
                new(a, b, 0.3f),
                new(b, c, 0.2f),
            };
            var chosen = new List<BubbleMergeCandidate>();

            BubbleMergeRule.Choose(candidates, chosen, new HashSet<Vector2Int>());

            Assert.AreEqual(1, chosen.Count);
            Assert.AreEqual((a, b), (chosen[0].Absorbed, chosen[0].Survivor));
        }

        [Test]
        public void SeparatePairs_AllMergeInTheSameNudge()
        {
            var candidates = new List<BubbleMergeCandidate>
            {
                new(new Vector2Int(0, 0), new Vector2Int(1, 0), 0.3f),
                new(new Vector2Int(5, 5), new Vector2Int(6, 5), 0.2f),
            };
            var chosen = new List<BubbleMergeCandidate>();

            BubbleMergeRule.Choose(candidates, chosen, new HashSet<Vector2Int>());

            Assert.AreEqual(2, chosen.Count);
        }
    }
}
