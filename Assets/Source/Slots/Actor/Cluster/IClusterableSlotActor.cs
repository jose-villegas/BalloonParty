namespace BalloonParty.Slots.Actor.Cluster
{
    /// <summary>
    /// A slot actor that can participate in adjacency-based cluster grouping.
    /// The registry assigns <see cref="ClusterId"/> during flood-fill; consumers
    /// read it to correlate a model with its visual cluster.
    /// </summary>
    internal interface IClusterableSlotActor : IWriteableSlotActor
    {
        int ClusterId { get; set; }
    }
}
