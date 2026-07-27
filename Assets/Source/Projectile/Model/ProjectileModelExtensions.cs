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
        ///     Arms piercing and starts the one-shot ramp into the (from here on frozen) top speed,
        ///     anchored at <paramref name="fromSpeed" /> — the speed the shot is actually travelling at
        ///     this instant. Both paths that earn piercing through taps (the resolver's cruise bounce and
        ///     the view's sweep) go through here, so neither can arm without an anchor and leave the ramp
        ///     accelerating from a standstill.
        /// </summary>
        public static void ArmPierce(this IWriteableProjectileModel model, float fromSpeed)
        {
            model.Flight.PierceArmElapsed = 0f;
            model.Flight.PierceArmFromSpeed = fromSpeed;
            model.IsPiercing.Value = true;
        }

        /// <summary>Drops the cruise and the taps that fed it, returning the shot to base speed.</summary>
        public static void EndCruise(this IWriteableProjectileModel model)
        {
            model.Flight.ConsecutiveWallBounces = 0;
            model.Flight.TotalCruiseTaps = 0;
            model.IsCruising.Value = false;
        }
    }
}
