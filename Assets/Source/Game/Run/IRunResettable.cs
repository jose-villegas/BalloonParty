namespace BalloonParty.Game.Run
{
    /// <summary>
    ///     Implemented by services holding per-run state that must be cleared when a run
    ///     restarts. <see cref="RunController"/> invokes implementations in ascending
    ///     <see cref="ResetOrder"/> — lower runs first — so teardown that must precede
    ///     other resets (quiesce async, return actors, clear the grid) can order itself.
    /// </summary>
    internal interface IRunResettable
    {
        int ResetOrder { get; }

        /// <param name="generation">
        ///     The new run's number — monotonically increasing, owned by <see cref="RunController"/>.
        ///     Services with frame-deferred async (spawner, balancer) capture it at schedule time and
        ///     compare against the latest to drop work belonging to a prior run; others can ignore it.
        /// </param>
        void ResetRun(int generation);
    }
}
