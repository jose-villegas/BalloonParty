using BalloonParty.Shared.GameState;
using UnityEngine;

namespace BalloonParty.Configuration
{
    /// <summary>
    ///     The single setup point for cinematics: per-state behavioural traits and each camera-rig
    ///     cinematic's tuning block — see <c>Game/Cinematics/README.md</c> and
    ///     <c>Plans/PLAN-CinematicsArchitecture.md</c>.
    /// </summary>
    internal interface ICinematicsSettings
    {
        CameraRigCinematicSettings LevelUp { get; }
        CameraRigCinematicSettings HeartDrain { get; }

        /// <summary>Scale of the level-up tipping trail over its manual flight (pulses mid-flight).</summary>
        AnimationCurve LevelUpTrackedTrailScaleCurve { get; }

        /// <summary>
        ///     The <see cref="CinematicTraits" /> declared for <paramref name="state" />. Throws on a
        ///     state with no declaration, so a missing entry fails loudly (and in the EditMode test)
        ///     instead of silently behaving trait-less.
        /// </summary>
        CinematicTraits TraitsOf(CinematicState state);
    }
}
