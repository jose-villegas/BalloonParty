using BalloonParty.Balloon.Model;
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
    }
}
