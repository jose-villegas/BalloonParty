using System;

namespace BalloonParty.Shared.GameState
{
    /// <summary>
    ///     Behavioural traits a cinematic declares (per state, in <c>CinematicsSettings</c>) and
    ///     consumers query through <see cref="ICinematicState.Has" /> — instead of one boolean property
    ///     per trait on the service, pattern-matched per state. Adding a cinematic means declaring its
    ///     traits in one settings entry; consumers never change.
    /// </summary>
    [Flags]
    internal enum CinematicTraits
    {
        None = 0,

        /// <summary>The 0-HP game-over must wait for the cinematic to finish (level-up).</summary>
        BlocksLoss = 1 << 0,

        /// <summary>
        ///     The cinematic hard-owns the camera, so the additive shake stands down (level-up).
        ///     The heart-drain pans too, but its shakes are part of the drama.
        /// </summary>
        BlocksShake = 1 << 1,
    }
}
