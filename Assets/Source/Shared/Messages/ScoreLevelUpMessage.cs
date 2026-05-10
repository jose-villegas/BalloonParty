namespace BalloonParty.Shared.Messages
{
    public readonly struct ScoreLevelUpMessage
    {
        public readonly int NewLevel;

        public ScoreLevelUpMessage(int newLevel)
        {
            NewLevel = newLevel;
        }
    }
}
