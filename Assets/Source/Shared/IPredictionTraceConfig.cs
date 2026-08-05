using UnityEngine;

namespace BalloonParty.Shared
{
    /// <summary>Prediction trace settings: step size, bounce/step limits, and line colour.</summary>
    public interface IPredictionTraceConfig
    {
        float PredictionTraceStep { get; }
        /// <summary>
        ///     Reflections the trace will draw before it stops — walls and balloon deflections
        ///     together, because to the player they are the same event: the line turned. The leg
        ///     leaving the last allowed reflection is still drawn; the one after it is not.
        /// </summary>
        /// <remarks>
        ///     Deliberately one budget rather than one per kind. The telegraph is an aid, not a
        ///     solution — showing the whole ricochet answers the shot for the player. Expected to
        ///     rise with progression.
        /// </remarks>
        int PredictionTraceMaxReflections { get; }
        int PredictionTraceMaxSteps { get; }
        Color PredictionTraceColor { get; }
    }
}
