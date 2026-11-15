namespace BalloonParty.Configuration.Ranges
{
    /// <summary>Common contract for weighted prefab entries that support max-count limits.</summary>
    public interface IWeightedEntry
    {
        float Weight { get; }
        int MaxCount { get; }
        string PoolKey { get; }

        /// <summary>How much the player's need raises <see cref="Weight" />: × (1 + this × need), need 0 to 1.</summary>
        float NeedBoost { get; }
    }
}
