using System.Collections.Generic;
using BalloonParty.Slots.Actor;
using BalloonParty.Slots.Grid;
using UnityEngine;

namespace BalloonParty.Balloon.Reward
{
    /// <summary>
    ///     The shape shared by the disco ball's wave and the nuke's blast: a disc fixed where the pop was, easing out
    ///     from it until it covers the whole board.
    /// </summary>
    internal static class BoardDisc
    {
        /// <summary>
        ///     The disc's full radius: past the farthest slot from <paramref name="center" /> by half a slot's spacing,
        ///     so its edge clears the last balloon.
        /// </summary>
        internal static float FarthestReach(SlotGrid grid, Vector3 center)
        {
            var farthestSqr = 0f;
            for (var col = 0; col < grid.Columns; col++)
            {
                for (var row = 0; row < grid.Rows; row++)
                {
                    var offset = (Vector2)(grid.IndexToWorldPosition(new Vector2Int(col, row)) - center);
                    farthestSqr = Mathf.Max(farthestSqr, offset.sqrMagnitude);
                }
            }

            var spacing = grid.IndexToWorldPosition(new Vector2Int(1, 0)) - grid.IndexToWorldPosition(Vector2Int.zero);
            return Mathf.Sqrt(farthestSqr) + spacing.magnitude * 0.5f;
        }

        /// <summary>
        ///     Fills <paramref name="into" /> with the occupied slots within <paramref name="radiusSqr" /> of
        ///     <paramref name="center" /> whose actor isn't in <paramref name="skip" />. Collected before the caller acts
        ///     on them, so a pop freeing a slot can't disturb the scan.
        /// </summary>
        internal static void SlotsWithin(
            SlotGrid grid, Vector3 center, float radiusSqr, HashSet<ISlotActor> skip, List<Vector2Int> into)
        {
            into.Clear();
            for (var col = 0; col < grid.Columns; col++)
            {
                for (var row = 0; row < grid.Rows; row++)
                {
                    if (grid.IsEmpty(col, row))
                    {
                        continue;
                    }

                    var slot = new Vector2Int(col, row);
                    if (skip.Contains(grid.At(slot))
                        || ((Vector2)(grid.IndexToWorldPosition(slot) - center)).sqrMagnitude > radiusSqr)
                    {
                        continue;
                    }

                    into.Add(slot);
                }
            }
        }

        /// <summary>The radius at <paramref name="progress" /> (0 to 1) of the way: fast at first, slowing at the far side.</summary>
        internal static float RadiusAt(float maxRadius, float progress)
        {
            return maxRadius * progress * (2f - progress);
        }
    }
}
