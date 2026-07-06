using System;
using UnityEngine;
using BalloonParty.Configuration.Effects;

namespace BalloonParty.Configuration.Effects
{
    [Flags]
    internal enum StampSource
    {
        Projectile    = 1 << 0,
        BalloonPath  = 1 << 1,
        BalloonPop    = 1 << 2,
        Bomb          = 1 << 3,
        Laser         = 1 << 4,
        Paint         = 1 << 5,
    }

    [Serializable]
    internal struct StampProfile
    {
        [Tooltip("Which sources use this profile. Flag multiple to share settings.")]
        public StampSource Sources;

        public float Radius;
        public float Strength;

        [Tooltip("Duration over which the stamp ramps up. 0 = instant.")]
        [Range(0f, 0.5f)]
        public float Duration;
    }

    [CreateAssetMenu(menuName = "Configuration/Disturbance Field Settings", fileName = "DisturbanceFieldSettings")]
    internal class DisturbanceFieldSettings : ScriptableObject, IDisturbanceFieldSettings
    {
        private static readonly StampProfile DefaultProfile = new()
        {
            Sources = 0,
            Radius = 0.3f,
            Strength = 0.5f,
            Duration = 0f
        };

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

        [Header("Performance")]
        [Tooltip("Minimum strength for a stamp to be processed. Below this the stamp is discarded.")]
        [SerializeField] [Range(0f, 0.1f)] private float _minStampStrength = 0.01f;

        [Tooltip("Maximum number of lerp stamps active at once. Oldest are evicted when full.")]
        [SerializeField] [Range(4, 64)] private int _maxLerpStamps = 32;

        [Header("Shaders")]
        [Tooltip("Diffusion blit shader. Must be assigned — Shader.Find strips Hidden shaders from builds.")]
        [SerializeField] private Shader _diffusionShader;

        [Tooltip("Batched stamp blit shader. Must be assigned — Shader.Find strips Hidden shaders from builds.")]
        [SerializeField] private Shader _stampBatchedShader;

        [Header("Stamp Profiles")]
        [SerializeField] private StampProfile[] _stampProfiles = new[]
        {
            new StampProfile { Sources = StampSource.Projectile,   Radius = 0.3f, Strength = 0.8f, Duration = 0f },
            new StampProfile { Sources = StampSource.BalloonPath, Radius = 0.5f, Strength = 0.4f, Duration = 0f },
            new StampProfile { Sources = StampSource.BalloonPop,   Radius = 0.8f, Strength = 1.0f, Duration = 0.15f },
            new StampProfile { Sources = StampSource.Bomb,         Radius = 1.2f, Strength = 1.0f, Duration = 0.2f },
            new StampProfile { Sources = StampSource.Laser,        Radius = 0.4f, Strength = 0.6f, Duration = 0.1f },
            new StampProfile { Sources = StampSource.Paint,        Radius = 0.6f, Strength = 0.5f, Duration = 0.1f },
        };

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
        public float MinStampStrength => _minStampStrength;
        public int MaxLerpStamps => _maxLerpStamps;
        public Shader DiffusionShader => _diffusionShader;
        public Shader StampBatchedShader => _stampBatchedShader;

        StampProfile IDisturbanceFieldSettings.GetProfile(StampSource source)
        {
            foreach (var profile in _stampProfiles)
            {
                if ((profile.Sources & source) != 0)
                {
                    return profile;
                }
            }

            return DefaultProfile;
        }
    }
}
