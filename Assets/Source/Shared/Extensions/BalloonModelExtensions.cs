using BalloonParty.Balloon.Model;
using BalloonParty.Balloon.Type;
using BalloonParty.Configuration.Palette;
using BalloonParty.Slots.Capabilities;
using BalloonParty.Slots.Grid;
using UnityEngine;

namespace BalloonParty.Shared.Extensions
{
    internal static class BalloonModelExtensions
    {
        /// <summary>Returns the balloon's color ID if it implements <see cref="IHasColor"/>, else empty string.</summary>
        internal static string GetColorId(this IBalloonModel model)
        {
            return (model as IHasColor)?.Color.Value ?? "";
        }

        /// <summary>The reserved presentation color for a heavy type's impacts: metallic sparks for the
        /// unbreakable, the tough entry otherwise.</summary>
        internal static string GetImpactColorId(this IBalloonModel model)
        {
            return model.TypeName == BalloonType.Unbreakable
                ? GamePalette.SparksColorId
                : GamePalette.ToughColorId;
        }

        /// <summary>The palette color a pop stamps with: the balloon's own color when it has one, the reserved
        /// impact color for colorless heavies (Tough/Unbreakable), and empty for other colorless types.</summary>
        internal static string GetPopColorId(this IBalloonModel model)
        {
            var color = model.GetColorId();
            if (!string.IsNullOrEmpty(color))
            {
                return color;
            }

            var isHeavy = model.TypeName is BalloonType.Tough or BalloonType.Unbreakable;
            return isHeavy ? model.GetImpactColorId() : color;
        }

        /// <summary>Whether a piercing shot plows this actor rather than one-shotting it — a durable
        /// balloon with more than one hit left, or an unbreakable.</summary>
        internal static bool IsTough(this IBalloonModel model)
        {
            return (model is IHasDurability durable && durable.HitsRemaining.Value > 1)
                   || model is UnbreakableBalloonModel;
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
                found = SearchRing(grid, centerQ, centerR, ring, exclude, palette);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }

        private static string SearchRing(
            SlotGrid grid, int centerQ, int centerR, int ring, IBalloonModel exclude, IGamePalette palette)
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
                        var found = TryGetColorAt(grid, slot, exclude, palette);
                        if (found != null)
                        {
                            return found;
                        }
                    }

                    q += CubeDirections[side].dq;
                    r += CubeDirections[side].dr;
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
