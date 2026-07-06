using System.Collections.Generic;
using BalloonParty.Shared;
using BalloonParty.Slots.Grid;
using UnityEngine;

namespace BalloonParty.Editor.EffectPreview
{
    /// <summary>
    ///     Editor-only grid position utilities.
    /// </summary>
    internal static class EditorGridHelper
    {
        /// <summary>
        ///     Picks <paramref name="count" /> random slot positions, nearest to <paramref name="origin" /> first.
        /// </summary>
        internal static List<Vector3> RandomSlotPositions(
            int count,
            IGameConfiguration config,
            Vector3 origin)
        {
            var gridSize = config.SlotsSize;
            var sep = config.SlotSeparation;
            var offset = config.SlotsOffset;
            var totalSlots = gridSize.x * gridSize.y;

            var indices = new List<Vector2Int>(totalSlots);
            for (var col = 0; col < gridSize.x; col++)
            {
                for (var row = 0; row < gridSize.y; row++)
                {
                    indices.Add(new Vector2Int(col, row));
                }
            }

            for (var i = indices.Count - 1; i > 0; i--)
            {
                var j = Random.Range(0, i + 1);
                (indices[i], indices[j]) = (indices[j], indices[i]);
            }

            var pick = Mathf.Min(count, indices.Count);
            var result = new List<Vector3>(pick);
            for (var i = 0; i < pick; i++)
            {
                result.Add(HexCoordinates.IndexToWorldPosition(indices[i], sep, offset));
            }

            result.Sort((a, b) =>
                Vector3.Distance(origin, a)
                    .CompareTo(Vector3.Distance(origin, b)));

            return result;
        }
    }
}
