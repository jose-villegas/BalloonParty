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

        void ResetRun();
    }
}
