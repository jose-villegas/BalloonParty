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
    }
}
