namespace BalloonParty.Configuration.Ranges
{
    /// <summary>Common contract for weighted prefab entries that support max-count limits.</summary>
    public interface IWeightedEntry
    {
        float Weight { get; }
        int MaxCount { get; }
        string PoolKey { get; }
    }
}
