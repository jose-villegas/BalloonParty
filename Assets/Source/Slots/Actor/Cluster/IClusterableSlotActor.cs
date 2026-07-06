namespace BalloonParty.Slots.Actor.Cluster
{
    /// <summary>The registry assigns <see cref="ClusterId"/> during flood-fill.</summary>
    internal interface IClusterableSlotActor : IWriteableSlotActor
    {
        int ClusterId { get; set; }
    }
}
