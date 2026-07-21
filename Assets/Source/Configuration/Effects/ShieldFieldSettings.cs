using UnityEngine;

namespace BalloonParty.Configuration.Effects
{
    [CreateAssetMenu(menuName = "Configuration/Shield Field Settings", fileName = "ShieldFieldSettings")]
    internal class ShieldFieldSettings : ScriptableObject, IShieldFieldSettings
    {
        [Header("Geometry")]
        [Tooltip("Radius of the innermost field shell in UV space.")]
        [SerializeField] [Range(0.1f, 0.5f)] private float _baseRadius = 0.2f;

        [Tooltip("UV-space gap between successive shells.")]
        [SerializeField] [Range(0.02f, 0.15f)] private float _layerSpacing = 0.06f;

        [Header("Line Appearance")]
        [Tooltip("Half-width of the core field line strand.")]
        [SerializeField] [Range(0.002f, 0.05f)] private float _fieldLineThickness = 0.015f;

        [Tooltip("Falloff width for the additive glow around each strand.")]
        [SerializeField] [Range(0.01f, 0.2f)] private float _glowWidth = 0.06f;

        [Tooltip("Brightness multiplier for the glow halo.")]
        [SerializeField] [Range(0f, 3f)] private float _glowIntensity = 1.0f;

        [Tooltip("Speed of the sinusoidal pulse animating line brightness.")]
        [SerializeField] [Range(0f, 10f)] private float _pulseSpeed = 3.0f;

        [Header("Dissolve")]
        [Tooltip("Scale of the hash noise used for the dissolve pattern.")]
        [SerializeField] [Range(1f, 20f)] private float _noiseScale = 8.0f;

        [Tooltip("How strongly the dissolve sweeps from apex to base (0 = uniform, 1 = fully directional).")]
        [SerializeField] [Range(0f, 1f)] private float _directionalBias = 0.6f;

        [Tooltip("Duration in seconds for a shield layer to dissolve away.")]
        [SerializeField] [Range(0.1f, 2f)] private float _dissolveSeconds = 0.4f;

        [Tooltip("Duration in seconds for a new shield layer to appear.")]
        [SerializeField] [Range(0.1f, 2f)] private float _appearSeconds = 0.3f;

        [Header("Color")]
        [Tooltip("Alpha multiplier for the tint color applied to the field.")]
        [SerializeField] [Range(0f, 1f)] private float _tintAlpha = 0.8f;

        [Header("Performance")]
        [Tooltip("Maximum number of visual layers rendered. Clamp for low-end GPUs.")]
        [SerializeField] [Range(1, 5)] private int _maxVisualLayers = 5;

        public float BaseRadius => _baseRadius;
        public float LayerSpacing => _layerSpacing;
        public float FieldLineThickness => _fieldLineThickness;
        public float GlowWidth => _glowWidth;
        public float GlowIntensity => _glowIntensity;
        public float PulseSpeed => _pulseSpeed;
        public float NoiseScale => _noiseScale;
        public float DirectionalBias => _directionalBias;
        public float DissolveSeconds => _dissolveSeconds;
        public float AppearSeconds => _appearSeconds;
        public float TintAlpha => _tintAlpha;
        public int MaxVisualLayers => _maxVisualLayers;
    }
}
