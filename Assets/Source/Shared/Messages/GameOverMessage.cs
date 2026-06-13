namespace BalloonParty.Shared.Messages
{
    public readonly struct GameOverMessage
    {
        public readonly int FinalLevel;
        public readonly int FinalScore;

        public GameOverMessage(int finalLevel, int finalScore)
        {
            FinalLevel = finalLevel;
            FinalScore = finalScore;
        }
    }
}
