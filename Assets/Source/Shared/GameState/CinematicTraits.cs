using System;

namespace BalloonParty.Shared.GameState
{
    /// <summary>Per-state traits declared in <c>CinematicsSettings</c>, queried via <see cref="ICinematicState.Has" /> so consumers never enumerate states.</summary>
    [Flags]
    internal enum CinematicTraits
    {
        None = 0,

        /// <summary>0-HP game-over waits for the cinematic to finish (level-up).</summary>
        BlocksLoss = 1 << 0,

        /// <summary>Cinematic hard-owns the camera, so additive shake stands down (level-up; heart-drain shakes are exempt).</summary>
        BlocksShake = 1 << 1,
    }
}
