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

        private static readonly Vector2Int A = new(2, 3);
        private static readonly Vector2Int B = new(3, 3);
        private static readonly Vector2Int C = new(4, 3);

        [Test]
        public void TwoSingleBubbles_Merge()
        {
            Assert.IsTrue(BubbleMergeRule.Merges(1, 1, Max));
        }

        [Test]
        public void ASumThatFillsTheCluster_Merges()
        {
            Assert.IsTrue(BubbleMergeRule.Merges(2, 3, Max));
        }

        [Test]
        public void ASumPastTheCap_DoesNotMerge()
        {
            Assert.IsFalse(BubbleMergeRule.Merges(3, 3, Max));
        }

        [Test]
        public void AFullCluster_NeverMerges()
        {
            Assert.IsFalse(BubbleMergeRule.Merges(Max, 1, Max));
        }

        [Test]
        public void AnEmptyCluster_NeverMerges()
        {
            // A popped cluster can still be on the grid the frame it pops.
            Assert.IsFalse(BubbleMergeRule.Merges(0, 1, Max));
            Assert.IsFalse(BubbleMergeRule.Merges(1, 0, Max));
        }

        [Test]
        public void TheSmallerCluster_IsAbsorbed()
        {
            Assert.IsTrue(BubbleMergeRule.IsAbsorbed(B, 1, A, 3));
            Assert.IsFalse(BubbleMergeRule.IsAbsorbed(A, 3, B, 1));
        }

        [Test]
        public void BetweenEquals_ExactlyOneSideIsAbsorbed()
        {
            Assert.AreNotEqual(BubbleMergeRule.IsAbsorbed(A, 2, B, 2), BubbleMergeRule.IsAbsorbed(B, 2, A, 2));

            var above = new Vector2Int(2, 4);
            Assert.AreNotEqual(BubbleMergeRule.IsAbsorbed(A, 2, above, 2), BubbleMergeRule.IsAbsorbed(above, 2, A, 2));
        }

        [Test]
        public void Choose_TakesEveryDisjointPair()
        {
            var far = new Vector2Int(8, 1);
            var farNeighbour = new Vector2Int(8, 2);
            var chosen = ChooseFrom(
                new BubbleMergeCandidate(B, A, 2),
                new BubbleMergeCandidate(farNeighbour, far, 2));

            Assert.AreEqual(2, chosen.Count);
        }

        [Test]
        public void Choose_MergesEachClusterOnce()
        {
            // B sits between A and C: it joins one of them this pass, never both.
            var chosen = ChooseFrom(
                new BubbleMergeCandidate(B, A, 2),
                new BubbleMergeCandidate(B, C, 2));

            Assert.AreEqual(1, chosen.Count);
        }

        [Test]
        public void Choose_PrefersTheMergeThatMakesTheBiggerCluster()
        {
            var chosen = ChooseFrom(
                new BubbleMergeCandidate(B, A, 2),
                new BubbleMergeCandidate(B, C, 4));

            Assert.AreEqual(1, chosen.Count);
            Assert.AreEqual(C, chosen[0].Survivor);
        }

        [Test]
        public void Choose_DoesNotDependOnTheCandidateOrder()
        {
            var first = ChooseFrom(new BubbleMergeCandidate(B, A, 2), new BubbleMergeCandidate(B, C, 2));
            var second = ChooseFrom(new BubbleMergeCandidate(B, C, 2), new BubbleMergeCandidate(B, A, 2));

            Assert.AreEqual(first[0].Survivor, second[0].Survivor);
        }

        [Test]
        public void Choose_ASurvivorIsNotAbsorbedInTheSamePass()
        {
            // B absorbs A while C absorbs B: one of the two waits for the next pass.
            var chosen = ChooseFrom(
                new BubbleMergeCandidate(A, B, 2),
                new BubbleMergeCandidate(B, C, 3));

            Assert.AreEqual(1, chosen.Count);
        }

        private static List<BubbleMergeCandidate> ChooseFrom(params BubbleMergeCandidate[] candidates)
        {
            var chosen = new List<BubbleMergeCandidate>();
            BubbleMergeRule.Choose(new List<BubbleMergeCandidate>(candidates), chosen, new HashSet<Vector2Int>());
            return chosen;
        }
    }
}
