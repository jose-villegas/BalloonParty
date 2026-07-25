using BalloonParty.Slots.Actor;
using BalloonParty.Slots.Grid;
using UnityEngine;

namespace BalloonParty.Shared.Extensions
{
    /// <summary>
    ///     The three balance-bias formulas (color-diagonal, line, clump) shared by every
    ///     <see cref="IBalanceInfluence.WeightBias" /> override — live models and (later) sim stub actors
    ///     run the identical formula because both implement <see cref="IBalanceBiasSource" />.
    /// </summary>
    internal static class BalanceBiasExtensions
    {
        // Doubled-coordinate offsets of the 10 cells the color bias looks at: only the adjacent diagonals
        // in the ±1 rows (their lateral ±3 continuations excluded), plus all three cells of the ±2 rows.
        private static readonly int[] AdjacentRowOffsets = { -1, 1 };
        private static readonly int[] OuterRowOffsets = { -2, 0, 2 };

        // The three hex axes expressed as doubled-coordinate steps: horizontal (±2,0), diagonal-right
        // (±1,±1 same sign), diagonal-left (±1,∓1 opposite sign).
        private static readonly (int dDoubled, int dRow)[] AxisSteps = { (2, 0), (1, 1), (1, -1) };

        /// <summary>Dispatches to the formula named by <paramref name="kind" />; <see cref="BalanceBiasKind.None" /> is always 0.</summary>
        internal static int Evaluate(
            this IBalanceBiasSource self, BalanceBiasKind kind, SlotGrid grid, Vector2Int candidate, float bias)
        {
            return kind switch
            {
                BalanceBiasKind.ColorDiagonal => self.ColorDiagonal(grid, candidate, bias),
                BalanceBiasKind.Line => self.Line(grid, candidate, bias),
                BalanceBiasKind.Clump => self.Clump(grid, candidate, bias),
                _ => 0,
            };
        }

        /// <summary>Color-diagonal formula, lifted verbatim from <c>BalloonModel.WeightBias</c>.</summary>
        internal static int ColorDiagonal(this IBalanceBiasSource self, SlotGrid grid, Vector2Int candidate, float bias)
        {
            if (bias <= 0f)
            {
                return 0;
            }

            return Mathf.RoundToInt(bias * self.CountSameColorDiagonals(grid, candidate));
        }

        /// <summary>Line formula, lifted verbatim from <c>ToughBalloonModel.WeightBias</c>.</summary>
        internal static int Line(this IBalanceBiasSource self, SlotGrid grid, Vector2Int candidate, float bias)
        {
            if (bias <= 0f)
            {
                return 0;
            }

            return Mathf.RoundToInt(bias * self.BestLineCountSameType(grid, candidate));
        }

        /// <summary>Clump formula, lifted verbatim from <c>BubbleClusterModel.WeightBias</c> (note: unlike
        /// the other two, this guard allows a negative bias — 0 excludes only exactly-zero).</summary>
        internal static int Clump(this IBalanceBiasSource self, SlotGrid grid, Vector2Int candidate, float bias)
        {
            if (bias == 0f)
            {
                return 0;
            }

            var sqrDistance = self.NearestSameTypeSqrDistance(grid, candidate);
            return sqrDistance < float.MaxValue ? Mathf.RoundToInt(-bias * sqrDistance) : 0;
        }

        /// <summary>Counts same-color balloons in <paramref name="candidate" />'s diagonal band: its four adjacent diagonals plus the full rows two above/below (own row and the ±1-row outer cells excluded); 0 when colorless.</summary>
        internal static int CountSameColorDiagonals(this IBalanceBiasSource self, SlotGrid grid, Vector2Int candidate)
        {
            var color = self.ColorId;
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

        /// <summary>
        ///     Counts same-type balloons along the best-aligned hex axis through <paramref name="candidate" />.
        ///     For each of the three hex axes, walks both directions and counts consecutive same-type
        ///     occupants. Returns the max across axes — rewarding candidates that extend an existing line
        ///     (wall) rather than forming a lump.
        /// </summary>
        internal static int BestLineCountSameType(this IBalanceBiasSource self, SlotGrid grid, Vector2Int candidate)
        {
            var doubled = candidate.x * 2 + (candidate.y & 1);
            var best = 0;

            foreach (var (dDoubled, dRow) in AxisSteps)
            {
                var count = WalkAxis(self, grid, candidate.y, doubled, dDoubled, dRow)
                          + WalkAxis(self, grid, candidate.y, doubled, -dDoubled, -dRow);
                if (count > best)
                {
                    best = count;
                }
            }

            return best;
        }

        private static int WalkAxis(IBalanceBiasSource self, SlotGrid grid, int row, int doubled, int dDoubled, int dRow)
        {
            var count = 0;
            var r = row + dRow;
            var d = doubled + dDoubled;

            // Walk up to 6 cells — enough for any practical board width/height.
            for (var i = 0; i < 6; i++, r += dRow, d += dDoubled)
            {
                var col = (d - (r & 1)) / 2;
                var pos = new Vector2Int(col, r);
                if (!grid.InBounds(pos))
                {
                    break;
                }

                if (grid.At(pos) is IBalanceBiasSource other && !ReferenceEquals(other, self)
                    && other.BiasTypeId == self.BiasTypeId)
                {
                    count++;
                }
                else
                {
                    break;
                }
            }

            return count;
        }

        /// <summary>Squared world distance from <paramref name="candidate" /> to the nearest balloon of <paramref name="self" />'s type (excluding itself); <see cref="float.MaxValue" /> if none.</summary>
        internal static float NearestSameTypeSqrDistance(this IBalanceBiasSource self, SlotGrid grid, Vector2Int candidate)
        {
            var candidatePos = grid.IndexToWorldPosition(candidate);
            var nearest = float.MaxValue;

            for (var col = 0; col < grid.Columns; col++)
            {
                for (var row = 0; row < grid.Rows; row++)
                {
                    if (grid.At(new Vector2Int(col, row)) is not IBalanceBiasSource other
                        || ReferenceEquals(other, self) || other.BiasTypeId != self.BiasTypeId)
                    {
                        continue;
                    }

                    var offset = grid.IndexToWorldPosition(new Vector2Int(col, row)) - candidatePos;
                    nearest = Mathf.Min(nearest, offset.sqrMagnitude);
                }
            }

            return nearest;
        }

        // IsEmpty is bounds-safe, so off-board cells fall out naturally.
        private static int CountMatchesInRow(
            IBalanceBiasSource self, SlotGrid grid, Vector2Int candidate, int row, int[] offsets, string color)
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

                if (grid.At(new Vector2Int(col, row)) is IBalanceBiasSource other && !ReferenceEquals(other, self)
                    && other.ColorId == color)
                {
                    count++;
                }
            }

            return count;
        }
    }
}
