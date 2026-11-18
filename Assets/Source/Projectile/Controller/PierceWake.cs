using BalloonParty.Shared;
using BalloonParty.Shared.Disturbance;
using UnityEngine;

namespace BalloonParty.Projectile.Controller
{
    /// <summary>
    ///     What a piercing shot in flight (a Snipe lance, or a cruise-earned pierce: the same state on the shot)
    ///     reports to the impact bus beside an ordinary ball's stamps, for the water. Along every stretch it flies,
    ///     two blasts that run the stretch in the step's time: a wide, weak one whose shallow furrow throws a wave
    ///     out to both sides, and a narrow, strong one that bares a thin sliver of floor under the shot. One blast
    ///     can't do both, since a furrow deep enough to bare the floor bares most of its width. Stretch after
    ///     stretch they make one wake, from where the shot was armed to the wall its charge ends at. There the
    ///     wall stamp every shot already leaves (<c>ProjectileView</c>'s <c>ProjectileImpact</c> stamp) carries one
    ///     small round blast (<see cref="WallBlast" />), the wake's full stop. Nothing but the water answers a
    ///     blast.
    /// </summary>
    internal static class PierceWake
    {
        /// <summary>
        ///     Reports the wake of one fixed step of a shot's flight, from <paramref name="from" /> to where
        ///     <paramref name="step" /> left it. <paramref name="wasPiercing" /> is the shot's state as the step
        ///     began and <paramref name="stillPiercing" /> once the wall it reached (if any) has been resolved: the
        ///     way to a wall is the lance's it flew in as, the way on from it only a lance's still armed.
        /// </summary>
        internal static void Report(
            DisturbanceFieldService field, IProjectileFlightConfig config, Vector3 from, in ProjectileStep step,
            bool wasPiercing, bool stillPiercing, float stepSeconds)
        {
            if (step.Outcome == ProjectileStepOutcome.Moved)
            {
                if (stillPiercing)
                {
                    ReportStretch(field, config, from, step.Position, stepSeconds);
                }

                return;
            }

            if (wasPiercing)
            {
                ReportStretch(field, config, from, step.WallContact, stepSeconds);
            }

            if (step.Outcome == ProjectileStepOutcome.Bounced && stillPiercing)
            {
                ReportStretch(field, config, step.WallContact, step.Position, stepSeconds);
            }
        }

        /// <summary>
        ///     The blast the wall stamp carries when a shot meets a wall: the small crater that ends a lance's wake,
        ///     where its charge ends. That is the wall a lance dies at, or one it reaches having plowed toughs
        ///     (<paramref name="pendingPlows" />), which shatter there, or having lost its pierce on the way
        ///     (<paramref name="stillPiercing" /> false). None for an ordinary ball, and none for a lance that only
        ///     bounces, still armed with nothing plowed: its wake turns with it.
        /// </summary>
        internal static ImpactEventBus.Blast WallBlast(
            IProjectileFlightConfig config, ProjectileStepOutcome outcome, bool wasPiercing, bool stillPiercing,
            int pendingPlows)
        {
            if (!wasPiercing || outcome == ProjectileStepOutcome.Moved)
            {
                return default;
            }

            var ends = outcome == ProjectileStepOutcome.Destroyed || pendingPlows > 0 || !stillPiercing;
            return ends
                ? new ImpactEventBus.Blast(
                    config.PierceWallImpactRadius, config.PierceWallImpactStrength, config.PierceWallImpactSeconds)
                : default;
        }

        // A stretch of no length reports nothing: a shot held still parts no water.
        private static void ReportStretch(
            DisturbanceFieldService field, IProjectileFlightConfig config, Vector3 from, Vector3 to, float seconds)
        {
            Vector2 stretch = to - from;
            if (stretch.sqrMagnitude < 1e-8f)
            {
                return;
            }

            var middle = (from + to) * 0.5f;
            field.ReportBlast(
                middle,
                new ImpactEventBus.Blast(
                    config.PierceWakeWaveRadius, config.PierceWakeWaveStrength, seconds, stretch));
            field.ReportBlast(
                middle,
                new ImpactEventBus.Blast(
                    config.PierceWakeHeartRadius, config.PierceWakeHeartStrength, seconds, stretch));
        }
    }
}
