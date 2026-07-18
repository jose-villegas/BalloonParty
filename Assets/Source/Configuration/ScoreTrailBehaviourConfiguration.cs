using System;
using System.Collections.Generic;
using BalloonParty.Game.Score.Behaviours;
using UnityEngine;

namespace BalloonParty.Configuration
{
    /// <summary>One row of the choreography table: the handler to use once a group clears <see cref="MinPoints"/>.</summary>
    [Serializable]
    internal struct ScoreTrailBehaviourEntry
    {
        [SerializeField] private ScoreTrailBehaviourId _id;

        [Tooltip("Lowest group total this handler claims; the resolver picks the highest-MinPoints entry the group clears.")]
        [SerializeField] private int _minPoints;

        public ScoreTrailBehaviourId Id => _id;
        public int MinPoints => _minPoints;

        internal ScoreTrailBehaviourEntry(ScoreTrailBehaviourId id, int minPoints)
        {
            _id = id;
            _minPoints = minPoints;
        }
    }

    /// <summary>
    ///     One BigScore visual tier: the star polygon {n/k} the carrier's tributaries draw, its nesting, and
    ///     the phase timing. Selected by group total (highest <see cref="MinPoints"/> the group clears).
    /// </summary>
    [Serializable]
    internal struct BigScoreTierConfig
    {
        [Tooltip("Lowest group total this tier claims.")]
        [SerializeField] private int _minPoints;

        [Tooltip("Star polygon vertex count n (3-8).")]
        [Range(3, 8)]
        [SerializeField] private int _vertexCount;

        [Tooltip("Chord skip k in {n/k}: each vertex draws to vertex (i+k). 1 = convex n-gon, 2 = pentagram-style.")]
        [SerializeField] private int _skip;

        [Tooltip("Nested repetitions m: the star redraws inward this many times.")]
        [SerializeField] private int _repeats;

        [Tooltip("Radius ratio between successive nested stars (default 1/phi^2 = 0.381966).")]
        [SerializeField] private float _nestScale;

        [Tooltip("Rotation added per nesting, in degrees. 0 = auto (180/n, the natural nesting offset).")]
        [SerializeField] private float _nestRotationDegrees;

        [Tooltip("Outermost star radius in world units, before the in-bounds clamp.")]
        [SerializeField] private float _baseRadius;

        [Tooltip("Fly-out (deploy) phase duration for the outer star; nested stars scale down with radius.")]
        [SerializeField] private float _deployDuration;

        [Tooltip("Chord-draw phase duration for the outer star.")]
        [SerializeField] private float _drawDuration;

        [Tooltip("Inward-collapse phase duration for the outer star.")]
        [SerializeField] private float _collapseDuration;

        [Tooltip("TrailRenderer.time the vertex ribbons use, long enough to keep outer stars visible while inner ones draw.")]
        [SerializeField] private float _ribbonTime;

        [Tooltip("Collapse in-plane rotation, degrees/second. 0 = pure radial collapse (no spin).")]
        [SerializeField] private float _rotationSpeedDegrees;

        public int MinPoints => _minPoints;
        public int VertexCount => Mathf.Clamp(_vertexCount, 3, 8);
        public int Skip => Mathf.Clamp(_skip, 1, VertexCount - 1);
        public int Repeats => Mathf.Max(1, _repeats);
        public float NestScale => _nestScale;
        public float BaseRadius => _baseRadius;
        public float DeployDuration => _deployDuration;
        public float DrawDuration => _drawDuration;
        public float CollapseDuration => _collapseDuration;
        public float RibbonTime => _ribbonTime;

        // 0 authored → the natural nesting offset pi/n so successive stars land where the geometry puts them.
        public float NestRotationRadians =>
            Mathf.Approximately(_nestRotationDegrees, 0f)
                ? Mathf.PI / VertexCount
                : _nestRotationDegrees * Mathf.Deg2Rad;

        public float RotationSpeedRadians => _rotationSpeedDegrees * Mathf.Deg2Rad;

        internal BigScoreTierConfig(
            int minPoints,
            int vertexCount,
            int skip,
            int repeats,
            float nestScale,
            float nestRotationDegrees,
            float baseRadius,
            float deployDuration,
            float drawDuration,
            float collapseDuration,
            float ribbonTime,
            float rotationSpeedDegrees)
        {
            _minPoints = minPoints;
            _vertexCount = vertexCount;
            _skip = skip;
            _repeats = repeats;
            _nestScale = nestScale;
            _nestRotationDegrees = nestRotationDegrees;
            _baseRadius = baseRadius;
            _deployDuration = deployDuration;
            _drawDuration = drawDuration;
            _collapseDuration = collapseDuration;
            _ribbonTime = ribbonTime;
            _rotationSpeedDegrees = rotationSpeedDegrees;
        }
    }

    [CreateAssetMenu(menuName = "Configuration/Score Trail Behaviours", fileName = "ScoreTrailBehaviourConfiguration")]
    internal class ScoreTrailBehaviourConfiguration : ScriptableObject, IScoreTrailBehaviourConfiguration
    {
        private const float GoldenNestScale = 0.381966f;

        [Tooltip("Choreography handlers keyed by score magnitude, evaluated highest MinPoints first. " +
                 "A DefaultScore entry at MinPoints 0 is the catch-all.")]
        [SerializeField] private ScoreTrailBehaviourEntry[] _entries = DefaultEntries();

        [Tooltip("BigScore star-polygon tiers, evaluated highest MinPoints first (the tier the group total clears).")]
        [SerializeField] private BigScoreTierConfig[] _bigScoreTiers = DefaultTiers();

        public IReadOnlyList<ScoreTrailBehaviourEntry> Entries => _entries;
        public IReadOnlyList<BigScoreTierConfig> BigScoreTiers => _bigScoreTiers;

        private static ScoreTrailBehaviourEntry[] DefaultEntries()
        {
            return new[]
            {
                new ScoreTrailBehaviourEntry(ScoreTrailBehaviourId.BigScore, 40),
                new ScoreTrailBehaviourEntry(ScoreTrailBehaviourId.DefaultScore, 0),
            };
        }

        // triangle {3/1} at 40, square {4/1} at 80, nested pentagram {5/2}x2 at 150; durations accelerate inward.
        private static BigScoreTierConfig[] DefaultTiers()
        {
            return new[]
            {
                new BigScoreTierConfig(40, 3, 1, 1, GoldenNestScale, 0f, 2.2f, 0.25f, 0.35f, 0.5f, 0.8f, 180f),
                new BigScoreTierConfig(80, 4, 1, 1, GoldenNestScale, 0f, 2.5f, 0.25f, 0.35f, 0.5f, 0.9f, 0f),
                new BigScoreTierConfig(150, 5, 2, 2, GoldenNestScale, 0f, 2.8f, 0.25f, 0.35f, 0.5f, 1.0f, 0f),
            };
        }
    }
}
