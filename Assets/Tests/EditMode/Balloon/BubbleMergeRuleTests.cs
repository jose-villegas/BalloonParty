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
        public void AGiverThatFits_GivesAllItsBubbles()
        {
            Assert.AreEqual(1, BubbleMergeRule.Given(1, 1, Max));
            Assert.AreEqual(2, BubbleMergeRule.Given(2, 3, Max));
        }

        [Test]
        public void AGiverThatDoesNotFit_GivesOnlyWhatFillsTheReceiver()
        {
            // 3 beside 3: two fill the receiver to 5, one stays behind.
            Assert.AreEqual(2, BubbleMergeRule.Given(3, 3, Max));
            Assert.AreEqual(1, BubbleMergeRule.Given(2, 4, Max));
        }

        [Test]
        public void AFullReceiver_TakesNothing()
        {
            Assert.AreEqual(0, BubbleMergeRule.Given(1, Max, Max));
        }

        [Test]
        public void APoppedCluster_NeitherGivesNorTakes()
        {
            // A popped cluster can still be on the grid the frame it pops.
            Assert.AreEqual(0, BubbleMergeRule.Given(0, 1, Max));
            Assert.AreEqual(0, BubbleMergeRule.Given(1, 0, Max));
        }

        [Test]
        public void TheSmallerCluster_Gives()
        {
            Assert.IsTrue(BubbleMergeRule.IsGiver(B, 1, A, 3));
            Assert.IsFalse(BubbleMergeRule.IsGiver(A, 3, B, 1));
        }

        [Test]
        public void BetweenEquals_ExactlyOneSideGives()
        {
            Assert.AreNotEqual(BubbleMergeRule.IsGiver(A, 2, B, 2), BubbleMergeRule.IsGiver(B, 2, A, 2));

            var above = new Vector2Int(2, 4);
            Assert.AreNotEqual(BubbleMergeRule.IsGiver(A, 2, above, 2), BubbleMergeRule.IsGiver(above, 2, A, 2));
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
            // B sits between A and C: it gives to one of them this pass, never both.
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
            Assert.AreEqual(C, chosen[0].Receiver);
        }

        [Test]
        public void Choose_DoesNotDependOnTheCandidateOrder()
        {
            var first = ChooseFrom(new BubbleMergeCandidate(B, A, 2), new BubbleMergeCandidate(B, C, 2));
            var second = ChooseFrom(new BubbleMergeCandidate(B, C, 2), new BubbleMergeCandidate(B, A, 2));

            Assert.AreEqual(first[0].Receiver, second[0].Receiver);
        }

        [Test]
        public void Choose_AReceiverDoesNotGiveInTheSamePass()
        {
            // B takes from A while C takes from B: one of the two waits for the next pass.
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
