namespace BalloonParty.Shared.Messages
{
    public readonly struct GameOverMessage
    {
        public readonly int FinalLevel;
        public readonly int FinalScore;

        /// <summary>
        ///     The loss is one from a saved run, brought back on launch so the game-over screen and its offer
        ///     return: it was already recorded, scored and sounded when it happened, so its beat isn't played again.
        /// </summary>
        public readonly bool Restored;

        public GameOverMessage(int finalLevel, int finalScore, bool restored = false)
        {
            FinalLevel = finalLevel;
            FinalScore = finalScore;
            Restored = restored;
        }
    }
}
