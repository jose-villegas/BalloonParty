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
    /// </remarks>
    internal sealed class PaintRangePreview : IItemRangePreview
    {
        private readonly IItemConfiguration _itemConfig;

        public ItemType Type => ItemType.Paint;

        internal PaintRangePreview(IItemConfiguration itemConfig)
        {
            _itemConfig = itemConfig;
        }

        public void BuildShape(in ItemPreviewContext context, ItemPreviewShape shape)
        {
            var settings = _itemConfig[ItemType.Paint];
            if (settings == null)
            {
                return;
            }

            var spread = new PaintSpreadParams(
                settings.Paint.SpreadOffset, settings.Paint.SpreadLength, settings.Paint.SpreadBaseWidth);
            var triangle = PaintTriangle.Build(context.Origin, context.AimDirection, spread);

            shape.BeginStroke();
            shape.AddPoint(triangle.Apex);
            shape.AddPoint(triangle.Left);
            shape.AddPoint(triangle.Right);
            shape.EndStroke(closed: true);
        }
    }
}
