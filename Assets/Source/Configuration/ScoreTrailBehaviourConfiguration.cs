using System;
using System.Collections.Generic;
using BalloonParty.Game.Score.Behaviours;
using BalloonParty.Shared;
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
    ///     Global knobs shared by every BigScore formation: radius, pen speed, coverage, spin and the
    ///     scale-over-travel curve that blooms/tapers each shape.
    /// </summary>
    [Serializable]
    internal struct BigScoreFormationSettings
    {
        [Tooltip("Outermost formation radius in world units (before the per-shape RadiusScale and the in-bounds fit).")]
        [SerializeField] private float _baseRadius;

        [Tooltip("Normalized shape scale over the formation's life. Y multiplies the fitted radius; the curve's " +
                 "LAST KEY TIME is the formation duration (seconds). Author: bloom from 0 to ~1 early, hold/decay, " +
                 "taper to 0 at the bar.")]
        [SerializeField] private AnimationCurve _scaleOverTravel;

        [Tooltip("PRIMARY STYLE KNOB (with Coverage): pen travel speed between vertices, in WORLD units/second — " +
                 "so ink density and energy read the same across shape sizes. A walk of world perimeter L laps in " +
                 "L/PenSpeed seconds.")]
        [SerializeField] private float _penSpeed;

        [Tooltip("STYLE DIAL, not a clamp. Each pen's ribbon time is (loopPeriod / pensOnWalk) * this. >= 1 reads " +
                 "as a solid wireframe; < 1 is an intentional look — chasing comet heads with fading tails; << 1 = " +
                 "orbiting pearls.")]
        [Range(0.2f, 2f)]
        [SerializeField] private float _coverage;

        [Tooltip("Tumble speed, degrees/second, about a random axis for the whole life (invisible while the shape " +
                 "is still a point): spins the pens + drawn ink as the shape travels to the bar.")]
        [SerializeField] private float _spinSpeedDegrees;

        [Tooltip("Amplitude multiplier for shapes with a vertex displacer (the waving sphere). 1 = authored default.")]
        [Range(0f, 3f)]
        [SerializeField] private float _displacementScale;

        [Tooltip("Time multiplier for vertex displacement animation speed. 1 = authored default.")]
        [Range(0f, 5f)]
        [SerializeField] private float _displacementSpeed;

        public float BaseRadius => _baseRadius;
        public AnimationCurve ScaleOverTravel => _scaleOverTravel;
        public float PenSpeed => _penSpeed;
        public float Coverage => _coverage;
        public float SpinSpeedDegrees => _spinSpeedDegrees;
        public float DisplacementScale => _displacementScale;
        public float DisplacementSpeed => _displacementSpeed;

        internal BigScoreFormationSettings(
            float baseRadius,
            AnimationCurve scaleOverTravel,
            float penSpeed,
            float coverage,
            float spinSpeedDegrees,
            float displacementScale = 1f,
            float displacementSpeed = 1f)
        {
            _baseRadius = baseRadius;
            _scaleOverTravel = scaleOverTravel;
            _penSpeed = penSpeed;
            _coverage = coverage;
            _spinSpeedDegrees = spinSpeedDegrees;
            _displacementScale = displacementScale;
            _displacementSpeed = displacementSpeed;
        }
    }

    [CreateAssetMenu(menuName = "Configuration/Score Trail Behaviours", fileName = "ScoreTrailBehaviourConfiguration")]
    internal class ScoreTrailBehaviourConfiguration : ScriptableObject, IScoreTrailBehaviourConfiguration, IScoreTrailConfig
    {
        [Tooltip("Choreography handlers keyed by score magnitude, evaluated highest MinPoints first. " +
                 "A DefaultScore entry at MinPoints 0 is the catch-all.")]
        [SerializeField] private ScoreTrailBehaviourEntry[] _entries = DefaultEntries();

        [Tooltip("Global BigScore formation timing/size/spin knobs shared by every decomposed shape.")]
        [SerializeField] private BigScoreFormationSettings _bigScoreSettings = DefaultSettings();

        [Header("Score")]
        [SerializeField] private float _scorePointTraceDuration;
        [SerializeField] private float _scorePointsScatterDelay = 0.08f;
        [SerializeField] private float _scorePointBurstDuration = 0.12f;

        [Tooltip("Per-color trail pool size prewarmed at level setup — a big pop or level-up ceremony " +
                 "can burst 20-40 live trails for one color.")]
        [SerializeField] private int _scoreTrailPrewarmPerColor = 64;

        [Tooltip("Per-color prewarm size for EACH of the point/streak notice pools — these rarely have " +
                 "more than a handful live at once, hence the lower default than the trail pool.")]
        [SerializeField] private int _progressNoticePrewarmPerColor = 16;

        public IReadOnlyList<ScoreTrailBehaviourEntry> Entries => _entries;
        public BigScoreFormationSettings BigScoreSettings => _bigScoreSettings;
        public float ScorePointTraceDuration => _scorePointTraceDuration;
        public float ScorePointsScatterDelay => _scorePointsScatterDelay;
        public float ScorePointBurstDuration => _scorePointBurstDuration;
        public int ScoreTrailPrewarmPerColor => _scoreTrailPrewarmPerColor;
        public int ProgressNoticePrewarmPerColor => _progressNoticePrewarmPerColor;

        private static ScoreTrailBehaviourEntry[] DefaultEntries()
        {
            return new[]
            {
                new ScoreTrailBehaviourEntry(ScoreTrailBehaviourId.BigScore, 2),
                new ScoreTrailBehaviourEntry(ScoreTrailBehaviourId.DefaultScore, 0),
            };
        }

        // Bloom to full within ~14% of the 1.8 s life, gentle decay through the middle, taper to a point at the
        // bar; pens travel at 6 world units/s and tile their walks with 15% overlap; the figure tumbles at 60°/s.
        private static BigScoreFormationSettings DefaultSettings()
        {
            var scaleOverTravel = new AnimationCurve(
                new Keyframe(0f, 0f),
                new Keyframe(0.25f, 1f),
                new Keyframe(1.2f, 0.8f),
                new Keyframe(1.8f, 0f));
            return new BigScoreFormationSettings(1.2f, scaleOverTravel, 6f, 1.15f, 60f);
        }
    }
}
