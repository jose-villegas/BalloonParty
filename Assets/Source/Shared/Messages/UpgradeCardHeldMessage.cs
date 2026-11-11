namespace BalloonParty.Shared.Messages
{
    /// <summary>
    ///     A level-up's card is now held: published as the level advances, when its pick is kept. Stacks is how
    ///     many of that card the run holds with this one.
    /// </summary>
    public readonly struct UpgradeCardHeldMessage
    {
        public readonly IUpgradeCardSettings Card;
        public readonly int Stacks;

        public UpgradeCardHeldMessage(IUpgradeCardSettings card, int stacks)
        {
            Card = card;
            Stacks = stacks;
        }
    }
}
