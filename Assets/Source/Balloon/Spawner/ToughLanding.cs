using System;
using System.Collections.Generic;
using BalloonParty.Slots.Grid;
using UnityEngine;

namespace BalloonParty.Balloon.Spawner
{
    /// <summary>
    ///     Where a clock line's toughs rise when they walk as one snake: into the columns whose landing slots lie
    ///     nearest the toughs already on the board, each one after the first beside the one before too, so they
    ///     arrive next to the snake's body rather than wherever the line's depth order sends them.
    /// </summary>
    internal static class ToughLanding
    {
        /// <summary>
        ///     Moves up to <paramref name="count" /> columns to the front of <paramref name="columns" />, from
        ///     <paramref name="start" /> on: each time the one, of those not yet moved, whose landing slot (its
        ///     <paramref name="landingRow" />, or none when it takes nothing) is nearest any of
        ///     <paramref name="anchors" />, which then gains that slot. With no anchors the first keeps its place.
        ///     Returns how many it moved.
        /// </summary>
        internal static int PutNearest(
            List<int> columns, int start, int count, Func<int, int?> landingRow, List<Vector2Int> anchors)
        {
            var moved = 0;
            for (var k = start; k < columns.Count && moved < count; k++)
            {
                var best = -1;
                var bestDistance = int.MaxValue;
                for (var i = k; i < columns.Count; i++)
                {
                    var row = landingRow(columns[i]);
                    if (!row.HasValue)
                    {
                        continue;
                    }

                    var distance = Nearest(anchors, new Vector2Int(columns[i], row.Value));
                    if (distance < bestDistance)
                    {
                        bestDistance = distance;
                        best = i;
                    }
                }

                if (best < 0)
                {
                    break;
                }

                (columns[k], columns[best]) = (columns[best], columns[k]);
                anchors.Add(new Vector2Int(columns[k], landingRow(columns[k]).GetValueOrDefault()));
                moved++;
            }

            return moved;
        }

        private static int Nearest(IReadOnlyList<Vector2Int> anchors, Vector2Int slot)
        {
            var nearest = 0;
            for (var i = 0; i < anchors.Count; i++)
            {
                var distance = HexCoordinates.Distance(anchors[i], slot);
                if (i == 0 || distance < nearest)
                {
                    nearest = distance;
                }
            }

            return nearest;
        }
    }
}
