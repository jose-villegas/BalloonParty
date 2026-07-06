using BalloonParty.Shared.GameState;
using BalloonParty.Configuration.Cinematics;

namespace BalloonParty.Configuration.Cinematics
{
    /// <summary>
    ///     The single setup point for cinematics: one composed entry per <see cref="CinematicState" />
    ///     (traits + camera-rig segment + capability blocks) — see <c>Game/Cinematics/README.md</c> and
    ///     <c>Plans/PLAN-CinematicsArchitecture.md</c>.
    /// </summary>
    internal interface ICinematicsSettings
    {
        /// <summary>
        ///     The entry declared for <paramref name="state" />. Throws on a state with no declaration,
        ///     so a missing entry fails loudly (and in the EditMode test) instead of silently behaving
        ///     trait-less.
        /// </summary>
        CinematicStateEntry EntryOf(CinematicState state);
    }
}
