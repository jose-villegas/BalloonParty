namespace BalloonParty.Balloon.Formation
{
    /// <summary>
    ///     Unbreakables the board's formations have called in. The spawner takes them at each wave's first line,
    ///     right after the formations arrange, and adds them to that line past the level's own cap.
    /// </summary>
    internal interface IFormationReinforcements
    {
        /// <summary>Returns the calls made since the last take, and clears them.</summary>
        int TakeReinforcementCalls();
    }
}
