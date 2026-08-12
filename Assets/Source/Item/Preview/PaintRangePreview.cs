using BalloonParty.Configuration;
using BalloonParty.Configuration.Items;
using BalloonParty.Item.Paint;

namespace BalloonParty.Item.Preview
{
    /// <summary>
    ///     Paint's telegraph: the spread triangle, over the balloons it would repaint.
    /// </summary>
    /// <remarks>
    ///     Built by <c>PaintTriangle.Build</c> itself — the same call <c>PaintItemHandler</c> and the shot
    ///     solver's <c>ShotItemLayer</c> make — so the outline is the effect's own region rather than a
    ///     second derivation of it. Its own zero-direction fallback (aim straight up) covers a degenerate
    ///     aim for free.
    ///     <para>
    ///         The drawn figure is the triangle, not the packed blob discs inside it: blobs are how the
    ///         core BUCKETS occupants within the region, and outlining sixty-odd circles would read as
    ///         noise where the region reads as intent. A balloon just outside the edge can still be
    ///         bucketed to a blob that overlaps it, so the outline is indicative, not exact.
    ///     </para>
    ///     <para>
    ///         The drawn triangle is the spread alone, scaled by <see cref="IPaintPreviewSettings.Scale" /> —
    ///         a display-only multiplier on top of the source of truth, same rationale as Bomb's
    ///         <c>RadiusOffset</c>: <c>PaintSpread</c> buckets an occupant to its nearest packed blob within
    ///         <c>SpreadBlobRadius</c>, so a balloon just outside the bare outline can still be repainted,
    ///         and a scale below 1 draws tighter than that true reach. The scale is applied about the
    ///         triangle's own axis midpoint rather than its apex, so the figure shrinks in place instead
    ///         of sliding toward the host — see the apex-shift comment below for how.
    ///     </para>
    /// </remarks>
    internal sealed class PaintRangePreview : IItemRangePreview
    {
        private readonly IItemConfiguration _itemConfig;
        private readonly IItemPreviewConfig _previewConfig;

        public ItemType Type => ItemType.Paint;

        internal PaintRangePreview(IItemConfiguration itemConfig, IItemPreviewConfig previewConfig)
        {
            _itemConfig = itemConfig;
            _previewConfig = previewConfig;
        }

        public void BuildShape(in ItemPreviewContext context, ItemPreviewShape shape)
        {
            var settings = _itemConfig[ItemType.Paint];
            if (settings == null)
            {
                return;
            }

            var scale = _previewConfig.Paint.Scale;
            var spreadLength = settings.Paint.SpreadLength * scale;
            var baseWidth = settings.Paint.SpreadBaseWidth * scale;

            // Scale about the axis midpoint, not the apex: Build anchors the apex and grows the base away from
            // it, so a bare multiply drags the whole figure back toward the host as it shrinks. Shifting the
            // apex by half the length lost keeps the triangle where it was drawn and only makes it smaller.
            // SpreadLength carries its own sign (negative points the triangle backwards), and this term
            // inherits that sign unchanged, so the backwards case needs no special handling. At scale == 1
            // the term is exactly zero, so an unscaled figure is bit-identical to the unshifted apex.
            var offset = settings.Paint.SpreadOffset + (settings.Paint.SpreadLength * 0.5f * (1f - scale));
            var spread = new PaintSpreadParams(offset, spreadLength, baseWidth);
            var triangle = PaintTriangle.Build(context.Origin, context.AimDirection, spread);

            shape.BeginStroke();
            shape.AddPoint(triangle.Apex);
            shape.AddPoint(triangle.Left);
            shape.AddPoint(triangle.Right);
            shape.EndStroke(closed: true);
        }
    }
}
