namespace BalloonParty.Slots.Actor.Archetype
{
    internal readonly struct PuffClusterChangedEvent
    {
        public readonly int ClusterId;
        public readonly PuffClusterChangeType ChangeType;
        public readonly PuffCluster Cluster;

        public PuffClusterChangedEvent(int clusterId, PuffClusterChangeType changeType, PuffCluster cluster)
        {
            ClusterId = clusterId;
            ChangeType = changeType;
            Cluster = cluster;
        }
    }

    internal enum PuffClusterChangeType
    {
        Created,
        Resized,
        Removed
    }
}
