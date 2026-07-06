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

        /// <summary>Catalog default — runtime reads the resolved value via IActiveLevelParameters.ItemCadence.</summary>
        public int TurnCheckEvery => _turnCheckEvery;

        /// <summary>Catalog default — the resolver multiplies this by the active range's per-type weight.</summary>
        public float Weight => _weight;

        /// <summary>Catalog fallback — used unless the active range's ItemTypeWeight.MaximumAllowedOverride is set.</summary>
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
        [SerializeField] private NudgeOverride[] _nudgeOverrides;

        public float Radius => _bombRadius;
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

        public float FlightDuration => _paintBlobFlightDuration;
        public AnimationCurve ArcCurve => _paintBlobArcCurve;
        public AnimationCurve ScaleCurve => _paintBlobScaleCurve;
        public AnimationCurve ShadowScaleCurve => _paintBlobShadowScaleCurve;
        public AnimationCurve SpriteScaleCurve => _paintBlobSpriteScaleCurve;
        public float SpinSpeed => _paintBlobSpinSpeed;
    }
}
