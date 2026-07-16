using BalloonParty.Projectile.View;

namespace BalloonParty.Thrower
{
    internal class ThrowerSettings
    {
        public readonly ProjectileView ProjectilePrefab;

        // Palette entry id tinting the prediction line's glow; the color itself is authored on the
        // palette asset's entry, so tuning it never touches code.
        public readonly string PredictionLightColor;

        public ThrowerSettings(ProjectileView projectilePrefab, string predictionLightColor)
        {
            ProjectilePrefab = projectilePrefab;
            PredictionLightColor = predictionLightColor;
        }
    }
}
