using UnityEngine;

namespace BalloonParty.Shared
{
    /// <summary>Prediction trace settings: step size, bounce/step limits, and line colour.</summary>
    public interface IPredictionTraceConfig
    {
        float PredictionTraceStep { get; }
        int PredictionTraceMaxBounces { get; }
        int PredictionTraceMaxSteps { get; }
        Color PredictionTraceColor { get; }
    }
}
