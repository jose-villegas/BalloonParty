using System.Collections.Generic;
using BalloonParty.Slots.Grid;
using UnityEngine;

namespace BalloonParty.Slots.Actor
{
    /// <summary>
    /// Selects slots that favor hex-adjacency, encouraging cluster formation.
    /// Picks a random seed slot, then greedily expands into hex neighbors
    /// from the available set. When no more neighbors are available, picks
    /// a new seed biased toward the opposite side of the grid from existing
    /// clusters — producing spatially distributed, facing clusters.
    /// </summary>
    internal class ClusterSlotSelectionStrategy : ISlotSelectionStrategy
    {
        /// <summary>
        /// How many of the farthest candidates to consider when picking the
        /// next cluster seed. A small pool adds variety while still biasing
        /// toward the opposite side.
        /// </summary>
        private const int FarthestCandidatePool = 3;

        // Reused scratch for hex-neighbor lookups. The selection runs on the main thread
        // and fills-then-reads this synchronously, so a single shared buffer is safe.
        private static readonly Vector2Int[] NeighborBuffer = new Vector2Int[6];

        public List<Vector2Int> SelectSlots(IReadOnlyList<Vector2Int> emptySlots, int count, int maxPerCluster = 0)
        {
            if (emptySlots.Count == 0 || count <= 0)
            {
                return new List<Vector2Int>();
            }

            var available = new HashSet<Vector2Int>(emptySlots);
            var result = new List<Vector2Int>(count);
            var frontier = new List<Vector2Int>();
            var clusterCentroids = new List<Vector2>();
            var currentClusterSlots = new List<Vector2Int>();

            while (result.Count < count && available.Count > 0)
            {
                // Cap reached for the current cluster — force a new seed
                if (maxPerCluster > 0 && currentClusterSlots.Count >= maxPerCluster)
                {
                    frontier.Clear();
                }

                if (frontier.Count == 0)
                {
                    FinishCurrentCluster(currentClusterSlots, clusterCentroids);

                    var seed = clusterCentroids.Count == 0
                        ? PickRandom(available)
                        : PickFarthestSeed(available, clusterCentroids);

                    available.Remove(seed);
                    result.Add(seed);
                    currentClusterSlots.Add(seed);
                    AddNeighborsToFrontier(seed, available, frontier);
                    continue;
                }

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
                currentClusterSlots.Add(next);
                AddNeighborsToFrontier(next, available, frontier);
            }

            return result;
        }

        private static void FinishCurrentCluster(
            List<Vector2Int> currentClusterSlots,
            List<Vector2> clusterCentroids)
        {
            if (currentClusterSlots.Count > 0)
            {
                var centroid = Vector2.zero;
                foreach (var s in currentClusterSlots)
                {
                    centroid += new Vector2(s.x, s.y);
                }

                centroid /= currentClusterSlots.Count;
                clusterCentroids.Add(centroid);
                currentClusterSlots.Clear();
            }
        }

        /// <summary>
        /// Picks a seed slot that maximises the minimum distance to all
        /// existing cluster centroids — pushing new clusters to the
        /// opposite side of the grid.
        /// </summary>
        private static Vector2Int PickFarthestSeed(
            IReadOnlyCollection<Vector2Int> available,
            IReadOnlyList<Vector2> centroids)
        {
            var best = new List<(Vector2Int slot, float minDist)>();

            foreach (var candidate in available)
            {
                var pos = new Vector2(candidate.x, candidate.y);
                var minDist = float.MaxValue;
                foreach (var c in centroids)
                {
                    var d = (pos - c).sqrMagnitude;
                    if (d < minDist)
                    {
                        minDist = d;
                    }
                }

                InsertSorted(best, candidate, minDist);
            }

            var poolSize = Mathf.Min(FarthestCandidatePool, best.Count);
            return best[Random.Range(0, poolSize)].slot;
        }

        private static void InsertSorted(
            List<(Vector2Int slot, float minDist)> list,
            Vector2Int slot,
            float minDist)
        {
            var entry = (slot, minDist);

            if (list.Count < FarthestCandidatePool)
            {
                list.Add(entry);
                list.Sort((a, b) => b.minDist.CompareTo(a.minDist));
                return;
            }

            if (minDist > list[^1].minDist)
            {
                list[^1] = entry;
                list.Sort((a, b) => b.minDist.CompareTo(a.minDist));
            }
        }

        private static void AddNeighborsToFrontier(
            Vector2Int slot,
            HashSet<Vector2Int> available,
            List<Vector2Int> frontier)
        {
            HexCoordinates.HexNeighborIndices(slot.x, slot.y, NeighborBuffer);
            foreach (var neighbor in NeighborBuffer)
            {
                if (available.Contains(neighbor))
                {
                    frontier.Add(neighbor);
                }
            }
        }

        private static Vector2Int PickRandom(IReadOnlyCollection<Vector2Int> set)
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

            foreach (var item in set)
            {
                return item;
            }

            return Vector2Int.zero;
        }
    }
}
