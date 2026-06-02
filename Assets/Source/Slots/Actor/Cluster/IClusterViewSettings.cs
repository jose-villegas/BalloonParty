namespace BalloonParty.Slots.Actor.Cluster
{
    /// <summary>
    /// Shared visual configuration for cluster-based renderers (clouds, bushes, etc.).
    /// Concrete settings interfaces extend this and add a typed prefab reference.
    /// </summary>
    internal interface IClusterViewSettings
    {
        float AnimationSpeed { get; }
        float Padding { get; }
        int SortingLayerId { get; }
        int SortingOrderOffset { get; }
    }
}

