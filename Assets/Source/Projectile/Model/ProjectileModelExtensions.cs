namespace BalloonParty.Projectile.Model
{
    internal static class ProjectileModelExtensions
    {
        /// <summary>Ends the piercing state and the cruise that fed it — called at wall-discharge.</summary>
        public static void EndPierce(this IWriteableProjectileModel model)
        {
            model.EndCruise();
            model.IsPiercing.Value = false;
        }

        /// <summary>
        ///     Spends the pierce at a discharge. A banked Snipe charge re-arms the lance in the same breath:
        ///     the cruise that fed the old pierce still ends (a re-armed lance starts from base speed, so
        ///     the ramps never compound), but <c>IsPiercing</c> never dips — a dip would release the
        ///     level-up hold keyed off it (<c>LevelController</c>) mid-flight. Returns true when a charge
        ///     kept the shot armed; the charge itself is consumed by <c>SnipeItemHandler</c> on the
        ///     discharge message, which owns the grant it re-applies.
        /// </summary>
        public static bool SpendPierce(this IWriteableProjectileModel model)
        {
            var rearms = model.Flight.BankedPierceCharges > 0 || model.Flight.BankedRainbowPierceCharges > 0;
            if (rearms)
            {
                model.EndCruise();
                return true;
            }

            model.EndPierce();
            return false;
        }

        /// <summary>
        ///     Arms piercing (idempotently) and starts the acceleration into the tap count's new target,
        ///     anchored at the speed the shot is actually travelling right now — so arming, and every armed
        ///     tap after it, is continuous rather than a snap or a dip. Both rules that earn a tap (the
        ///     resolver's cruise bounce and the view's sweep) come through
        ///     <c>ProjectileTapResolver.TryGrantTap</c>.
        /// </summary>
        public static void ArmPierce(this IWriteableProjectileModel model)
        {
            model.BeginSpeedTransition(SpeedTransitionKind.ArmRamp, model.Flight.CurrentSpeed);
            model.IsPiercing.Value = true;
        }

        /// <summary>
        ///     Starts a tap's freeze-then-pickup beat — the same transition as the arm ramp, anchored at a
        ///     standstill instead. That anchor IS the beat: the shot drops and winds back up, which is how
        ///     earning a tap reads.
        /// </summary>
        public static void BeginTapBeat(this IWriteableProjectileModel model)
        {
            model.BeginSpeedTransition(SpeedTransitionKind.TapBeat, 0f);
        }

        /// <summary>Drops the cruise and the taps that fed it, returning the shot to base speed.</summary>
        public static void EndCruise(this IWriteableProjectileModel model)
        {
            model.Flight.ConsecutiveWallBounces = 0;
            model.Flight.TotalCruiseTaps = 0;

            // No target left to blend toward — leaving a transition running would keep easing against the
            // base speed the shot has already returned to, and would keep reporting a beat to feedback.
            model.BeginSpeedTransition(SpeedTransitionKind.None, 0f);
            model.IsCruising.Value = false;
        }

        private static void BeginSpeedTransition(
            this IWriteableProjectileModel model, SpeedTransitionKind kind, float fromSpeed)
        {
            model.Flight.TransitionKind = kind;
            model.Flight.TransitionFromSpeed = fromSpeed;
            model.Flight.TransitionElapsed = 0f;
        }
    }
}
