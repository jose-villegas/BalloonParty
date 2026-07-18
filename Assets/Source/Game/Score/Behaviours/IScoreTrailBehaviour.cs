using BalloonParty.Shared.Messages;

namespace BalloonParty.Game.Score.Behaviours
{
    /// <summary>A per-group choreography handler: it owns everything between the group's spawn and its arrivals.</summary>
    internal interface IScoreTrailBehaviour
    {
        /// <summary>
        ///     The id the level-up cinematic waits on and matches arrivals against. The handler nominates it
        ///     because only the handler knows which of its visual objects spawns immediately and is therefore
        ///     safe under the cinematic's bounded registry wait — for the staggered default fan-out that is the
        ///     FIRST trail (<c>msg.FirstScore</c>); a carrier that spawns at once could nominate <c>LastScore</c>.
        /// </summary>
        TrailId GetPrincipalId(in ScorePointsGroupMessage msg);

        /// <summary>
        ///     Owns the group from spawn to final arrival. The handler MUST:
        ///     report arrivals via <see cref="ScoreTrailContext.Reporter"/> with true cumulative scores that sum to
        ///     <see cref="ScoreTrailContext.Points"/>; register and unregister its own flights in
        ///     <see cref="ScoreTrailContext.Flights"/> (the cinematic pauses/tracks the principal by its id);
        ///     and return every pooled instance it spawned to the pool.
        /// </summary>
        void Begin(in ScoreTrailContext context);
    }
}
