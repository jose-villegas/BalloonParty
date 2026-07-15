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
        /// White = neutral, no look change.</summary>
        Color LightColor { get; }

        /// <summary>Scales the light's contribution (diffuse contrast, specular brightness).
        /// 1 = neutral, authored look.</summary>
        float Intensity { get; }
    }
}
