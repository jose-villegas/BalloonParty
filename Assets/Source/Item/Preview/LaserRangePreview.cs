using BalloonParty.Configuration.Items;
using BalloonParty.Shared;
using UnityEngine;

namespace BalloonParty.Item.Preview
{
    /// <summary>
    ///     Laser's telegraph: two crossing lines — the beam's axes, clipped to what's actually visible.
    /// </summary>
    /// <remarks>
    ///     <c>LaserCross</c> casts four arms along the rotated ±right/±up axes, each a swept circle of
    ///     <c>CircleCastRadius</c> travelling <c>RaycastDistance</c>. An occupant is caught when the
    ///     segment-to-centre distance is within the cast radius PLUS its own radius, and at 0.065 the
    ///     cast radius is negligible next to a balloon's — the catchment is dominated by the balloon
    ///     radius either way, so an axis line represents the beam at least as truthfully as the
    ///     0.13-wide rectangle it replaced, without implying a precision the number doesn't carry.
    ///     Opposite arms still share one corridor, so the four collapse into two lines rather than four.
    ///     <para>
    ///         Each half-arm is clipped to <see cref="ItemPreviewContext.Viewport" /> (the same shared
    ///         bounds <see cref="ItemPreviewTicker" /> culls pens against) rather than drawn the full
    ///         <c>RaycastDistance</c>: the real cast still runs the full distance — nothing is
    ///         under-promised — but nobody can see past the edge of the screen, so drawing the unclipped
    ///         40-unit corridor would mostly be off-screen and would starve the shared pen budget for no
    ///         visual gain. When the viewport is unavailable (no camera yet, or it isn't orthographic)
    ///         this falls back to <see cref="WallLimits" />: falling back to the raw
    ///         <c>RaycastDistance</c> instead would spike the derived dash count straight into
    ///         <c>MaxPens</c>, which is the exact problem the clipping exists to prevent.
    ///     </para>
    /// </remarks>
    internal sealed class LaserRangePreview : IItemRangePreview
    {
        private readonly IItemConfiguration _itemConfig;
        private readonly IProjectileFlightConfig _flightConfig;

        public ItemType Type => ItemType.Laser;

        internal LaserRangePreview(IItemConfiguration itemConfig, IProjectileFlightConfig flightConfig)
        {
            _itemConfig = itemConfig;
            _flightConfig = flightConfig;
        }

        public void BuildShape(in ItemPreviewContext context, ItemPreviewShape shape)
        {
            var settings = _itemConfig[ItemType.Laser];
            if (settings == null)
            {
                return;
            }

            var raycastDistance = settings.Laser.RaycastDistance;
            if (raycastDistance <= 0f)
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

            var origin = new Vector3(context.Origin.x, context.Origin.y, 0f);
            var viewport = context.Viewport;
            var walls = new WallLimits(_flightConfig.LimitsClockwise);

            AddAxisStroke(shape, context.Origin, origin, right, raycastDistance, viewport, walls);
            AddAxisStroke(shape, context.Origin, origin, up, raycastDistance, viewport, walls);
        }

        private static void AddAxisStroke(
            ItemPreviewShape shape, Vector2 origin2D, Vector3 origin3D, Vector2 axis, float raycastDistance,
            ItemPreviewViewport viewport, in WallLimits walls)
        {
            var direction = new Vector3(axis.x, axis.y, 0f);
            var plusLength = ClipHalfArm(origin3D, direction, raycastDistance, viewport, walls);
            var minusLength = ClipHalfArm(origin3D, -direction, raycastDistance, viewport, walls);

            shape.AddSegment(origin2D - (axis * minusLength), origin2D + (axis * plusLength));
        }

        // Each half-arm clips independently — a beam near one wall (or one screen edge) is short on
        // that side and long on the other. Prefers the viewport; falls back to the walls when it isn't
        // available, and to the full raycast distance when neither reports a crossing (a degenerate
        // direction, or an origin somehow outside the walls).
        private static float ClipHalfArm(
            Vector3 origin, Vector3 direction, float raycastDistance, ItemPreviewViewport viewport,
            in WallLimits walls)
        {
            if (viewport is { IsActive: true } && viewport.TryFindExit(origin, direction, out var exitDistance))
            {
                return Mathf.Min(raycastDistance, exitDistance);
            }

            if (!walls.TryFindCrossing(origin, direction, out var crossing, out _))
            {
                return raycastDistance;
            }

            return Mathf.Min(raycastDistance, Vector3.Distance(origin, crossing));
        }
    }
}
