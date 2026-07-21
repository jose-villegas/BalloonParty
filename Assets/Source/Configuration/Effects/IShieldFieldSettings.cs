namespace BalloonParty.Configuration.Effects
{
    internal interface IShieldFieldSettings
    {
        float DissolveSeconds { get; }
        float FinalDissolveSeconds { get; }
        float AppearSeconds { get; }
        int MaxVisualLayers { get; }
        float MaxVisualSpeed { get; }
        float LeanImpulseScale { get; }
        float LeanCurve { get; }
        float LeanFrequency { get; }
        float LeanDamping { get; }
        float LeanStrengthY { get; }
        float NoiseSpringFrequency { get; }
        float NoiseSpringDamping { get; }
        float SquashStrength { get; }
        float SquashFrequency { get; }
        float SquashDamping { get; }
        float SquashImpulseScale { get; }
        float SquashRecoveryTau { get; }
        float SquashCurve { get; }
    }
}
