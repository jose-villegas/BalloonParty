namespace BalloonParty.Shared.Messages
{
    /// <summary>A retry bought with a rewarded ad was dealt its bonus card, for that attempt only.</summary>
    public readonly struct BonusCardDealtMessage
    {
        public readonly IUpgradeCardSettings Card;

        public BonusCardDealtMessage(IUpgradeCardSettings card)
        {
            Card = card;
        }
    }
}
