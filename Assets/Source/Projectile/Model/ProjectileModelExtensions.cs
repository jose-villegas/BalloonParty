namespace BalloonParty.Projectile.Model
{
    internal static class ProjectileModelExtensions
    {
        /// <summary>Ends the piercing state and the cruise that fed it — called at wall-discharge.</summary>
        public static void EndPierce(this IWriteableProjectileModel model)
        {
            model.Flight.ConsecutiveWallBounces = 0;
            model.Flight.TotalCruiseTaps = 0;
            model.IsCruising.Value = false;
            model.IsPiercing.Value = false;
        }
    }
}
