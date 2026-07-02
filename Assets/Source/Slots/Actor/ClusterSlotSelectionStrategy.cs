using System.Collections.Generic;
using BalloonParty.Shared.Extensions;
using BalloonParty.Slots.Grid;
using UnityEngine;

namespace BalloonParty.Slots.Actor
{
    /// <summary>
    /// Selects slots that favor hex-adjacency, encouraging cluster formation.
    /// Picks a random seed slot, then greedily expands into hex neighbors
    /// from the available set. When no more neighbors are available, picks
    /// a new seed biased toward the opposite side of the grid from existing
    /// clusters — producing spatially distributed, facing clusters. Closed
    /// clusters quarantine their neighboring slots, so separately-seeded
    /// clusters can never touch — without this, adjacent clusters would merge
    /// on the board (SlotClusterRegistry joins touching same-type actors) and
    /// silently exceed maxPerCluster.
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

            var fill = new ClusterFill
            {
                Available = new HashSet<Vector2Int>(emptySlots),
                Result = new List<Vector2Int>(count)
            };

            while (fill.Result.Count < count && fill.Available.Count > 0)
            {
                // Cap reached for the current cluster — force a new seed
                if (maxPerCluster > 0 && fill.CurrentClusterSlots.Count >= maxPerCluster)
                {
                    fill.Frontier.Clear();
                }

                if (fill.Frontier.Count == 0)
                {
                    SeedNewCluster(fill);
                    continue;
                }

                GrowCluster(fill);
            }

            return fill.Result;
        }

        // Starts a fresh cluster: closes the current one, picks a seed far from existing clusters,
        // and primes the frontier with its neighbours.
        private void SeedNewCluster(ClusterFill fill)
        {
            FinishCurrentCluster(fill);

            var seed = fill.ClusterCentroids.Count == 0
                ? PickRandom(fill.Available)
                : PickFarthestSeed(fill.Available, fill.ClusterCentroids);

            fill.Available.Remove(seed);
            fill.Result.Add(seed);
            fill.CurrentClusterSlots.Add(seed);
            AddNeighborsToFrontier(seed, fill.Available, fill.Frontier);
        }

        // Consumes one frontier slot (swap-removed), claiming it if still available and extending
        // the frontier from it.
        private void GrowCluster(ClusterFill fill)
        {
            var idx = Random.Range(0, fill.Frontier.Count);
            var next = fill.Frontier[idx];
            fill.Frontier.SwapRemoveAt(idx);

            if (!fill.Available.Contains(next))
            {
                return;
            }

            fill.Available.Remove(next);
            fill.Result.Add(next);
            fill.CurrentClusterSlots.Add(next);
            AddNeighborsToFrontier(next, fill.Available, fill.Frontier);
        }

        // Mutable working set for one SelectSlots run, passed between the seed/grow steps.
        private sealed class ClusterFill
        {
            public readonly List<Vector2Int> Frontier = new();
            public readonly List<Vector2> ClusterCentroids = new();
            public readonly List<Vector2Int> CurrentClusterSlots = new();
            public HashSet<Vector2Int> Available;
            public List<Vector2Int> Result;
        }

        private static void FinishCurrentCluster(ClusterFill fill)
        {
            if (fill.CurrentClusterSlots.Count == 0)
            {
                return;
            }

            var centroid = Vector2.zero;
            foreach (var s in fill.CurrentClusterSlots)
            {
                centroid += new Vector2(s.x, s.y);

                // Quarantine the closed cluster: removing its neighbors from the available set
                // guarantees no later seed or growth can touch it and merge past maxPerCluster.
                HexCoordinates.HexNeighborIndices(s.x, s.y, NeighborBuffer);
                foreach (var neighbor in NeighborBuffer)
                {
                    fill.Available.Remove(neighbor);
                }
            }

            centroid /= fill.CurrentClusterSlots.Count;
            fill.ClusterCentroids.Add(centroid);
            fill.CurrentClusterSlots.Clear();
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
