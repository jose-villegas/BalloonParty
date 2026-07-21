namespace BalloonParty.Configuration.Effects
{
    internal interface IShieldFieldSettings
    {
        float DissolveSeconds { get; }
        float FinalDissolveSeconds { get; }
        float AppearSeconds { get; }
        int MaxVisualLayers { get; }
        float MaxVisualSpeed { get; }
        float NoiseSpringFrequency { get; }
        float NoiseSpringDamping { get; }
        float MorphCloseDistance { get; }
        float MorphCloseDuration { get; }
        float MorphOpenDuration { get; }
        float MorphBraceDuration { get; }
        float SquashFrequency { get; }
        float SquashDamping { get; }
        float SquashImpulseStrength { get; }
    }
}
