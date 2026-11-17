using System.Collections.Generic;
using BalloonParty.Balloon.Spawner;
using NUnit.Framework;
using UnityEngine;

namespace BalloonParty.Tests.Balloon
{
    // A clock line's toughs rise into the columns that land them nearest the toughs already on the board.
    [TestFixture]
    public class ToughLandingTests
    {
        // Every column lands on row 6 but column 5, which is full.
        private static int? LandingRow(int column)
        {
            return column == 5 ? null : 6;
        }

        [Test]
        public void ATough_RisesInTheColumnNearestTheToughsOnTheBoard()
        {
            var columns = new List<int> { 0, 1, 2, 3, 4, 5, 6 };
            var anchors = new List<Vector2Int> { new(6, 5) };

            Assert.AreEqual(1, ToughLanding.PutNearest(columns, 0, 1, LandingRow, anchors));

            Assert.AreEqual(6, columns[0], "beside the tough at (6,5)");
            Assert.AreEqual(new Vector2Int(6, 6), anchors[1]);
        }

        [Test]
        public void TheLinesToughs_RiseSideBySide_PastAFullColumn()
        {
            var columns = new List<int> { 0, 1, 2, 3, 4, 5, 6 };
            var anchors = new List<Vector2Int> { new(5, 5) };

            ToughLanding.PutNearest(columns, 0, 2, LandingRow, anchors);

            CollectionAssert.AreEqual(new[] { 6, 4 }, new[] { columns[0], columns[1] },
                "column 5 takes nothing: 6 lands beside the tough, then 4 nearest the two");
        }

        [Test]
        public void TheReward_KeepsTheFirstColumn()
        {
            var columns = new List<int> { 0, 1, 2, 3, 4, 5, 6 };
            var anchors = new List<Vector2Int> { new(6, 5) };

            ToughLanding.PutNearest(columns, 1, 1, LandingRow, anchors);

            Assert.AreEqual(0, columns[0]);
            Assert.AreEqual(6, columns[1]);
        }

        [Test]
        public void WithNoToughsOnTheBoard_TheLinesToughsStillRiseTogether()
        {
            var columns = new List<int> { 2, 0, 1, 3, 4, 5, 6 };

            ToughLanding.PutNearest(columns, 0, 3, LandingRow, new List<Vector2Int>());

            CollectionAssert.AreEquivalent(new[] { 0, 1, 2 }, new[] { columns[0], columns[1], columns[2] });
            Assert.AreEqual(2, columns[0], "the first keeps its place");
        }
    }
}
