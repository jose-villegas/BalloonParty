namespace BalloonParty.Configuration.Effects
{
    internal interface IShieldFieldSettings
    {
        float DissolveSeconds { get; }
        float FinalDissolveSeconds { get; }
        float AppearSeconds { get; }
        int MaxVisualLayers { get; }
        float MaxVisualSpeed { get; }
        float SpringFrequency { get; }
        float SpringDamping { get; }
        float SpringFrequencySlow { get; }
        float SpringDampingSlow { get; }
        float LeanImpulseScale { get; }
        float NoiseSpringFrequency { get; }
        float NoiseSpringDamping { get; }
    }
}
