using BalloonParty.Configuration.Items;
using UnityEngine;

namespace BalloonParty.Item.Preview
{
    /// <summary>
    ///     Laser's telegraph: two crossing rectangles — the corridors its four arms sweep.
    /// </summary>
    /// <remarks>
    ///     <c>LaserCross</c> casts four arms along the rotated ±right/±up axes, each a swept circle of
    ///     <c>CircleCastRadius</c> travelling <c>RaycastDistance</c>. Opposite arms therefore share one
    ///     corridor, so the four collapse into two rectangles: each is <c>2 × RaycastDistance</c> long and
    ///     <c>2 × CircleCastRadius</c> wide, centred on the host and rotated with the icon.
    ///     <para>
    ///         The corridor's real ends are round (a swept circle, not a box) and an occupant is caught
    ///         when the segment-to-centre distance is within the cast radius plus its OWN radius — so the
    ///         drawn rectangle is the beam's own footprint, not the exact catchment. Same deliberate
    ///         simplification as <c>BombRangePreview</c>'s circle.
    ///     </para>
    /// </remarks>
    internal sealed class LaserRangePreview : IItemRangePreview
    {
        private readonly IItemConfiguration _itemConfig;

        public ItemType Type => ItemType.Laser;

        internal LaserRangePreview(IItemConfiguration itemConfig)
        {
            _itemConfig = itemConfig;
        }

        public void BuildShape(in ItemPreviewContext context, ItemPreviewShape shape)
        {
            var settings = _itemConfig[ItemType.Laser];
            if (settings == null)
            {
                return;
            }

            var halfWidth = settings.Laser.CircleCastRadius;
            var halfLength = settings.Laser.RaycastDistance;
            if (halfWidth <= 0f || halfLength <= 0f)
            {
                return;
            }

            // Mirrors LaserCross.Rotate: a pure 2D rotation of right/up by the icon's Z angle, matching
            // LaserItemRotation's own Quaternion.AngleAxis(angle, forward).
            var radians = context.ItemSpinDegrees * Mathf.Deg2Rad;
            var cos = Mathf.Cos(radians);
            var sin = Mathf.Sin(radians);
            var right = new Vector2(cos, sin);
            var up = new Vector2(-sin, cos);

            AddRectangle(shape, context.Origin, right, up, halfLength, halfWidth);
            AddRectangle(shape, context.Origin, up, right, halfLength, halfWidth);
        }

        private static void AddRectangle(
            ItemPreviewShape shape, Vector2 center, Vector2 along, Vector2 across, float halfLength, float halfWidth)
        {
            var length = along * halfLength;
            var width = across * halfWidth;

            shape.BeginStroke();
            shape.AddPoint(center - length - width);
            shape.AddPoint(center + length - width);
            shape.AddPoint(center + length + width);
            shape.AddPoint(center - length + width);
            shape.EndStroke(closed: true);
        }
    }
}
