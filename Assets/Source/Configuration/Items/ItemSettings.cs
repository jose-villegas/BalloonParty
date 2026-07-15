using System;
using BalloonParty.Nudge;
using BalloonParty.Shared.Pool;
using BalloonParty.Slots.Capabilities;
using UnityEngine;
using BalloonParty.Configuration.Items;

namespace BalloonParty.Configuration.Items
{
    [Serializable]
    public class ItemSettings
    {
        [SerializeField] private ItemType _type;
        [SerializeField] private int _maximumAllowed;
        [SerializeField] private GameObject _visualPrefab;
        [SerializeField] private EffectView _activationEffectPrefab;

        [SerializeField] private int _damage = 1;
        [SerializeField] private DamageFlags _damageFlags = DamageFlags.Normal;

        [SerializeField] private BombSettings _bomb = new();
        [SerializeField] private LaserSettings _laser = new();
        [SerializeField] private LightningSettings _lightning = new();
        [SerializeField] private PaintSettings _paint = new();

        public ItemType Type => _type;

        /// <summary>Catalog fallback unless ItemTypeWeight.MaximumAllowedOverride is set.</summary>
        public int MaximumAllowed => _maximumAllowed;
        public GameObject VisualPrefab => _visualPrefab;
        public EffectView ActivationEffectPrefab => _activationEffectPrefab;
        public int Damage => _damage;
        public DamageFlags Flags => _damageFlags;

        public BombSettings Bomb => _bomb;
        public LaserSettings Laser => _laser;
        public LightningSettings Lightning => _lightning;
        public PaintSettings Paint => _paint;
    }

    [Serializable]
    public class BombSettings
    {
        [SerializeField] private float _bombRadius = 1.25f;
        [SerializeField] private float _bombRainbowEffectScale = 1.5f;
        [SerializeField] [Min(0f)] private float _bombRainbowConversionRange = 1f;
        [SerializeField] private NudgeOverride[] _nudgeOverrides;

        [SerializeField] [Min(0f)] private float _blastLightRadiusScale = 3f;
        [SerializeField] [Min(0f)] private float _blastLightIntensity = 3f;
        [SerializeField] [Min(0f)] private float _blastLightFallbackSeconds = 0.4f;

        public float Radius => _bombRadius;

        /// <summary>Visual-only: scales a rainbow bomb's activation-effect transform so the blast reads bigger. Does not change the kill radius.</summary>
        public float RainbowEffectScale => _bombRainbowEffectScale;

        /// <summary>World-space width of the ring *outside* the kill radius where a rainbow bomb converts balloons to rainbow instead of destroying them (0 disables conversion).</summary>
        public float RainbowConversionRange => _bombRainbowConversionRange;

        public NudgeOverride[] NudgeOverrides => _nudgeOverrides;

        /// <summary>Flash-light radius as a multiple of the (visually-scaled) blast radius.</summary>
        public float BlastLightRadiusScale => _blastLightRadiusScale;

        /// <summary>Peak magnitude of the blast flash light.</summary>
        public float BlastLightIntensity => _blastLightIntensity;

        /// <summary>Flash-light lifetime when the activation effect reports no duration.</summary>
        public float BlastLightFallbackSeconds => _blastLightFallbackSeconds;
    }

    [Serializable]
    public class LaserSettings
    {
        [SerializeField] private float _laserRaycastDistance = 20f;
        [SerializeField] private float _laserCircleCastRadius = 0.065f;

        [Tooltip("Rainbow holder only: how many times the beam lerps through the allowed colours over the anim.")]
        [SerializeField] [Min(0f)] private float _laserColorCycles = 2f;

        [SerializeField] [Min(0f)] private float _beamLightHalfWidth = 0.7f;
        [SerializeField] [Min(0f)] private float _beamLightIntensity = 2f;
        [SerializeField] [Min(0f)] private float _beamLightFalloff = 1.5f;
        [SerializeField] [Min(0f)] private float _beamLightFallbackSeconds = 0.4f;

        [SerializeField] private bool _telegraphEnabled;
        [SerializeField] [Min(0f)] private float _telegraphHalfLength = 2f;
        [SerializeField] [Min(0f)] private float _telegraphHalfWidth = 0.5f;
        [SerializeField] [Min(0f)] private float _telegraphIntensity = 1.5f;

        public float RaycastDistance => _laserRaycastDistance;
        public float CircleCastRadius => _laserCircleCastRadius;
        public float ColorCycles => _laserColorCycles;

        /// <summary>Perpendicular half-width (reach) of each beam's area light.</summary>
        public float BeamLightHalfWidth => _beamLightHalfWidth;

        /// <summary>Peak magnitude of each beam light.</summary>
        public float BeamLightIntensity => _beamLightIntensity;

        /// <summary>Beam-light falloff power — lower = broader, so the whole beam reads lit, not a thin core.</summary>
        public float BeamLightFalloff => _beamLightFalloff;

