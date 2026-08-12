using BalloonParty.Configuration;
using BalloonParty.Configuration.Items;
using UnityEngine;

namespace BalloonParty.Item.Preview
{
    /// <summary>
    ///     Shield's telegraph: a stub of the bounce the shot would survive — a short segment leaving the
    ///     aim line's end along the direction it would carry on in.
    /// </summary>
    /// <remarks>
    ///     Shield is the one item with no board range at all: it buys the shot a hit it would otherwise
    ///     die on. So the useful thing to show is not an area but the CONSEQUENCE — where the shot carries
    ///     on to. The aim line already stops at that contact (its reflection budget is spent), and this
    ///     picks up exactly there.
    ///     <para>
    ///         Works off whatever the line ran into — a wall OR a deflecting balloon. The normal comes
    ///         from <see cref="ItemPreviewContext.TraceEnd" />, which the trace calculator already solved
    ///         to stop there; deriving it here instead (testing the endpoint against the walls, hunting a
    ///         deflector circle near it) would be a second answer to a settled question, and got the
    ///         deflection case wrong outright by only ever checking walls.
    ///     </para>
    ///     <para>
    ///         Draws nothing when the line simply ran out of segments in open air — there is no contact,
    ///         so there is no bounce to promise.
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
            if (!context.TraceEnd.HasContact)
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
