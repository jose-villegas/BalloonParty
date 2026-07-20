using UnityEngine;
using BalloonParty.Configuration.Effects;

namespace BalloonParty.Configuration.Effects
{
    internal interface IDisturbanceFieldSettings
    {
        int TexelsPerUnit { get; }
        float DiffusionRate { get; }
        float ReformSpeed { get; }

        /// <summary>How fast a stamped palette tag's in-slot life drains, per second (see the diffusion shader's A packing).</summary>
        float ColorTagDecay { get; }
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

        /// <summary>Scales the projectile's shield-loss wall-impact stamp (radius + strength) by its velocity,
        /// normalized 0 (base/non-cruising speed) → 1 (the cruise ramp's max) — see
        /// <see cref="BalloonParty.Shared.Disturbance.DisturbanceFieldService.StampShieldLoss"/>. Author y≈0
        /// at t=0 so a base-speed hit reproduces the un-scaled ProjectileImpact profile.</summary>
        AnimationCurve ShieldLossVelocityCurve { get; }

        StampProfile GetProfile(StampSource source);
    }
}
