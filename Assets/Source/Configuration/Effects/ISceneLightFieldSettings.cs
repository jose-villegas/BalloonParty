using UnityEngine;

namespace BalloonParty.Configuration.Effects
{
    /// <summary>Read-only tuning for the scene-light field (see @ref plan_lighting "Milestone 3").</summary>
    internal interface ISceneLightFieldSettings
    {
        /// <summary>The three field-pipeline shaders. Serialized (not <c>Shader.Find</c>) so a device build
        /// keeps them — Hidden shaders only reached by name are stripped otherwise.</summary>
        Shader FillShader { get; }
        Shader AccumulateShader { get; }
        Shader GradientShader { get; }

        /// <summary>Field RT density. Higher = smoother colour/light regions at a larger (but still tiny,
        /// dirty-gated) RT. Far finer than the disturbance field's, which ticks every frame.</summary>
        int TexelsPerUnit { get; }

        /// <summary>Max simultaneous lights composited in one accumulate batch. Capped by the accumulate
        /// shader's compile-time stamp array, so raising it past that needs a shader edit too.</summary>
        int MaxLights { get; }

        /// <summary>Ceiling on the summed magnitude boost overlapping lights can add above the rest
        /// (soft-clamped in the accumulate shader) so overlaps never blow R out.</summary>
        float AccumulationCeiling { get; }

        /// <summary>How strongly a light's local magnitude bends the field direction toward it:
        /// <c>weight = saturate(localR * DirectionResponse)</c>. Higher = the direction snaps to a local
        /// light at a lower brightness. (Falloff shape is now per-<see cref="BalloonParty.Shared.SceneLight"/>
        /// light, not global.)</summary>
        float DirectionResponse { get; }
    }
}
