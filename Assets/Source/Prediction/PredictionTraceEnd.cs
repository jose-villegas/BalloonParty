using UnityEngine;

namespace BalloonParty.Prediction
{
    /// <summary>What stopped the aim trace where it did.</summary>
    internal enum PredictionTraceEndKind
    {
        /// <summary>Ran out of segments in open air — nothing was struck, so nothing bounces.</summary>
        OpenAir,

        Wall,
        Deflector
    }

    /// <summary>
    ///     Everything about a finished trace beyond its polyline: where piercing began, and what the line
    ///     ran into at the end.
    /// </summary>
    /// <remarks>
    ///     The end contact's normal is reported because the calculator is the only thing that knows it —
    ///     it solved the wall crossing or the circle entry to stop there in the first place. A consumer
    ///     that re-derived it (testing the last point against the walls, or hunting a deflector circle
    ///     near it) would be computing a second answer to a question already answered, and the two can
    ///     drift. Same reasoning as <c>Shared/CircleContact</c> being the one ray-circle solver.
    /// </remarks>
    internal readonly struct PredictionTraceEnd
    {
        /// <summary>
        ///     Index into the trace's points where the shot starts piercing, or -1 if it never does.
        /// </summary>
        public readonly int PierceStartIndex;

        public readonly PredictionTraceEndKind Kind;

        /// <summary>
        ///     Unit surface normal at the end contact, pointing back toward the incoming shot.
        ///     <see cref="Vector2.zero" /> when <see cref="Kind" /> is
        ///     <see cref="PredictionTraceEndKind.OpenAir" />.
        /// </summary>
        public readonly Vector2 Normal;

        /// <summary>True when the shot actually struck something a bounce could leave from.</summary>
        public bool HasContact => Kind != PredictionTraceEndKind.OpenAir;

        public PredictionTraceEnd(int pierceStartIndex, PredictionTraceEndKind kind, Vector2 normal)
        {
            PierceStartIndex = pierceStartIndex;
            Kind = kind;
            Normal = normal;
        }

        internal static PredictionTraceEnd OpenAir(int pierceStartIndex)
        {
            return new PredictionTraceEnd(pierceStartIndex, PredictionTraceEndKind.OpenAir, Vector2.zero);
        }
    }
}
