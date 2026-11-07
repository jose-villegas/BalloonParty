namespace BalloonParty.Game.Run
{
    /// <summary>
    ///     Read-only retry state — how many retries remain this run and the level to retry at
    ///     (zero when the next restart is a fresh run).
    /// </summary>
    internal interface IRetryState
    {
        /// <summary>No retry cap (<see cref="Shared.IRunConfig.MaxRetries" /> of 0 or less).</summary>
        bool IsUnlimited { get; }

        /// <summary>Retries left this run; <see cref="int.MaxValue" /> while <see cref="IsUnlimited" />.</summary>
        int RetriesRemaining { get; }

        /// <summary>Retries this run has used. Counted even when unlimited: the ad gate reads it.</summary>
        int RetriesUsed { get; }

        /// <summary>
        ///     The level the next restart should begin at. Zero means a fresh run (level 1).
        ///     Set transiently before the dismiss; consumed during the reset cascade.
        /// </summary>
        int RetryLevel { get; }
    }
}
