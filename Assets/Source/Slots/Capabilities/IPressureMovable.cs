namespace BalloonParty.Slots.Capabilities
{
    /// <summary>Actors without this capability are immovable and halt the pressure-push cascade.</summary>
    public interface IPressureMovable
    {
        PressureResponse PushResponse { get; }
    }
}
