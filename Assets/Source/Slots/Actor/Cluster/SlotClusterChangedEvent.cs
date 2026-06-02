namespace BalloonParty.Slots.Actor.Cluster
{
    internal readonly struct SlotClusterChangedEvent
    {
        public readonly int ClusterId;
        public readonly SlotClusterChangeType ChangeType;
        public readonly SlotCluster Cluster;

        public SlotClusterChangedEvent(int clusterId, SlotClusterChangeType changeType, SlotCluster cluster)
        {
            ClusterId = clusterId;
            ChangeType = changeType;
            Cluster = cluster;
        }
    }

    internal enum SlotClusterChangeType
    {
        Created,
        Resized,
        Removed
    }
}
