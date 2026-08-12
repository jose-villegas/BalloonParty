using BalloonParty.Configuration;
using BalloonParty.Configuration.Items;
using UnityEngine;

namespace BalloonParty.Item.Preview
{
    /// <summary>
    ///     Bomb's telegraph: a circle at the blast's kill radius, centred on the host.
    /// </summary>
    /// <remarks>
    ///     Reads <c>BombSettings.Radius</c> — the same field <c>BombBlast</c> selects targets with — and
    ///     deliberately NOT <c>RainbowEffectScale</c>, which scales only the activation VFX and never the
    ///     kill radius (see <c>BombItemHandler.Activate</c>). A telegraph drawn at the visual scale would
    ///     promise reach the blast doesn't have.
    ///     <para>
    ///         The drawn circle is the radius alone, plus <see cref="IBombPreviewSettings.RadiusOffset" />
    ///         — a display-only nudge on top of the source of truth. <c>BombBlast</c> actually catches an
    ///         occupant whose centre lies within the radius plus its OWN radius, so the true catchment is
    ///         meaningfully larger than the bare-radius circle; a small positive offset reads CLOSER to
    ///         that real reach, not further from it. Revisit if players read the edge as exact.
    ///     </para>
    /// </remarks>
    internal sealed class BombRangePreview : IItemRangePreview
    {
        // Enough that the ribbon reads as a curve rather than a polygon at the radii Bomb ships with.
        private const int CircleSegments = 48;

        private readonly IItemConfiguration _itemConfig;
        private readonly IItemPreviewConfig _previewConfig;

        public ItemType Type => ItemType.Bomb;

        internal BombRangePreview(IItemConfiguration itemConfig, IItemPreviewConfig previewConfig)
        {
            _itemConfig = itemConfig;
            _previewConfig = previewConfig;
        }

        public void BuildShape(in ItemPreviewContext context, ItemPreviewShape shape)
        {
            var settings = _itemConfig[ItemType.Bomb];
            if (settings == null)
            {
                return;
            }

            // Clamped at the source, even though AddCircle already no-ops on radius <= 0 — a large
            // negative offset should read as "as tight as it gets", not silently draw nothing.
            var radius = Mathf.Max(0f, settings.Bomb.Radius + _previewConfig.Bomb.RadiusOffset);
            shape.AddCircle(context.Origin, radius, CircleSegments);
        }
    }
}
