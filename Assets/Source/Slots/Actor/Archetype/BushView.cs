using BalloonParty.Slots.Actor.Cluster;

namespace BalloonParty.Slots.Actor.Archetype
{
    /// <summary>
    /// Cluster renderer for Bush obstacles. Currently inherits all behaviour from
    /// <see cref="ClusterView"/> — override <see cref="ClusterView.OnConfigured"/>
    /// if Bush needs additional per-cluster shader properties in the future.
    /// </summary>
    internal class BushView : ClusterView
    {
    }
}

