namespace BalloonParty.Game.Score
{
    /// <summary>
    ///     Read-only access to the current same-colour pop streak, for HUD feedback.
    /// </summary>
    internal interface IColorStreak
    {
        int GetStreak(string colorName);
    }
}
