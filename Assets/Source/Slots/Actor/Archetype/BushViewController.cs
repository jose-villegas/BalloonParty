using System.Collections.Generic;
using BalloonParty.Configuration;
using BalloonParty.Slots.Actor.Cluster;
using BalloonParty.Slots.Grid;
using UnityEngine;
using VContainer;

namespace BalloonParty.Slots.Actor.Archetype
{
    /// <summary>
    /// Bush-specific cluster view controller. Adds gap-fill circles at midpoints
    /// between adjacent bush slots so the rendered shape spans the gaps
    /// for a more continuous, natural coverage.
    /// </summary>
    internal class BushViewController
        : ClusterViewController<BushObstacleModel, BushView, IBushSettings>
    {
        private const float GapRadiusScale = 0.65f;

        [Inject]
        internal BushViewController(
            BushClusterRegistry registry,
            SlotGrid grid,
            IBushSettings settings,
            IObjectResolver resolver)
            : base(registry, grid, settings, resolver)
        {
        }

        protected override BushView GetPrefab(IBushSettings settings)
        {
            return settings.BushPrefab;
        }

        protected override int PopulatePositions(
            Vector4[] buffer,
            IReadOnlyDictionary<int, SlotCluster> clusters,
            SlotGrid grid)
        {
            var count = base.PopulatePositions(buffer, clusters, grid);

            var slotSet = new HashSet<Vector2Int>();
            foreach (var cluster in clusters.Values)
            {
                foreach (var slot in cluster.Slots)
                {
                    slotSet.Add(slot);
                }
            }

            var addedEdges = new HashSet<(Vector2Int, Vector2Int)>();
            var neighborBuffer = new Vector2Int[6];

            foreach (var cluster in clusters.Values)
            {
                var seed = (cluster.ClusterId * 0.7123f) % 1f;
                foreach (var slot in cluster.Slots)
                {
                    SlotGrid.HexNeighborIndices(slot.x, slot.y, neighborBuffer);
                    foreach (var neighbor in neighborBuffer)
                    {
                        if (!slotSet.Contains(neighbor))
                        {
                            continue;
                        }

                        var edge = OrderedEdge(slot, neighbor);
                        if (!addedEdges.Add(edge))
                        {
                            continue;
                        }

                        if (count >= buffer.Length)
                        {
                            break;
                        }

                        var posA = grid.IndexToWorldPosition(slot);
                        var posB = grid.IndexToWorldPosition(neighbor);
                        var mid = (posA + posB) * 0.5f;
                        buffer[count++] = new Vector4(mid.x, mid.y, seed, GapRadiusScale);
                    }
                }
            }

            return count;
        }

        private static (Vector2Int, Vector2Int) OrderedEdge(Vector2Int a, Vector2Int b)
        {
            if (a.x > b.x || (a.x == b.x && a.y > b.y))
            {
                return (b, a);
            }

            return (a, b);
        }
    }
}

