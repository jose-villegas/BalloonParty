using UnityEngine;

namespace BalloonParty.Configuration.Effects
{
    /// <summary>Read-only ambient/main scene-light configuration — direction, colour, intensity.
    /// Replaces the former per-scene <c>SceneLightService</c> MonoBehaviour (see @ref plan_lighting).
    /// The globals are now pushed by <see cref="BalloonParty.Shared.SceneLight.SceneLightFieldService"/>.</summary>
    internal interface ISceneLightSettings
    {
        /// <summary>Normalized toward-the-light vector (screen/world XY, +y up). Shadows extend the
        /// opposite way; the canonical direction is upper-left (−0.707, 0.707).</summary>
        Vector2 LightDirection { get; }

        /// <summary>The main light's colour tint — multiplies into every specular/diffuse consumer.
        /// White = neutral, no look change. May be a solid authored tint or, in the night-mode
        /// day/night source, sampled from a full-circle gradient indexed by <see cref="LightDirection"/>;
        /// which one is an implementation detail of the settings asset.</summary>
        Color LightColor { get; }

        /// <summary>Scales the light's contribution (diffuse contrast, specular brightness).
        /// 1 = neutral, authored look.</summary>
        float Intensity { get; }

        /// <summary>Scales the projectile's shield-loss light flash (radius + intensity) by its velocity,
        /// normalized 0 (base/non-cruising speed) → 1 (the cruise ramp's max) — see
        /// <see cref="BalloonParty.Projectile.View.ProjectileView"/>'s shield-loss flash. Author y≈0 at t=0
        /// so a base-speed hit reproduces the flash's un-scaled authored radius/intensity.</summary>
        AnimationCurve ShieldLossLightVelocityCurve { get; }
    }
}
