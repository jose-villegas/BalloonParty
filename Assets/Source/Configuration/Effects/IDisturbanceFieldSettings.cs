using UnityEngine;
using BalloonParty.Configuration.Effects;

namespace BalloonParty.Configuration.Effects
{
    internal interface IDisturbanceFieldSettings
    {
        int TexelsPerUnit { get; }
        float DiffusionRate { get; }
        float ReformSpeed { get; }
        float DiffusionTickInterval { get; }
        float WindSpeed { get; }
        float WindSmoothing { get; }
        float WindDecay { get; }
        float PressureStrength { get; }
        float DisplaceAmount { get; }
        float DisplaceDecay { get; }
        float MinStampStrength { get; }
        int MaxLerpStamps { get; }
        Shader DiffusionShader { get; }
        Shader StampBatchedShader { get; }
        StampProfile GetProfile(StampSource source);
    }
}
