namespace BalloonParty.Configuration
{
    /// <summary>
    /// Common contract for weighted prefab entries that support max-count limits.
    /// Enables generic weighted random selection via
    /// <see cref="WeightedPickExtensions.PickRandom{T}"/>.
    /// </summary>
    public interface IWeightedEntry
    {
        float Weight { get; }
        int MaxCount { get; }
        string PoolKey { get; }
    }
}

