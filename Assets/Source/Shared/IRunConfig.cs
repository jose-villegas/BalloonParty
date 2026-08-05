namespace BalloonParty.Shared
{
    /// <summary>Run-level rules — starting hit points, retry allowance, and (future) loss thresholds.</summary>
    public interface IRunConfig
    {
        int StartingHitPoints { get; }

        /// <summary>How many times the player may retry at the same level per run (0 = disabled).</summary>
        int MaxRetries { get; }

        /// <summary>
        ///     Whether continuing a colour streak grants a projectile shield. Off makes items the only
        ///     source of shields — a balance experiment; see <c>Projectile/README.md</c>.
        /// </summary>
        bool StreakGrantsShields { get; }

        /// <summary>
        ///     Whether shields are placed along a flight the thrower can fly, rather than by the
        ///     plain weighted draw. Off restores the previous random placement.
        /// </summary>
        bool PlanShieldChains { get; }

        /// <summary>Tuning for that placement; unread while <see cref="PlanShieldChains" /> is off.</summary>
        IShieldChainSettings ShieldChain { get; }

        /// <summary>Seconds over which timeScale ramps from 1 to <see cref="LevelCompleteRampUpScale"/> after the completing flight ends.</summary>
        float LevelCompleteRampUpDuration { get; }

        /// <summary>Peak timeScale during the post-flight ramp-up (e.g. 2 = double speed).</summary>
        float LevelCompleteRampUpScale { get; }
    }
}
