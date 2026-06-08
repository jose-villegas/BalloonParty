using BalloonParty.Slots.Actor.Cluster;

namespace BalloonParty.Slots.Actor.Archetype
{
    /// <summary>
    /// Cloud renderer for Puff clusters. Currently inherits all behaviour from
    /// <see cref="ClusterView"/> — override <see cref="ClusterView.OnConfigured"/>
    /// if Puff needs additional shader properties in the future.
    /// </summary>
    internal class PuffCloudView : ClusterView
    {
    }
}
