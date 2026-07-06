namespace BalloonParty.Game.Level
{
    /// <summary>
    ///     The per-level score goal: how many points complete a given level. A cross-level query — it
    ///     answers for any level, not just the active one — composed from the base points curve and the
    ///     level's pacing threshold modifier.
    /// </summary>
    internal interface ILevelThresholds
    {
        int PointsRequiredForLevel(int level);
    }
}
