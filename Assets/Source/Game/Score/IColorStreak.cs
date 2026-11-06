namespace BalloonParty.Game.Score
{
    /// <summary>
    ///     Read-only access to the current same-colour pop streak, for HUD feedback.
    /// </summary>
    internal interface IColorStreak
    {
        int GetStreak(string colorName);

        /// <summary>The highest streak of any colour among balls still in play.</summary>
        int HighestStreak { get; }
    }
}
