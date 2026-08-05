using BalloonParty.Shared;
using UnityEngine;
using UnityEngine.Serialization;

namespace BalloonParty.Configuration
{
    [CreateAssetMenu(menuName = "Configuration/Prediction Trace Config", fileName = "PredictionTraceConfig")]
    internal class PredictionTraceConfig : ScriptableObject, IPredictionTraceConfig
    {
        [Header("Trace")]
        [SerializeField] private float _initialPredictionLength;
        [Tooltip("Reflections drawn before the trace stops — walls and balloon deflections " +
            "together. 1 shows the player where the shot turns once and leaves the rest to them.")]
        [FormerlySerializedAs("_predictionTraceMaxBounces")]
        [SerializeField] [Min(0)] private int _predictionTraceMaxReflections = 1;
        [SerializeField] private int _predictionTraceMaxSteps;
        [Tooltip("Start/end color applied to the prediction trace LineRenderer.")]
        [SerializeField] private Color _predictionTraceColor = Color.white;

        public float PredictionTraceStep => _initialPredictionLength;
        public int PredictionTraceMaxReflections => _predictionTraceMaxReflections;
        public int PredictionTraceMaxSteps => _predictionTraceMaxSteps;
        public Color PredictionTraceColor => _predictionTraceColor;
    }
}
