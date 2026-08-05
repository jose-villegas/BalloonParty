using UnityEngine;

namespace BalloonParty.Shared
{
    /// <summary>Prediction trace settings: step size, bounce/step limits, and line colour.</summary>
    public interface IPredictionTraceConfig
    {
        float PredictionTraceStep { get; }
        int PredictionTraceMaxBounces { get; }

        /// <summary>
        ///     Balloon deflections the trace will draw, budgeted separately from wall bounces — a
        ///     deflection costs no shield, and an uncapped chain of them is unreadable.
        /// </summary>
        int PredictionTraceMaxDeflections { get; }
        int PredictionTraceMaxSteps { get; }
        Color PredictionTraceColor { get; }
    }
}
