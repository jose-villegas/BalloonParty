using UnityEngine;

namespace BalloonParty.Configuration
{
    [CreateAssetMenu(menuName = "Configuration/Disturbance Field Settings", fileName = "DisturbanceFieldSettings")]
    internal class DisturbanceFieldSettings : ScriptableObject
    {
        [Header("Resolution")]
        [Tooltip("Texels per world unit for the shared disturbance RT.")]
        [SerializeField] private int _texelsPerUnit = 8;

        [Header("Diffusion")]
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

        public int TexelsPerUnit => _texelsPerUnit;
        public float DiffusionRate => _diffusionRate;
        public float ReformSpeed => _reformSpeed;
        public float DiffusionTickInterval => _diffusionTickInterval;
        public float WindSpeed => _windSpeed;
        public float WindSmoothing => _windSmoothing;
        public float WindDecay => _windDecay;
        public float PressureStrength => _pressureStrength;
        public float DisplaceAmount => _displaceAmount;
        public float DisplaceDecay => _displaceDecay;
    }
}

