namespace BalloonParty.Game.Run
{
    /// <summary>
    ///     Stages a saved run for the next reset. Held only by <see cref="RunResumeController" />. A resume
    ///     restores the retries the run had used rather than spending one, so it can't get around the ad gate.
    /// </summary>
    internal interface IRunResumeStaging
    {
        /// <summary>
        ///     The next reset restarts at the saved level as its retry would, with what the level began with
        ///     restored from <paramref name="snapshot" />, and leaves the retries used as saved.
        /// </summary>
        void PrepareResume(RunSnapshot snapshot);
    }
}
