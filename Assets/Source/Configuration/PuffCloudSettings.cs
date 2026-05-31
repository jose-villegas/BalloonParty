using UnityEngine;

namespace BalloonParty.Configuration
{
    [CreateAssetMenu(menuName = "Configuration/Puff Cloud Settings", fileName = "PuffCloudSettings")]
    internal class PuffCloudSettings : ScriptableObject
    {

        [Header("Animation")]
        [Tooltip("Noise scroll speed multiplier. Drives _TimeOffset on the shader.")]
        [SerializeField] private float _animationSpeed = 0.8f;

        [Header("Density Field")]
        [Tooltip("Resolution per slot axis. Single-slot cloud = texelsPerSlot x texelsPerSlot.")]
        [SerializeField] private int _texelsPerSlot = 32;

        [Tooltip("Spatial blur rate per diffusion tick. Higher = faster spread from neighbors.")]
        [SerializeField] [Range(0f, 1f)] private float _diffusionRate = 0.3f;

        [Tooltip("Speed at which density trends back toward 1.0 (equilibrium).")]
        [SerializeField] [Range(0f, 0.5f)] private float _reformSpeed = 0.05f;

        [Tooltip("Seconds between diffusion blit passes.")]
        [SerializeField] [Range(0.016f, 0.2f)] private float _diffusionTickInterval = 0.05f;

        [Header("Wind")]
        [SerializeField] [Range(0f, 5f)] private float _windSpeed = 1.0f;
        [SerializeField] [Range(0.5f, 20f)] private float _windSmoothing = 6.0f;
        [SerializeField] [Range(0.5f, 10f)] private float _windDecay = 2.0f;
        [SerializeField] [Range(0f, 1f)] private float _pressureStrength = 0.4f;

        [Header("Displacement")]
        [SerializeField] [Range(0f, 1f)] private float _displaceAmount = 0.3f;
        [SerializeField] [Range(0f, 5f)] private float _displaceDecay = 1.5f;

        [Header("Visual")]
        [Tooltip("Extra world-space padding beyond the cluster bounding box.")]
        [SerializeField] private float _padding = 0.3f;

        [Tooltip("Sorting layer for cloud renderers.")]
        [SerializeField] private string _sortingLayerName = "Default";

        [Tooltip("Sorting order offset (relative to slot base order) to place clouds behind balloons.")]
        [SerializeField] private int _sortingOrderOffset = -1;

        [Header("Disturbance")]
        [SerializeField] private float _projectileRadius = 0.3f;
        [SerializeField] private float _projectileStrength = 0.8f;
        [SerializeField] private float _balloonRadius = 0.5f;
        [SerializeField] private float _balloonStrength = 0.4f;
        [SerializeField] private float _popBurstRadius = 0.8f;
        [SerializeField] private float _popBurstStrength = 1.0f;

        [Header("Merging")]
        [Tooltip("Whether to preserve existing density data when a cluster resizes.")]
        [SerializeField] private bool _preserveDensityOnResize = true;

        public float AnimationSpeed => _animationSpeed;
        public int TexelsPerSlot => _texelsPerSlot;
        public float DiffusionRate => _diffusionRate;
        public float ReformSpeed => _reformSpeed;
        public float DiffusionTickInterval => _diffusionTickInterval;
        public float WindSpeed => _windSpeed;
        public float WindSmoothing => _windSmoothing;
        public float WindDecay => _windDecay;
        public float PressureStrength => _pressureStrength;
        public float DisplaceAmount => _displaceAmount;
        public float DisplaceDecay => _displaceDecay;
        public float Padding => _padding;
        public string SortingLayerName => _sortingLayerName;
        public int SortingOrderOffset => _sortingOrderOffset;
        public float ProjectileRadius => _projectileRadius;
        public float ProjectileStrength => _projectileStrength;
        public float BalloonRadius => _balloonRadius;
        public float BalloonStrength => _balloonStrength;
        public float PopBurstRadius => _popBurstRadius;
        public float PopBurstStrength => _popBurstStrength;
        public bool PreserveDensityOnResize => _preserveDensityOnResize;
    }
}

