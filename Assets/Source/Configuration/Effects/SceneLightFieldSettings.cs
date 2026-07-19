using BalloonParty.Shared;
using UnityEngine;

namespace BalloonParty.Configuration.Effects
{
    [CreateAssetMenu(menuName = "Configuration/Scene Light Settings", fileName = "SceneLightFieldSettings")]
    internal class SceneLightFieldSettings : ScriptableObject, ISceneLightFieldSettings, IScreenSpaceLightSettings, ISceneLightSettings
    {
        [Header("Main Light")]
        [Tooltip("Points TOWARD the light (normalized on read); shadows extend the opposite way. " +
                 "The canonical scene light sits upper-left.")]
        [UnitCircle]
        [SerializeField] private Vector2 _lightDirection = new(-0.707f, 0.707f);

        [Tooltip("The light's tint — multiplies into each consumer's authored response colour " +
                 "(cloud highlight, speculars). White = neutral, no look change.")]
        [SerializeField] private Color _lightColor = Color.white;

        [Tooltip("Scales the light's contribution in every consumer (diffuse contrast, specular " +
                 "brightness). 1 = neutral, authored look.")]
        [Range(0f, 2f)] [SerializeField] private float _intensity = 1f;

        [Header("Field — Resolution")]
        [Tooltip("Field RT density (texels per world unit). Higher = smoother colour/light regions; the RT " +
                 "stays small and only re-renders when a light or the owner changes, so this can be far " +
                 "finer than the disturbance field's 8.")]
        [SerializeField] [Range(8, 64)] private int _texelsPerUnit = 32;

        [Header("Field — Cadence")]
        [Tooltip("Render the field every Nth frame at 60 fps (reinterpreted as seconds so cost doesn't " +
                 "scale with display refresh — a 120 Hz panel would otherwise double it). The dirty gate " +
                 "still applies: an idle scene never re-renders regardless of this interval.")]
        [SerializeField] [Range(1, 4)] private int _fieldFrameInterval = 1;

        [Header("Field — Lights")]
        [Tooltip("Max simultaneous lights composited per accumulate batch. Capped by the accumulate " +
                 "shader's MAX_STAMPS (32) — raising it past that also needs a shader edit.")]
        [SerializeField] [Range(1, 32)] private int _maxLights = 32;

        [Tooltip("Ceiling on the summed magnitude boost overlapping lights add above the ambient rest " +
                 "(soft-clamped), so overlaps never blow the field's brightness out.")]
        [SerializeField] [Range(1f, 6f)] private float _accumulationCeiling = 3f;

        [Header("Field — Direction")]
        [Tooltip("How strongly a light's local brightness bends the field direction toward it " +
                 "(weight = saturate(localR * this)). Higher = direction snaps to a local light sooner. " +
                 "A light's falloff shape is a per-light property, not here.")]
        [SerializeField] [Range(0.1f, 5f)] private float _directionResponse = 1f;

        [Header("Field — Shaders")]
        [Tooltip("Assign the three field-pipeline shaders explicitly — a device build strips Hidden shaders " +
                 "that are only reached by name. Fill = Hidden/BalloonParty/SceneLightFieldFill, " +
                 "Accumulate = …/SceneLightAccumulate, Gradient = …/SceneLightGradient.")]
        [SerializeField] private Shader _fillShader;
        [SerializeField] private Shader _accumulateShader;
        [SerializeField] private Shader _gradientShader;

        [Header("GI — March")]
        [Tooltip("How far an object's shadow/bleed reaches, in world units.")]
        [SerializeField] private float _smearDistance = 1.5f;

        [Tooltip("Divisor applied to the capture resolution for the smear/work targets. 1 = capture " +
                 "resolution (the old behavior, for A/B); 2 quarters the fragment count. The light buffer " +
                 "is low-frequency, so it survives well below capture resolution.")]
        [Range(1, 4)]
        [SerializeField] private int _smearDownscale = 2;

        [Tooltip("Per-tap weight decay along the march — lower dies off faster.")]
        [Range(0.1f, 1f)]
        [SerializeField] private float _tapDecay = 0.8f;

        [Tooltip("Taps skipped at the march start so occluders don't fully self-shadow.")]
        [SerializeField] private float _tapStart = 1f;

        [Tooltip("How aggressively the march cone widens — each tap samples a higher mip " +
                 "level (mip = spread × log₂(1 + tapIndex)), capturing averaged scene color " +
                 "over an increasing solid angle. 0 disables (flat march, all mip 0).")]
        [Range(0f, 2f)]
        [SerializeField] private float _mipSpread = 0.7f;

        [Tooltip("Shadow-specific mip spread — controls distance-dependent penumbra. " +
                 "Higher than the bounce spread means shadows soften faster with distance " +
                 "(sharp at contact, soft far away). 0 = uses the bounce spread instead.")]
        [Range(0f, 4f)]
        [SerializeField] private float _shadowMipSpread = 1.4f;

        [Header("GI — Shadow")]
        [Range(0f, 1f)]
        [SerializeField] private float _shadowStrength = 0.35f;

        [SerializeField] private Color _shadowTint = new Color(0.55f, 0.6f, 0.75f);

        [Tooltip("How strongly the shared cloud field gates the GI shadow. 1 = shadow only survives where " +
                 "there's cloud; 0 = cloud ignored. No effect when no cloud field is in the scene.")]
        [Range(0f, 1f)]
        [SerializeField] private float _cloudShadowGate = 1f;

        [Header("GI — Bounce")]
        [Range(0f, 2f)]
        [SerializeField] private float _bounceStrength = 0.25f;

        [Tooltip("Weight of the three secondary bounce directions (±90° and 180° from " +
                 "the primary light direction). 0 = single-direction march (cheapest), " +
                 "1 = all four directions contribute equally (omnidirectional bounce).")]
        [Range(0f, 1f)]
        [SerializeField] private float _secondaryBounceWeight = 0.5f;

        [Header("GI — Shaders")]
        [Tooltip("Assign explicitly — device builds strip shaders that are only " +
                 "referenced via Shader.Find.")]
        [SerializeField] private Shader _smearShader;
        [SerializeField] private Shader _overlayShader;

        // ISceneLightFieldSettings
        public Shader FillShader => _fillShader;
        public Shader AccumulateShader => _accumulateShader;
        public Shader GradientShader => _gradientShader;
        public int TexelsPerUnit => _texelsPerUnit;
        public int FieldFrameInterval => _fieldFrameInterval;
        public int MaxLights => _maxLights;
        public float AccumulationCeiling => _accumulationCeiling;
        public float DirectionResponse => _directionResponse;

        // IScreenSpaceLightSettings
        public Shader SmearShader => _smearShader;
        public Shader OverlayShader => _overlayShader;
        public float SmearDistance => _smearDistance;
        public int SmearDownscale => _smearDownscale;
        public float TapDecay => _tapDecay;
        public float TapStart => _tapStart;
        public float MipSpread => _mipSpread;
        public float ShadowMipSpread => _shadowMipSpread > 0f ? _shadowMipSpread : _mipSpread;
        public float ShadowStrength => _shadowStrength;
        public Color ShadowTint => _shadowTint;
        public float CloudShadowGate => _cloudShadowGate;
        public float BounceStrength => _bounceStrength;
        public float SecondaryBounceWeight => _secondaryBounceWeight;

        // ISceneLightSettings
        public Vector2 LightDirection =>
            _lightDirection.sqrMagnitude > 0.0001f ? _lightDirection.normalized : Vector2.up;
        public Color LightColor => _lightColor;
        public float Intensity => _intensity;
    }
}
