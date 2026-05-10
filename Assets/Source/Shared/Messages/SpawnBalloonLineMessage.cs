namespace BalloonParty.Shared.Messages
{
    public readonly struct SpawnBalloonLineMessage
    {
        public int LineCount { get; }

        public SpawnBalloonLineMessage(int lineCount = 1)
        {
            LineCount = lineCount;
        }
    }
}
