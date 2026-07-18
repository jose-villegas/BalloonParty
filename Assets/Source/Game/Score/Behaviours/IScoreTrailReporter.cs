using UnityEngine;

namespace BalloonParty.Game.Score.Behaviours
{
    /// <summary>
    ///     Service-owned sink a handler reports group arrivals through. The implementation publishes one
    ///     <c>ScoreTrailArrivedMessage(color, score, points, at)</c> per call. Flight registration is NOT
    ///     the reporter's concern — the handler registers and unregisters its own flights so the ownership
    ///     of the <see cref="TrailId"/> bookkeeping stays in one place (parity with the pre-seam inline code).
    /// </summary>
    internal interface IScoreTrailReporter
    {
        /// <param name="score">Cumulative per-color number the arrival confirms. Ascending across a group.</param>
        /// <param name="points">Points landing with this arrival; the group's reports must sum to its total.</param>
        /// <param name="at">World position the arrival lands at.</param>
        void ReportArrival(int score, int points, Vector3 at);
    }
}
