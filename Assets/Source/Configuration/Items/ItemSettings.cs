using System;
using BalloonParty.Nudge;
using BalloonParty.Shared.Pool;
using BalloonParty.Slots.Capabilities;
using UnityEngine;
using BalloonParty.Configuration.Items;
using BalloonParty.Configuration.Ranges;

namespace BalloonParty.Configuration.Items
{
    [Serializable]
    public class ItemSettings : IWeightedEntry
    {
        [SerializeField] private ItemType _type;
        [SerializeField] private int _turnCheckEvery;
        [SerializeField] private float _weight;
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

        /// <summary>Catalog default; runtime reads the resolved value via IActiveLevelParameters.ItemCadence.</summary>
        public int TurnCheckEvery => _turnCheckEvery;

        /// <summary>Catalog default; the resolver multiplies this by the active range's per-type weight.</summary>
        public float Weight => _weight;

        /// <summary>Catalog fallback unless ItemTypeWeight.MaximumAllowedOverride is set.</summary>
        public int MaximumAllowed => _maximumAllowed;
        int IWeightedEntry.MaxCount => _maximumAllowed;
        string IWeightedEntry.PoolKey => _type.ToString();
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

        public float Radius => _bombRadius;

        /// <summary>Visual-only: scales a rainbow bomb's activation-effect transform so the blast reads bigger. Does not change the kill radius.</summary>
        public float RainbowEffectScale => _bombRainbowEffectScale;

        /// <summary>World-space width of the ring *outside* the kill radius where a rainbow bomb converts balloons to rainbow instead of destroying them (0 disables conversion).</summary>
        public float RainbowConversionRange => _bombRainbowConversionRange;

        public NudgeOverride[] NudgeOverrides => _nudgeOverrides;
    }

    [Serializable]
    public class LaserSettings
    {
        [SerializeField] private float _laserRaycastDistance = 20f;
        [SerializeField] private float _laserCircleCastRadius = 0.065f;

        public float RaycastDistance => _laserRaycastDistance;
        public float CircleCastRadius => _laserCircleCastRadius;
    }

    [Serializable]
    public class LightningSettings
    {
        [SerializeField] private float _lightningSegmentsMultiplier = 3f;
        [SerializeField] private float _lightningRandomness = 0.2f;
        [SerializeField] private float _lightningJumpTime = 0.15f;
        [SerializeField] private int _lightningGlowSubdivisions = 4;
        [SerializeField] private float _lightningFractalDecay = 0.55f;

        public float SegmentsMultiplier => _lightningSegmentsMultiplier;
        public float Randomness => _lightningRandomness;
        public float JumpTime => _lightningJumpTime;
        public int GlowSubdivisions => _lightningGlowSubdivisions;
        public float FractalDecay => _lightningFractalDecay;
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

        public float SpreadOffset => _spreadOffset;
        public float SpreadLength => _spreadLength;
        public float SpreadBaseWidth => _spreadBaseWidth;
        public float SpreadBlobRadius => _spreadBlobRadius;
    }
}
