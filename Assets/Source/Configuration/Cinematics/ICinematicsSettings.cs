using BalloonParty.Shared.GameState;
using BalloonParty.Configuration.Cinematics;

namespace BalloonParty.Configuration.Cinematics
{
    /// <summary>
    ///     The single setup point for cinematics: one composed entry per <see cref="CinematicState" />.
    /// </summary>
    internal interface ICinematicsSettings
    {
        /// <summary>
        ///     Throws on a state with no declaration, so a missing entry fails loudly instead of silently behaving trait-less.
        /// </summary>
        CinematicStateEntry EntryOf(CinematicState state);
    }
}
