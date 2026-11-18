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
        /// unbreakable and the plated reward balloons, the tough entry otherwise.</summary>
        internal static string GetImpactColorId(this IBalloonModel model)
        {
            return model.TypeName is BalloonType.Unbreakable or BalloonType.Golden or BalloonType.Disco
                or BalloonType.Nuke
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

            var isHeavy = model.TypeName is BalloonType.Tough or BalloonType.Unbreakable or BalloonType.Golden
                          or BalloonType.Disco or BalloonType.Nuke;
            return isHeavy ? model.GetImpactColorId() : color;
        }

        /// <summary>How many ordinary hits this balloon takes to pop: its coat's layers (<see cref="ICoatable" />), each
        /// taking a hit of its own, on top of the hits under it (<see cref="HitsUnderCoat" />).</summary>
        internal static int HitsToPop(this IBalloonModel model)
        {
            var hits = model.HitsUnderCoat();
            return model is ICoatable coatable ? hits + coatable.Coat.Value : hits;
        }

        /// <summary>Whether a piercing shot plows this actor rather than one-shotting it — a balloon that takes more
        /// than one hit to pop (the nuke too), or one that deflects an ordinary hit (an unbreakable, the golden
        /// balloon). A coat does not count, unlike in <see cref="HitsToPop" />: a pierce pops a coated balloon on
        /// contact, coat and all, so there is nothing to plow and discharge at the wall.</summary>
        internal static bool IsTough(this IBalloonModel model)
        {
            return model.HitsUnderCoat() > 1 || model is IDeflectsShots { DeflectsOrdinaryHit: true };
        }

        /// <summary>Whether this balloon's contact washes the shot's stolen colour: a soap cluster
        /// (<see cref="IWashesProjectileColor" />) or a balloon wearing foam. Read before the hit is evaluated: the hit
        /// that takes the last layer off leaves no coat to read.</summary>
        internal static bool WashesProjectileColor(this IBalloonModel model)
        {
            return model is IWashesProjectileColor || model is ICoatable { Coat: { Value: > 0 } };
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

        // 1 for a ripe bubble cluster, which bursts whole, otherwise the hits a durable balloon has left, and 1 for
        // anything without durability.
        private static int HitsUnderCoat(this IBalloonModel model)
        {
            if (model is BubbleClusterModel { Ripe: { Value: true } })
            {
                return 1;
            }

            return model is IHasDurability durable ? durable.HitsRemaining.Value : 1;
        }
    }
}
