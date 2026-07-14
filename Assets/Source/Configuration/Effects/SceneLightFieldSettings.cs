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

        [Header("Direction")]
        [Tooltip("How strongly a light's local brightness bends the field direction toward it " +
                 "(weight = saturate(localR * this)). Higher = direction snaps to a local light sooner. " +
                 "A light's falloff shape is a per-light property, not here.")]
        [SerializeField] [Range(0.1f, 5f)] private float _directionResponse = 1f;

        public int TexelsPerUnit => _texelsPerUnit;
        public int MaxLights => _maxLights;
        public float AccumulationCeiling => _accumulationCeiling;
        public float DirectionResponse => _directionResponse;
    }
}
