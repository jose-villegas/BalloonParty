using System.Collections.Generic;
using BalloonParty.Slots.Grid;
using UnityEngine;

namespace BalloonParty.Slots.Actor
{
    /// <summary>
    /// Selects slots that favor hex-adjacency, encouraging cluster formation.
    /// Picks a random seed slot, then greedily expands into hex neighbors
    /// from the available set. When no more neighbors are available, picks
    /// a new random seed (possibly forming multiple small clusters).
    /// </summary>
    internal class ClusterSlotSelectionStrategy : ISlotSelectionStrategy
    {
        public List<Vector2Int> SelectSlots(IReadOnlyList<Vector2Int> emptySlots, int count)
        {
            if (emptySlots.Count == 0 || count <= 0)
            {
                return new List<Vector2Int>();
            }

            var available = new HashSet<Vector2Int>(emptySlots);
            var result = new List<Vector2Int>(count);
            var frontier = new List<Vector2Int>();

            while (result.Count < count && available.Count > 0)
            {
                if (frontier.Count == 0)
                {
                    // Pick a new random seed from remaining available slots
                    var seed = PickRandom(available);
                    available.Remove(seed);
                    result.Add(seed);
                    AddNeighborsToFrontier(seed, available, frontier);
                    continue;
                }

                // Pick a random frontier slot (biases toward compact clusters)
                var idx = Random.Range(0, frontier.Count);
                var next = frontier[idx];
                frontier[idx] = frontier[^1];
                frontier.RemoveAt(frontier.Count - 1);

                if (!available.Contains(next))
                {
                    continue;
                }

                available.Remove(next);
                result.Add(next);
                AddNeighborsToFrontier(next, available, frontier);
            }

            return result;
        }

        private static void AddNeighborsToFrontier(
            Vector2Int slot,
            HashSet<Vector2Int> available,
            List<Vector2Int> frontier)
        {
            foreach (var neighbor in SlotGrid.HexNeighborIndices(slot.x, slot.y))
            {
                if (available.Contains(neighbor))
                {
                    frontier.Add(neighbor);
                }
            }
        }

        private static Vector2Int PickRandom(HashSet<Vector2Int> set)
        {
            var idx = Random.Range(0, set.Count);
            var i = 0;
            foreach (var item in set)
            {
                if (i == idx)
                {
                    return item;
                }

                i++;
            }

            // Fallback — shouldn't reach here
            foreach (var item in set)
            {
                return item;
            }

            return Vector2Int.zero;
        }
    }
}

