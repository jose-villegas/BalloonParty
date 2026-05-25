namespace BalloonParty.Slots.Capabilities
{
    /// <summary>One entry per color bar that should receive points on actor destruction.</summary>
    public readonly struct ScoreAttribution
    {
        public readonly string ColorId;
        public readonly int Points;
        public readonly bool BreaksStreak;

        public ScoreAttribution(string colorId, int points, bool breaksStreak = false)
        {
            ColorId = colorId;
            Points = points;
            BreaksStreak = breaksStreak;
        }
    }
}
