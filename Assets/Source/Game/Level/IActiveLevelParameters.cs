using BalloonParty.Configuration.Level;

namespace BalloonParty.Game.Level
{
    /// <summary>
    ///     Single read surface for the live per-level difficulty mix; never read <c>ILevelPacingConfiguration</c> directly.
    /// </summary>
    internal interface IActiveLevelParameters
    {
        ILevelParameters Current { get; }

        /// <summary>
        ///     The level <see cref="Current" /> was resolved for. Ask this, and not the level in progress, for
        ///     anything that must match what <see cref="Current" /> says: the two are set by different messages, and
        ///     between a level-up and its dismissal <see cref="Current" /> is already the next level's.
        /// </summary>
        int Level { get; }
    }
}
