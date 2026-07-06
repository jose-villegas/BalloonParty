namespace BalloonParty.Slots.Actor.Archetype
{
    /// <summary>
    /// A static grid actor that knows its own <see cref="GridActorType" />, so the spawner can match without switching on type.
    /// </summary>
    internal interface IGridActorModel : IWriteableSlotActor
    {
        GridActorType ActorType { get; }
    }
}
