using UnityEngine;

namespace BalloonParty.Configuration.Balloons
{
    /// <summary>Resolves a balloon's effective travel speed: its per-type value, or the config's
    /// fallback when that is 0, floored to a positive value so the distance ÷ speed duration never
    /// divides by zero.</summary>
    internal static class BalloonMoveSpeedExtensions
    {
        internal const float MinMoveSpeed = 0.01f;

        internal static float ResolveMoveSpeed(this IBalloonsConfiguration config, float typeMoveSpeed)
        {
            var speed = typeMoveSpeed > 0f ? typeMoveSpeed : config.DefaultBalloonMoveSpeed;
            return Mathf.Max(speed, MinMoveSpeed);
        }

        /// <summary>The resolved speed with a one-off ± <see cref="IBalloonsConfiguration.MoveSpeedVariation" />
        /// roll on top — call once per balloon at spawn and store the result on the model, so the balloon
        /// keeps one stable personal pace across spawn entry, balance settles, and shot-solver prediction
        /// (a fresh roll per move would desync the solver's mirrored timing).</summary>
        internal static float RollMoveSpeed(this IBalloonsConfiguration config, float typeMoveSpeed)
        {
            var variation = Mathf.Clamp01(config.MoveSpeedVariation);
            var speed = config.ResolveMoveSpeed(typeMoveSpeed) * (1f + Random.Range(-variation, variation));
            return Mathf.Max(speed, MinMoveSpeed);
        }
    }
}
