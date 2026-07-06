using BalloonParty.Configuration.Level;

namespace BalloonParty.Game.Level
{
    /// <summary>
    ///     The single read surface for the live per-level difficulty mix. Runtime systems inject and
    ///     pull from this — never from <c>ILevelPacingConfiguration</c> or the catalog configs
    ///     directly, so there's exactly one source of truth for "what's active right now."
    ///     <see cref="Current" /> is the resolved level. The cross-level points-to-complete query lives
    ///     on <see cref="ILevelThresholds" />, not here — it's not a property of the active mix.
    /// </summary>
    internal interface IActiveLevelParameters
    {
        ILevelParameters Current { get; }
    }
}
