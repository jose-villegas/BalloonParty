namespace BalloonParty.Shared
{
    /// <summary>
    ///     A card's title as the UI writes it: the card's hand-drawn icon, then its title. The icon is a sprite in
    ///     the <see cref="IconAssetName" /> TextMeshPro sprite asset, named by the card's <see cref="UpgradeKind" />,
    ///     and takes the text's colour, so it is tinted and faded with the words around it.
    /// </summary>
    internal static class UpgradeCardText
    {
        internal const string IconAssetName = "CardIcons";

        internal static string Title(IUpgradeCardSettings card)
        {
            return $"<sprite=\"{IconAssetName}\" name=\"{card.Kind}\" tint=1> {card.Title}";
        }
    }
}
