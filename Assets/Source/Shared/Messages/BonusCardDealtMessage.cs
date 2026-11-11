namespace BalloonParty.Shared.Messages
{
    /// <summary>
    ///     A retry bought with a rewarded ad was dealt its bonus card, for that attempt only. Stacks is how many of
    ///     that card the attempt plays with: the ones held, plus the bonus.
    /// </summary>
    public readonly struct BonusCardDealtMessage
    {
        public readonly IUpgradeCardSettings Card;
        public readonly int Stacks;

        public BonusCardDealtMessage(IUpgradeCardSettings card, int stacks)
        {
            Card = card;
            Stacks = stacks;
        }
    }
}
