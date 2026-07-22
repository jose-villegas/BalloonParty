using UnityEngine;

namespace BalloonParty.Configuration.Effects
{
    /// <summary>Authored tuning for the painting field; assign stamp/decay shaders + resolution here and
    /// wire the asset into <c>GameLifetimeScope</c>.</summary>
    [CreateAssetMenu(menuName = "Configuration/Painting Field Settings", fileName = "PaintingFieldSettings")]
    internal sealed class PaintingFieldSettings : ScriptableObject, IPaintingFieldSettings
    {
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

        [Tooltip("World-space radius of each paint stamp from the projectile trail.")]
        [SerializeField] private float _stampRadius = 0.15f;

        [Tooltip("Base wind speed for smoke advection (world units/second).")]
        [SerializeField] private float _windSpeed = 0.4f;

        [Tooltip("0–1 base wind influence at normal projectile speed.")]
        [SerializeField] [Range(0f, 1f)] private float _windInfluence = 1f;

        [Tooltip("Power curve for age-based wind. Higher = fresh paint stays put longer before wind takes hold.")]
        [SerializeField] [Range(0.1f, 4f)] private float _windAgeBias = 1.5f;

        [Tooltip("Normalized wind direction for smoke advection.")]
        [SerializeField] private Vector2 _windDirection = new(0.3f, 0.1f);

        public Shader StampShader => _stampShader;
        public Shader DecayShader => _decayShader;
        public float TexelsPerUnit => _texelsPerUnit;
        public float DecayRate => _decayRate;
        public float DecayTickInterval => _decayTickInterval;
        public float StampRadius => _stampRadius;
        public float WindSpeed => _windSpeed;
        public float WindInfluence => _windInfluence;
        public float WindAgeBias => _windAgeBias;
        public Vector2 WindDirection => _windDirection.normalized;
    }
}
