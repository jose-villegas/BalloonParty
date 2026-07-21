namespace BalloonParty.Configuration.Effects
{
    internal interface IShieldFieldSettings
    {
        float DissolveSeconds { get; }
        float AppearSeconds { get; }
        int MaxVisualLayers { get; }
    }
}
