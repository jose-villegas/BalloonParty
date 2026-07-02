namespace BalloonParty.Game.Score
{
    /// <summary>
    ///     Read-only queries into per-colour level progress, for consumers (HUD bars, the level-up
    ///     cinematic) that observe scoring without driving it.
    /// </summary>
    internal interface IScoreQuery
    {
        int GetRequiredPoints();
        int GetProgress(string colorName);
        bool WillLevelUp();
    }
}
