using BalloonParty.Balloon.Model;
using BalloonParty.Slots.Capabilities;
using BalloonParty.Slots.Grid;
using UnityEngine;

namespace BalloonParty.Shared.Extensions
{
    internal static class BalloonModelExtensions
    {
        // Doubled-coordinate offsets of the 10 cells the color bias looks at: only the adjacent diagonals
        // in the ±1 rows (their lateral ±3 continuations excluded), plus all three cells of the ±2 rows.
        private static readonly int[] AdjacentRowOffsets = { -1, 1 };
        private static readonly int[] OuterRowOffsets = { -2, 0, 2 };

        /// <summary>Returns the balloon's color ID if it implements <see cref="IHasColor"/>, else empty string.</summary>
        internal static string GetColorId(this IBalloonModel model)
        {
            return (model as IHasColor)?.Color.Value ?? "";
        }

        /// <summary>Counts same-color balloons in <paramref name="candidate" />'s diagonal band: its four adjacent diagonals plus the full rows two above/below (own row and the ±1-row outer cells excluded); 0 when colorless.</summary>
        internal static int CountSameColorDiagonals(this IBalloonModel self, SlotGrid grid, Vector2Int candidate)
        {
            var color = self.GetColorId();
            if (string.IsNullOrEmpty(color))
            {
                return 0;
            }

            var count = 0;
            for (var rowDelta = -2; rowDelta <= 2; rowDelta++)
            {
                if (rowDelta == 0)
                {
                    continue;
                }

                var offsets = rowDelta is -1 or 1 ? AdjacentRowOffsets : OuterRowOffsets;
                count += CountMatchesInRow(self, grid, candidate, candidate.y + rowDelta, offsets, color);
            }

            return count;
        }

        /// <summary>Squared world distance from <paramref name="candidate" /> to the nearest balloon of <paramref name="self" />'s type (excluding itself); <see cref="float.MaxValue" /> if none.</summary>
        internal static float NearestSameTypeSqrDistance(this IBalloonModel self, SlotGrid grid, Vector2Int candidate)
        {
            var candidatePos = grid.IndexToWorldPosition(candidate);
            var nearest = float.MaxValue;

            for (var col = 0; col < grid.Columns; col++)
            {
                for (var row = 0; row < grid.Rows; row++)
                {
                    if (grid.At(new Vector2Int(col, row)) is not IBalloonModel other
                        || ReferenceEquals(other, self) || other.TypeName != self.TypeName)
                    {
                        continue;
                    }

                    var offset = grid.IndexToWorldPosition(new Vector2Int(col, row)) - candidatePos;
                    nearest = Mathf.Min(nearest, offset.sqrMagnitude);
                }
            }

            return nearest;
        }

        // Converts each doubled-coordinate offset back to a column for the target row's parity; IsEmpty is
        // bounds-safe, so off-board cells fall out naturally.
        private static int CountMatchesInRow(
            IBalloonModel self, SlotGrid grid, Vector2Int candidate, int row, int[] offsets, string color)
        {
            var doubled = candidate.x * 2 + (candidate.y & 1);
            var count = 0;

            foreach (var offset in offsets)
            {
                var col = (doubled + offset - (row & 1)) / 2;
                if (grid.IsEmpty(col, row))
                {
                    continue;
                }

                if (grid.At(new Vector2Int(col, row)) is IBalloonModel other && !ReferenceEquals(other, self)
                    && other.GetColorId() == color)
                {
                    count++;
                }
            }

            return count;
        }
    }
}
