using UnityEngine;

namespace BalloonParty.Shared
{
    /// <summary>Prediction trace settings: how the line is segmented, how far it reaches,
    /// how many reflections it gives away, and its colour.</summary>
    public interface IPredictionTraceConfig
    {
        /// <summary>World length of one polyline segment — smoothness, not accuracy.</summary>
        float SegmentLength { get; }

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
        int MaxReflections { get; }

        /// <summary>Segments before the trace gives up; times SegmentLength, its reach.</summary>
        int MaxSegments { get; }

        Color LineColor { get; }
    }
}
