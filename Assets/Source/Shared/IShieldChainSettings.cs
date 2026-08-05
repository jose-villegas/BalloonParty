namespace BalloonParty.Shared
{
    /// <summary>
    ///     Tuning for how shield chains are planned: the fan of openings tried, and how selective the
    ///     planner is about where a shield may land.
    /// </summary>
    /// <remarks>
    ///     These are balance knobs, not implementation constants — the band they define is what
    ///     decides whether a level's shields read as a route or as scatter, and it is retuned against
    ///     playtests rather than derived. Guards that only keep the algorithm terminating (surface
    ///     epsilon, leg cap) stay in the planner.
    /// </remarks>
    public interface IShieldChainSettings
    {
        /// <summary>How many opening angles the planner samples across the fan.</summary>
        int FanSamples { get; }

        /// <summary>Lowest opening angle in degrees; below it a shot never climbs the board.</summary>
        float FanMinDegrees { get; }

        /// <summary>Highest opening angle in degrees, mirroring <see cref="FanMinDegrees" />.</summary>
        float FanMaxDegrees { get; }

        /// <summary>
        ///     Fewest openings that must reach a slot for a shield to go there. A shield only one or
        ///     two angles reach is the brittle chain the planner exists to avoid.
        /// </summary>
        int MinEntryAngles { get; }

        /// <summary>
        ///     Reachable by more than this share of the fan means the player already sweeps the slot
        ///     for free, so a shield there extends nothing.
        /// </summary>
        float CheapZoneFraction { get; }

        /// <summary>
        ///     Openings sampled by the reachability field, which re-sweeps on every grid mutation
        ///     rather than once per spawn batch — hence coarser than <see cref="FanSamples" />.
        /// </summary>
        int ReachabilityFanSamples { get; }

        /// <summary>
        ///     How deep the reachability field looks. It is a nudge for the balancer, not a solver,
        ///     so this sits below what a player would plan.
        /// </summary>
        int ReachabilityMaxReflections { get; }

        /// <summary>
        ///     Balance-bias bonus a shield-carrying balloon gets for a slot a straight shot reaches.
        ///     Kept inside the range the ordinary biases occupy so support and pressure still decide.
        /// </summary>
        int ReachableSlotBonus { get; }

        /// <summary>Subtracted from <see cref="ReachableSlotBonus" /> per reflection needed.</summary>
        int PerReflectionPenalty { get; }
    }
}
