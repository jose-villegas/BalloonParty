using BalloonParty.Shared;
using UnityEngine;
using UnityEngine.Serialization;

namespace BalloonParty.Configuration
{
    [CreateAssetMenu(menuName = "Configuration/Prediction Trace Config", fileName = "PredictionTraceConfig")]
    internal class PredictionTraceConfig : ScriptableObject, IPredictionTraceConfig
    {
        [Header("Trace")]
        [Tooltip("World-space length of one polyline segment. Purely how finely the line is " +
            "subdivided — contact against balloons is solved analytically per segment, so a coarse " +
            "value costs smoothness, never accuracy.")]
        [FormerlySerializedAs("_initialPredictionLength")]
        [SerializeField] [Min(0.01f)] private float _segmentLength = 1f;

        [Tooltip("Segments drawn before the trace gives up. Times the segment length, this is how " +
            "far the line can reach — and it is what ends a line that nothing else terminates, such " +
            "as one piercing straight through every remaining tough with no wall left to end it on.")]
        [FormerlySerializedAs("_predictionTraceMaxSteps")]
        [SerializeField] [Min(1)] private int _maxSegments = 15;

        [Tooltip("Reflections drawn before the trace stops — walls and balloon deflections " +
            "together. 1 shows the player where the shot turns once and leaves the rest to them.")]
        [FormerlySerializedAs("_predictionTraceMaxBounces")]
        [FormerlySerializedAs("_predictionTraceMaxReflections")]
        [SerializeField] [Min(0)] private int _maxReflections = 1;

        [Tooltip("Start/end color applied to the prediction trace LineRenderer.")]
        [FormerlySerializedAs("_predictionTraceColor")]
        [SerializeField] private Color _lineColor = Color.white;

        [Header("Light (experimental)")]
        [Tooltip("Also cast capsule scene-lights (one per trace leg) matching the drawn line. Off by " +
            "default — see IPredictionTraceConfig.LightingEnabled for why.")]
        [SerializeField] private bool _lightingEnabled;

        [Tooltip("Half-width of the capsule lights mirroring the line (one per trace leg).")]
        [SerializeField] [Min(0f)] private float _lightHalfWidth = 0.35f;

        [SerializeField] [Min(0f)] private float _lightIntensity = 1f;

        [Tooltip("Radial falloff exponent for the capsule lights: (1 - dist/radius)^power.")]
        [SerializeField] [Min(0.01f)] private float _lightFalloffPower = 2f;

        [Tooltip("Intensity multiplier along the line from launch (t=0) to tip (t=1), sampled at each " +
            "trace leg's arc-length midpoint. Linear by default — same reasoning as _lightWidthCurve.")]
        [SerializeField] private AnimationCurve _lightFadeCurve = AnimationCurve.Linear(0f, 1f, 1f, 0.15f);

        [Tooltip("Half-width multiplier along the line from launch (t=0) to tip (t=1), sampled by total " +
            "arc-length fraction. Linear by default, deliberately not eased — with unequal leg lengths " +
            "an S-curve squeezes the whole taper into whichever leg straddles its steep middle, reading " +
            "as a hard step instead of a smooth narrowing through every bend.")]
        [SerializeField] private AnimationCurve _lightWidthCurve = AnimationCurve.Linear(0f, 1f, 1f, 0.15f);

        public float SegmentLength => _segmentLength;
        public int MaxSegments => _maxSegments;
        public int MaxReflections => _maxReflections;
        public Color LineColor => _lineColor;
        public bool LightingEnabled => _lightingEnabled;
        public float LightHalfWidth => _lightHalfWidth;
        public float LightIntensity => _lightIntensity;
        public float LightFalloffPower => _lightFalloffPower;
        public AnimationCurve LightFadeCurve => _lightFadeCurve;
        public AnimationCurve LightWidthCurve => _lightWidthCurve;
    }
}
