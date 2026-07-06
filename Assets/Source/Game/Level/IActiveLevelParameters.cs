using BalloonParty.Configuration.Level;

namespace BalloonParty.Game.Level
{
    /// <summary>
    ///     Single read surface for the live per-level difficulty mix; never read <c>ILevelPacingConfiguration</c> directly.
    /// </summary>
    internal interface IActiveLevelParameters
    {
        ILevelParameters Current { get; }
    }
}
