using System;
using BalloonParty.Nudge;
using BalloonParty.Shared.Pool;
using UnityEngine;

namespace BalloonParty.Configuration
{
    [Serializable]
    public class ItemSettings
    {
        [SerializeField] private ItemType _type;
        [SerializeField] private int _turnCheckEvery;
        [SerializeField] private float _weight;
        [SerializeField] private int _maximumAllowed;
        [SerializeField] private GameObject _visualPrefab;
        [SerializeField] private EffectView _activationEffectPrefab;

        [SerializeField] private int _damage = 1;

        [SerializeField] private float _bombRadius = 1.25f;
        [SerializeField] private NudgeOverride[] _nudgeOverrides;

        [SerializeField] private float _laserRaycastDistance = 20f;
        [SerializeField] private float _laserCircleCastRadius = 0.065f;

        [SerializeField] private float _lightningSegmentsMultiplier = 3f;
        [SerializeField] private float _lightningRandomness = 0.2f;
        [SerializeField] private float _lightningJumpTime = 0.15f;
        [SerializeField] private int _lightningGlowSubdivisions = 4;
        [SerializeField] private float _lightningFractalDecay = 0.55f;

        [SerializeField] private float _paintBlobFlightDuration = 0.35f;

        [SerializeField] private AnimationCurve _paintBlobArcCurve = new(
            new Keyframe(0f, 0f, 0f, 4f),
            new Keyframe(0.5f, 1f, 0f, 0f),
            new Keyframe(1f, 0f, -4f, 0f));

        [SerializeField] private AnimationCurve _paintBlobScaleCurve = AnimationCurve.Constant(0f, 1f, 1f);

        [SerializeField] private AnimationCurve _paintBlobShadowScaleCurve = AnimationCurve.Constant(0f, 1f, 1f);

        [SerializeField] private AnimationCurve _paintBlobSpriteScaleCurve = AnimationCurve.Constant(0f, 1f, 1f);

        [SerializeField] private float _paintBlobSpinSpeed = 720f;

        public ItemType Type => _type;
        public int TurnCheckEvery => _turnCheckEvery;
        public float Weight => _weight;
        public int MaximumAllowed => _maximumAllowed;
        public GameObject VisualPrefab => _visualPrefab;
        public EffectView ActivationEffectPrefab => _activationEffectPrefab;
        public int Damage => _damage;
        public float BombRadius => _bombRadius;
        public NudgeOverride[] NudgeOverrides => _nudgeOverrides;
        public float LaserRaycastDistance => _laserRaycastDistance;
        public float LaserCircleCastRadius => _laserCircleCastRadius;
        public float LightningSegmentsMultiplier => _lightningSegmentsMultiplier;
        public float LightningRandomness => _lightningRandomness;
        public float LightningJumpTime => _lightningJumpTime;
        public int LightningGlowSubdivisions => _lightningGlowSubdivisions;
        public float LightningFractalDecay => _lightningFractalDecay;
        public float PaintBlobFlightDuration => _paintBlobFlightDuration;
        public AnimationCurve PaintBlobArcCurve => _paintBlobArcCurve;
        public AnimationCurve PaintBlobScaleCurve => _paintBlobScaleCurve;
        public AnimationCurve PaintBlobShadowScaleCurve => _paintBlobShadowScaleCurve;
        public AnimationCurve PaintBlobSpriteScaleCurve => _paintBlobSpriteScaleCurve;
        public float PaintBlobSpinSpeed => _paintBlobSpinSpeed;
    }
}
