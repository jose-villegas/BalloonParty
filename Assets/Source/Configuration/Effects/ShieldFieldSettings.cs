using UnityEngine;

namespace BalloonParty.Configuration.Effects
{
    [CreateAssetMenu(menuName = "Configuration/Shield Field Settings", fileName = "ShieldFieldSettings")]
    internal class ShieldFieldSettings : ScriptableObject, IShieldFieldSettings
    {
        [Header("Animation")]
        [Tooltip("Duration in seconds for a shield layer to dissolve away.")]
        [SerializeField] [Range(0.1f, 2f)] private float _dissolveSeconds = 0.4f;

        [Tooltip("Duration in seconds for the last shield layer to dissolve (1 → 0).")]
        [SerializeField] [Range(0.1f, 5f)] private float _finalDissolveSeconds = 1.5f;

        [Tooltip("Duration in seconds for a new shield layer to appear.")]
        [SerializeField] [Range(0.1f, 2f)] private float _appearSeconds = 0.3f;

        [Header("Visual")]
        [Tooltip("Projectile speed that maps to full ripple amplitude.")]
        [SerializeField] [Range(1f, 30f)] private float _maxVisualSpeed = 15f;

        [Header("Noise Direction")]
        [Tooltip("Spring frequency for noise scroll direction — higher = faster tracking.")]
        [SerializeField] [Range(2f, 20f)] private float _noiseSpringFrequency = 10f;

        [Tooltip("Noise direction spring damping. 1 = critically damped, <1 = overshoot.")]
        [SerializeField] [Range(0.5f, 1.2f)] private float _noiseSpringDamping = 0.85f;

        [Header("Shape Morph")]
        [Tooltip("Distance to wall (world units) at which the shield starts closing to circle.")]
        [SerializeField] [Range(0.1f, 3f)] private float _morphCloseDistance = 1.0f;

        [Tooltip("Seconds to transition from tail to circle.")]
        [SerializeField] [Range(0.01f, 1f)] private float _morphCloseDuration = 0.15f;

        [Tooltip("Seconds to transition from circle back to tail after bounce.")]
        [SerializeField] [Range(0.01f, 1f)] private float _morphOpenDuration = 0.25f;

        [Tooltip("Seconds to hold full circle at bounce moment.")]
        [SerializeField] [Range(0f, 0.5f)] private float _morphBraceDuration = 0.05f;

        [Header("Performance")]
        [Tooltip("Maximum number of visual layers rendered.")]
        [SerializeField] [Range(1, 30)] private int _maxVisualLayers = 5;

        public float DissolveSeconds => _dissolveSeconds;
        public float FinalDissolveSeconds => _finalDissolveSeconds;
        public float AppearSeconds => _appearSeconds;
        public float MaxVisualSpeed => _maxVisualSpeed;
        public float NoiseSpringFrequency => _noiseSpringFrequency;
        public float NoiseSpringDamping => _noiseSpringDamping;
        public float MorphCloseDistance => _morphCloseDistance;
        public float MorphCloseDuration => _morphCloseDuration;
        public float MorphOpenDuration => _morphOpenDuration;
        public float MorphBraceDuration => _morphBraceDuration;
        public int MaxVisualLayers => _maxVisualLayers;
    }
}