        /// <summary>Beam-light lifetime when the beam effect reports no duration.</summary>
        public float BeamLightFallbackSeconds => _beamLightFallbackSeconds;

        /// <summary>Whether the idle telegraph cross-light is active.</summary>
        public bool TelegraphEnabled => _telegraphEnabled;

        /// <summary>Half-length of each arm of the telegraph cross.</summary>
        public float TelegraphHalfLength => _telegraphHalfLength;

        /// <summary>Perpendicular half-width of each telegraph arm segment.</summary>
        public float TelegraphHalfWidth => _telegraphHalfWidth;

        /// <summary>Intensity of the telegraph light.</summary>
        public float TelegraphIntensity => _telegraphIntensity;
    }

    [Serializable]
    public class LightningSettings
    {
        [SerializeField] private float _lightningSegmentsMultiplier = 3f;
        [SerializeField] private float _lightningRandomness = 0.2f;
        [SerializeField] private float _lightningJumpTime = 0.15f;
        [SerializeField] private int _lightningGlowSubdivisions = 4;
        [SerializeField] private float _lightningFractalDecay = 0.55f;

        [Tooltip("How many times the glow lerps through the full colour set over the anim's duration.")]
        [SerializeField] [Min(0f)] private float _lightningGlowColorCycles = 2f;

        [SerializeField] [Min(0f)] private float _popLightRadius = 0.8f;
        [SerializeField] [Min(0f)] private float _popLightIntensity = 2f;
        [SerializeField] [Min(0f)] private float _popLightSeconds = 0.2f;

        public float SegmentsMultiplier => _lightningSegmentsMultiplier;
        public float Randomness => _lightningRandomness;
        public float JumpTime => _lightningJumpTime;
        public int GlowSubdivisions => _lightningGlowSubdivisions;
        public float FractalDecay => _lightningFractalDecay;
        public float GlowColorCycles => _lightningGlowColorCycles;

        /// <summary>Radius of the flash light cast at each node the chain reaches.</summary>
        public float PopLightRadius => _popLightRadius;

        /// <summary>Peak magnitude of each chain-node flash light.</summary>
        public float PopLightIntensity => _popLightIntensity;

        /// <summary>Lifetime of each chain-node flash light.</summary>
        public float PopLightSeconds => _popLightSeconds;
    }

    [Serializable]
    public class PaintSettings
    {
        [SerializeField] private float _paintBlobFlightDuration = 0.35f;

        [SerializeField] private AnimationCurve _paintBlobArcCurve = new(
            new Keyframe(0f, 0f, 0f, 4f),
            new Keyframe(0.5f, 1f, 0f, 0f),
            new Keyframe(1f, 0f, -4f, 0f));

        [SerializeField] private AnimationCurve _paintBlobScaleCurve = AnimationCurve.Constant(0f, 1f, 1f);

        [SerializeField] private AnimationCurve _paintBlobShadowScaleCurve = AnimationCurve.Constant(0f, 1f, 1f);

        [SerializeField] private AnimationCurve _paintBlobSpriteScaleCurve = AnimationCurve.Constant(0f, 1f, 1f);

        [SerializeField] private float _paintBlobSpinSpeed = 720f;

        [Tooltip("Rainbow holder only: how many times each blob lerps through the allowed colours over its flight.")]
        [SerializeField] [Min(0f)] private float _paintBlobColorCycles = 2f;

        [Tooltip("Shifts the triangle along the travel axis from the hit point, in world units. " +
                 "Negative pulls it back toward the launcher.")]
        [SerializeField] private float _spreadOffset;

        [Tooltip("How far the triangle reaches along the travel axis, in world units. Positive opens it " +
                 "forward (along travel); negative opens it backward.")]
        [SerializeField] private float _spreadLength = 3f;

        [Tooltip("Width of the triangle's far edge, perpendicular to travel, in world units.")]
        [SerializeField] private float _spreadBaseWidth = 2.5f;

        [Tooltip("Radius of each packed paint blob, in world units. Smaller packs more blobs = denser.")]
        [SerializeField] private float _spreadBlobRadius = 0.35f;

        public float FlightDuration => _paintBlobFlightDuration;
        public AnimationCurve ArcCurve => _paintBlobArcCurve;
        public AnimationCurve ScaleCurve => _paintBlobScaleCurve;
        public AnimationCurve ShadowScaleCurve => _paintBlobShadowScaleCurve;
        public AnimationCurve SpriteScaleCurve => _paintBlobSpriteScaleCurve;
        public float SpinSpeed => _paintBlobSpinSpeed;
        public float BlobColorCycles => _paintBlobColorCycles;

        public float SpreadOffset => _spreadOffset;
        public float SpreadLength => _spreadLength;
        public float SpreadBaseWidth => _spreadBaseWidth;
        public float SpreadBlobRadius => _spreadBlobRadius;
    }
}
