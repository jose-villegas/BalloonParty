namespace BalloonParty.Slots.Actor
{
    /// <summary>
    ///     A dynamic actor that can be pinned where it is. While <see cref="IsAnchored" /> is true, the
    ///     balance race, the wave-start roam and pressure shoves all leave it in its slot, so it holds
    ///     in midair and the actors below settle against it.
    /// </summary>
    internal interface IBalanceAnchor
    {
        bool IsAnchored { get; }
    }
}
