namespace BalloonParty.Shared
{
    /// <summary>Run-level rules — starting hit points, retry allowance, and (future) loss thresholds.</summary>
    public interface IRunConfig
    {
        int StartingHitPoints { get; }

        /// <summary>How many times the player may retry at the same level per run (0 = disabled).</summary>
        int MaxRetries { get; }
    }
}
