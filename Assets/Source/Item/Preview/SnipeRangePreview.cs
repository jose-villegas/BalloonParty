using BalloonParty.Configuration.Items;
using BalloonParty.Shared;
using UnityEngine;

namespace BalloonParty.Item.Preview
{
    /// <summary>
    ///     Snipe's telegraph: the pierce corridor — one line from the host to the wall the shot would run
    ///     to. Snipe arms piercing, so what it grants is reach, and reach is what this draws.
    /// </summary>
    /// <remarks>
    ///     Deliberately traced from the host along the aim heading rather than copied out of the aim
    ///     polyline past its pierce marker: the two agree (<c>PredictionTraceCalculator</c> stops deflecting
    ///     at exactly this host), but the polyline stops at <c>MaxSegments</c>/<c>MaxReflections</c> —
    ///     telegraph budget, not flight — so reading it would cut the corridor short of the wall the shot
    ///     really reaches.
    /// </remarks>
    internal sealed class SnipeRangePreview : IItemRangePreview
    {
        private readonly IProjectileFlightConfig _flightConfig;

        public ItemType Type => ItemType.Snipe;

        internal SnipeRangePreview(IProjectileFlightConfig flightConfig)
        {
            _flightConfig = flightConfig;
        }

        public void BuildShape(in ItemPreviewContext context, ItemPreviewShape shape)
        {
            if (context.AimDirection.sqrMagnitude < 1e-8f)
            {
                return;
            }

            var origin = new Vector3(context.Origin.x, context.Origin.y, 0f);
            var direction = new Vector3(context.AimDirection.x, context.AimDirection.y, 0f).normalized;

            var walls = new WallLimits(_flightConfig.LimitsClockwise);
            if (!walls.TryFindCrossing(origin, direction, out var crossing, out _))
            {
                return;
            }

            shape.AddSegment(context.Origin, new Vector2(crossing.x, crossing.y));
        }
    }
}
