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
            "as one sent downward by a deflection, where there is no bottom wall.")]
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

        public float SegmentLength => _segmentLength;
        public int MaxSegments => _maxSegments;
        public int MaxReflections => _maxReflections;
        public Color LineColor => _lineColor;
    }
}
