namespace BalloonParty.Slots
{
    /// <summary>
    ///     Marks an actor whose grid slot can be visually traversed by other actors'
    ///     animation paths — spawn entries, balancer moves, etc.
    ///     Actors that do NOT implement this interface block traversal; paths must route
    ///     around them (routing logic introduced in Phase 9).
    /// </summary>
    public interface IPassThrough { }
}

