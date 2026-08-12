using BalloonParty.Configuration.Items;
using UnityEngine;

namespace BalloonParty.Item.Preview
{
    /// <summary>
    ///     Shield's telegraph: a plus sign at the end of the prediction line.
    /// </summary>
    /// <remarks>
    ///     Shield is the one item with no board range at all — it grants the SHOT a life, so there is no
    ///     area to outline. Marking the aim line's end says "this shot gets to keep going" in the place the
    ///     player is already looking. Placeholder by intent (@ref plan_item_range_preview): if it reads as
    ///     a range rather than a bonus it is worse than drawing nothing.
    /// </remarks>
    internal sealed class ShieldRangePreview : IItemRangePreview
    {
        // World units — the arm half-length of the drawn plus. Tuning-pass material.
        private const float ArmLength = 0.45f;

        public ItemType Type => ItemType.Shield;

        public void BuildShape(in ItemPreviewContext context, ItemPreviewShape shape)
        {
            var points = context.TracePoints;
            if (points == null || points.Count == 0)
            {
                return;
            }

            var tip = points[points.Count - 1];
            var center = new Vector2(tip.x, tip.y);

            shape.AddSegment(center + (Vector2.left * ArmLength), center + (Vector2.right * ArmLength));
            shape.AddSegment(center + (Vector2.down * ArmLength), center + (Vector2.up * ArmLength));
        }
    }
}
