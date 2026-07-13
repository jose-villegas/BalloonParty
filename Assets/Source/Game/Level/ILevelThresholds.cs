namespace BalloonParty.Game.Level
{
    /// <summary>
    ///     The per-level score goal each colour must reach while playing that level — answers for any level, not
    ///     just the active one.
    /// </summary>
    internal interface ILevelThresholds
    {
        int PointsRequiredForLevel(int level);
    }
}
