using System.Collections.Generic;

namespace BalloonParty.Configuration
{
    /// <summary>
    ///     Read-only view of the score-trail choreography table — the ordered
    ///     <see cref="ScoreTrailBehaviourEntry"/> list the resolver evaluates highest-<c>MinPoints</c>-first.
    /// </summary>
    internal interface IScoreTrailBehaviourConfiguration
    {
        IReadOnlyList<ScoreTrailBehaviourEntry> Entries { get; }

        /// <summary>BigScore star-polygon tiers, evaluated highest-<c>MinPoints</c>-first by the handler.</summary>
        IReadOnlyList<BigScoreTierConfig> BigScoreTiers { get; }
    }
}
