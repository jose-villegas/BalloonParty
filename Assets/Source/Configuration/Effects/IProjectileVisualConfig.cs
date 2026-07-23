namespace BalloonParty.Configuration.Effects
{
    /// <summary>
    /// Read-only visual tuning for the projectile: glow, pierce spiral, scene light, and flash FX.
    /// Extracted from ProjectileView [SerializeField] fields into a designer-editable SO.
    /// </summary>
    internal interface IProjectileVisualConfig
    {
        // Glow
        float GlowAlpha { get; }
        float GlowColorDuration { get; }
        float RainbowGlowSpeed { get; }

        // Pierce spiral
        float PierceFadeDuration { get; }
        float PierceFadeInPower { get; }
        float PierceFadeInReach { get; }
        float PierceTapBeatAlpha { get; }

        // Scene light
        float LightRadius { get; }
        float LightIntensity { get; }

        // Shield-loss flash
        float ShieldFlashIntensity { get; }
        float ShieldFlashRadius { get; }
        float ShieldFlashDuration { get; }

        // Pierce telegraph
        float PierceTelegraphHalfWidth { get; }
        float PierceTelegraphIntensity { get; }

        // Pierce spark
        float PierceSparkIntensity { get; }
        float PierceSparkRadius { get; }
        float PierceSparkDuration { get; }
    }
}
