namespace BalloonParty.Game.Score
{
    /// <summary>
    ///     Read-only queries into per-colour level progress, for HUD bars that display it without
    ///     driving the scoring itself.
    /// </summary>
    internal interface IScoreQuery
    {
        int GetRequiredPoints();
        int GetProgress(string colorName);
    }
}
