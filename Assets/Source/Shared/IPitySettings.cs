namespace BalloonParty.Shared
{
    /// <summary>
    ///     How a chance roll forgives a player in trouble (<c>Game/Assist/PityRoll</c>): each roll it misses while
    ///     the player is in need raises the next one, in proportion to that need, until a hit.
    /// </summary>
    public interface IPitySettings
    {
        /// <summary>
        ///     How much each roll missed at full need raises the chance, in multiples of the base chance: 4 takes a
        ///     1% chance to 5%, then 9%. A miss at half need counts half. 0 = no pity.
        /// </summary>
        float GrowthPerMiss { get; }

        /// <summary>The most pity can raise the chance to. A base chance above it is kept as it is.</summary>
        float MaxChance { get; }
    }
}
