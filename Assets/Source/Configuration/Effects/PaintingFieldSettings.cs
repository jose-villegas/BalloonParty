using System;
using UnityEngine;

namespace BalloonParty.Configuration.Effects
{
    [Flags]
    internal enum PaintSource
    {
        ProjectileTrail = 1 << 0,
        ToughPop        = 1 << 1,
    }

    internal enum PaintColorMode
    {
        Dynamic,
        Palette,
        Custom,
    }

    [Serializable]
    internal struct PaintProfile
    {
        [Tooltip("Which sources use this profile. Flag multiple to share settings.")]
        public PaintSource Sources;

        public float Radius;

        [Range(0f, 1f)]
        public float Opacity;

        [Tooltip("Dynamic = caller provides the palette index at runtime; " +
                 "Palette = use the authored palette entry below; " +
                 "Custom = use the fixed RGB color below.")]
        public PaintColorMode ColorMode;

        [Tooltip("Palette entry name (used when ColorMode is Palette).")]
        public string PaletteColorName;

        [Tooltip("Fixed color (used when ColorMode is Custom).")]
        public Color CustomColor;
    }

    /// <summary>Authored tuning for the painting field; assign stamp/decay shaders + resolution here and
    /// wire the asset into <c>GameLifetimeScope</c>.</summary>
    [CreateAssetMenu(menuName = "Configuration/Painting Field Settings", fileName = "PaintingFieldSettings")]
    internal sealed class PaintingFieldSettings : ScriptableObject, IPaintingFieldSettings
    {
        private static readonly PaintProfile DefaultProfile = new()
        {
            Sources = 0,
            Radius = 0.15f,
            Opacity = 1f,
            ColorMode = PaintColorMode.Dynamic,
            PaletteColorName = "",
            CustomColor = Color.white
        };

        [Tooltip("Blit shader (BalloonParty/Display/PaintingFieldStamp) for batched color stamps.")]
        [SerializeField] private Shader _stampShader;

        [Tooltip("Blit shader (BalloonParty/Display/PaintingFieldDecay) for per-tick opacity decay.")]
        [SerializeField] private Shader _decayShader;

        [Tooltip("Painting-RT resolution per world unit.")]
        [SerializeField] private float _texelsPerUnit = 16f;

        [Tooltip("Opacity units lost per second (linear decay). Higher = faster fade.")]
        [SerializeField] private float _decayRate = 0.08f;

        [Tooltip("Seconds between decay blit ticks. 0 = every frame.")]
        [SerializeField] private float _decayTickInterval = 0.05f;

        [Tooltip("Base wind speed for smoke advection (world units/second).")]
        [SerializeField] private float _windSpeed = 0.4f;

        [Tooltip("0–1 base wind influence at normal projectile speed.")]
        [SerializeField] [Range(0f, 1f)] private float _windInfluence = 1f;

        [Tooltip("Power curve for age-based wind. Higher = fresh paint stays put longer before wind takes hold.")]
        [SerializeField] [Range(0.1f, 4f)] private float _windAgeBias = 1.5f;

        [Tooltip("Normalized wind direction for smoke advection.")]
        [SerializeField] private Vector2 _windDirection = new(0.3f, 0.1f);

        [Tooltip("Degrees the wind swings left/right from the base direction (0 = fixed).")]
        [SerializeField] [Range(0f, 90f)] private float _windSwingAngle = 15f;

        [Tooltip("How fast the wind swings back and forth (cycles per second).")]
        [SerializeField] [Range(0.01f, 2f)] private float _windSwingSpeed = 0.1f;

        [Header("Stamp Profiles")]
        [SerializeField] private PaintProfile[] _stampProfiles = new[]
        {
            new PaintProfile { Sources = PaintSource.ProjectileTrail, Radius = 0.15f, Opacity = 1f, ColorMode = PaintColorMode.Dynamic, PaletteColorName = "", CustomColor = Color.white },
            new PaintProfile { Sources = PaintSource.ToughPop, Radius = 0.35f, Opacity = 0.4f, ColorMode = PaintColorMode.Palette, PaletteColorName = "Tough", CustomColor = Color.white },
        };

        public Shader StampShader => _stampShader;
        public Shader DecayShader => _decayShader;
        public float TexelsPerUnit => _texelsPerUnit;
        public float DecayRate => _decayRate;
        public float DecayTickInterval => _decayTickInterval;
        public float WindSpeed => _windSpeed;
        public float WindInfluence => _windInfluence;
        public float WindAgeBias => _windAgeBias;
        public Vector2 WindDirection => _windDirection.normalized;
        public float WindSwingAngle => _windSwingAngle;
        public float WindSwingSpeed => _windSwingSpeed;

        PaintProfile IPaintingFieldSettings.GetProfile(PaintSource source)
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
