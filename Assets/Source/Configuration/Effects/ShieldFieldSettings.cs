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

        [Header("Deformation")]
        [Tooltip("Projectile speed that maps to full ripple amplitude.")]
        [SerializeField] [Range(1f, 30f)] private float _maxVisualSpeed = 15f;

        [Tooltip("Fast spring frequency (dome region) — higher = quicker response.")]
        [SerializeField] [Range(1f, 8f)] private float _springFrequency = 4f;

        [Tooltip("Fast spring damping. 1 = critically damped, <1 = slight overshoot.")]
        [SerializeField] [Range(0.5f, 1.2f)] private float _springDamping = 0.9f;

        [Tooltip("Slow spring frequency (tail region) — lower = longer trail memory.")]
        [SerializeField] [Range(0.3f, 4f)] private float _springFrequencySlow = 1.2f;

        [Tooltip("Slow spring damping. Lower = more lag and overshoot on the tail.")]
        [SerializeField] [Range(0.3f, 1.2f)] private float _springDampingSlow = 0.6f;

        [Tooltip("Velocity impulse scale applied on direction change.")]
        [SerializeField] [Range(0.5f, 6f)] private float _leanImpulseScale = 1.5f;

        [Header("Noise Direction")]
        [Tooltip("Spring frequency for noise scroll direction — higher = faster tracking on direction change.")]
        [SerializeField] [Range(2f, 20f)] private float _noiseSpringFrequency = 10f;

        [Tooltip("Noise direction spring damping. 1 = critically damped, <1 = overshoot.")]
        [SerializeField] [Range(0.5f, 1.2f)] private float _noiseSpringDamping = 0.85f;

        [Header("Performance")]
        [Tooltip("Maximum number of visual layers rendered. Clamp for low-end GPUs.")]
        [SerializeField] [Range(1, 30)] private int _maxVisualLayers = 5;

        public float DissolveSeconds => _dissolveSeconds;
        public float FinalDissolveSeconds => _finalDissolveSeconds;
        public float AppearSeconds => _appearSeconds;
        public float MaxVisualSpeed => _maxVisualSpeed;
        public float SpringFrequency => _springFrequency;
        public float SpringDamping => _springDamping;
        public float SpringFrequencySlow => _springFrequencySlow;
        public float SpringDampingSlow => _springDampingSlow;
        public float LeanImpulseScale => _leanImpulseScale;
        public float NoiseSpringFrequency => _noiseSpringFrequency;
        public float NoiseSpringDamping => _noiseSpringDamping;
        public int MaxVisualLayers => _maxVisualLayers;
    }
}
