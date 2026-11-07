namespace BalloonParty.Shared.Messages
{
    public readonly struct LevelUpDismissedMessage
    {
        /// <summary>The level-up dealt no cards, so no screen was shown: it passed in play.</summary>
        public readonly bool PassedInPlay;

        public LevelUpDismissedMessage(bool passedInPlay)
        {
            PassedInPlay = passedInPlay;
        }
    }
}
