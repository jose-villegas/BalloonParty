namespace BalloonParty.Configuration.CardReveal
{
    /// <summary>How the card reveal (<c>Game/CardReveal/</c>) chooses its balloon pop.</summary>
    internal interface ICardRevealSettings
    {
        /// <summary>How many baked pops there are to choose from, 1 or more.</summary>
        int PopCount { get; }

        /// <summary>How many of the last pops shown a new reveal never repeats, so a pop rarely comes back soon.</summary>
        int RecentPopsAvoided { get; }
    }
}
