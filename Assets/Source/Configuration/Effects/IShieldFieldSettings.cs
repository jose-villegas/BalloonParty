namespace BalloonParty.Configuration.Effects
{
    internal interface IShieldFieldSettings
    {
        float BaseRadius { get; }
        float LayerSpacing { get; }
        float FieldLineThickness { get; }
        float GlowWidth { get; }
        float GlowIntensity { get; }
        float PulseSpeed { get; }
        float NoiseScale { get; }
        float DirectionalBias { get; }
        float DissolveSeconds { get; }
        float AppearSeconds { get; }
        float TintAlpha { get; }
        int MaxVisualLayers { get; }
    }
}
