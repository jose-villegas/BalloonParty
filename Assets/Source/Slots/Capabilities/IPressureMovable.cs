namespace BalloonParty.Slots.Capabilities
{
    /// <summary>
    ///     A dynamic actor that pressure balancing may relocate to make room for an incoming
    ///     balloon. <see cref="PushResponse"/> defines how it behaves when the push chain reaches
    ///     it; actors without this capability are immovable and halt the cascade.
    /// </summary>
    public interface IPressureMovable
    {
        PressureResponse PushResponse { get; }
    }
}
