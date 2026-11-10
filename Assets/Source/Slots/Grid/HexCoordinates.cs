using UnityEngine;

namespace BalloonParty.Slots.Grid
{
    /// <summary>Pure coordinate math for the pointy-top, odd-row-shifted hex grid used by <see cref="SlotGrid" />.</summary>
    internal static class HexCoordinates
    {
        public static Vector2Int[] HexNeighborIndices(int col, int row)
        {
            var shiftedCol = col + (row % 2 == 0 ? -1 : 1);

            return new[]
            {
                new Vector2Int(col - 1, row),
                new Vector2Int(col + 1, row),
                new Vector2Int(col, row - 1),
                new Vector2Int(shiftedCol, row - 1),
                new Vector2Int(col, row + 1),
                new Vector2Int(shiftedCol, row + 1)
            };
        }

        public static void HexNeighborIndices(int col, int row, Vector2Int[] buffer)
        {
            var shiftedCol = col + (row % 2 == 0 ? -1 : 1);
            buffer[0] = new Vector2Int(col - 1, row);
            buffer[1] = new Vector2Int(col + 1, row);
            buffer[2] = new Vector2Int(col, row - 1);
            buffer[3] = new Vector2Int(shiftedCol, row - 1);
            buffer[4] = new Vector2Int(col, row + 1);
            buffer[5] = new Vector2Int(shiftedCol, row + 1);
        }

        public static Vector3 IndexToWorldPosition(Vector2Int index, Vector2 separation, Vector2 offset)
        {
            var hIndex = (index.x * 2) + (index.y % 2);
            return new Vector3(
                ((hIndex - offset.x) * separation.x) - (separation.x / 2f),
                (-index.y * separation.y) + offset.y,
                0f);
        }

        /// <summary>Steps between two slots along hex neighbours (odd rows shifted right).</summary>
        public static int Distance(Vector2Int a, Vector2Int b)
        {
            // Odd-row offset → axial: odd rows sit half a column right, so their axial q is col - (row - 1) / 2.
            var aq = a.x - ((a.y - (a.y & 1)) / 2);
            var bq = b.x - ((b.y - (b.y & 1)) / 2);
            var dq = aq - bq;
            var dr = a.y - b.y;
            return (Mathf.Abs(dq) + Mathf.Abs(dr) + Mathf.Abs(dq + dr)) / 2;
        }

        /// <summary>The next slot one row down along a diagonal: down-right when <paramref name="right" />, else down-left.</summary>
        public static Vector2Int DiagonalDown(Vector2Int slot, bool right)
        {
            var even = slot.y % 2 == 0;
            var col = right
                ? (even ? slot.x : slot.x + 1)
                : (even ? slot.x - 1 : slot.x);
            return new Vector2Int(col, slot.y + 1);
        }
    }
}
