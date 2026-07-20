using UnityEngine;

namespace BalloonParty.Configuration.Effects
{
    /// <summary>Authored tuning for the shared cloud field; assign the blit material + resolution here and
    /// wire the asset into <c>GameLifetimeScope</c>.</summary>
    [CreateAssetMenu(menuName = "Configuration/Cloud Field Settings", fileName = "CloudFieldSettings")]
    internal sealed class CloudFieldSettings : ScriptableObject, ICloudFieldSettings
    {
        [Tooltip("Blit material using BalloonParty/Display/CloudFieldDensity — the cloud roll lives here.")]
        [SerializeField] private Material _densityMaterial;

        [Tooltip("Density-RT resolution per world unit.")]
        [SerializeField] private float _texelsPerUnit = 12f;

        [Tooltip("How much the scenario's Ascent/descent scrolls the clouds. 0 = ignore the transition; " +
            "sign flips the direction.")]
        [SerializeField] private float _transitionParallax = 0.5f;

        public Material DensityMaterial => _densityMaterial;
        public float TexelsPerUnit => _texelsPerUnit;
        public float TransitionParallax => _transitionParallax;
    }
}
