using BalloonParty.Shared;
using UnityEngine;

namespace BalloonParty.Configuration
{
    [CreateAssetMenu(menuName = "Configuration/Prediction Trace Config", fileName = "PredictionTraceConfig")]
    internal class PredictionTraceConfig : ScriptableObject, IPredictionTraceConfig
    {
        [Header("Trace")]
        [SerializeField] private float _initialPredictionLength;
        [SerializeField] private int _predictionTraceMaxBounces;

        [Tooltip("Balloon deflections drawn, separate from the wall-bounce budget. Each one is a " +
            "free direction change for the shot, but the line gets hard to read past a couple.")]
        [SerializeField] [Min(0)] private int _predictionTraceMaxDeflections = 2;
        [SerializeField] private int _predictionTraceMaxSteps;
        [Tooltip("Start/end color applied to the prediction trace LineRenderer.")]
        [SerializeField] private Color _predictionTraceColor = Color.white;

        public float PredictionTraceStep => _initialPredictionLength;
        public int PredictionTraceMaxBounces => _predictionTraceMaxBounces;
        public int PredictionTraceMaxDeflections => _predictionTraceMaxDeflections;
        public int PredictionTraceMaxSteps => _predictionTraceMaxSteps;
        public Color PredictionTraceColor => _predictionTraceColor;
    }
}
