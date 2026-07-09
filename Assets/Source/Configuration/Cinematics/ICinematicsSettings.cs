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

        /// <summary>The Ascent's tuning — a transform-descent, so it lives outside the per-state camera-rig entries.</summary>
        LevelAscendSettings LevelAscend { get; }

        /// <summary>Tuning for the float-away board effect (level-clear balloons rise + zigzag + shrink).</summary>
        BoardFloatAwaySettings BoardFloatAway { get; }
    }
}
