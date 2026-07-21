using UnityEngine;

namespace BalloonParty.Configuration.Effects
{
    [CreateAssetMenu(menuName = "Configuration/Shield Field Settings", fileName = "ShieldFieldSettings")]
    internal class ShieldFieldSettings : ScriptableObject, IShieldFieldSettings
    {
        [Header("Animation")]
        [Tooltip("Duration in seconds for a shield layer to dissolve away.")]
        [SerializeField] [Range(0.1f, 2f)] private float _dissolveSeconds = 0.4f;

        [Tooltip("Duration in seconds for a new shield layer to appear.")]
        [SerializeField] [Range(0.1f, 2f)] private float _appearSeconds = 0.3f;

        [Header("Performance")]
        [Tooltip("Maximum number of visual layers rendered. Clamp for low-end GPUs.")]
        [SerializeField] [Range(1, 5)] private int _maxVisualLayers = 5;

        public float DissolveSeconds => _dissolveSeconds;
        public float AppearSeconds => _appearSeconds;
        public int MaxVisualLayers => _maxVisualLayers;
    }
}
