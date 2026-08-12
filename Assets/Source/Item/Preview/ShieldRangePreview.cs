using BalloonParty.Configuration;
using BalloonParty.Configuration.Items;
using BalloonParty.Prediction;
using UnityEngine;

namespace BalloonParty.Item.Preview
{
    /// <summary>
    ///     Shield's telegraph: a stub of the WALL bounce the shot would survive — a short segment leaving
    ///     the aim line's end along the direction it would carry on in.
    /// </summary>
    /// <remarks>
    ///     Shield is the one item with no board range at all: it buys the shot a wall hit it would
    ///     otherwise die on. So the useful thing to show is not an area but the CONSEQUENCE — where the
    ///     shot carries on to. The aim line already stops at that contact (its reflection budget is
    ///     spent), and this picks up exactly there.
    ///     <para>
    ///         Walls ONLY, deliberately. A shield is spent on a wall and nothing else —
    ///         <c>ProjectileMotionResolver.Step</c> decrements <c>ShieldsRemaining</c> on a wall
    ///         reflection, while its <c>Deflect</c> never touches it — so a bounce off a balloon costs
    ///         the shot nothing and this item has no consequence to advertise there.
    ///     </para>
    ///     <para>
    ///         The normal comes from <see cref="ItemPreviewContext.TraceEnd" />, which the trace
    ///         calculator already solved to stop there; deriving it here (testing the endpoint against
    ///         the walls) would be a second answer to a settled question that can drift from it.
    ///     </para>
    /// </remarks>
    internal sealed class ShieldRangePreview : IItemRangePreview
    {
        private readonly IItemPreviewConfig _previewConfig;

        public ItemType Type => ItemType.Shield;

        internal ShieldRangePreview(IItemPreviewConfig previewConfig)
        {
            _previewConfig = previewConfig;
        }

        public void BuildShape(in ItemPreviewContext context, ItemPreviewShape shape)
        {
            if (context.TraceEnd.Kind != PredictionTraceEndKind.Wall)
            {
                return;
            }

            var points = context.TracePoints;
            if (points == null || points.Count < 2)
            {
                return;
            }

            var end = points[points.Count - 1];
            var incoming = end - points[points.Count - 2];
            if (incoming.sqrMagnitude < 1e-8f)
            {
                return;
            }

            var normal = context.TraceEnd.Normal;
            if (normal.sqrMagnitude < 1e-8f)
            {
                return;
            }

            // Normalized because a corner crossing sums two wall normals — reflecting about a non-unit
            // normal scales the outgoing heading instead of mirroring it (the same reason
            // PredictionTraceCalculator normalizes its own).
            var reflected = Vector2.Reflect(((Vector2)incoming).normalized, normal.normalized).normalized;
            shape.AddSegment(end, (Vector2)end + (reflected * _previewConfig.Shield.StubLength));
        }
    }
}
