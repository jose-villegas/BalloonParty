namespace BalloonParty.Game.Run
{
    /// <summary>
    ///     Read-only retry state — how many retries remain this run and the level to retry at
    ///     (zero when the next restart is a fresh run).
    /// </summary>
    internal interface IRetryState
    {
        int RetriesRemaining { get; }

        /// <summary>
        ///     The level the next restart should begin at. Zero means a fresh run (level 1).
        ///     Set transiently before the dismiss; consumed during the reset cascade.
        /// </summary>
        int RetryLevel { get; }
    }
}
