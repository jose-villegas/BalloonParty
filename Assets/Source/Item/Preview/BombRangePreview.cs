using BalloonParty.Configuration.Items;

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
    ///         The drawn circle is the radius alone. <c>BombBlast</c> actually catches an occupant whose
    ///         centre lies within the radius plus its OWN radius, so a balloon straddling the line still
    ///         pops — the outline reads as "what is inside dies", which is the useful lie. Revisit if
    ///         players read the edge as exact.
    ///     </para>
    /// </remarks>
    internal sealed class BombRangePreview : IItemRangePreview
    {
        // Enough that the ribbon reads as a curve rather than a polygon at the radii Bomb ships with.
        private const int CircleSegments = 48;

        private readonly IItemConfiguration _itemConfig;

        public ItemType Type => ItemType.Bomb;

        internal BombRangePreview(IItemConfiguration itemConfig)
        {
            _itemConfig = itemConfig;
        }

        public void BuildShape(in ItemPreviewContext context, ItemPreviewShape shape)
        {
            var settings = _itemConfig[ItemType.Bomb];
            if (settings == null)
            {
                return;
            }

            shape.AddCircle(context.Origin, settings.Bomb.Radius, CircleSegments);
        }
    }
}
