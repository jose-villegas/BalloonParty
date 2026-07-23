using DG.Tweening;
using UnityEngine;

namespace BalloonParty.Configuration.Effects
{
    [CreateAssetMenu(menuName = "Configuration/Projectile Visual Config", fileName = "ProjectileVisualConfig")]
    internal class ProjectileVisualConfig : ScriptableObject, IProjectileVisualConfig
    {
        [Header("Glow")]
        [SerializeField] [Range(0f, 1f)] private float _glowAlpha = 0.5f;
        [SerializeField] [Min(0f)] private float _glowColorDuration = 1.25f;
        [Tooltip("Full palette loops per second the glow cycles through while the rainbow buff is active.")]
        [SerializeField] [Min(0f)] private float _rainbowGlowSpeed = 0.35f;

        [Header("Death")]
        [SerializeField] private float _projectileDisappearDuration = 0.2f;
        [SerializeField] private Ease _projectileDisappearEase = Ease.InBack;
        [Tooltip("How far a dead shot drifts along its heading as it shrinks, as a multiple of speed×duration. 0 = stop in place.")]
        [SerializeField] private float _projectileDeadDriftFactor = 1f;

        [Header("Pierce Spiral")]
        [Tooltip("Seconds for the spiral to lerp in/out when the piercing state flips.")]
        [SerializeField] [Min(0f)] private float _pierceFadeDuration = 0.35f;
        [Tooltip("Power curve exponent for distance-based fade-in (< 1 = faster roll-in).")]
        [SerializeField] [Range(0.1f, 1f)] private float _pierceFadeInPower = 0.45f;
        [Tooltip("Fraction of the segment distance at which the aura reaches full alpha.")]
        [SerializeField] [Range(0.3f, 1f)] private float _pierceFadeInReach = 0.75f;
        [Tooltip("How far the piercing aura ducks during each cruise tap beat (0 = off, 1 = full).")]
        [SerializeField] [Range(0f, 1f)] private float _pierceTapBeatAlpha = 0.1f;

        [Header("Scene Light")]
        [Tooltip("Radius of the light this shot casts into the scene-light field.")]
        [SerializeField] [Min(0f)] private float _lightRadius = 1.3f;
        [SerializeField] [Min(0f)] private float _lightIntensity = 0.65f;
        [Tooltip("Light radius when shields reach the visual layer cap; values at or below LightRadius disable the ramp.")]
        [SerializeField] [Min(0f)] private float _maxShieldsLightRadius = 0f;

        [Header("Shield-Loss Flash")]
        [Tooltip("A brief sparks-colour light popped at the wall each time a bounce spends a shield.")]
        [SerializeField] [Min(0f)] private float _shieldFlashIntensity = 3f;
        [SerializeField] [Min(0f)] private float _shieldFlashRadius = 1.3f;
        [SerializeField] [Min(0f)] private float _shieldFlashDuration = 0.1f;

        [Header("Pierce Telegraph")]
        [Tooltip("While piercing toward a tough, the shot's light stretches into an area line — its half-width.")]
        [SerializeField] [Min(0f)] private float _pierceTelegraphHalfWidth = 0.4f;
        [SerializeField] [Min(0f)] private float _pierceTelegraphIntensity = 2f;

        [Header("Pierce Spark")]
        [Tooltip("A brief sparks-colour light popped at each tough the piercing shot plows through.")]
        [SerializeField] [Min(0f)] private float _pierceSparkIntensity = 2.5f;
        [SerializeField] [Min(0f)] private float _pierceSparkRadius = 0.7f;
        [SerializeField] [Min(0f)] private float _pierceSparkDuration = 0.12f;

        public float GlowAlpha => _glowAlpha;
        public float GlowColorDuration => _glowColorDuration;
        public float RainbowGlowSpeed => _rainbowGlowSpeed;
        public float ProjectileDisappearDuration => _projectileDisappearDuration;
        public Ease ProjectileDisappearEase => _projectileDisappearEase;
        public float ProjectileDeadDriftFactor => _projectileDeadDriftFactor;
        public float PierceFadeDuration => _pierceFadeDuration;
        public float PierceFadeInPower => _pierceFadeInPower;
        public float PierceFadeInReach => _pierceFadeInReach;
        public float PierceTapBeatAlpha => _pierceTapBeatAlpha;
        public float LightRadius => _lightRadius;
        public float LightIntensity => _lightIntensity;
        public float MaxShieldsLightRadius => _maxShieldsLightRadius;
        public float ShieldFlashIntensity => _shieldFlashIntensity;
        public float ShieldFlashRadius => _shieldFlashRadius;
        public float ShieldFlashDuration => _shieldFlashDuration;
        public float PierceTelegraphHalfWidth => _pierceTelegraphHalfWidth;
        public float PierceTelegraphIntensity => _pierceTelegraphIntensity;
        public float PierceSparkIntensity => _pierceSparkIntensity;
        public float PierceSparkRadius => _pierceSparkRadius;
        public float PierceSparkDuration => _pierceSparkDuration;
    }
}
