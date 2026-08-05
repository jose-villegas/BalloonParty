using System;

namespace BalloonParty.Shared
{
    /// <summary>
    ///     The one place that says when a continued colour streak pays a projectile shield.
    /// </summary>
    /// <remarks>
    ///     The rule ran in two copies — the live hit resolver and the solver's flight simulation — and
    ///     they drifted: the solver went on granting streak shields after
    ///     <see cref="IRunConfig.StreakGrantsShields" /> turned them off, so its "best" shots relied on
    ///     shields the game no longer hands out. Kept behind the toggle rather than deleted, since
    ///     items-only shields is still an experiment.
    ///     <para>
    ///         Takes primitives so both callers can state their case: one reads live models and the
    ///         streak tracker, the other a simulated flight state that has no models at all.
    ///     </para>
    /// </remarks>
    internal static class StreakShieldRule
    {
        /// <summary>Pops needed before the streak starts paying — the pop that reaches this is the second.</summary>
        internal const int MinStreak = 2;

        internal static bool GrantsShield(
            bool enabled, bool projectilePop, bool targetHasColor, int streakCount, string streakColor,
            string projectileColor)
        {
            return enabled
                && projectilePop
                && targetHasColor
                && streakCount >= MinStreak
                && string.Equals(streakColor, projectileColor, StringComparison.Ordinal);
        }
    }
}
