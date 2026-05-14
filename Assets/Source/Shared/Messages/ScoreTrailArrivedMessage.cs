namespace BalloonParty.Shared.Messages
{
    public readonly struct ScoreTrailArrivedMessage
    {
        public readonly string ColorName;

        public ScoreTrailArrivedMessage(string colorName)
        {
            ColorName = colorName;
        }
    }
}

