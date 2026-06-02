using System.Collections.Generic;
using System.Linq;
using BalloonParty.Slots.Actor;
using BalloonParty.Slots.Grid;
using NUnit.Framework;
using UnityEngine;

namespace BalloonParty.Tests.Slots
{
    [TestFixture]
    public class ClusterSlotSelectionStrategyTests
    {
        private ClusterSlotSelectionStrategy _strategy;

        [SetUp]
        public void SetUp()
        {
            _strategy = new ClusterSlotSelectionStrategy();
        }

        [Test]
        public void ClusterSlotSelection_EmptySlots_ReturnsEmpty()
        {
            var result = _strategy.SelectSlots(new List<Vector2Int>(), 5);

            Assert.AreEqual(0, result.Count);
        }

        [Test]
        public void ClusterSlotSelection_CountZero_ReturnsEmpty()
        {
            var slots = new List<Vector2Int> { new(0, 0), new(1, 0) };

            var result = _strategy.SelectSlots(slots, 0);

            Assert.AreEqual(0, result.Count);
        }

        [Test]
        public void ClusterSlotSelection_SingleSlotAvailable_ReturnsThatSlot()
        {
            var slots = new List<Vector2Int> { new(2, 3) };

            var result = _strategy.SelectSlots(slots, 1);

            Assert.AreEqual(1, result.Count);
            Assert.AreEqual(new Vector2Int(2, 3), result[0]);
        }

        [Test]
        public void ClusterSlotSelection_ResultCount_DoesNotExceedRequestedCount()
        {
            var slots = new List<Vector2Int>();
            for (var c = 0; c < 5; c++)
            {
                for (var r = 0; r < 5; r++)
                {
                    slots.Add(new Vector2Int(c, r));
                }
            }

            var result = _strategy.SelectSlots(slots, 3);

            Assert.LessOrEqual(result.Count, 3);
        }

        [Test]
        public void ClusterSlotSelection_ResultCount_DoesNotExceedAvailableSlots()
        {
            var slots = new List<Vector2Int> { new(0, 0), new(1, 0) };

            var result = _strategy.SelectSlots(slots, 10);

            Assert.LessOrEqual(result.Count, 2);
        }

        [Test]
        public void ClusterSlotSelection_AllResultsAreFromAvailableSet()
        {
            var slots = new List<Vector2Int>
            {
                new(0, 0), new(1, 0), new(2, 0),
                new(0, 1), new(1, 1), new(2, 1)
            };

            var available = new HashSet<Vector2Int>(slots);
            var result = _strategy.SelectSlots(slots, 4);

            foreach (var slot in result)
            {
                Assert.IsTrue(available.Contains(slot),
                    $"Selected slot {slot} is not in the available set");
            }
        }

        [Test]
        public void ClusterSlotSelection_MaxPerCluster_NoClusterExceedsLimit()
        {
            var slots = new List<Vector2Int>();
            for (var c = 0; c < 6; c++)
            {
                for (var r = 0; r < 6; r++)
                {
                    slots.Add(new Vector2Int(c, r));
                }
            }

            var maxPerCluster = 2;
            var result = _strategy.SelectSlots(slots, 8, maxPerCluster);

            var clusters = IdentifyClusters(result);
            foreach (var cluster in clusters)
            {
                Assert.LessOrEqual(cluster.Count, maxPerCluster,
                    $"Cluster of size {cluster.Count} exceeds maxPerCluster={maxPerCluster}");
            }
        }

        [Test]
        public void ClusterSlotSelection_SelectedSlotsAreHexAdjacent_WithinCluster()
        {
            var slots = new List<Vector2Int>();
            for (var c = 0; c < 6; c++)
            {
                for (var r = 0; r < 6; r++)
                {
                    slots.Add(new Vector2Int(c, r));
                }
            }

            var result = _strategy.SelectSlots(slots, 6, maxPerCluster: 3);
            var clusters = IdentifyClusters(result);

            foreach (var cluster in clusters)
            {
                if (cluster.Count <= 1)
                {
                    continue;
                }

                foreach (var slot in cluster)
                {
                    var neighbors = SlotGrid.HexNeighborIndices(slot.x, slot.y);
                    var hasAdjacentInCluster = cluster.Any(other =>
                        other != slot && neighbors.Contains(other));

                    Assert.IsTrue(hasAdjacentInCluster,
                        $"Slot {slot} in cluster has no hex-adjacent neighbor within the same cluster");
                }
            }
        }

        /// <summary>
        /// Groups the selected slots into clusters by hex adjacency via flood fill.
        /// </summary>
        private static List<List<Vector2Int>> IdentifyClusters(IReadOnlyList<Vector2Int> slots)
        {
            var remaining = new HashSet<Vector2Int>(slots);
            var clusters = new List<List<Vector2Int>>();

            while (remaining.Count > 0)
            {
                var seed = remaining.First();
                var cluster = new List<Vector2Int>();
                var queue = new Queue<Vector2Int>();
                queue.Enqueue(seed);
                remaining.Remove(seed);

                while (queue.Count > 0)
                {
                    var current = queue.Dequeue();
                    cluster.Add(current);

                    foreach (var neighbor in SlotGrid.HexNeighborIndices(current.x, current.y))
                    {
                        if (remaining.Remove(neighbor))
                        {
                            queue.Enqueue(neighbor);
                        }
                    }
                }

                clusters.Add(cluster);
            }

            return clusters;
        }
    }
}

