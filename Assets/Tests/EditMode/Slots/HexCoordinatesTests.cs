using System.Collections.Generic;
using BalloonParty.Slots.Grid;
using NUnit.Framework;
using UnityEngine;

namespace BalloonParty.Tests.Slots
{
    [TestFixture]
    public class HexCoordinatesTests
    {
        private const int Size = 13;

        [TestCase(4, 4)]
        [TestCase(4, 5)]
        public void Step_EveryDirection_IsADifferentNeighbour(int col, int row)
        {
            var origin = new Vector2Int(col, row);
            var neighbours = new HashSet<Vector2Int>(HexCoordinates.HexNeighborIndices(col, row));
            var reached = new HashSet<Vector2Int>();

            for (var direction = 0; direction < HexCoordinates.Directions; direction++)
            {
                var step = HexCoordinates.Step(origin, direction);
                Assert.IsTrue(neighbours.Contains(step), $"{origin} step {direction} -> {step}");
                reached.Add(step);
            }

            Assert.AreEqual(6, reached.Count);
        }

        [TestCase(4, 4)]
        [TestCase(4, 5)]
        public void Step_ThenTheOppositeWay_ComesHome(int col, int row)
        {
            var origin = new Vector2Int(col, row);

            for (var direction = 0; direction < HexCoordinates.Directions; direction++)
            {
                var there = HexCoordinates.Step(origin, direction);
                Assert.AreEqual(origin, HexCoordinates.Step(there, HexCoordinates.Turn(direction, 3)));
                Assert.AreEqual(direction, HexCoordinates.DirectionBetween(origin, there));
            }
        }

        [Test]
        public void Step_RunsClockwiseFromEast()
        {
            var origin = new Vector2Int(4, 4);

            Assert.AreEqual(new Vector2Int(5, 4), HexCoordinates.Step(origin, 0), "east");
            Assert.AreEqual(5, HexCoordinates.Step(origin, 1).y, "south-east is a row down");
            Assert.Greater(HexCoordinates.Step(origin, 1).x, HexCoordinates.Step(origin, 2).x, "south-east of south-west");
            Assert.AreEqual(new Vector2Int(3, 4), HexCoordinates.Step(origin, 3), "west");
            Assert.AreEqual(3, HexCoordinates.Step(origin, 4).y, "north-west is a row up");
            Assert.Greater(HexCoordinates.Step(origin, 5).x, HexCoordinates.Step(origin, 4).x, "north-east of north-west");
        }

        [Test]
        public void DirectionBetween_SlotsThatArentNeighbours_IsMinusOne()
        {
            Assert.AreEqual(-1, HexCoordinates.DirectionBetween(new Vector2Int(2, 2), new Vector2Int(5, 2)));
            Assert.AreEqual(-1, HexCoordinates.DirectionBetween(new Vector2Int(2, 2), new Vector2Int(2, 2)));
        }

        [Test]
        public void Distance_SameSlot_IsZero()
        {
            Assert.AreEqual(0, HexCoordinates.Distance(new Vector2Int(3, 2), new Vector2Int(3, 2)));
            Assert.AreEqual(0, HexCoordinates.Distance(new Vector2Int(3, 3), new Vector2Int(3, 3)));
        }

        [TestCase(4, 4)]
        [TestCase(4, 5)]
        public void Distance_EveryHexNeighbour_IsOne(int col, int row)
        {
            var origin = new Vector2Int(col, row);

            foreach (var neighbour in HexCoordinates.HexNeighborIndices(col, row))
            {
                Assert.AreEqual(1, HexCoordinates.Distance(origin, neighbour), $"{origin} -> {neighbour}");
                Assert.AreEqual(1, HexCoordinates.Distance(neighbour, origin), $"{neighbour} -> {origin}");
            }
        }

        [TestCase(6, 6)]
        [TestCase(6, 7)]
        public void Distance_MatchesBreadthFirstStepsOverHexNeighbours(int col, int row)
        {
            var origin = new Vector2Int(col, row);
            var steps = StepsFrom(origin);

            for (var x = 0; x < Size; x++)
            {
                for (var y = 0; y < Size; y++)
                {
                    var slot = new Vector2Int(x, y);
                    var distance = HexCoordinates.Distance(origin, slot);

                    // Past 4 steps a bounded walk can detour around the edge, so only compare the near field.
                    if (distance <= 4 || steps[x, y] <= 4)
                    {
                        Assert.AreEqual(steps[x, y], distance, $"{origin} -> {slot}");
                    }
                }
            }
        }

        [Test]
        public void Distance_SameRowSpan_IsTheColumnGap()
        {
            Assert.AreEqual(4, HexCoordinates.Distance(new Vector2Int(1, 2), new Vector2Int(5, 2)));
            Assert.AreEqual(4, HexCoordinates.Distance(new Vector2Int(1, 3), new Vector2Int(5, 3)));
        }

        [TestCase(3, 2, true)]
        [TestCase(3, 2, false)]
        [TestCase(3, 3, true)]
        [TestCase(3, 3, false)]
        [TestCase(0, 4, false)]
        public void DiagonalDown_IsAHexNeighbourOneRowBelow(int col, int row, bool right)
        {
            var slot = new Vector2Int(col, row);

            var next = HexCoordinates.DiagonalDown(slot, right);

            Assert.AreEqual(row + 1, next.y);
            CollectionAssert.Contains(HexCoordinates.HexNeighborIndices(col, row), next);
        }

        [TestCase(3, 2)]
        [TestCase(3, 3)]
        public void DiagonalDown_RightIsOneColumnPastLeft(int col, int row)
        {
            var slot = new Vector2Int(col, row);

            var left = HexCoordinates.DiagonalDown(slot, false);
            var right = HexCoordinates.DiagonalDown(slot, true);

            Assert.AreEqual(left.x + 1, right.x);
        }

        [Test]
        public void DiagonalDown_Repeated_KeepsDriftingTheSameWay()
        {
            var right = new Vector2Int(2, 0);
            var left = new Vector2Int(4, 0);
            for (var i = 0; i < 4; i++)
            {
                right = HexCoordinates.DiagonalDown(right, true);
                left = HexCoordinates.DiagonalDown(left, false);
            }

            // Four hops down-right from an even row gain 2 columns (one per odd row crossed); down-left loses 2.
            Assert.AreEqual(new Vector2Int(4, 4), right);
            Assert.AreEqual(new Vector2Int(2, 4), left);
        }

        private static int[,] StepsFrom(Vector2Int origin)
        {
            var steps = new int[Size, Size];
            for (var x = 0; x < Size; x++)
            {
                for (var y = 0; y < Size; y++)
                {
                    steps[x, y] = int.MaxValue;
                }
            }

            steps[origin.x, origin.y] = 0;
            var queue = new Queue<Vector2Int>();
            queue.Enqueue(origin);
            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                foreach (var next in HexCoordinates.HexNeighborIndices(current.x, current.y))
                {
                    if (next.x < 0 || next.y < 0 || next.x >= Size || next.y >= Size
                        || steps[next.x, next.y] != int.MaxValue)
                    {
                        continue;
                    }

                    steps[next.x, next.y] = steps[current.x, current.y] + 1;
                    queue.Enqueue(next);
                }
            }

            return steps;
        }
    }
}
