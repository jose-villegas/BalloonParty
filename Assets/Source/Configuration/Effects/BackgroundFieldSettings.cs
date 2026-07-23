using UnityEngine;

namespace BalloonParty.Configuration.Effects
{
    /// <summary>Authored tuning for the shared cloud field; assign the blit material + resolution here and
    /// wire the asset into <c>GameLifetimeScope</c>.</summary>
    [CreateAssetMenu(menuName = "Configuration/Background Field Settings", fileName = "BackgroundFieldSettings")]
    internal sealed class BackgroundFieldSettings : ScriptableObject, IBackgroundFieldSettings
    {
        [Tooltip("Blit material using BalloonParty/Display/BackgroundFieldDensity — the cloud roll lives here.")]
        [SerializeField] private Material _densityMaterial;

        [Tooltip("Display material (BalloonParty/Scenario/BackgroundCloud) on the backdrop SpriteRenderer. " +
            "Used to enable _LOW_QUALITY_CLOUD on mobile.")]
        [SerializeField] private Material _cloudDisplayMaterial;

        [Tooltip("Density-RT resolution per world unit.")]
        [SerializeField] private float _texelsPerUnit = 12f;

        [Tooltip("How much the scenario's Ascent/descent scrolls the clouds. 0 = ignore the transition; " +
            "sign flips the direction.")]
        [SerializeField] private float _transitionParallax = 0.5f;

        [Tooltip("Bake cadence: every N frames at 60 fps (1 = every frame, 3 = every 3rd). " +
            "The slow-scrolling density is imperceptible at higher rates on mobile.")]
        [SerializeField] private float _bakeFrameInterval = 3f;

        public Material DensityMaterial => _densityMaterial;
        public Material CloudDisplayMaterial => _cloudDisplayMaterial;
        public float TexelsPerUnit => _texelsPerUnit;
        public float TransitionParallax => _transitionParallax;
        public float BakeFrameInterval => _bakeFrameInterval;
    }
}
