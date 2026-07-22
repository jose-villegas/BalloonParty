namespace BalloonParty.Shared.Cadence
{
    /// <summary>
    ///     Implemented by field services that perform periodic RT blits on a cadence timer.
    ///     The <see cref="EffectCadenceCoordinator"/> assigns each implementor a phase offset at startup
    ///     so their blits spread across frames, minimizing tile-flush peaks on mobile GPUs.
    /// </summary>
    internal interface ICadencedEffect
    {
        /// <summary>
        ///     Number of GPU blit operations this service performs per render cycle.
        ///     Used to assign maximum phase separation between the heaviest hitters.
        /// </summary>
        int BlitWeight { get; }

        /// <summary>
        ///     Called once by the coordinator at startup. Sets the initial accumulator to
        ///     <paramref name="offset01"/> × interval so this service's first render fires at a
        ///     staggered time relative to other services.
        /// </summary>
        void ApplyPhaseOffset(float offset01);
    }
}
