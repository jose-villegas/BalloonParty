using BalloonParty.Balloon.Model;
using BalloonParty.Configuration.Palette;
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

        /// <summary>Whether a piercing shot plows this actor rather than one-shotting it — a durable
        /// balloon with more than one hit left, or an unbreakable.</summary>
        internal static bool IsTough(this IBalloonModel model)
        {
            return (model is IHasDurability durable && durable.HitsRemaining.Value > 1)
                   || model is UnbreakableBalloonModel;
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

        // The three hex axes expressed as doubled-coordinate steps: horizontal (±2,0), diagonal-right
        // (±1,±1 same sign), diagonal-left (±1,∓1 opposite sign).
        private static readonly (int dDoubled, int dRow)[] AxisSteps = { (2, 0), (1, 1), (1, -1) };

        /// <summary>
        ///     Counts same-type balloons along the best-aligned hex axis through <paramref name="candidate" />.
        ///     For each of the three hex axes, walks both directions and counts consecutive same-type
        ///     occupants. Returns the max across axes — rewarding candidates that extend an existing line
        ///     (wall) rather than forming a lump.
        /// </summary>
        internal static int BestLineCountSameType(this IBalloonModel self, SlotGrid grid, Vector2Int candidate)
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

        private static int WalkAxis(IBalloonModel self, SlotGrid grid, int row, int doubled, int dDoubled, int dRow)
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

                if (grid.At(pos) is IBalloonModel other && !ReferenceEquals(other, self)
                    && other.TypeName == self.TypeName)
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

        // IsEmpty is bounds-safe, so off-board cells fall out naturally.
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

        // Cube direction vectors for the six hex edges (used by the ring walk).
        private static readonly (int dq, int dr)[] CubeDirections =
        {
            (1, 0), (1, -1), (0, -1), (-1, 0), (-1, 1), (0, 1)
        };

        /// <summary>
        ///     Starting at <paramref name="center" />, searches outward in concentric hex rings for the
        ///     nearest balloon with a concrete (non-rainbow, non-empty) color. Returns the color ID, or
        ///     null if none found on the grid.
        /// </summary>
        internal static string FindNearestColorId(
            this SlotGrid grid, Vector2Int center, IBalloonModel exclude, IGamePalette palette)
        {
            // Check center slot itself.
            var found = TryGetColorAt(grid, center, exclude, palette);
            if (found != null)
            {
                return found;
            }

            var maxRadius = Mathf.Max(grid.Columns, grid.Rows);
            var centerQ = center.x - (center.y - (center.y & 1)) / 2;
            var centerR = center.y;

            for (var ring = 1; ring <= maxRadius; ring++)
            {
                // Start corner: center + direction[4] * ring (south-west in cube coords).
                var q = centerQ + CubeDirections[4].dq * ring;
                var r = centerR + CubeDirections[4].dr * ring;

                for (var side = 0; side < 6; side++)
                {
                    for (var step = 0; step < ring; step++)
                    {
                        var col = q + (r - (r & 1)) / 2;
                        var slot = new Vector2Int(col, r);

                        if (grid.InBounds(slot))
                        {
                            found = TryGetColorAt(grid, slot, exclude, palette);
                            if (found != null)
                            {
                                return found;
                            }
                        }

                        q += CubeDirections[side].dq;
                        r += CubeDirections[side].dr;
                    }
                }
            }

            return null;
        }

        private static string TryGetColorAt(
            SlotGrid grid, Vector2Int slot, IBalloonModel exclude, IGamePalette palette)
        {
            if (grid.IsEmpty(slot.x, slot.y))
            {
                return null;
            }

            if (grid.At(slot) is not IBalloonModel model || ReferenceEquals(model, exclude))
            {
                return null;
            }

            if (model is not IHasColor colored)
            {
                return null;
            }

            var color = colored.Color.Value;
            if (string.IsNullOrEmpty(color) || palette.IsRainbow(color))
            {
                return null;
            }

            return color;
        }
    }
}
