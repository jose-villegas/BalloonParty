using UnityEngine;

namespace BalloonParty.Configuration.Effects
{
    [CreateAssetMenu(menuName = "Configuration/Scene Light Field Settings", fileName = "SceneLightFieldSettings")]
    internal class SceneLightFieldSettings : ScriptableObject, ISceneLightFieldSettings
    {
        [Header("Resolution")]
        [Tooltip("Field RT density (texels per world unit). Higher = smoother colour/light regions; the RT " +
                 "stays small and only re-renders when a light or the owner changes, so this can be far " +
                 "finer than the disturbance field's 8.")]
        [SerializeField] [Range(8, 64)] private int _texelsPerUnit = 32;

        [Header("Lights")]
        [Tooltip("Max simultaneous lights composited per accumulate batch. Capped by the accumulate " +
                 "shader's MAX_STAMPS (32) — raising it past that also needs a shader edit.")]
        [SerializeField] [Range(1, 32)] private int _maxLights = 32;

        [Tooltip("Ceiling on the summed magnitude boost overlapping lights add above the ambient rest " +
                 "(soft-clamped), so overlaps never blow the field's brightness out.")]
        [SerializeField] [Range(1f, 6f)] private float _accumulationCeiling = 3f;

        [Header("Falloff")]
        [Tooltip("Radial falloff exponent (1 - dist/radius)^power. 1 = linear cone; higher pulls the light " +
                 "in toward its centre with a longer dim tail. Shapes magnitude and how sharply the " +
                 "direction points at the light.")]
        [SerializeField] [Range(0.5f, 8f)] private float _falloffPower = 2f;

        [Tooltip("|grad R| where the direction begins bending from the global light toward a local one.")]
        [SerializeField] [Range(0f, 0.2f)] private float _directionOnset = 0.002f;
        [Tooltip("|grad R| where the direction is fully the local light's. Larger band = softer capture.")]
        [SerializeField] [Range(0f, 0.5f)] private float _directionFull = 0.05f;

        public int TexelsPerUnit => _texelsPerUnit;
        public int MaxLights => _maxLights;
        public float AccumulationCeiling => _accumulationCeiling;
        public float FalloffPower => _falloffPower;
        public float DirectionOnset => _directionOnset;
        public float DirectionFull => _directionFull;
    }
}
