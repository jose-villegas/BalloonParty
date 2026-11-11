using BalloonParty.Shared;

namespace BalloonParty.Game.CardReveal
{
    /// <summary>One card to reveal: the card, how many of it the player now has, whether it's a retry's bonus
    /// card (for that attempt only) rather than one won at a level-up, and which baked pop shows it.</summary>
    internal sealed class RevealedCard
    {
        internal IUpgradeCardSettings Card { get; }
        internal int Stacks { get; }
        internal bool IsBonus { get; }
        internal int Pop { get; }

        internal RevealedCard(IUpgradeCardSettings card, int stacks, bool isBonus, int pop)
        {
            Card = card;
            Stacks = stacks;
            IsBonus = isBonus;
            Pop = pop;
        }
    }
}
