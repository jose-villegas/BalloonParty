namespace BalloonParty.Shared.Messages
{
    /// <summary>
    ///     Published whenever the colour pop-streak state changes — grew, switched colour, broke, or reset.
    ///     A colour switch changes two colours at once (old drops to 0, new starts at 1), so consumers
    ///     should re-query <c>IColorStreak.GetStreak</c> for their own colour rather than trust the payload.
    /// </summary>
    public readonly struct StreakChangedMessage
    {
        public readonly string Color;
        public readonly int Streak;

        public StreakChangedMessage(string color, int streak)
        {
            Color = color;
            Streak = streak;
        }
    }
}
