namespace BalloonParty.Shared.Messages
{
    /// <summary>
    ///     A retry bought with a rewarded ad was dealt its bonus card, for that attempt only. Stacks is how many of
    ///     that card the attempt plays with: the ones held, plus the bonus. Capped: the deal landed on a card
    ///     already at its limit, which breaks instead (Stacks is then the ones held); another message follows with
    ///     the card the attempt does get.
    /// </summary>
    public readonly struct BonusCardDealtMessage
    {
        public readonly IUpgradeCardSettings Card;
        public readonly int Stacks;
        public readonly bool Capped;

        public BonusCardDealtMessage(IUpgradeCardSettings card, int stacks, bool capped = false)
        {
            Card = card;
            Stacks = stacks;
            Capped = capped;
        }
    }
}
