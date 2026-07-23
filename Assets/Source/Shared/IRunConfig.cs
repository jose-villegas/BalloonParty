namespace BalloonParty.Shared
{
    /// <summary>Run-level rules — starting hit points and (future) loss thresholds.</summary>
    public interface IRunConfig
    {
        int StartingHitPoints { get; }
    }
}
